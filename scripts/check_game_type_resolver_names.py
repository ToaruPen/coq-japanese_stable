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

_STRING_LITERAL = r'"(?P<{name}>(?:\\.|[^"\\])*)"'
_IDENTIFIER = r"[A-Za-z_][A-Za-z0-9_]*"
_MEMBER_ACCESS = rf"{_IDENTIFIER}(?:\.{_IDENTIFIER})*"
_FIND_TYPE_PATTERN = re.compile(
    "".join(
        (
            r"GameTypeResolver\.FindType\(\s*",
            rf"(?P<full_arg>{_STRING_LITERAL.format(name='full_literal')}|{_MEMBER_ACCESS})",
            r"\s*,\s*(?:simpleTypeName\s*:\s*)?",
            rf"{_STRING_LITERAL.format(name='simple_literal')}",
        ),
    ),
    re.DOTALL,
)
_CONST_STRING_PATTERN = re.compile(
    rf"\bconst\s+string\s+(?P<name>{_IDENTIFIER})\s*=\s*{_STRING_LITERAL.format(name='value')}",
)
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

    _ = sys.stdout.write(f"checked={result.checked} ok={result.checked - result.non_ok} non_ok={result.non_ok}\n")
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

    for match in _FIND_TYPE_PATTERN.finditer(text):
        line = text.count("\n", 0, match.start()) + 1
        full_arg = match.group("full_arg")
        simple_type_name = _decode_csharp_string(match.group("simple_literal"))
        full_literal = match.groupdict().get("full_literal")
        if full_literal is not None:
            full_type_name = _decode_csharp_string(full_literal)
        elif full_arg in constants:
            full_type_name = constants[full_arg]
        else:
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


def _extract_const_strings(text: str) -> dict[str, str]:
    constants = {
        match.group("name"): _decode_csharp_string(match.group("value"))
        for match in _CONST_STRING_PATTERN.finditer(text)
    }
    for type_name, body in _iter_type_bodies(text):
        for match in _CONST_STRING_PATTERN.finditer(body):
            constants[f"{type_name}.{match.group('name')}"] = _decode_csharp_string(match.group("value"))
    return constants


def _find_namespace(text: str) -> str:
    match = _NAMESPACE_PATTERN.search(text)
    return match.group("namespace") if match else ""


def _find_type_names(text: str) -> tuple[str, ...]:
    return tuple(match.group("name") for match in _TYPE_PATTERN.finditer(text))


def _iter_type_bodies(text: str) -> tuple[tuple[str, str], ...]:
    bodies: list[tuple[str, str]] = []
    for match in _CLASS_PATTERN.finditer(text):
        open_brace_index = text.find("{", match.end())
        if open_brace_index == -1:
            continue
        close_brace_index = _find_matching_brace(text, open_brace_index)
        if close_brace_index == -1:
            continue
        bodies.append((match.group("name"), text[open_brace_index + 1 : close_brace_index]))
    return tuple(bodies)


def _find_matching_brace(text: str, open_brace_index: int) -> int:
    depth = 0
    for index in range(open_brace_index, len(text)):
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                return index
    return -1


def _decode_csharp_string(value: str) -> str:
    return bytes(value, "utf-8").decode("unicode_escape")


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
