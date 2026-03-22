"""Integration tests for the triage pipeline CLI."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from scripts.triage_untranslated import main

_PLAYER_LOG = Path.home() / "Library" / "Logs" / "Freehold Games" / "CavesOfQud" / "Player.log"


def _write_log(path: Path, lines: list[str]) -> None:
    """Write lines to a fake Player.log file."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def test_cli_produces_json_report(tmp_path: Path) -> None:
    """CLI reads a log file and produces a grouped JSON report."""
    log = tmp_path / "Player.log"
    out = tmp_path / "triage.json"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key 'Inventory' (hit 1). (context: UITextSkinTranslationPatch)",
            "[QudJP] Translator: missing key 'Points Remaining: 12' (hit 4). (context: UITextSkinTranslationPatch)",
            "[QudJP] MessagePatternTranslator: no pattern for 'Game saved!' (hit 1). (context: MessageLogPatch)",
        ],
    )
    result = main(["--log", str(log), "--output", str(out)])
    assert result == 0

    data = json.loads(out.read_text(encoding="utf-8"))
    assert "summary" in data
    assert "by_classification" in data
    assert data["summary"]["total"] == 3
    assert data["summary"]["static_leaf"] >= 1
    assert data["summary"]["logic_required"] >= 1
    assert data["summary"]["route_patch"] >= 1


def test_cli_groups_by_route(tmp_path: Path) -> None:
    """Report groups entries by route and classification."""
    log = tmp_path / "Player.log"
    out = tmp_path / "triage.json"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key 'A label' (hit 1). (context: RouteA)",
            "[QudJP] Translator: missing key 'Level: 2' (hit 1). (context: RouteB)",
        ],
    )
    result = main(["--log", str(log), "--output", str(out)])
    assert result == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    assert "by_route" in data
    assert set(data["by_route"]) == {"RouteA", "RouteB"}
    assert len(data["by_route"]["RouteA"]["static_leaf"]) == 1
    assert len(data["by_route"]["RouteB"]["logic_required"]) == 1


def test_cli_ignores_dynamic_text_probe_entries(tmp_path: Path) -> None:
    """DynamicTextProbe observations stay out of the actionable triage report."""
    log = tmp_path / "Player.log"
    out = tmp_path / "triage.json"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key 'Inventory' (hit 1). (context: UITextSkinTranslationPatch)",
            "[QudJP] DynamicTextProbe/v1:"
            " route='UITextSkinTranslationPatch'"
            " family='DirectUiText.ExactLookup'"
            " hit=8 changed=true"
            " source='Inventory'"
            " translated='持ち物'."
            " (context: UITextSkinTranslationPatch)",
        ],
    )
    result = main(["--log", str(log), "--output", str(out)])
    assert result == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["summary"]["total"] == 1
    assert data["summary"]["static_leaf"] == 1
    assert data["summary"]["logic_required"] == 0
    assert all(
        entry["kind"] != "dynamic_text_probe" for entries in data["by_classification"].values() for entry in entries
    )


@pytest.mark.skipif(not _PLAYER_LOG.exists(), reason="No Player.log available")
def test_cli_real_player_log(tmp_path: Path) -> None:
    """Smoke test against the real Player.log."""
    out = tmp_path / "triage.json"
    result = main(["--log", str(_PLAYER_LOG), "--output", str(out)])
    assert result == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    assert "summary" in data
    assert data["summary"]["total"] >= 0
