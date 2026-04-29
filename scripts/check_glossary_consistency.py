"""Validate localized assets against approved glossary English residue."""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import cast

if __package__ in {None, ""}:
    _PROJECT_ROOT = Path(__file__).resolve().parents[1]
    _PROJECT_ROOT_STR = str(_PROJECT_ROOT)
    if _PROJECT_ROOT_STR not in sys.path:
        sys.path.insert(0, _PROJECT_ROOT_STR)

from scripts import check_translation_tokens

_DEFAULT_GLOSSARY = Path("docs/glossary.csv")
_DEFAULT_BASELINE = Path(__file__).with_name("glossary_consistency_baseline.json")
_BASELINE_VERSION = 1
_ACTIVE_STATUSES = frozenset({"approved", "confirmed"})

_VISIBLE_XML_ATTRIBUTES = frozenset(
    {
        "Accomplishment",
        "Activity",
        "ArableLand",
        "BehaviorDescription",
        "BiomeAdjective",
        "BiomeEpithet",
        "Blurb",
        "BuyDescription",
        "Category",
        "ChargenDescription",
        "ChargenTitle",
        "ClassName",
        "ColdName",
        "CollapseMessage",
        "CrafterName",
        "DamageMessage",
        "Description",
        "DetonationMessage",
        "DisplayName",
        "DisplayText",
        "FlightSourceDescription",
        "Frozen",
        "GenotypeAlt",
        "Gospel",
        "Hagiograph",
        "HagiographCategory",
        "HeatName",
        "Long",
        "Message",
        "MessageByCountSuffix",
        "Messages",
        "NameContext",
        "NameForStatus",
        "NeedsItemFor",
        "Ordinary",
        "PhaseMessage",
        "PoeticFeatures",
        "Postfix",
        "Prefix",
        "PrepMessageOther",
        "PrepMessageSelf",
        "Primary",
        "PullMessage",
        "RecipeText",
        "SacredThing",
        "Says",
        "ScopeDescription",
        "Short",
        "SingularTitle",
        "Skin",
        "Snippet",
        "SpawnMessage",
        "Text",
        "Title",
        "TinkerDisplayName",
        "ValuedOre",
        "Verb",
        "VillageActivity",
        "YounglingNoise",
        "message",
        "text",
    }
)
_VISIBLE_NAME_ELEMENT_TAGS = frozenset({"zone"})

_GAME_TEXT_VARIABLE = re.compile(r"=[A-Za-z_][A-Za-z0-9_.:']*=")
_BRACE_PLACEHOLDER = re.compile(r"\{[A-Za-z0-9_][A-Za-z0-9_.:-]*\}")
_LOWERCASE_STAR_PLACEHOLDER = re.compile(r"\*[a-z][a-z0-9_.:-]*\*")
_QUD_OPENER = re.compile(r"\{\{[^|}]+\|")
_BARE_QUD_SPAN = re.compile(r"\{\{[^|{}]+\}\}")
_LEGACY_COLOR = re.compile(r"(?<![&^])[&^][A-Za-z0-9]")
_HTML_COLOR_TAG = re.compile(r"</?color=[^>]+>|</color>")


@dataclass(frozen=True)
class GlossaryTerm:
    """One authoritative glossary row."""

    english: str
    japanese: str
    status: str
    pattern: re.Pattern[str]


@dataclass(frozen=True)
class LocalizedValue:
    """One player-visible localized value scanned by the glossary gate."""

    relative_path: str
    location: str
    text: str


@dataclass(frozen=True)
class GlossaryIssue:
    """A glossary consistency finding."""

    kind: str
    relative_path: str
    location: str
    term: str
    expected_japanese: str | None
    text: str


@dataclass(frozen=True)
class CheckResult:
    """Glossary consistency check result."""

    file_count: int
    value_count: int
    issues: list[GlossaryIssue]


@dataclass(frozen=True)
class _BaselineOccurrence:
    term: str
    path: str
    location: str
    text: str


def load_glossary_terms(path: Path) -> list[GlossaryTerm]:
    """Load confirmed/approved glossary terms that have Japanese equivalents."""
    terms: list[GlossaryTerm] = []
    first_active_english_by_normalized: dict[str, str] = {}
    with path.open(encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            status = row.get("Status", "").strip().lower()
            english = row.get("English", "").strip()
            japanese = row.get("Japanese", "").strip()
            if status not in _ACTIVE_STATUSES or not english or not japanese or english == japanese:
                continue
            normalized_english = english.casefold()
            first_english = first_active_english_by_normalized.get(normalized_english)
            if first_english is not None:
                msg = (
                    f"Duplicate active glossary English term '{english}' in {path} "
                    f"(first active term: '{first_english}')"
                )
                raise ValueError(msg)
            first_active_english_by_normalized[normalized_english] = english
            terms.append(
                GlossaryTerm(
                    english=english,
                    japanese=japanese,
                    status=status,
                    pattern=_compile_term_pattern(english),
                ),
            )
    return terms


def _compile_term_pattern(term: str) -> re.Pattern[str]:
    return re.compile(rf"(?<![A-Za-z0-9]){re.escape(term)}(?![A-Za-z0-9])", re.IGNORECASE)


def _load_json(path: Path) -> object:
    return cast("object", json.loads(path.read_text(encoding="utf-8")))


def _relative_path_from_marker(path: Path, marker: str) -> str | None:
    parts = path.parts
    if marker not in parts:
        return None
    index = len(parts) - 1 - parts[::-1].index(marker)
    return Path(*parts[index + 1 :]).as_posix()


def _relative_localization_path(path: Path) -> str:
    for candidate in (path, path.resolve()):
        relative_path = _relative_path_from_marker(candidate, "Localization")
        if relative_path is not None:
            return relative_path
    return path.name


def _collect_xml_files(paths: list[Path]) -> list[Path]:
    files: set[Path] = set()
    for input_path in paths:
        if not input_path.exists():
            msg = f"Path not found: {input_path}"
            raise FileNotFoundError(msg)

        resolved_input_path = input_path.resolve()
        if resolved_input_path.is_file():
            if resolved_input_path.name.endswith(".jp.xml"):
                files.add(resolved_input_path)
            continue

        if resolved_input_path.is_dir():
            files.update(path.resolve() for path in resolved_input_path.rglob("*.jp.xml") if path.is_file())
            continue

        msg = f"Path is not a regular file or directory: {input_path}"
        raise ValueError(msg)
    return sorted(files, key=lambda item: item.as_posix())


def _iter_json_values(paths: list[Path]) -> tuple[set[str], list[LocalizedValue]]:
    files = check_translation_tokens.collect_translation_json_files(paths)
    values: list[LocalizedValue] = []
    for path in files:
        values.extend(
            LocalizedValue(
                relative_path=entry.relative_path,
                location=f"entry[{entry.index}].text",
                text=entry.text,
            )
            for entry in check_translation_tokens.iter_translation_entries(path)
        )
    return {_relative_localization_path(path) for path in files}, values


def _iter_xml_values(paths: list[Path]) -> tuple[set[str], list[LocalizedValue]]:
    files = _collect_xml_files(paths)
    values: list[LocalizedValue] = []
    for path in files:
        root = ET.parse(path).getroot()  # noqa: S314 -- Scans local repo localization assets only.
        relative_path = _relative_localization_path(path)
        values.extend(_localized_values_from_xml(root, relative_path=relative_path))
    return {_relative_localization_path(path) for path in files}, values


def _localized_values_from_xml(root: ET.Element, *, relative_path: str) -> list[LocalizedValue]:
    values: list[LocalizedValue] = []

    def walk(element: ET.Element, element_path: str) -> None:
        if element.text and element.text.strip():
            values.append(
                LocalizedValue(
                    relative_path=relative_path,
                    location=f"{element_path}/text()",
                    text=element.text.strip(),
                ),
            )

        for attribute_name, attribute_value in element.attrib.items():
            if not _is_visible_xml_attribute(element, attribute_name):
                continue
            values.append(
                LocalizedValue(
                    relative_path=relative_path,
                    location=f"{element_path}/@{attribute_name}",
                    text=attribute_value,
                ),
            )

        child_counts: dict[str, int] = {}
        for child in element:
            child_counts[child.tag] = child_counts.get(child.tag, 0) + 1
            child_path = f"{element_path}/{child.tag}[{child_counts[child.tag]}]"
            walk(child, child_path)
            if child.tail and child.tail.strip():
                values.append(
                    LocalizedValue(
                        relative_path=relative_path,
                        location=f"{child_path}/tail()",
                        text=child.tail.strip(),
                    ),
                )

    walk(root, f"/{root.tag}[1]")
    return values


def _is_visible_xml_attribute(element: ET.Element, attribute_name: str) -> bool:
    return attribute_name in _VISIBLE_XML_ATTRIBUTES or (
        attribute_name == "Name" and element.tag in _VISIBLE_NAME_ELEMENT_TAGS
    )


def _visible_text_for_matching(value: str) -> str:
    text = _GAME_TEXT_VARIABLE.sub(" ", value)
    text = _BRACE_PLACEHOLDER.sub(" ", text)
    text = _LOWERCASE_STAR_PLACEHOLDER.sub(" ", text)
    text = _BARE_QUD_SPAN.sub(" ", text)
    text = _QUD_OPENER.sub("", text).replace("}}", "")
    text = _LEGACY_COLOR.sub(" ", text)
    return _HTML_COLOR_TAG.sub(" ", text)


def _find_raw_english_issues(values: list[LocalizedValue], terms: list[GlossaryTerm]) -> list[GlossaryIssue]:
    issues: list[GlossaryIssue] = []
    for value in values:
        visible_text = _visible_text_for_matching(value.text)
        for term in terms:
            if not term.pattern.search(visible_text):
                continue
            issues.append(
                GlossaryIssue(
                    kind="RAW_ENGLISH",
                    relative_path=value.relative_path,
                    location=value.location,
                    term=term.english,
                    expected_japanese=term.japanese,
                    text=value.text,
                ),
            )
    return issues


def _load_baseline(path: Path | None) -> dict[tuple[str, str, str, str], _BaselineOccurrence]:
    if path is None:
        return {}
    if not path.exists():
        msg = f"Glossary baseline file not found: {path}"
        raise FileNotFoundError(msg)
    payload = _load_json(path)
    if not isinstance(payload, dict):
        msg = f"Glossary baseline must be a JSON object: {path}"
        raise ValueError(msg)  # noqa: TRY004 -- Baseline schema validation reports ValueError.
    payload_fields = cast("dict[object, object]", payload)
    if payload_fields.get("version") != _BASELINE_VERSION:
        msg = f"Glossary baseline expected version {_BASELINE_VERSION}: {path}"
        raise ValueError(msg)
    occurrences = payload_fields.get("allowed_occurrences")
    if not isinstance(occurrences, list):
        msg = f"Glossary baseline must contain an 'allowed_occurrences' list: {path}"
        raise ValueError(msg)  # noqa: TRY004 -- Baseline schema validation reports ValueError.
    occurrence_items = cast("list[object]", occurrences)

    baseline: dict[tuple[str, str, str, str], _BaselineOccurrence] = {}
    for item in occurrence_items:
        occurrence = _baseline_occurrence(item, path)
        identity = _baseline_identity(occurrence)
        if identity in baseline:
            msg = f"Glossary baseline contains duplicate occurrence for {identity!r}: {path}"
            raise ValueError(msg)
        baseline[identity] = occurrence
    return baseline


def _baseline_occurrence(item: object, baseline_path: Path) -> _BaselineOccurrence:
    if not isinstance(item, dict):
        msg = f"Invalid glossary baseline occurrence in {baseline_path}: {item!r}"
        raise ValueError(msg)  # noqa: TRY004 -- Baseline schema validation reports ValueError.
    fields = cast("dict[object, object]", item)
    item_context = repr(fields)
    return _BaselineOccurrence(
        term=_baseline_string_field(fields, "term", item_context=item_context, baseline_path=baseline_path),
        path=_baseline_string_field(fields, "path", item_context=item_context, baseline_path=baseline_path),
        location=_baseline_string_field(fields, "location", item_context=item_context, baseline_path=baseline_path),
        text=_baseline_string_field(fields, "text", item_context=item_context, baseline_path=baseline_path),
    )


def _baseline_string_field(
    fields: dict[object, object],
    field_name: str,
    *,
    item_context: str,
    baseline_path: Path,
) -> str:
    value = fields.get(field_name)
    if not isinstance(value, str):
        msg = (
            f"Invalid glossary baseline occurrence in {baseline_path}: "
            f"field {field_name!r} must be a string in {item_context}"
        )
        raise ValueError(msg)  # noqa: TRY004 -- Baseline schema validation reports ValueError.
    return value


def _baseline_identity(occurrence: _BaselineOccurrence) -> tuple[str, str, str, str]:
    return (occurrence.term, occurrence.path, occurrence.location, occurrence.text)


def _issue_identity(issue: GlossaryIssue) -> tuple[str, str, str, str]:
    return (issue.term, issue.relative_path, issue.location, issue.text)


def _apply_baseline(
    issues: list[GlossaryIssue],
    baseline: dict[tuple[str, str, str, str], _BaselineOccurrence],
    *,
    scanned_paths: set[str],
    report_unscanned_baseline: bool,
) -> list[GlossaryIssue]:
    current_identities = {_issue_identity(issue) for issue in issues}
    filtered_issues = [issue for issue in issues if _issue_identity(issue) not in baseline]
    for identity, occurrence in sorted(baseline.items()):
        if identity in current_identities:
            continue
        if not report_unscanned_baseline and occurrence.path not in scanned_paths:
            continue
        filtered_issues.append(
            GlossaryIssue(
                kind="BASELINE",
                relative_path=occurrence.path,
                location=occurrence.location,
                term=occurrence.term,
                expected_japanese=None,
                text=occurrence.text,
            ),
        )
    return filtered_issues


def _is_full_localization_scan(paths: list[Path]) -> bool:
    for path in paths:
        if not path.exists():
            continue
        resolved_path = path.resolve()
        if resolved_path.is_dir() and resolved_path.name == "Localization":
            return True
    return False


def check_paths(
    paths: list[Path],
    *,
    glossary_path: Path = _DEFAULT_GLOSSARY,
    baseline_path: Path | None = _DEFAULT_BASELINE,
) -> CheckResult:
    """Check localized JSON/XML values for raw English glossary residue."""
    terms = load_glossary_terms(glossary_path)
    json_paths, json_values = _iter_json_values(paths)
    xml_paths, xml_values = _iter_xml_values(paths)
    values = [*json_values, *xml_values]
    scanned_paths = json_paths | xml_paths
    issues = _find_raw_english_issues(values, terms)
    issues = _apply_baseline(
        issues,
        _load_baseline(baseline_path),
        scanned_paths=scanned_paths,
        report_unscanned_baseline=_is_full_localization_scan(paths),
    )
    return CheckResult(
        file_count=len(scanned_paths),
        value_count=len(values),
        issues=sorted(issues, key=lambda issue: (issue.relative_path, issue.location, issue.term, issue.kind)),
    )


def _print_report(result: CheckResult) -> None:
    value_count = result.value_count
    file_count = result.file_count
    issue_count = len(result.issues)
    summary = f"Scanned {value_count} localized value(s) from {file_count} file(s): {issue_count} issue(s)"
    print(summary)  # noqa: T201
    for issue in result.issues:
        print(f"  [{issue.kind}] {issue.relative_path} {issue.location}: term={issue.term!r}")  # noqa: T201
        if issue.expected_japanese is not None:
            print(f"    expected Japanese={issue.expected_japanese!r}")  # noqa: T201
        print(f"    text={issue.text!r}")  # noqa: T201


def main(argv: list[str] | None = None) -> int:
    """Run the glossary consistency gate CLI."""
    parser = argparse.ArgumentParser(
        description="Validate approved/confirmed glossary terms against localized JSON/XML values.",
    )
    _ = parser.add_argument("paths", nargs="+", type=Path, help="Localization file or directory paths to scan.")
    _ = parser.add_argument("--glossary", type=Path, default=_DEFAULT_GLOSSARY, help="Glossary CSV path.")
    _ = parser.add_argument("--baseline", type=Path, default=_DEFAULT_BASELINE, help="Known occurrence baseline JSON.")
    _ = parser.add_argument("--no-baseline", action="store_true", help="Do not suppress known occurrences.")
    args = parser.parse_args(argv)
    paths = cast("list[Path]", args.paths)
    glossary_path = cast("Path", args.glossary)
    baseline_path = None if cast("bool", args.no_baseline) else cast("Path", args.baseline)

    try:
        result = check_paths(
            paths,
            glossary_path=glossary_path,
            baseline_path=baseline_path,
        )
    except (
        ET.ParseError,
        FileNotFoundError,
        json.JSONDecodeError,
        TypeError,
        UnicodeDecodeError,
        ValueError,
    ) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    if result.file_count == 0:
        print("Error: No target localized JSON/XML files found.", file=sys.stderr)  # noqa: T201
        return 1

    _print_report(result)
    return 1 if result.issues else 0


if __name__ == "__main__":
    sys.exit(main())
