"""Scan decompiled C# source files for string provenance heuristics."""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import TYPE_CHECKING

from scripts.provenance.models import GeneratorSignature, StringClassification

if TYPE_CHECKING:
    from pathlib import Path

_GENERATOR_PATTERNS: list[tuple[re.Pattern[str], str]] = [
    (
        re.compile(r"\bDescriptionBuilder\b[\s\S]*?(?:\.\s*)?Add\w*\s*\("),
        "DescriptionBuilder.Add* composition",
    ),
    (re.compile(r"\bDescriptionBuilder\b"), "DescriptionBuilder.Add* composition"),
    (re.compile(r"\bGetDisplayNameEvent\.[A-Za-z_][A-Za-z0-9_]*\b"), "GetDisplayNameEvent composition"),
    (re.compile(r"\bGetDisplayNameEvent\b"), "GetDisplayNameEvent composition"),
    (re.compile(r"\bGrammar\.(?:A|Pluralize|MakePossessive)\s*\("), "Grammar API"),
    (re.compile(r"\bHistoricStringExpander\.[A-Za-z_][A-Za-z0-9_]*\s*\("), "HistoricStringExpander"),
    (re.compile(r"\bHistoricStringExpander\b"), "HistoricStringExpander"),
    (re.compile(r"\bGameText\.(?:Process|VariableReplace)\s*\("), "GameText variable replacement"),
]
_DYNAMIC_PATTERNS: list[tuple[re.Pattern[str], str]] = [
    (re.compile(r"\bstring\.Format\s*\("), "string.Format"),
    (re.compile(r"\bStringBuilder\b[\s\S]*?\.Append\w*\s*\("), "StringBuilder.Append chain"),
    (re.compile(r"\.Append\w*\s*\("), "StringBuilder.Append chain"),
    (re.compile(r"\bstring\.Concat\s*\("), "string.Concat"),
]
_METHOD_SIGNATURE_PATTERN = re.compile(
    r"^\s*(?:\[[^\]]+\]\s*)*(?:public|private|protected|internal)"
    r"(?:\s+static|\s+virtual|\s+override|\s+sealed|\s+extern|\s+unsafe|\s+partial)*"
    r"\s+[A-Za-z_][A-Za-z0-9_<>,\[\]?\s\.]*?\s+([A-Za-z_][A-Za-z0-9_]*)\s*\([^;]*\)",
)
_NAMESPACE_PATTERN = re.compile(r"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)", re.MULTILINE)
_CLASS_PATTERN = re.compile(
    r"^\s*(?:public|private|protected|internal)?(?:\s+static|\s+partial|\s+abstract|\s+sealed)*\s*"
    r"(?:class|struct)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)


@dataclass(frozen=True)
class _BraceScanState:
    """State carried across lines while counting structural braces."""

    in_block_comment: bool = False


def scan_source_file(path: Path) -> list[GeneratorSignature]:
    """Scan a decompiled C# source file for string-producing methods.

    Args:
        path: Source file to scan.

    Returns:
        Generator signatures for methods matching known heuristics.
    """
    if not path.exists() or not path.is_file() or path.stat().st_size == 0:
        return []

    content = path.read_text(encoding="utf-8", errors="replace")
    qualified_class_name = _resolve_qualified_class_name(path, content)
    signatures: list[GeneratorSignature] = []
    for method_name, method_body, start_line in _split_into_methods(content):
        classification, pattern_kind, evidence_line = _classify_method(
            qualified_class_name,
            method_name,
            method_body,
            start_line,
        )
        if classification is None:
            continue
        signatures.append(
            GeneratorSignature(
                source_file=path.name,
                class_name=qualified_class_name,
                method_name=method_name,
                classification=classification,
                pattern_kind=pattern_kind,
                evidence_line=evidence_line,
            )
        )
    return signatures


def scan_directory(ilspy_dir: Path) -> list[GeneratorSignature]:
    """Scan every C# file beneath a decompile directory."""
    if not ilspy_dir.exists():
        return []

    signatures: list[GeneratorSignature] = []
    for path in sorted(ilspy_dir.rglob("*.cs")):
        signatures.extend(scan_source_file(path))
    return signatures


def _resolve_qualified_class_name(path: Path, content: str) -> str:
    """Build a qualified class name from namespace and class declarations."""
    namespace_match = _NAMESPACE_PATTERN.search(content)
    class_match = _CLASS_PATTERN.search(content)
    namespace = namespace_match.group(1) if namespace_match else None
    class_name = class_match.group(1) if class_match else path.name.removesuffix(".cs")
    if namespace:
        return f"{namespace}.{class_name}"
    return class_name


def _split_into_methods(content: str) -> list[tuple[str, str, int]]:
    """Split source text into ``(method_name, body, start_line)`` tuples."""
    methods: list[tuple[str, str, int]] = []
    lines = content.splitlines()
    line_index = 0
    while line_index < len(lines):
        signature_match = _METHOD_SIGNATURE_PATTERN.search(lines[line_index])
        if signature_match is None:
            line_index += 1
            continue

        method_name = signature_match.group(1)
        start_line = line_index + 1
        body_lines = [lines[line_index]]
        state = _BraceScanState()
        brace_delta, state = _brace_delta(lines[line_index], state)
        brace_depth = brace_delta
        saw_open_brace = brace_delta > 0
        cursor = line_index + 1

        while cursor < len(lines):
            line = lines[cursor]
            body_lines.append(line)
            brace_delta, state = _brace_delta(line, state)
            brace_depth += brace_delta
            if brace_delta > 0:
                saw_open_brace = True
            if saw_open_brace and brace_depth <= 0:
                break
            cursor += 1

        if saw_open_brace and brace_depth <= 0:
            methods.append((method_name, "\n".join(body_lines), start_line))
            line_index = cursor + 1
            continue

        line_index += 1
    return methods


def _classify_method(
    qualified_class_name: str,
    method_name: str,
    body: str,
    start_line: int,
) -> tuple[StringClassification | None, str, int]:
    """Classify one method body and return evidence metadata."""
    class_pattern_kind = _classify_known_generator_class(qualified_class_name, method_name)
    if class_pattern_kind is not None:
        return StringClassification.GENERATOR_FAMILY, class_pattern_kind, start_line

    for pattern, kind in _GENERATOR_PATTERNS:
        match = pattern.search(body)
        if match is not None:
            return StringClassification.GENERATOR_FAMILY, kind, _match_line(body, start_line, match.start())

    for pattern, kind in _DYNAMIC_PATTERNS:
        match = pattern.search(body)
        if match is not None:
            return StringClassification.STRUCTURED_DYNAMIC, kind, _match_line(body, start_line, match.start())

    return None, "", 0


def _match_line(body: str, start_line: int, offset: int) -> int:
    """Translate a regex match offset into a 1-based source line number."""
    return start_line + body[:offset].count("\n")


def _classify_known_generator_class(qualified_class_name: str, method_name: str) -> str | None:
    """Return a generator pattern label for methods on known generator classes."""
    class_name = qualified_class_name.rsplit(".", maxsplit=1)[-1]
    if class_name == "DescriptionBuilder" and (
        method_name.startswith("Add") or method_name in {"TryAddTag", "ToString"}
    ):
        return "DescriptionBuilder.Add* composition"
    if class_name == "GetDisplayNameEvent" and method_name in {"GetFor", "ProcessFor"}:
        return "GetDisplayNameEvent composition"
    if class_name == "Grammar" and method_name in {"A", "Pluralize", "MakePossessive"}:
        return "Grammar API"
    if class_name == "HistoricStringExpander" and method_name == "ExpandString":
        return "HistoricStringExpander"
    if class_name == "GameText" and method_name in {"Process", "VariableReplace"}:
        return "GameText variable replacement"
    return None


def _brace_delta(line: str, state: _BraceScanState) -> tuple[int, _BraceScanState]:  # noqa: C901, PLR0912, PLR0915
    """Count structural braces in a line while ignoring strings and comments."""
    delta = 0
    index = 0
    in_string = False
    in_char = False
    in_verbatim_string = False
    in_block_comment = state.in_block_comment

    while index < len(line):
        char = line[index]
        next_char = line[index + 1] if index + 1 < len(line) else ""

        if in_block_comment:
            if char == "*" and next_char == "/":
                in_block_comment = False
                index += 2
                continue
            index += 1
            continue

        if in_verbatim_string:
            if char == '"' and next_char == '"':
                index += 2
                continue
            if char == '"':
                in_verbatim_string = False
            index += 1
            continue

        if in_string:
            if char == "\\":
                index += 2
                continue
            if char == '"':
                in_string = False
            index += 1
            continue

        if in_char:
            if char == "\\":
                index += 2
                continue
            if char == "'":
                in_char = False
            index += 1
            continue

        if char == "/" and next_char == "/":
            break
        if char == "/" and next_char == "*":
            in_block_comment = True
            index += 2
            continue
        if char == "@" and next_char == '"':
            in_verbatim_string = True
            index += 2
            continue
        if char == '"':
            in_string = True
            index += 1
            continue
        if char == "'":
            in_char = True
            index += 1
            continue
        if char == "{":
            delta += 1
        elif char == "}":
            delta -= 1
        index += 1

    return delta, _BraceScanState(in_block_comment=in_block_comment)
