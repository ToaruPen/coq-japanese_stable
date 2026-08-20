"""Validate localized quest-step gameplay semantics against the base XML."""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

StepContract = dict[str, object]
QuestStepGameplayContract = dict[str, tuple[StepContract, ...]]

_INT32_MIN = -2_147_483_648
_INT32_MAX = 2_147_483_647
_INPUT_ERROR_EXIT_CODE = 2

_SEMANTIC_FIELDS = (
    "Value",
    "XP",
    "Optional",
    "Ordinal",
    "Collapse",
    "Awarded",
    "Failed",
    "Hidden",
    "Base",
)


class DuplicateRuntimeIDError(ValueError):
    """Raised when quest XML contains an ambiguous runtime identity."""


def _runtime_id(element: ET.Element) -> str | None:
    runtime_id = element.get("ID")
    return runtime_id if runtime_id is not None else element.get("Name")


def _parse_int_attribute(element: ET.Element, name: str, default: int) -> int:
    raw_value = element.get(name)
    if raw_value is None:
        return default
    normalized = raw_value.strip()
    if re.fullmatch(r"[+-]?[0-9]+", normalized) is None:
        return default
    try:
        value = int(normalized)
    except ValueError:
        return default
    return value if _INT32_MIN <= value <= _INT32_MAX else default


def _parse_bool_attribute(element: ET.Element, name: str, *, default: bool) -> bool:
    raw_value = element.get(name)
    if raw_value is None:
        return default
    normalized = raw_value.strip().casefold()
    if normalized == "true":
        return True
    if normalized == "false":
        return False
    return default


def _normalize_step(step: ET.Element, document_order: int) -> StepContract:
    return {
        "ID": _runtime_id(step),
        "Value": step.get("Value"),
        "XP": _parse_int_attribute(step, "XP", 0),
        "Optional": _parse_bool_attribute(step, "Optional", default=False),
        "Ordinal": _parse_int_attribute(step, "Ordinal", document_order),
        "Collapse": _parse_bool_attribute(step, "Collapse", default=True),
        "Awarded": _parse_bool_attribute(step, "Awarded", default=False),
        "Failed": _parse_bool_attribute(step, "Failed", default=False),
        "Hidden": _parse_bool_attribute(step, "Hidden", default=False),
        "Base": _parse_bool_attribute(step, "Base", default=False),
    }


def build_step_gameplay_contract(root: ET.Element) -> QuestStepGameplayContract:
    """Build the QuestLoader-visible step contract in XML document order."""
    contract: QuestStepGameplayContract = {}
    for quest in root.findall("quest"):
        quest_id = _runtime_id(quest) or ""
        if quest_id in contract:
            msg = f"duplicate quest runtime ID {quest_id!r}"
            raise DuplicateRuntimeIDError(msg)

        steps: list[StepContract] = []
        seen_step_ids: set[object] = set()
        for index, step in enumerate(quest.findall("step")):
            step_id = _runtime_id(step)
            if step_id in seen_step_ids:
                msg = f"quest {quest_id!r}: duplicate step runtime ID {step_id!r}"
                raise DuplicateRuntimeIDError(msg)
            seen_step_ids.add(step_id)
            steps.append(_normalize_step(step, index))
        contract[quest_id] = tuple(steps)
    return contract


def _explicit_ordinal_ids(root: ET.Element) -> dict[str, set[object]]:
    return {
        _runtime_id(quest) or "": {
            _runtime_id(step) for step in quest.findall("step") if step.get("Ordinal") is not None
        }
        for quest in root.findall("quest")
    }


def _step_ids(steps: tuple[StepContract, ...]) -> list[object]:
    return [step["ID"] for step in steps]


def _compare_step_ids(
    quest_id: str,
    base_steps: tuple[StepContract, ...],
    localized_steps: tuple[StepContract, ...],
) -> list[str]:
    base_ids = _step_ids(base_steps)
    localized_ids = _step_ids(localized_steps)
    base_id_set = set(base_ids)
    localized_id_set = set(localized_ids)
    missing_ids = [step_id for step_id in base_ids if step_id not in localized_id_set]
    extra_ids = [step_id for step_id in localized_ids if step_id not in base_id_set]

    diagnostics: list[str] = []
    if missing_ids:
        diagnostics.append(f"quest {quest_id!r}: missing localized step IDs: {missing_ids!r}")
    if extra_ids:
        diagnostics.append(f"quest {quest_id!r}: extra localized step IDs: {extra_ids!r}")
    if not missing_ids and not extra_ids and base_ids != localized_ids:
        diagnostics.append(
            f"quest {quest_id!r}: reordered localized step IDs "
            f"(base={base_ids!r}, localized={localized_ids!r})"
        )
    return diagnostics


def _format_semantic(value: object) -> str:
    if isinstance(value, bool):
        return str(value).lower()
    if value is None:
        return "null"
    if isinstance(value, str):
        return repr(value)
    return str(value)


def _compare_step_semantics(
    quest_id: str,
    base_steps: tuple[StepContract, ...],
    localized_steps: tuple[StepContract, ...],
    *,
    base_explicit_ordinals: set[object],
    localized_explicit_ordinals: set[object],
    ordered_ids_match: bool,
) -> list[str]:
    base_by_id = {step["ID"]: step for step in base_steps}
    diagnostics: list[str] = []
    for localized_step in localized_steps:
        step_id = localized_step["ID"]
        base_step = base_by_id.get(step_id)
        if base_step is None:
            continue
        for field in _SEMANTIC_FIELDS:
            base_value = base_step[field]
            localized_value = localized_step[field]
            if (
                field == "Ordinal"
                and not ordered_ids_match
                and step_id not in base_explicit_ordinals
                and step_id not in localized_explicit_ordinals
            ):
                continue
            if base_value != localized_value:
                diagnostics.append(
                    f"quest {quest_id!r}, step {step_id!r}: {field} mismatch "
                    f"(base={_format_semantic(base_value)}, "
                    f"localized={_format_semantic(localized_value)})"
                )
    return diagnostics


def compare_quest_step_contracts(base_root: ET.Element, localized_root: ET.Element) -> list[str]:
    """Return deterministic drift diagnostics for quests owned by localized XML."""
    diagnostics: list[str] = []
    try:
        base_contract = build_step_gameplay_contract(base_root)
    except DuplicateRuntimeIDError as exc:
        diagnostics.append(f"base quests: {exc}")
        base_contract = None
    try:
        localized_contract = build_step_gameplay_contract(localized_root)
    except DuplicateRuntimeIDError as exc:
        diagnostics.append(f"localized quests: {exc}")
        localized_contract = None

    if base_contract is None or localized_contract is None:
        return diagnostics

    base_explicit_ordinals = _explicit_ordinal_ids(base_root)
    localized_explicit_ordinals = _explicit_ordinal_ids(localized_root)

    for quest_id, localized_steps in localized_contract.items():
        if quest_id not in base_contract:
            diagnostics.append(f"quest {quest_id!r}: missing from base quests")
            continue
        base_steps = base_contract[quest_id]
        step_id_diagnostics = _compare_step_ids(quest_id, base_steps, localized_steps)
        diagnostics.extend(step_id_diagnostics)
        diagnostics.extend(
            _compare_step_semantics(
                quest_id,
                base_steps,
                localized_steps,
                base_explicit_ordinals=base_explicit_ordinals[quest_id],
                localized_explicit_ordinals=localized_explicit_ordinals[quest_id],
                ordered_ids_match=not step_id_diagnostics,
            )
        )

    return diagnostics


def _load_xml(path: Path, role: str) -> tuple[ET.Element | None, str | None]:
    try:
        return ET.parse(path).getroot(), None  # noqa: S314 -- user-selected local XML
    except ET.ParseError as exc:
        return None, f"error: malformed {role} quests XML {str(path)!r}: {exc}"
    except OSError as exc:
        detail = exc.strerror or str(exc)
        return None, f"error: cannot read {role} quests XML {str(path)!r}: {detail}"


def main(argv: list[str] | None = None) -> int:
    """Run the quest-step gameplay contract validator CLI."""
    parser = argparse.ArgumentParser(
        description="Compare localized quest-step gameplay semantics with the base quest XML."
    )
    parser.add_argument("base_quests", type=Path)
    parser.add_argument("localized_quests", type=Path)
    args = parser.parse_args(argv)

    base_root, base_error = _load_xml(args.base_quests, "base")
    localized_root, localized_error = _load_xml(args.localized_quests, "localized")
    input_errors = [error for error in (base_error, localized_error) if error is not None]
    if input_errors:
        for error in input_errors:
            print(error, file=sys.stderr)  # noqa: T201
        return _INPUT_ERROR_EXIT_CODE
    if base_root is None or localized_root is None:
        return _INPUT_ERROR_EXIT_CODE

    diagnostics = compare_quest_step_contracts(base_root, localized_root)
    for diagnostic in diagnostics:
        print(diagnostic)  # noqa: T201
    return int(bool(diagnostics))


if __name__ == "__main__":
    raise SystemExit(main())
