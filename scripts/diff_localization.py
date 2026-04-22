"""Compare base game XML entries against Japanese localization entries."""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import asdict, dataclass
from pathlib import Path

_DEFAULT_GAME_BASE_DIR = (
    Path.home()
    / "Library"
    / "Application Support"
    / "Steam"
    / "steamapps"
    / "common"
    / "Caves of Qud"
    / "CoQ.app"
    / "Contents"
    / "Resources"
    / "Data"
    / "StreamingAssets"
    / "Base"
)
_BOOK_ID_PATTERN = re.compile(rb"<book\b[^>]*\bID=[\"']([^\"']+)[\"']")
_OBJECT_NAME_PATTERN = re.compile(rb"<object\b[^>]*\bName=[\"']([^\"']+)[\"']")
_CONVERSATION_ID_PATTERN = re.compile(rb"<conversation\b[^>]*\bID=[\"']([^\"']+)[\"']")
_GENERIC_ID_OR_NAME_PATTERN = re.compile(rb"\b(?:ID|Name)=[\"']([^\"']+)[\"']")


@dataclass(frozen=True)
class CategoryDiff:
    """Coverage details for a single localization category.

    Attributes:
        category: Category name (for example, ObjectBlueprints).
        total: Number of base entries in this category.
        translated: Number of entries present in both base and translation.
        coverage_percent: translated / total * 100.
        untranslated: Entry IDs present in base but missing in translation.
        removed: Entry IDs present in translation but missing in base.
    """

    category: str
    total: int
    translated: int
    coverage_percent: float
    untranslated: list[str]
    removed: list[str]


@dataclass(frozen=True)
class DiffReport:
    """Complete localization diff report.

    Attributes:
        categories: Per-category coverage details.
        total: Total number of base entries.
        translated: Total translated entries.
        coverage_percent: translated / total * 100.
        untranslated: Total untranslated entries.
        removed: Total removed entries.
    """

    categories: list[CategoryDiff]
    total: int
    translated: int
    coverage_percent: float
    untranslated: int
    removed: int


def _find_project_root() -> Path:
    """Locate the project root by traversing up to find pyproject.toml.

    Returns:
        Path to the project root directory.

    Raises:
        FileNotFoundError: If pyproject.toml cannot be found in any parent.
    """
    current = Path(__file__).resolve().parent
    while current != current.parent:
        if (current / "pyproject.toml").exists():
            return current
        current = current.parent
    msg = "Could not find project root (no pyproject.toml found)"
    raise FileNotFoundError(msg)


def _validate_directory(path: Path, *, label: str) -> None:
    """Validate that a directory exists.

    Args:
        path: Directory path to validate.
        label: Human-readable label for error messages.

    Raises:
        FileNotFoundError: If the directory does not exist.
    """
    if not path.is_dir():
        msg = f"{label} directory not found: {path}"
        raise FileNotFoundError(msg)


def _category_for_base(path: Path, *, base_dir: Path) -> str:
    relative = path.relative_to(base_dir)
    if relative.parts and relative.parts[0] == "ObjectBlueprints":
        return "ObjectBlueprints"
    return path.stem


def _category_for_mod(path: Path, *, mod_dir: Path) -> str:
    relative = path.relative_to(mod_dir)
    if relative.parts and relative.parts[0] == "ObjectBlueprints":
        return "ObjectBlueprints"
    return path.stem.removesuffix(".jp")


def _is_blank_xml(path: Path) -> bool:
    """Return True when a file has only whitespace content."""
    return not path.read_bytes().strip()


def _parse_xml_root(path: Path) -> ET.Element:
    return ET.parse(path).getroot()  # noqa: S314 -- local repository/game XML input only


def _extract_generic_entries(path: Path) -> set[str]:
    root = _parse_xml_root(path)
    direct_ids: set[str] = set()
    for child in root:
        identifier = child.attrib.get("ID") or child.attrib.get("Name")
        if identifier:
            direct_ids.add(identifier)
    if direct_ids:
        return direct_ids

    fallback_ids: set[str] = set()
    for element in root.iter():
        identifier = element.attrib.get("ID") or element.attrib.get("Name")
        if identifier:
            fallback_ids.add(identifier)
    if not fallback_ids:
        msg = f"No ID or Name attributes found in {path}. File may use an unsupported XML structure."
        print(f"WARNING: {msg}", file=sys.stderr)  # noqa: T201
        return set()
    return fallback_ids


def _extract_books_entries(path: Path) -> set[str]:
    try:
        root = _parse_xml_root(path)
        return {book_id for book in root.findall("book") if (book_id := book.attrib.get("ID"))}
    except ET.ParseError as exc:
        print(  # noqa: T201
            f"WARNING: Books XML parse failed for {path}, falling back to regex extraction: {exc}",
            file=sys.stderr,
        )
        return _extract_books_entries_regex(path)


def _extract_books_entries_regex(path: Path) -> set[str]:
    return _extract_regex_entries(path, _BOOK_ID_PATTERN, label="book ID")


def _extract_regex_entries(path: Path, pattern: re.Pattern[bytes], *, label: str) -> set[str]:
    raw = path.read_bytes()
    result: set[str] = set()
    for match in pattern.findall(raw):
        if not match:
            continue
        try:
            result.add(match.decode("utf-8"))
        except UnicodeDecodeError as exc:
            msg = f"Non-UTF-8 {label} in {path}: {exc}"
            raise ValueError(msg) from exc
    return result


def _extract_entries_regex_fallback(path: Path, *, category: str, reason: ET.ParseError) -> set[str]:
    if category == "ObjectBlueprints":
        pattern = _OBJECT_NAME_PATTERN
        label = "object Name"
    elif category == "Conversations":
        pattern = _CONVERSATION_ID_PATTERN
        label = "conversation ID"
    elif category == "Books":
        pattern = _BOOK_ID_PATTERN
        label = "book ID"
    else:
        pattern = _GENERIC_ID_OR_NAME_PATTERN
        label = "ID/Name"

    entries = _extract_regex_entries(path, pattern, label=label)
    if entries:
        print(  # noqa: T201
            f"WARNING: Failed to parse XML: {path} ({reason}), recovered {len(entries)} {label} entries by regex.",
            file=sys.stderr,
        )
        return entries

    print(  # noqa: T201
        f"WARNING: Failed to parse XML: {path} ({reason}), and regex fallback found no {label} entries.",
        file=sys.stderr,
    )
    return set()


def _extract_entries(path: Path, *, category: str) -> set[str]:
    try:
        if category == "ObjectBlueprints":
            root = _parse_xml_root(path)
            return {name for obj in root.findall("object") if (name := obj.attrib.get("Name"))}
        if category == "Conversations":
            root = _parse_xml_root(path)
            return {
                conv_id for conversation in root.findall("conversation") if (conv_id := conversation.attrib.get("ID"))
            }
        if category == "Books":
            return _extract_books_entries(path)
        return _extract_generic_entries(path)
    except ET.ParseError as exc:
        if _is_blank_xml(path):
            return set()
        return _extract_entries_regex_fallback(path, category=category, reason=exc)


def build_report(base_dir: Path, mod_dir: Path) -> DiffReport:
    """Build a localization coverage report.

    Args:
        base_dir: Base game XML directory.
        mod_dir: Mod localization directory containing ``*.jp.xml`` files.

    Returns:
        Diff report with per-category and total coverage.

    Raises:
        FileNotFoundError: If base_dir or mod_dir does not exist.
        ValueError: If an XML file cannot be parsed.
    """
    _validate_directory(base_dir, label="Base")
    _validate_directory(mod_dir, label="Mod")

    base_map: dict[str, set[str]] = {}
    for base_xml in sorted(base_dir.rglob("*.xml")):
        category = _category_for_base(base_xml, base_dir=base_dir)
        base_map.setdefault(category, set()).update(_extract_entries(base_xml, category=category))

    mod_map: dict[str, set[str]] = {}
    for mod_xml in sorted(mod_dir.rglob("*.jp.xml")):
        category = _category_for_mod(mod_xml, mod_dir=mod_dir)
        mod_map.setdefault(category, set()).update(_extract_entries(mod_xml, category=category))

    categories: list[CategoryDiff] = []
    for category in sorted(base_map.keys() | mod_map.keys()):
        base_entries = base_map.get(category, set())
        mod_entries = mod_map.get(category, set())

        translated_entries = base_entries & mod_entries
        untranslated_entries = sorted(base_entries - mod_entries)
        removed_entries = sorted(mod_entries - base_entries)
        total = len(base_entries)
        translated = len(translated_entries)
        coverage_percent = 0.0 if total == 0 else (translated / total) * 100

        categories.append(
            CategoryDiff(
                category=category,
                total=total,
                translated=translated,
                coverage_percent=coverage_percent,
                untranslated=untranslated_entries,
                removed=removed_entries,
            ),
        )

    total_entries = sum(item.total for item in categories)
    translated_total = sum(item.translated for item in categories)
    untranslated_total = sum(len(item.untranslated) for item in categories)
    removed_total = sum(len(item.removed) for item in categories)
    total_coverage = 0.0 if total_entries == 0 else (translated_total / total_entries) * 100

    return DiffReport(
        categories=categories,
        total=total_entries,
        translated=translated_total,
        coverage_percent=total_coverage,
        untranslated=untranslated_total,
        removed=removed_total,
    )


def _render_summary(report: DiffReport) -> str:
    lines = ["Category              Total  Translated  Coverage"]
    lines.extend(
        f"{item.category:<20}{item.total:>7}{item.translated:>12}{item.coverage_percent:>9.1f}%"
        for item in report.categories
    )
    lines.append(
        f"{'Total':<20}{report.total:>7}{report.translated:>12}{report.coverage_percent:>9.1f}%",
    )
    return "\n".join(lines)


def _render_detailed(report: DiffReport, *, missing_only: bool) -> str:
    lines: list[str] = []
    for item in report.categories:
        if missing_only and not item.untranslated:
            continue

        lines.append(
            f"[{item.category}] total={item.total} translated={item.translated} coverage={item.coverage_percent:.1f}%",
        )

        if item.untranslated:
            lines.append("  untranslated:")
            lines.extend(f"    - {entry}" for entry in item.untranslated)
        elif not missing_only:
            lines.append("  untranslated: (none)")

        if not missing_only:
            if item.removed:
                lines.append("  removed:")
                lines.extend(f"    - {entry}" for entry in item.removed)
            else:
                lines.append("  removed: (none)")

    if missing_only and not lines:
        return "No untranslated entries."
    if not lines:
        return "No categories found."
    return "\n".join(lines)


def _write_json_report(report: DiffReport, json_path: Path) -> None:
    payload = {
        "categories": [asdict(item) for item in report.categories],
        "totals": {
            "total": report.total,
            "translated": report.translated,
            "coverage_percent": report.coverage_percent,
            "untranslated": report.untranslated,
            "removed": report.removed,
        },
    }
    json_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main(argv: list[str] | None = None) -> int:
    """Run the localization diff CLI.

    Args:
        argv: Command-line arguments. Defaults to sys.argv[1:].

    Returns:
        Exit code: 0 on success, 1 on validation or parsing errors.
    """
    parser = argparse.ArgumentParser(
        description="Compare Base XML and Japanese localization XML coverage.",
    )
    parser.add_argument(
        "--summary",
        action="store_true",
        help="Print per-category and total coverage summary.",
    )
    parser.add_argument(
        "--missing-only",
        action="store_true",
        help="Show only untranslated entries.",
    )
    parser.add_argument(
        "--json-path",
        type=Path,
        help="Write detailed diff output as JSON to this path.",
    )
    parser.add_argument(
        "--base-dir",
        type=Path,
        default=_DEFAULT_GAME_BASE_DIR,
        help="Override Base XML directory.",
    )

    try:
        project_root = _find_project_root()
    except FileNotFoundError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    parser.add_argument(
        "--mod-dir",
        type=Path,
        default=project_root / "Mods" / "QudJP" / "Localization",
        help="Override mod localization directory.",
    )

    args = parser.parse_args(argv)

    try:
        report = build_report(args.base_dir, args.mod_dir)
    except (FileNotFoundError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    output = _render_summary(report) if args.summary else _render_detailed(report, missing_only=args.missing_only)
    print(output)  # noqa: T201

    if args.json_path is not None:
        _write_json_report(report, args.json_path)

    return 0


if __name__ == "__main__":
    sys.exit(main())
