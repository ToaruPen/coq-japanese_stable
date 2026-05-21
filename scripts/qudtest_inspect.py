"""Inspect QudTest runtime result artifacts produced by in-game wish commands."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import cast

DEFAULT_RESULTS_ROOT = Path.home() / "Library/Application Support/Freehold Games/CavesOfQud/Local/QudTest"
DEFAULT_PLAYER_LOG = Path.home() / "Library/Logs/Freehold Games/CavesOfQud/Player.log"
type JsonObject = dict[str, object]
FATAL_PLAYER_LOG_PATTERNS = (
    "QudJP: compile error",
    "== COMPILER ERRORS ==",
    "Exception compiling mod assembly:",
    "Exception running variable replacer",
    "HarmonyException",
    "MissingMethodException",
    "TypeLoadException",
)
DYNAMIC_RESULT_SUITES = frozenset({"bindings-all"})


@dataclass(frozen=True)
class InspectionInputs:
    """Inputs required to validate the latest QudTest runtime artifact."""

    fixtures: Path
    results: Path
    player_log: Path
    expected_mod_language: str
    max_age_seconds: int
    skip_player_log: bool


def _read_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        msg = f"missing file: {path}"
        raise ValueError(msg) from exc
    except json.JSONDecodeError as exc:
        msg = f"invalid JSON in {path}: {exc}"
        raise ValueError(msg) from exc


def _iter_fixture_documents(fixtures: Path) -> list[Path]:
    if not fixtures.exists():
        msg = f"missing fixtures directory: {fixtures}"
        raise ValueError(msg)
    paths = sorted(fixtures.glob("*.json"))
    if not paths:
        msg = f"no fixture files found in {fixtures}"
        raise ValueError(msg)
    return paths


def _load_fixture_expectations(fixtures: Path, suite: str) -> dict[str, JsonObject]:
    if suite in DYNAMIC_RESULT_SUITES:
        return {}

    expectations: dict[str, JsonObject] = {}
    for case in _iter_fixture_cases(fixtures, suite):
        case_id = case.get("id")
        if not isinstance(case_id, str) or not case_id:
            msg = "fixture case without id"
            raise ValueError(msg)
        if case_id in expectations:
            msg = f"duplicate fixture case: {case_id}"
            raise ValueError(msg)
        expectations[case_id] = case
    if not expectations:
        msg = f"no fixture cases found in {fixtures}"
        raise ValueError(msg)
    return expectations


def _iter_fixture_cases(fixtures: Path, suite: str) -> list[JsonObject]:
    fixture_cases: list[JsonObject] = []
    for path in _iter_fixture_documents(fixtures):
        document = _read_json(path)
        if not isinstance(document, dict):
            msg = f"fixture root must be a JSON object: {path}"
            raise TypeError(msg)
        document_suite = document.get("suite")
        if not _should_include_fixture_suite(document_suite, suite):
            continue
        cases = document.get("cases", [])
        if not isinstance(cases, list):
            msg = f"fixture cases must be a JSON array: {path}"
            raise TypeError(msg)
        for raw_case in cases:
            if not isinstance(raw_case, dict):
                msg = f"fixture case must be a JSON object: {path}"
                raise TypeError(msg)
            case = cast("JsonObject", raw_case)
            case_id = case.get("id")
            if not isinstance(case_id, str) or not case_id:
                msg = f"fixture case without id in {path}"
                raise ValueError(msg)
            fixture_cases.append(case)
    return fixture_cases


def _should_include_fixture_suite(document_suite: object, result_suite: str) -> bool:
    return result_suite in ("all", document_suite)


def _read_result_cases(document: JsonObject) -> dict[str, JsonObject]:
    cases: dict[str, JsonObject] = {}
    result_cases = document.get("cases", [])
    if not isinstance(result_cases, list):
        msg = "result cases must be a JSON array"
        raise TypeError(msg)
    for raw_case in result_cases:
        if not isinstance(raw_case, dict):
            msg = "result case must be a JSON object"
            raise TypeError(msg)
        case = cast("JsonObject", raw_case)
        case_id = case.get("id")
        if not isinstance(case_id, str) or not case_id:
            msg = "result case without id"
            raise ValueError(msg)
        if case_id in cases:
            msg = f"duplicate result case: {case_id}"
            raise ValueError(msg)
        cases[case_id] = case
    return cases


def _parse_ended_at(value: object) -> datetime:
    if not isinstance(value, str) or not value:
        msg = "results missing endedAtUtc"
        raise ValueError(msg)
    normalized = value.removesuffix("Z") + "+00:00" if value.endswith("Z") else value
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError as exc:
        msg = f"invalid endedAtUtc: {value}"
        raise ValueError(msg) from exc
    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=UTC)
    return parsed.astimezone(UTC)


def _validate_freshness(document: JsonObject, max_age_seconds: int) -> list[str]:
    ended_at = _parse_ended_at(document.get("endedAtUtc"))
    age = datetime.now(UTC) - ended_at
    if age.total_seconds() > max_age_seconds:
        return [f"stale results: endedAtUtc={ended_at.isoformat()} age_seconds={int(age.total_seconds())}"]
    return []


def _validate_mod_language(document: JsonObject, expected_mod_language: str) -> list[str]:
    actual_language = document.get("modLanguage")
    if actual_language != expected_mod_language:
        return [f"wrong mod language: expected {expected_mod_language}, got {actual_language}"]
    return []


def _validate_fixture_matches(
    fixture_cases: dict[str, JsonObject],
    result_cases: dict[str, JsonObject],
) -> list[str]:
    if not fixture_cases:
        return []

    errors: list[str] = []
    for case_id, fixture_case in fixture_cases.items():
        result_case = result_cases.get(case_id)
        if result_case is None:
            errors.append(f"missing result case: {case_id}")
            continue
        fixture_expected = _fixture_expected(fixture_case)
        result_expected = result_case.get("expected")
        if fixture_expected != result_expected:
            errors.append(
                f"fixture expected mismatch: {case_id}: fixture={fixture_expected!r} result={result_expected!r}",
            )
    return errors


def _fixture_expected(fixture_case: JsonObject) -> object:
    expected_targets = fixture_case.get("expectedTargets")
    if isinstance(expected_targets, list):
        if not all(isinstance(target, str) for target in expected_targets):
            msg = "fixture expectedTargets must contain only strings"
            raise TypeError(msg)
        return "\n".join(sorted(expected_targets))
    return fixture_case.get("expected")


def _validate_result_cases(document: JsonObject, result_cases: dict[str, JsonObject]) -> list[str]:
    errors: list[str] = []
    if document.get("passed") is not True:
        errors.append("result document reports passed=false")
    for case_id, result_case in result_cases.items():
        if result_case.get("passed") is True:
            continue
        expected = result_case.get("expected")
        actual = result_case.get("actual")
        diagnostic = result_case.get("diagnostic") or ""
        errors.append(
            f"failed case: {case_id}: expected={expected!r} actual={actual!r} {diagnostic}".rstrip(),
        )
    return errors


def _inspect_player_log(path: Path) -> list[str]:
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except FileNotFoundError:
        return [f"missing Player.log: {path}"]
    for pattern in FATAL_PLAYER_LOG_PATTERNS:
        if pattern in text:
            return [f"fatal Player.log pattern: {pattern}"]
    return []


def inspect(inputs: InspectionInputs) -> list[str]:
    """Return validation errors for a QudTest result artifact."""
    result_document = _read_json(inputs.results)
    if not isinstance(result_document, dict):
        msg = "results root must be a JSON object"
        raise TypeError(msg)
    result_object = cast("JsonObject", result_document)
    result_suite = result_object.get("suite")
    if not isinstance(result_suite, str) or not result_suite:
        msg = "results missing suite"
        raise ValueError(msg)
    fixture_cases = _load_fixture_expectations(inputs.fixtures, result_suite)
    result_cases = _read_result_cases(result_object)

    errors: list[str] = []
    errors.extend(_validate_mod_language(result_object, inputs.expected_mod_language))
    errors.extend(_validate_freshness(result_object, inputs.max_age_seconds))
    errors.extend(_validate_fixture_matches(fixture_cases, result_cases))
    errors.extend(_validate_result_cases(result_object, result_cases))
    if not inputs.skip_player_log:
        errors.extend(_inspect_player_log(inputs.player_log))
    return errors


def list_runs(runs_dir: Path, limit: int) -> list[str]:
    """Return recent QudTest run summaries from newest to oldest."""
    if not runs_dir.exists():
        return []
    lines: list[str] = []
    for run_dir in sorted((path for path in runs_dir.iterdir() if path.is_dir()), reverse=True)[:limit]:
        result_path = run_dir / "results.json"
        if not result_path.exists():
            continue
        document = _read_json(result_path)
        if not isinstance(document, dict):
            continue
        passed = "true" if document.get("passed") is True else "false"
        total = document.get("totalCount", 0)
        failed = document.get("failCount", 0)
        lines.append(f"{run_dir.name} passed={passed} total={total} failed={failed}")
    return lines


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixtures", type=Path, default=Path("Mods/QudJP/QudTest/fixtures"))
    parser.add_argument("--results", type=Path, default=DEFAULT_RESULTS_ROOT / "results.json")
    parser.add_argument("--player-log", type=Path, default=DEFAULT_PLAYER_LOG)
    parser.add_argument("--expected-mod-language", default="ja")
    parser.add_argument("--max-age-seconds", type=int, default=1800)
    parser.add_argument("--skip-player-log", action="store_true")
    parser.add_argument("--list-runs", action="store_true")
    parser.add_argument("--runs-dir", type=Path, default=DEFAULT_RESULTS_ROOT / "runs")
    parser.add_argument("--limit", type=int, default=10)
    return parser


def main(argv: list[str] | None = None) -> int:
    """Run the QudTest artifact inspector CLI."""
    parser = _build_parser()
    args = parser.parse_args(argv)
    try:
        if args.list_runs:
            for line in list_runs(args.runs_dir, args.limit):
                sys.stdout.write(line + "\n")
            return 0

        errors = inspect(
            InspectionInputs(
                fixtures=args.fixtures,
                results=args.results,
                player_log=args.player_log,
                expected_mod_language=args.expected_mod_language,
                max_age_seconds=args.max_age_seconds,
                skip_player_log=args.skip_player_log,
            ),
        )
    except (TypeError, ValueError) as exc:
        sys.stderr.write(str(exc) + "\n")
        return 1

    if errors:
        for error in errors:
            sys.stderr.write(error + "\n")
        return 1
    sys.stdout.write("QudTest passed\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
