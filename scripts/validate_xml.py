"""Validate translation XML files for structure and common content issues."""

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from collections.abc import Iterable
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class ValidationResult:
    """Validation outcome for one XML file.

    Attributes:
        path: Validated XML file path.
        errors: Fatal validation errors.
        warnings: Non-fatal validation findings.
    """

    path: Path
    errors: list[str]
    warnings: list[str]


DUPLICATE_DETECTION_RULES: tuple[tuple[str, str, str], ...] = (("objects", "object", "Name"),)
"""Schema-aware duplicate-sibling rules: (parent_tag, child_tag, key_attribute).

Only sibling pairs whose parent tag, child tag, and key attribute all match
one of these tuples are reported as duplicates. This avoids false positives
on schema-legitimate repetition (Naming weights, Conversations conditional
branches, Worlds zone differentiation, etc.).
"""


def _collect_xml_files(paths: list[Path]) -> list[Path]:
    xml_files: set[Path] = set()
    for input_path in paths:
        if not input_path.exists():
            msg = f"Path not found: {input_path}"
            raise FileNotFoundError(msg)

        if input_path.is_dir():
            xml_files.update(path for path in input_path.rglob("*.xml") if path.is_file())
            continue

        if input_path.is_file():
            xml_files.add(input_path)
            continue

        msg = f"Path is not a regular file or directory: {input_path}"
        raise ValueError(msg)

    return sorted(xml_files, key=str)


def _warning_path(path: Path, *, root: Path) -> str:
    resolved = path.resolve()
    try:
        return resolved.relative_to(root.resolve()).as_posix()
    except ValueError:
        return resolved.as_posix()


def _warning_key(path: Path, warning: str, *, root: Path) -> tuple[str, str]:
    return (_warning_path(path, root=root), warning)


def _load_warning_baseline(path: Path, *, root: Path) -> set[tuple[str, str]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    warnings = payload.get("warnings")
    if not isinstance(warnings, list):
        msg = f"Warning baseline must contain a 'warnings' list: {path}"
        raise TypeError(msg)

    baseline: set[tuple[str, str]] = set()
    for item in warnings:
        if (
            not isinstance(item, dict)
            or not isinstance(item.get("path"), str)
            or not isinstance(item.get("warning"), str)
        ):
            msg = f"Invalid warning baseline entry in {path}: {item!r}"
            raise TypeError(msg)
        baseline.add((_warning_path(root / item["path"], root=root), item["warning"]))
    return baseline


def _write_warning_baseline(path: Path, results: Iterable[ValidationResult], *, root: Path) -> None:
    warnings = [
        {"path": _warning_path(result.path, root=root), "warning": warning}
        for result in results
        for warning in result.warnings
    ]
    payload = {"version": 1, "warnings": sorted(warnings, key=lambda item: (item["path"], item["warning"]))}
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _find_unbalanced_color_line(text: str) -> int | None:
    open_lines: list[int] = []
    line_number = 1
    index = 0

    while index < len(text):
        if text.startswith("{{", index):
            open_lines.append(line_number)
            index += 2
            continue

        if text.startswith("}}", index):
            if not open_lines:
                return line_number
            open_lines.pop()
            index += 2
            continue

        if text[index] == "\n":
            line_number += 1
        index += 1

    if open_lines:
        return open_lines[0]
    return None


def _format_element_descriptor(element: ET.Element) -> str:
    if identifier := element.attrib.get("ID"):
        return f"'{element.tag}' ID=\"{identifier}\""
    if name := element.attrib.get("Name"):
        return f"'{element.tag}' Name=\"{name}\""
    return f"'{element.tag}'"


def _find_duplicate_siblings(root: ET.Element) -> list[str]:
    warnings: list[str] = []
    for parent in root.iter():
        for parent_tag, child_tag, key_attribute in DUPLICATE_DETECTION_RULES:
            if parent.tag != parent_tag:
                continue
            counts: dict[str, int] = {}
            for child in parent:
                if child.tag != child_tag:
                    continue
                if value := child.attrib.get(key_attribute):
                    counts[value] = counts.get(value, 0) + 1
            warnings.extend(
                f"Duplicate sibling {key_attribute}=\"{value}\" under parent '{parent_tag}'"
                for value, count in sorted(counts.items())
                if count > 1
            )
    return warnings


def _find_empty_text_elements(root: ET.Element) -> list[str]:
    warnings: list[str] = []

    for element in root.iter():
        if element.tag != "text":
            continue
        if len(element) > 0:
            continue
        if element.text is None or element.text.strip() == "":
            warnings.append(f"Empty text in element {_format_element_descriptor(element)}")

    return warnings


def validate_xml_file(path: Path) -> ValidationResult:
    """Validate one XML file and return its findings.

    Args:
        path: XML file path.

    Returns:
        Validation result with errors and warnings.
    """
    errors: list[str] = []
    warnings: list[str] = []

    try:
        root = ET.parse(path).getroot()  # noqa: S314 -- local repository XML validation tool
    except ET.ParseError as exc:
        errors.append(f"XML parse failed: {exc}")
        return ValidationResult(path=path, errors=errors, warnings=warnings)

    try:
        raw_text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        errors.append(f"File is not valid UTF-8 (cannot scan color codes): {exc}. Save as UTF-8 without BOM.")
        return ValidationResult(path=path, errors=errors, warnings=warnings)
    if line_number := _find_unbalanced_color_line(raw_text):
        warnings.append(f"Unbalanced color code at line {line_number}")

    warnings.extend(_find_duplicate_siblings(root))
    warnings.extend(_find_empty_text_elements(root))
    return ValidationResult(path=path, errors=errors, warnings=warnings)


def _print_result(result: ValidationResult) -> None:
    if result.errors:
        print(f"Checking {result.path}... ERROR")  # noqa: T201
        for error in result.errors:
            print(f"  ERROR: {error}")  # noqa: T201
        return

    if result.warnings:
        warning_count = len(result.warnings)
        suffix = "warning" if warning_count == 1 else "warnings"
        print(f"Checking {result.path}... {warning_count} {suffix}")  # noqa: T201
        for warning in result.warnings:
            print(f"  WARNING: {warning}")  # noqa: T201
        return

    print(f"Checking {result.path}... OK")  # noqa: T201


def run_validation(
    paths: list[Path],
    *,
    strict: bool,
    warning_baseline: Path | None = None,
    write_warning_baseline: Path | None = None,
) -> int:
    """Run validation for input paths.

    Args:
        paths: File or directory paths to validate.
        strict: Treat non-baselined warnings as failures.
        warning_baseline: Existing warning baseline to subtract from strict failures.
        write_warning_baseline: Optional path to write current warnings as a baseline.

    Returns:
        Exit code (0 on success, 1 on failure).
    """
    try:
        xml_files = _collect_xml_files(paths)
    except (FileNotFoundError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    if not xml_files:
        print("Error: No XML files found in the provided paths.", file=sys.stderr)  # noqa: T201
        return 1

    root = Path.cwd()
    try:
        baseline = set() if warning_baseline is None else _load_warning_baseline(warning_baseline, root=root)
    except (OSError, TypeError, json.JSONDecodeError) as exc:
        print(f"Error: failed to read warning baseline: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    results: list[ValidationResult] = []
    for file_path in xml_files:
        result = validate_xml_file(file_path)
        results.append(result)
        _print_result(result)

    if write_warning_baseline is not None:
        _write_warning_baseline(write_warning_baseline, results, root=root)
        print(f"Warning baseline written to {write_warning_baseline}", file=sys.stderr)  # noqa: T201

    total_errors = sum(len(result.errors) for result in results)
    unbaselined_warnings = [
        _warning_key(result.path, warning, root=root)
        for result in results
        for warning in result.warnings
        if _warning_key(result.path, warning, root=root) not in baseline
    ]

    if total_errors > 0:
        return 1
    if strict and unbaselined_warnings:
        for path, warning in unbaselined_warnings:
            print(f"  NEW WARNING: {path}: {warning}", file=sys.stderr)  # noqa: T201
        return 1
    return 0


def main(argv: list[str] | None = None) -> int:
    """Run the XML validation CLI.

    Args:
        argv: Command-line arguments. Defaults to ``sys.argv[1:]``.

    Returns:
        Exit code (0 on pass, 1 on failure).
    """
    parser = argparse.ArgumentParser(
        description="Validate translation XML files for parse errors and content warnings.",
    )
    parser.add_argument(
        "paths",
        nargs="+",
        type=Path,
        help="One or more XML files or directories to validate.",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Treat warnings as errors unless they are present in --warning-baseline.",
    )
    parser.add_argument(
        "--warning-baseline",
        type=Path,
        help="JSON baseline of known warnings to allow in strict mode.",
    )
    parser.add_argument(
        "--write-warning-baseline",
        type=Path,
        help="Write current warnings to a JSON baseline file.",
    )

    args = parser.parse_args(argv)
    return run_validation(
        args.paths,
        strict=args.strict,
        warning_baseline=args.warning_baseline,
        write_warning_baseline=args.write_warning_baseline,
    )


if __name__ == "__main__":
    sys.exit(main())
