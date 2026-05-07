"""Tests for the validate_pattern_routes module."""

import json
from pathlib import Path

import pytest

from scripts.validate_pattern_routes import ALLOWED_ROUTES, main, validate_pattern_routes

_EXPECTED_MESSAGE_ROUTE_COUNTS = {
    "message-frame": 41,
    "popup": 12,
    "journal": 0,
    "leaf": 0,
    "emit-message": 302,
    "does-verb": 0,
    "message-log": 1,
    "description": 4,
    "effect-cripple": 1,
    "needs-harmony-patch": 34,
    "unclassified": 6,
}


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


def test_repository_message_patterns_match_expected_route_inventory() -> None:
    """The shipped message pattern dictionary must match the reviewed route inventory."""
    path = Path("Mods/QudJP/Localization/Dictionaries/messages.ja.json")

    report = validate_pattern_routes(path, _EXPECTED_MESSAGE_ROUTE_COUNTS)

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


def test_main_reports_successful_validation(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI reports success and counts when all routes are present and valid."""
    path = tmp_path / "ok.json"
    _write_patterns(
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
