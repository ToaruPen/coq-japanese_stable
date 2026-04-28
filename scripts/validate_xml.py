"""Validate translation XML files for structure and common content issues."""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
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

_BARE_QUD_SPAN = re.compile(r"\{\{[^|{}]+\}\}")
_QUD_OPENER = re.compile(r"\{\{[^|}]+\|")
_QUD_CLOSER = re.compile(r"\}\}")
_LITERAL_AMPERSAND = re.compile(r"&&")
_LITERAL_CARET = re.compile(r"\^\^")
_LEGACY_AMPERSAND_COLOR = re.compile(r"(?<!&)&(?!&)[A-Za-z0-9]")
_LEGACY_CARET_COLOR = re.compile(r"(?<!\^)\^(?!\^)[A-Za-z0-9]")
_HTML_COLOR_OPEN = re.compile(r"<color=[^>]+>")
_HTML_COLOR_CLOSE = re.compile(r"</color>")
_VARIABLE_TOKEN = re.compile(r"=[A-Za-z_][A-Za-z0-9_.:']*=")
_MARKUP_TOKEN_PATTERNS = (
    _QUD_OPENER,
    _LITERAL_AMPERSAND,
    _LITERAL_CARET,
    _LEGACY_AMPERSAND_COLOR,
    _LEGACY_CARET_COLOR,
    _HTML_COLOR_OPEN,
    _HTML_COLOR_CLOSE,
    _VARIABLE_TOKEN,
)

type _MarkupUnitKey = tuple[str, str]


@dataclass(frozen=True)
class _ElementPathRecord:
    keyed_path: str
    positional_path: str
    element: ET.Element
    uses_idless_name_fallback: bool
    parent_keyed_path: str | None
    sibling_tag_index: int


type _SourceChildrenByParent = tuple[
    dict[str, _ElementPathRecord],
    defaultdict[str, list[_ElementPathRecord]],
    dict[tuple[str, str, int], _ElementPathRecord],
]


@dataclass(frozen=True)
class _PathWalkState:
    keyed_path: str
    positional_path: str
    uses_idless_name_fallback: bool
    parent_keyed_path: str | None
    sibling_tag_index: int

_MARKUP_COMPARISON_ATTRIBUTES = frozenset(
    {
        "ChargedName",
        "ChargenDescription",
        "ColdName",
        "Description",
        "DisplayName",
        "Gospel",
        "Hagiograph",
        "HeatName",
        "LethalMessage",
        "Long",
        "MaxHitpointsThresholdMessage",
        "Message",
        "Messages",
        "Name",
        "Ordinary",
        "PhaseMessage",
        "Postfix",
        "Prefix",
        "PrepMessageOther",
        "PrepMessageSelf",
        "Short",
        "Snippet",
        "SpawnMessage",
        "Text",
        "Title",
        "Value",
        "message",
        "text",
    }
)
_PATH_KEY_ATTRIBUTES = ("ID", "Name")
_PREFERRED_PATH_DISCRIMINATOR_ATTRIBUTES = (
    "IfHaveState",
    "IfNotHaveState",
    "If",
    "Speaker",
    "GotoID",
)
_PREFERRED_STABLE_REMATCH_ATTRIBUTES = ("ID", "Level", "x", "y")


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


def _ensure_source_root_is_directory(source_root: Path | None) -> None:
    if source_root is None or source_root.is_dir():
        return

    msg = f"source root is not a directory: {source_root}"
    raise ValueError(msg)


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


def _markup_token_multiset(value: str) -> Counter[str]:
    tokens: Counter[str] = Counter()
    tokens.update(match.group(0) for match in _BARE_QUD_SPAN.finditer(value))
    bare_span_stripped = _BARE_QUD_SPAN.sub("", value)
    # _MARKUP_TOKEN_PATTERNS intentionally scan the original value, while
    # _QUD_CLOSER scans bare_span_stripped so closers inside _BARE_QUD_SPAN do
    # not get double-counted.
    for pattern in _MARKUP_TOKEN_PATTERNS:
        tokens.update(match.group(0) for match in pattern.finditer(value))
    tokens.update(match.group(0) for match in _QUD_CLOSER.finditer(bare_span_stripped))
    return tokens


def _format_counter(counter: Counter[str]) -> str:
    return ", ".join(f"{token!r}: {count}" for token, count in sorted(counter.items()))


def _source_xml_relative_path(relative_path: Path) -> Path:
    source_name = relative_path.name.removesuffix(".jp.xml") + ".xml"
    return relative_path.with_name(source_name)


def _source_relative_path(path: Path, *, validation_roots: list[Path]) -> Path | None:
    if not path.name.endswith(".jp.xml"):
        return None

    for candidate in (path, path.resolve()):
        if "Localization" in candidate.parts:
            index = len(candidate.parts) - 1 - candidate.parts[::-1].index("Localization")
            relative_path = Path(*candidate.parts[index + 1 :])
            return _source_xml_relative_path(relative_path)

    resolved_path = path.resolve()
    for validation_root in validation_roots:
        if not validation_root.is_dir():
            continue
        try:
            relative_path = resolved_path.relative_to(validation_root.resolve())
        except ValueError:
            continue
        return _source_xml_relative_path(relative_path)

    return _source_xml_relative_path(Path(path.name))


def _quote_path_value(value: str) -> str:
    return value.replace("\\", "\\\\").replace("'", "\\'")


def _element_path_key_base(element: ET.Element) -> str:
    for key_attribute in _PATH_KEY_ATTRIBUTES:
        if value := element.attrib.get(key_attribute):
            return f"{element.tag}[@{key_attribute}='{_quote_path_value(value)}']"
    return element.tag


def _attribute_signature(element: ET.Element, attribute_names: Iterable[str]) -> tuple[str, ...]:
    return tuple(element.attrib.get(attribute_name, "") for attribute_name in attribute_names)


def _signature_is_unique(elements: list[ET.Element], attribute_names: Iterable[str]) -> bool:
    attribute_names = tuple(attribute_names)
    signatures = [_attribute_signature(element, attribute_names) for element in elements]
    return len(signatures) == len(set(signatures))


def _varying_attributes(elements: list[ET.Element], attribute_names: Iterable[str]) -> list[str]:
    varying_attributes: list[str] = []
    for attribute_name in attribute_names:
        values = {element.attrib.get(attribute_name, "") for element in elements}
        if len(values) > 1 and any(values):
            varying_attributes.append(attribute_name)
    return varying_attributes


def _path_discriminator_attributes(elements: list[ET.Element]) -> tuple[str, ...]:
    discriminator_attributes = _varying_attributes(elements, _PREFERRED_PATH_DISCRIMINATOR_ATTRIBUTES)
    if discriminator_attributes and _signature_is_unique(elements, discriminator_attributes):
        return tuple(discriminator_attributes)

    fallback_attribute_names = sorted(
        {
            attribute_name
            for element in elements
            for attribute_name in element.attrib
            if attribute_name not in _PATH_KEY_ATTRIBUTES
            and attribute_name not in _MARKUP_COMPARISON_ATTRIBUTES
            and attribute_name not in discriminator_attributes
        }
    )
    for attribute_name in _varying_attributes(elements, fallback_attribute_names):
        discriminator_attributes.append(attribute_name)
        if _signature_is_unique(elements, discriminator_attributes):
            break
    return tuple(discriminator_attributes)


def _element_path_base(element: ET.Element, discriminator_attributes: Iterable[str]) -> str:
    path_base = _element_path_key_base(element)
    for attribute_name in discriminator_attributes:
        value = element.attrib.get(attribute_name, "")
        path_base += f"[@{attribute_name}='{_quote_path_value(value)}']"
    return path_base


def _element_path_records(root: ET.Element) -> list[_ElementPathRecord]:
    records: list[_ElementPathRecord] = []

    def walk(element: ET.Element, state: _PathWalkState) -> None:
        uses_idless_name_fallback = state.uses_idless_name_fallback or (
            "ID" not in element.attrib and "Name" in element.attrib
        )
        records.append(
            _ElementPathRecord(
                keyed_path=state.keyed_path,
                positional_path=state.positional_path,
                element=element,
                uses_idless_name_fallback=uses_idless_name_fallback,
                parent_keyed_path=state.parent_keyed_path,
                sibling_tag_index=state.sibling_tag_index,
            )
        )
        keyed_groups: defaultdict[str, list[ET.Element]] = defaultdict(list)
        for child in element:
            keyed_groups[_element_path_key_base(child)].append(child)
        group_discriminator_attributes = {
            key_base: _path_discriminator_attributes(group)
            for key_base, group in keyed_groups.items()
            if len(group) > 1
        }
        sibling_counts: defaultdict[str, int] = defaultdict(int)
        tag_counts: defaultdict[str, int] = defaultdict(int)
        for child in element:
            key_base = _element_path_key_base(child)
            path_base = _element_path_base(child, group_discriminator_attributes.get(key_base, ()))
            sibling_counts[path_base] += 1
            tag_counts[child.tag] += 1
            walk(
                child,
                _PathWalkState(
                    keyed_path=f"{state.keyed_path}/{path_base}[{sibling_counts[path_base]}]",
                    positional_path=f"{state.positional_path}/{child.tag}[{tag_counts[child.tag]}]",
                    uses_idless_name_fallback=uses_idless_name_fallback,
                    parent_keyed_path=state.keyed_path,
                    sibling_tag_index=tag_counts[child.tag],
                ),
            )

    walk(
        root,
        _PathWalkState(
            keyed_path=f"/{root.tag}[1]",
            positional_path=f"/{root.tag}[1]",
            uses_idless_name_fallback=False,
            parent_keyed_path=None,
            sibling_tag_index=1,
        ),
    )
    return records


def _markup_unit_values(root: ET.Element, *, use_positional_paths: bool = False) -> dict[_MarkupUnitKey, str]:
    values: dict[_MarkupUnitKey, str] = {}
    for record in _element_path_records(root):
        element = record.element
        element_path = record.positional_path if use_positional_paths else record.keyed_path
        values[(element_path, "text()")] = element.text or ""
        for attribute_name, attribute_value in element.attrib.items():
            if attribute_name not in _MARKUP_COMPARISON_ATTRIBUTES:
                continue
            values[(element_path, f"@{attribute_name}")] = attribute_value
    return values


def _idless_name_element_fallback_paths(root: ET.Element) -> dict[str, str]:
    return {
        record.keyed_path: record.positional_path
        for record in _element_path_records(root)
        if record.uses_idless_name_fallback
    }


def _stable_source_rematch(
    localized_record: _ElementPathRecord,
    source_candidates: list[_ElementPathRecord],
) -> _ElementPathRecord | None:
    element = localized_record.element
    candidate_attribute_names = [
        attribute_name
        for attribute_name in _PREFERRED_STABLE_REMATCH_ATTRIBUTES
        if element.attrib.get(attribute_name)
    ]
    candidate_attribute_names.extend(
        sorted(
            {
                attribute_name
                for attribute_name, attribute_value in element.attrib.items()
                if attribute_value
                and attribute_name not in candidate_attribute_names
                and attribute_name != "Name"
                and attribute_name not in _MARKUP_COMPARISON_ATTRIBUTES
            }
        )
    )

    selected_attribute_names: list[str] = []
    for attribute_name in candidate_attribute_names:
        selected_attribute_names.append(attribute_name)
        matching_candidates = [
            candidate
            for candidate in source_candidates
            if all(
                candidate.element.attrib.get(selected_name) == element.attrib[selected_name]
                for selected_name in selected_attribute_names
            )
        ]
        if len(matching_candidates) == 1:
            return matching_candidates[0]
    return None


def _source_children_by_parent(
    source_records: list[_ElementPathRecord],
) -> _SourceChildrenByParent:
    source_record_by_keyed_path = {record.keyed_path: record for record in source_records}
    source_children: defaultdict[str, list[_ElementPathRecord]] = defaultdict(list)
    source_child_by_relative_position: dict[tuple[str, str, int], _ElementPathRecord] = {}
    for record in source_records:
        if record.parent_keyed_path is None:
            continue
        source_children[record.parent_keyed_path].append(record)
        source_child_by_relative_position[
            (record.parent_keyed_path, record.element.tag, record.sibling_tag_index)
        ] = record
    return source_record_by_keyed_path, source_children, source_child_by_relative_position


def _is_translated_idless_name_record(record: _ElementPathRecord) -> bool:
    return "ID" not in record.element.attrib and "Name" in record.element.attrib


def _relative_keyed_path(record: _ElementPathRecord) -> str:
    if record.parent_keyed_path is None:
        return record.keyed_path
    return record.keyed_path.removeprefix(f"{record.parent_keyed_path}/")


def _source_child_by_relative_keyed_path(
    localized_record: _ElementPathRecord,
    source_candidates: list[_ElementPathRecord],
) -> _ElementPathRecord | None:
    localized_relative_path = _relative_keyed_path(localized_record)
    for candidate in source_candidates:
        if (
            candidate.element.tag == localized_record.element.tag
            and _relative_keyed_path(candidate) == localized_relative_path
        ):
            return candidate
    return None


def _source_record_for_localized_record(
    localized_record: _ElementPathRecord,
    *,
    source_parent_path: str,
    source_record_by_keyed_path: dict[str, _ElementPathRecord],
    source_children_by_parent: defaultdict[str, list[_ElementPathRecord]],
    source_child_by_relative_position: dict[tuple[str, str, int], _ElementPathRecord],
) -> _ElementPathRecord | None:
    if source_parent_path == localized_record.parent_keyed_path:
        source_record = source_record_by_keyed_path.get(localized_record.keyed_path)
        if source_record is not None:
            return source_record

    source_candidates = source_children_by_parent[source_parent_path]
    source_record = _source_child_by_relative_keyed_path(localized_record, source_candidates)
    if source_record is not None:
        return source_record

    if _is_translated_idless_name_record(localized_record):
        source_record = _stable_source_rematch(
            localized_record,
            [
                candidate
                for candidate in source_candidates
                if candidate.element.tag == localized_record.element.tag
            ],
        )
        if source_record is not None:
            return source_record

    return source_child_by_relative_position.get(
        (source_parent_path, localized_record.element.tag, localized_record.sibling_tag_index)
    )


def _source_stable_rematch_paths(
    localized_root: ET.Element,
    source_root: ET.Element,
) -> dict[str, str]:
    localized_records = _element_path_records(localized_root)
    source_records = _element_path_records(source_root)
    if (
        not localized_records
        or not source_records
        or localized_records[0].element.tag != source_records[0].element.tag
    ):
        return {}

    source_record_by_keyed_path, source_children_by_parent, source_child_by_relative_position = (
        _source_children_by_parent(source_records)
    )

    localized_to_source_paths = {localized_records[0].keyed_path: source_records[0].keyed_path}
    for localized_record in localized_records[1:]:
        if localized_record.parent_keyed_path is None:
            continue
        source_parent_path = localized_to_source_paths.get(localized_record.parent_keyed_path)
        if source_parent_path is None:
            continue

        source_record = _source_record_for_localized_record(
            localized_record,
            source_parent_path=source_parent_path,
            source_record_by_keyed_path=source_record_by_keyed_path,
            source_children_by_parent=source_children_by_parent,
            source_child_by_relative_position=source_child_by_relative_position,
        )

        if source_record is None:
            continue

        localized_to_source_paths[localized_record.keyed_path] = source_record.keyed_path
    return localized_to_source_paths


def _find_markup_token_drift(
    localized_root: ET.Element,
    source_root: ET.Element,
    *,
    source_relative_path: Path,
) -> list[str]:
    warnings: list[str] = []
    localized_values = _markup_unit_values(localized_root)
    source_values = _markup_unit_values(source_root)
    localized_fallback_paths = _idless_name_element_fallback_paths(localized_root)
    source_paths = _source_stable_rematch_paths(localized_root, source_root)
    source_positional_values = _markup_unit_values(source_root, use_positional_paths=True)
    for unit_key, localized_value in sorted(localized_values.items()):
        element_path, unit_name = unit_key
        source_value = source_values.get(unit_key)
        warning_key = unit_key
        if source_value is None and (source_path := source_paths.get(element_path)) is not None:
            source_key = (source_path, unit_name)
            source_value = source_values.get(source_key, "")
            warning_key = source_key
        if source_value is None and (fallback_path := localized_fallback_paths.get(element_path)) is not None:
            fallback_key = (fallback_path, unit_name)
            source_value = source_positional_values.get(fallback_key, "")
            warning_key = fallback_key
        source_tokens = _markup_token_multiset(source_value or "")
        localized_tokens = _markup_token_multiset(localized_value)
        if source_tokens == localized_tokens:
            continue

        missing_tokens = source_tokens - localized_tokens
        extra_tokens = localized_tokens - source_tokens

        details: list[str] = []
        if missing_tokens:
            details.append(f"missing {_format_counter(missing_tokens)}")
        if extra_tokens:
            details.append(f"extra {_format_counter(extra_tokens)}")
        element_path, unit_name = warning_key
        warnings.append(
            f"Markup token drift vs {source_relative_path.as_posix()} at {element_path} {unit_name}: "
            + "; ".join(details)
        )
    return warnings


def _find_source_markup_token_drift(
    path: Path,
    root: ET.Element,
    source_path: Path,
    source_relative_path: Path,
) -> list[str]:
    if not source_path.exists():
        return [f"Source XML not found for {path.name}: {source_relative_path.as_posix()}"]

    try:
        source_root = ET.parse(source_path).getroot()  # noqa: S314 -- local repository XML validation tool
    except OSError as exc:
        return [f"Source XML read failed for {source_relative_path.as_posix()}: {exc}"]
    except ET.ParseError as exc:
        return [f"Source XML parse failed for {source_relative_path.as_posix()}: {exc}"]

    return _find_markup_token_drift(root, source_root, source_relative_path=source_relative_path)


def validate_xml_file(
    path: Path,
    *,
    source_root: Path | None = None,
    validation_roots: list[Path] | None = None,
) -> ValidationResult:
    """Validate one XML file and return its findings.

    Args:
        path: XML file path.
        source_root: Optional base XML root for source-vs-localized markup comparison.
        validation_roots: Original validation inputs used to map file paths to source-relative paths.

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
    if source_root is not None:
        source_relative_path = _source_relative_path(path, validation_roots=validation_roots or [])
        if source_relative_path is not None:
            warnings.extend(
                _find_source_markup_token_drift(
                    path,
                    root,
                    source_root / source_relative_path,
                    source_relative_path,
                )
            )
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
    source_root: Path | None = None,
) -> int:
    """Run validation for input paths.

    Args:
        paths: File or directory paths to validate.
        strict: Treat non-baselined warnings as failures.
        warning_baseline: Existing warning baseline to subtract from strict failures.
        write_warning_baseline: Optional path to write current warnings as a baseline.
        source_root: Optional base XML root for source-vs-localized markup comparison.

    Returns:
        Exit code (0 on success, 1 on failure).
    """
    try:
        xml_files = _collect_xml_files(paths)
        _ensure_source_root_is_directory(source_root)
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
        result = validate_xml_file(file_path, source_root=source_root, validation_roots=paths)
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
    parser.add_argument(
        "--source-root",
        type=Path,
        help="Optional base XML directory used to compare markup tokens in corresponding *.jp.xml files.",
    )

    args = parser.parse_args(argv)
    return run_validation(
        args.paths,
        strict=args.strict,
        warning_baseline=args.warning_baseline,
        write_warning_baseline=args.write_warning_baseline,
        source_root=args.source_root,
    )


if __name__ == "__main__":
    sys.exit(main())
