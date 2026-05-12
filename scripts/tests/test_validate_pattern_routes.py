"""Tests for the validate_pattern_routes module."""

import json
import re
from pathlib import Path

import pytest

from scripts.validate_pattern_routes import ALLOWED_ROUTES, main, validate_pattern_routes

_EXPECTED_MESSAGE_ROUTE_COUNTS = {
    "message-frame": 41,
    "popup": 12,
    "journal": 0,
    "leaf": 0,
    "emit-message": 303,
    "does-verb": 0,
    "message-log": 1,
    "description": 4,
    "effect-cripple": 1,
    "needs-harmony-patch": 37,
    "unclassified": 6,
}

_REPO_ROOT = Path(__file__).resolve().parents[2]
_REPOSITORY_MESSAGE_PATTERNS_PATH = (
    _REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries" / "messages.ja.json"
)


def _write_patterns(path: Path, patterns: list[dict[str, str]]) -> None:
    path.write_text(json.dumps({"patterns": patterns}, ensure_ascii=False), encoding="utf-8")


def test_validate_pattern_routes_reports_counts_for_valid_routes(tmp_path: Path) -> None:
    """Validation returns per-route counts for a fully annotated pattern file."""
    path = tmp_path / "valid.json"
    _write_patterns(
        path,
        [
            {"pattern": "^You hit (.+)$", "template": "x", "route": "emit-message"},
            {"pattern": "^You are stunned$", "template": "x", "route": "leaf"},
            {"pattern": "^The (.+?) hits (.+?)$", "template": "x", "route": "message-frame"},
            {"pattern": "^The (.+?) is exhausted!$", "template": "x", "route": "needs-harmony-patch"},
        ],
    )

    report = validate_pattern_routes(path)

    assert report.counts["emit-message"] == 1
    assert report.counts["leaf"] == 1
    assert report.counts["message-frame"] == 1
    assert report.counts["needs-harmony-patch"] == 1
    assert report.missing_routes == []
    assert report.invalid_routes == []
    assert set(report.counts) == set(ALLOWED_ROUTES)


def test_main_reports_missing_route_and_returns_nonzero(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """CLI fails when a pattern entry does not define a route."""
    path = tmp_path / "missing-route.json"
    _write_patterns(
        path,
        [
            {"pattern": "^You hit (.+)$", "template": "x", "route": "emit-message"},
            {"pattern": "^You miss (.+)$", "template": "x"},
        ],
    )

    result = main([str(path)])
    captured = capsys.readouterr()

    assert result == 1
    assert "Missing route entries: 1" in captured.out
    assert "patterns[1] missing route" in captured.out


def test_main_reports_invalid_route_and_returns_nonzero(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """CLI fails when a pattern entry uses a route outside the allowed set."""
    path = tmp_path / "invalid-route.json"
    _write_patterns(
        path,
        [{"pattern": "^You hit (.+)$", "template": "x", "route": "unknown-route"}],
    )

    result = main([str(path)])
    captured = capsys.readouterr()

    assert result == 1
    assert "Invalid route entries: 1" in captured.out
    assert "invalid route 'unknown-route'" in captured.out


def test_repository_message_patterns_match_expected_route_inventory(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The shipped message pattern dictionary must match the reviewed route inventory."""
    monkeypatch.chdir(tmp_path)

    report = validate_pattern_routes(_REPOSITORY_MESSAGE_PATTERNS_PATH, _EXPECTED_MESSAGE_ROUTE_COUNTS)

    assert report.missing_routes == []
    assert report.invalid_routes == []
    assert report.route_count_mismatches == []
    assert report.has_errors is False


def test_main_reports_nonstr_route_and_returns_nonzero(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """CLI fails cleanly when a route field is present but not a string."""
    path = tmp_path / "invalid-type-route.json"
    path.write_text(
        json.dumps({"patterns": [{"pattern": "^You hit (.+)$", "template": "x", "route": ["emit-message"]}]}),
        encoding="utf-8",
    )

    result = main([str(path)])
    captured = capsys.readouterr()

    assert result == 1
    assert "Invalid route entries: 1" in captured.out
    assert "invalid route '['emit-message']'" in captured.out


def test_main_reports_route_count_mismatch_and_returns_nonzero(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """CLI fails when an expected route count does not match the inventory."""
    path = tmp_path / "count-mismatch.json"
    _write_patterns(
        path,
        [
            {"pattern": "^You hit (.+)$", "template": "x", "route": "emit-message"},
            {"pattern": "^You are stunned$", "template": "x", "route": "leaf"},
        ],
    )

    result = main([str(path), "--expect-count", "emit-message=2", "--expect-count", "leaf=1"])
    captured = capsys.readouterr()

    assert result == 1
    assert "Route count mismatches: 1" in captured.out
    assert "route 'emit-message' expected 2 entries but found 1" in captured.out


def test_main_rejects_duplicate_expected_route_counts(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI fails when --expect-count repeats a route."""
    path = tmp_path / "duplicate-count.json"
    _write_patterns(path, [{"pattern": "^You hit (.+)$", "template": "x", "route": "emit-message"}])

    with pytest.raises(SystemExit) as exc_info:
        main([str(path), "--expect-count", "emit-message=1", "--expect-count", "emit-message=2"])
    captured = capsys.readouterr()

    assert exc_info.value.code == 2
    assert "--expect-count for route 'emit-message' is duplicated" in captured.err


def test_new_dont_penetrate_no_roll_pattern_is_in_repository() -> None:
    """The new 'You don't penetrate...armor' (no roll) pattern must exist in the shipped dictionary."""
    raw = json.loads(_REPOSITORY_MESSAGE_PATTERNS_PATH.read_text(encoding="utf-8"))
    patterns = [entry["pattern"] for entry in raw["patterns"]]

    assert "^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor[.!]?$" in patterns


def test_new_dont_penetrate_no_roll_pattern_is_classified_emit_message() -> None:
    """The new 'You don't penetrate...armor' (no roll) pattern must have route 'emit-message'."""
    raw = json.loads(_REPOSITORY_MESSAGE_PATTERNS_PATH.read_text(encoding="utf-8"))
    target_pattern = "^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor[.!]?$"
    matching_entries = [
        entry for entry in raw["patterns"] if entry.get("pattern") == target_pattern
    ]

    assert len(matching_entries) == 1
    assert matching_entries[0]["route"] == "emit-message"


@pytest.mark.parametrize(
    "message",
    [
        "You don't penetrate Snapjaw Scavenger's armor.",
        "You don't penetrate Snapjaw Scavenger's armor!",
        "You don't penetrate Snapjaw Scavenger's armor",
        "You don't penetrate the iron golem's armor.",
        "You don't penetrate Snapjaw Scavengers' armor.",
    ],
)
def test_new_dont_penetrate_no_roll_pattern_matches_expected_messages(message: str) -> None:
    """The new 'You don't penetrate...armor' pattern must match messages without a roll value."""
    pattern = re.compile("^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor[.!]?$")

    assert pattern.match(message) is not None, f"Pattern did not match: {message!r}"


@pytest.mark.parametrize(
    "message",
    [
        "You don't penetrate Snapjaw Scavenger's armor. [17]",
        "You don't penetrate Snapjaw Scavenger's armor! [17]",
        "You don't penetrate Snapjaw Scavenger's armor with your bronze dagger. [17]",
    ],
)
def test_new_dont_penetrate_no_roll_pattern_does_not_match_roll_messages(message: str) -> None:
    """The new 'You don't penetrate...armor' pattern must not match messages that include a roll."""
    pattern = re.compile("^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor[.!]?$")

    assert pattern.match(message) is None, f"Pattern unexpectedly matched: {message!r}"


def test_main_reports_successful_validation(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI reports success and counts when all routes are present and valid."""
    path = tmp_path / "ok.json"

        path,
        [
            {"pattern": "^You hit (.+)$", "template": "x", "route": "emit-message"},
            {"pattern": "^You are stunned$", "template": "x", "route": "leaf"},
            {"pattern": "^(.+?) has nothing to trade$", "template": "x", "route": "message-log"},
            {"pattern": "^This object is a monument to (.+)$", "template": "x", "route": "description"},
            {"pattern": "^You are crippled for (.+?)!$", "template": "x", "route": "effect-cripple"},
        ],
    )

    result = main([str(path), "--expect-count", "emit-message=1", "--expect-count", "leaf=1"])
    captured = capsys.readouterr()

    assert result == 0
    assert "Route counts:" in captured.out
    assert "emit-message: 1" in captured.out
    assert "leaf: 1" in captured.out
    assert "All pattern routes are present and valid." in captured.out
