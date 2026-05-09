"""Validate GameTypeResolver full type names against decompiled game sources."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, cast

if TYPE_CHECKING:
    from collections.abc import Sequence

_DEFAULT_SOURCE_ROOT = Path("Mods/QudJP/Assemblies/src/Patches")
_DEFAULT_DECOMPILED_ROOT = Path("~/dev/coq-decompiled_stable").expanduser()
_CSHARP_VARIABLE_HEX_ESCAPE_MAX_LENGTH = 4

_STRING_LITERAL = r'"(?P<{name}>(?:\\.|[^"\\])*)"'
_IDENTIFIER = r"[A-Za-z_][A-Za-z0-9_]*"
_MEMBER_ACCESS = rf"{_IDENTIFIER}(?:\.{_IDENTIFIER})*"
_STRING_LITERAL_PATTERN = re.compile(rf"^{_STRING_LITERAL.format(name='value')}$", re.DOTALL)
_MEMBER_ACCESS_PATTERN = re.compile(rf"^{_MEMBER_ACCESS}$")
_NAMED_ARGUMENT_PATTERN = re.compile(rf"^(?P<name>{_IDENTIFIER})\s*:\s*(?P<value>.*)$", re.DOTALL)
_FIND_TYPE_CALL_PATTERN = re.compile(r"GameTypeResolver\.FindType\((?P<arguments>.*?)\)", re.DOTALL)
_CONST_STRING_PATTERN = re.compile(
    rf"\bconst\s+string\s+(?P<name>{_IDENTIFIER})\s*=\s*{_STRING_LITERAL.format(name='value')}",
)
_CSHARP_SIMPLE_ESCAPES = {
    "'": "'",
    '"': '"',
    "\\": "\\",
    "0": "\0",
    "a": "\a",
    "b": "\b",
    "f": "\f",
    "n": "\n",
    "r": "\r",
    "t": "\t",
    "v": "\v",
}
_CLASS_PATTERN = re.compile(
    "".join(
        (
            r"^\s*",
            r"(?:public|internal|private|protected|protected\s+internal|private\s+protected)?\s*",
            r"(?:(?:static|abstract|sealed|partial|readonly|unsafe)\s+)*",
            r"(?:class|struct)\s+",
            rf"(?P<name>{_IDENTIFIER})\b",
        ),
    ),
    re.MULTILINE,
)
_NAMESPACE_PATTERN = re.compile(r"^\s*namespace\s+(?P<namespace>[A-Za-z_][A-Za-z0-9_.]*)\s*[;{]", re.MULTILINE)
_TYPE_PATTERN = re.compile(
    "".join(
        (
            r"^\s*",
            r"(?:public|internal|private|protected|protected\s+internal|private\s+protected)?\s*",
            r"(?:(?:static|abstract|sealed|partial|readonly|unsafe)\s+)*",
            r"(?:class|struct|interface|enum)\s+",
            rf"(?P<name>{_IDENTIFIER})\b",
        ),
    ),
    re.MULTILINE,
)


@dataclass(frozen=True)
class ResolverCall:
    """A statically checkable GameTypeResolver.FindType call."""

    path: str
    line: int
    full_type_name: str
    simple_type_name: str


@dataclass(frozen=True)
class ResolverMismatch:
    """A resolver call whose full type name is absent from decompiled sources."""

    path: str
    line: int
    full_type_name: str
    simple_type_name: str
    candidates: tuple[str, ...]


@dataclass(frozen=True)
class UnresolvedResolverCall:
    """A resolver call whose full type argument could not be resolved statically."""

    path: str
    line: int
    full_arg: str
    simple_type_name: str


@dataclass(frozen=True)
class ValidationResult:
    """Summary of statically checked GameTypeResolver calls."""

    checked: int
    mismatches: tuple[ResolverMismatch, ...]
    unresolved: tuple[UnresolvedResolverCall, ...]

    @property
    def non_ok(self) -> int:
        """Return the number of actionable validation problems."""
        return len(self.mismatches) + len(self.unresolved)


def validate_game_type_resolver_calls(
    *,
    source_root: Path,
    decompiled_root: Path,
    repo_root: Path,
) -> ValidationResult:
    """Validate statically resolvable GameTypeResolver.FindType calls."""
    source_root = source_root.expanduser().resolve()
    decompiled_root = decompiled_root.expanduser().resolve()
    repo_root = repo_root.expanduser().resolve()

    if not source_root.is_dir():
        msg = f"source root does not exist or is not a directory: {source_root}"
        raise ValueError(msg)
    if not decompiled_root.is_dir():
        msg = f"decompiled root does not exist or is not a directory: {decompiled_root}"
        raise ValueError(msg)

    type_index = _build_decompiled_type_index(decompiled_root)
    calls: list[ResolverCall] = []
    unresolved: list[UnresolvedResolverCall] = []
    for path in sorted(source_root.rglob("*.cs")):
        file_calls, file_unresolved = _extract_resolver_calls(path, repo_root)
        calls.extend(file_calls)
        unresolved.extend(file_unresolved)

    mismatches = tuple(
        ResolverMismatch(
            path=call.path,
            line=call.line,
            full_type_name=call.full_type_name,
            simple_type_name=call.simple_type_name,
            candidates=type_index.by_simple_name.get(call.simple_type_name, ()),
        )
        for call in calls
        if call.full_type_name not in type_index.full_type_names
    )

    return ValidationResult(
        checked=len(calls),
        mismatches=mismatches,
        unresolved=tuple(unresolved),
    )


def main(argv: Sequence[str] | None = None) -> int:
    """Run the GameTypeResolver type-name validation CLI."""
    parser = argparse.ArgumentParser(description=__doc__)
    _ = parser.add_argument("--source-root", type=Path, default=_DEFAULT_SOURCE_ROOT)
    _ = parser.add_argument("--decompiled-root", type=Path, default=_DEFAULT_DECOMPILED_ROOT)
    _ = parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    _ = parser.add_argument("--output", type=Path, help="Optional JSON output path.")
    args = parser.parse_args(argv)
    source_root = cast("Path", args.source_root)
    decompiled_root = cast("Path", args.decompiled_root)
    repo_root = cast("Path", args.repo_root)
    output = cast("Path | None", args.output)

    try:
        result = validate_game_type_resolver_calls(
            source_root=source_root,
            decompiled_root=decompiled_root,
            repo_root=repo_root,
        )
    except ValueError as exc:
        _ = sys.stderr.write(f"error: {exc}\n")
        return 2

    payload = _result_to_dict(result)
    if output is not None:
        output.parent.mkdir(parents=True, exist_ok=True)
        _ = output.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    ok_count = result.checked - len(result.mismatches)
    _ = sys.stdout.write(f"checked={result.checked} ok={ok_count} non_ok={result.non_ok}\n")
    for mismatch in result.mismatches:
        candidates = ", ".join(mismatch.candidates) if mismatch.candidates else "<none>"
        message = "".join(
            (
                f"{mismatch.path}:{mismatch.line}: full type '{mismatch.full_type_name}' ",
                f"not found; simple '{mismatch.simple_type_name}' candidates: {candidates}\n",
            ),
        )
        _ = sys.stderr.write(message)
    for unresolved in result.unresolved:
        message = "".join(
            (
                f"{unresolved.path}:{unresolved.line}: cannot resolve full type argument ",
                f"'{unresolved.full_arg}' for simple '{unresolved.simple_type_name}'\n",
            ),
        )
        _ = sys.stderr.write(message)

    return 1 if result.non_ok else 0


@dataclass(frozen=True)
class _TypeIndex:
    full_type_names: frozenset[str]
    by_simple_name: dict[str, tuple[str, ...]]


@dataclass(frozen=True)
class _TypeSpan:
    name: str
    body_start: int
    body_end: int


@dataclass(frozen=True)
class _ConstIndex:
    top_level: dict[str, str]
    qualified: dict[str, str]
    type_spans: tuple[_TypeSpan, ...]


@dataclass(frozen=True)
class _FindTypeArguments:
    full_arg: str
    full_literal: str | None
    simple_type_name: str


@dataclass
class _ArgumentScanState:
    depth: int = 0
    in_string: bool = False
    escaped: bool = False


def _build_decompiled_type_index(decompiled_root: Path) -> _TypeIndex:
    full_type_names: set[str] = set()
    by_simple_name: dict[str, list[str]] = {}
    for path in sorted(decompiled_root.rglob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="ignore")
        namespace = _find_namespace(text)
        for type_name in _find_type_names(text):
            full_name = f"{namespace}.{type_name}" if namespace else type_name
            full_type_names.add(full_name)
            by_simple_name.setdefault(type_name, []).append(full_name)

    return _TypeIndex(
        full_type_names=frozenset(full_type_names),
        by_simple_name={key: tuple(sorted(values)) for key, values in by_simple_name.items()},
    )


def _extract_resolver_calls(
    path: Path,
    repo_root: Path,
) -> tuple[tuple[ResolverCall, ...], tuple[UnresolvedResolverCall, ...]]:
    text = path.read_text(encoding="utf-8", errors="ignore")
    constants = _extract_const_strings(text)
    relative_path = _format_path(path, repo_root)
    calls: list[ResolverCall] = []
    unresolved: list[UnresolvedResolverCall] = []

    for match in _FIND_TYPE_CALL_PATTERN.finditer(text):
        arguments = _parse_find_type_arguments(match.group("arguments"))
        if arguments is None:
            continue

        line = text.count("\n", 0, match.start()) + 1
        full_arg = arguments.full_arg
        simple_type_name = arguments.simple_type_name
        if arguments.full_literal is not None:
            full_type_name = arguments.full_literal
        else:
            full_type_name = _resolve_const_argument(full_arg, constants, match.start())
            if full_type_name is None:
                unresolved.append(
                    UnresolvedResolverCall(
                        path=relative_path,
                        line=line,
                        full_arg=full_arg,
                        simple_type_name=simple_type_name,
                    ),
                )
                continue

        calls.append(
            ResolverCall(
                path=relative_path,
                line=line,
                full_type_name=full_type_name,
                simple_type_name=simple_type_name,
            ),
        )

    return tuple(calls), tuple(unresolved)


def _parse_find_type_arguments(argument_text: str) -> _FindTypeArguments | None:
    positional: list[str] = []
    named: dict[str, str] = {}
    for argument in _split_arguments(argument_text):
        name, value = _parse_argument(argument)
        if name is None:
            positional.append(value)
        else:
            named[name] = value

    full_arg = named.get("fullTypeName")
    if full_arg is None and positional:
        full_arg = positional[0]

    simple_arg = named.get("simpleTypeName")
    if simple_arg is None and len(positional) > 1:
        simple_arg = positional[1]

    if full_arg is None or simple_arg is None:
        return None

    simple_type_name = _extract_string_literal_value(simple_arg)
    if simple_type_name is None:
        return None

    full_arg = full_arg.strip()
    full_literal = _extract_string_literal_value(full_arg)
    if full_literal is None and _MEMBER_ACCESS_PATTERN.fullmatch(full_arg) is None:
        return None

    return _FindTypeArguments(
        full_arg=full_arg,
        full_literal=full_literal,
        simple_type_name=simple_type_name,
    )


def _split_arguments(argument_text: str) -> tuple[str, ...]:
    arguments: list[str] = []
    current: list[str] = []
    state = _ArgumentScanState()
    for char in argument_text:
        if _consume_argument_character(char, state):
            argument = "".join(current).strip()
            if argument:
                arguments.append(argument)
            current = []
            continue
        current.append(char)

    argument = "".join(current).strip()
    if argument:
        arguments.append(argument)
    return tuple(arguments)


def _consume_argument_character(char: str, state: _ArgumentScanState) -> bool:
    if state.in_string:
        _consume_string_character(char, state)
        return False

    if char == '"':
        state.in_string = True
        return False
    if char in "([{":
        state.depth += 1
        return False
    if char in ")]}":
        state.depth = max(0, state.depth - 1)
        return False
    return char == "," and state.depth == 0


def _consume_string_character(char: str, state: _ArgumentScanState) -> None:
    if state.escaped:
        state.escaped = False
    elif char == "\\":
        state.escaped = True
    elif char == '"':
        state.in_string = False


def _parse_argument(argument: str) -> tuple[str | None, str]:
    match = _NAMED_ARGUMENT_PATTERN.fullmatch(argument.strip())
    if match is None:
        return None, argument.strip()
    return match.group("name"), match.group("value").strip()


def _extract_string_literal_value(value: str) -> str | None:
    match = _STRING_LITERAL_PATTERN.fullmatch(value.strip())
    if match is None:
        return None
    return _decode_csharp_string(match.group("value"))


def _extract_const_strings(text: str) -> _ConstIndex:
    type_spans = _iter_type_spans(text)
    top_level: dict[str, str] = {}
    for match in _CONST_STRING_PATTERN.finditer(text):
        if _find_enclosing_type_name(type_spans, match.start()) is None:
            top_level[match.group("name")] = _decode_csharp_string(match.group("value"))

    qualified: dict[str, str] = {}
    for span in type_spans:
        body = text[span.body_start : span.body_end]
        for match in _CONST_STRING_PATTERN.finditer(body):
            qualified[f"{span.name}.{match.group('name')}"] = _decode_csharp_string(match.group("value"))

    return _ConstIndex(top_level=top_level, qualified=qualified, type_spans=type_spans)


def _resolve_const_argument(full_arg: str, constants: _ConstIndex, position: int) -> str | None:
    if "." in full_arg:
        return constants.qualified.get(full_arg)

    enclosing_type = _find_enclosing_type_name(constants.type_spans, position)
    if enclosing_type is not None:
        scoped_value = constants.qualified.get(f"{enclosing_type}.{full_arg}")
        if scoped_value is not None:
            return scoped_value

    return constants.top_level.get(full_arg)


def _find_namespace(text: str) -> str:
    match = _NAMESPACE_PATTERN.search(text)
    return match.group("namespace") if match else ""


def _find_type_names(text: str) -> tuple[str, ...]:
    return tuple(match.group("name") for match in _TYPE_PATTERN.finditer(text))


def _iter_type_spans(text: str) -> tuple[_TypeSpan, ...]:
    spans: list[_TypeSpan] = []
    for match in _CLASS_PATTERN.finditer(text):
        open_brace_index = text.find("{", match.end())
        if open_brace_index == -1:
            continue
        close_brace_index = _find_matching_brace(text, open_brace_index)
        if close_brace_index == -1:
            continue
        spans.append(
            _TypeSpan(
                name=match.group("name"),
                body_start=open_brace_index + 1,
                body_end=close_brace_index,
            ),
        )
    return tuple(spans)


def _find_enclosing_type_name(type_spans: tuple[_TypeSpan, ...], position: int) -> str | None:
    enclosing = [
        span
        for span in type_spans
        if span.body_start <= position <= span.body_end
    ]
    if not enclosing:
        return None
    return max(enclosing, key=lambda span: span.body_start).name


def _find_matching_brace(text: str, open_brace_index: int) -> int:
    depth = 0
    index = open_brace_index
    while index < len(text):
        if text.startswith("//", index) or _starts_preprocessor_directive(text, index):
            index = _skip_line(text, index)
            continue
        if text.startswith("/*", index):
            index = _skip_block_comment(text, index)
            continue
        if text[index] in "\"'":
            index = _skip_quoted_literal(text, index)
            continue

        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                return index
        index += 1
    return -1


def _starts_preprocessor_directive(text: str, index: int) -> bool:
    if text[index] != "#":
        return False
    line_start = text.rfind("\n", 0, index) + 1
    return text[line_start:index].strip() == ""


def _skip_line(text: str, index: int) -> int:
    newline_index = text.find("\n", index)
    return len(text) if newline_index == -1 else newline_index + 1


def _skip_block_comment(text: str, index: int) -> int:
    end_index = text.find("*/", index + 2)
    return len(text) if end_index == -1 else end_index + 2


def _skip_quoted_literal(text: str, index: int) -> int:
    if text[index] == '"' and index > 0 and text[index - 1] == "@":
        return _skip_verbatim_string(text, index)
    return _skip_escaped_literal(text, index, text[index])


def _skip_verbatim_string(text: str, index: int) -> int:
    index += 1
    while index < len(text):
        if text[index] == '"':
            if index + 1 < len(text) and text[index + 1] == '"':
                index += 2
                continue
            return index + 1
        index += 1
    return len(text)


def _skip_escaped_literal(text: str, index: int, quote: str) -> int:
    index += 1
    escaped = False
    while index < len(text):
        char = text[index]
        if escaped:
            escaped = False
        elif char == "\\":
            escaped = True
        elif char == quote:
            return index + 1
        index += 1
    return len(text)


def _decode_csharp_string(value: str) -> str:
    decoded: list[str] = []
    index = 0
    while index < len(value):
        if value[index] != "\\":
            decoded.append(value[index])
            index += 1
            continue

        replacement, next_index = _decode_csharp_escape(value, index)
        decoded.append(replacement)
        index = next_index

    return "".join(decoded)


def _decode_csharp_escape(value: str, slash_index: int) -> tuple[str, int]:
    escape_index = slash_index + 1
    if escape_index >= len(value):
        return "\\", escape_index

    escape = value[escape_index]
    simple_escape = _CSHARP_SIMPLE_ESCAPES.get(escape)
    if simple_escape is not None:
        return simple_escape, escape_index + 1
    if escape == "u":
        return _decode_fixed_hex_escape(value, escape_index + 1, 4, value[slash_index : escape_index + 1])
    if escape == "U":
        return _decode_fixed_hex_escape(value, escape_index + 1, 8, value[slash_index : escape_index + 1])
    if escape == "x":
        return _decode_variable_hex_escape(value, escape_index + 1, value[slash_index : escape_index + 1])

    return value[slash_index : escape_index + 1], escape_index + 1


def _decode_fixed_hex_escape(value: str, start_index: int, length: int, fallback: str) -> tuple[str, int]:
    end_index = start_index + length
    digits = value[start_index:end_index]
    if len(digits) != length or not _is_hex(digits):
        return fallback, start_index
    return _chr_or_fallback(digits, fallback), end_index


def _decode_variable_hex_escape(value: str, start_index: int, fallback: str) -> tuple[str, int]:
    end_index = start_index
    while (
        end_index < len(value)
        and end_index - start_index < _CSHARP_VARIABLE_HEX_ESCAPE_MAX_LENGTH
        and _is_hex(value[end_index])
    ):
        end_index += 1
    if end_index == start_index:
        return fallback, start_index
    digits = value[start_index:end_index]
    return _chr_or_fallback(digits, fallback), end_index


def _is_hex(value: str) -> bool:
    return all(char in "0123456789abcdefABCDEF" for char in value)


def _chr_or_fallback(hex_digits: str, fallback: str) -> str:
    try:
        return chr(int(hex_digits, 16))
    except ValueError:
        return fallback


def _format_path(path: Path, repo_root: Path) -> str:
    try:
        return path.resolve().relative_to(repo_root).as_posix()
    except ValueError:
        return path.as_posix()


def _result_to_dict(result: ValidationResult) -> dict[str, object]:
    return {
        "checked": result.checked,
        "non_ok": result.non_ok,
        "mismatches": [
            {
                "path": item.path,
                "line": item.line,
                "full_type_name": item.full_type_name,
                "simple_type_name": item.simple_type_name,
                "candidates": list(item.candidates),
            }
            for item in result.mismatches
        ],
        "unresolved": [
            {
                "path": item.path,
                "line": item.line,
                "full_arg": item.full_arg,
                "simple_type_name": item.simple_type_name,
            }
            for item in result.unresolved
        ],
    }


if __name__ == "__main__":
    raise SystemExit(main())
