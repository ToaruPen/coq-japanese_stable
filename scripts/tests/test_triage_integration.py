"""Integration tests for the triage pipeline CLI."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import pytest

from scripts.triage.log_parser import parse_log
from scripts.triage_untranslated import main

REPO_ROOT = Path(__file__).resolve().parents[2]
_PLAYER_LOG = Path.home() / "Library" / "Logs" / "Freehold Games" / "CavesOfQud" / "Player.log"
_MISSING_KEY_NEW = (
    "[QudJP] Translator: missing key 'Put away' (hit 3). (context: ExactKey);"
    " route=ExactKey; family=missing_key; template_id=<missing>; rendered_text_sample=Put away"
)
_NO_PATTERN_NEW = (
    "[QudJP] MessagePatternTranslator: no pattern for 'You catch fire'"
    " (hit 2). (context: MessagePattern); route=MessagePattern; family=message_pattern;"
    " template_id=<missing>; rendered_text_sample=You catch fire"
)
_DYNAMIC_PROBE_NEW = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='You catch fire' translated='あなたは燃え上がる'. (context: DoesVerbRoute);"
    " route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=full;"
    " payload_excerpt=You catch fire; payload_sha256=<missing>"
)
_SINK_OBSERVE_NEW = (
    "[QudJP] SinkObserve/v1: sink='MessageLog' route='EmitMessage' detail='ObservationOnly'"
    " source='You catch fire' stripped='You catch fire'; route=EmitMessage;"
    " family=sink_observe; template_id=<missing>; payload_mode=full;"
    " payload_excerpt=You catch fire; payload_sha256=<missing>"
)
_MISSING_KEY_ESCAPED = (
    "[QudJP] Translator: missing key 'Put away; route=Spoofed; family=spoof=value'"
    " (hit 3). (context: ExactKey); route=ExactKey; family=missing_key;"
    " template_id=<missing>; rendered_text_sample=Put away\\; route\\=Spoofed\\;"
    " family\\=spoof\\=value"
)
_DYNAMIC_PROBE_ESCAPED = (
    "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true"
    " source='You catch fire; route=Spoofed; family=spoof=value' translated='あなたは燃え上がる'."
    " (context: DoesVerbRoute); route=DoesVerbRoute; family=verb; template_id=<missing>;"
    " payload_mode=full; payload_excerpt=You catch fire\\; route\\=Spoofed\\;"
    " family\\=spoof\\=value; payload_sha256=<missing>"
)


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
    assert data["phase_f"]["summary"] == {"total": 1, "dynamic_text_probe": 1, "sink_observe": 0}
    assert len(data["phase_f"]["entries"]) == 1
    assert data["phase_f"]["entries"][0]["kind"] == "dynamic_text_probe"


def test_cli_adds_structured_group_without_changing_actionable_categories(tmp_path: Path) -> None:
    """Structured suffix fields are nested separately from the legacy actionable summary."""
    log = tmp_path / "Player.log"
    out = tmp_path / "triage.json"
    _write_log(log, [_MISSING_KEY_NEW])

    result = main(["--log", str(log), "--output", str(out)])
    assert result == 0

    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["summary"] == {
        "total": 1,
        "static_leaf": 1,
        "route_patch": 0,
        "logic_required": 0,
        "preserved_english": 0,
        "unexpected_translation_of_preserved_token": 0,
        "unresolved": 0,
    }
    entry = data["by_classification"]["static_leaf"][0]
    assert entry["text"] == "Put away"
    assert entry["phase_f"] == {
        "route": "ExactKey",
        "family": "missing_key",
        "template_id": None,
        "rendered_text_sample": "Put away",
    }


def test_cli_reports_sink_observe_in_phase_f_section(tmp_path: Path) -> None:
    """SinkObserve stays outside the actionable summary and appears only in Phase F output."""
    log = tmp_path / "Player.log"
    out = tmp_path / "triage.json"
    _write_log(log, [_SINK_OBSERVE_NEW])

    result = main(["--log", str(log), "--output", str(out)])
    assert result == 0

    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["summary"]["total"] == 0
    assert data["phase_f"]["summary"] == {"total": 1, "dynamic_text_probe": 0, "sink_observe": 1}
    assert data["phase_f"]["entries"][0]["kind"] == "sink_observe"
    assert data["phase_f"]["entries"][0]["phase_f"] == {
        "route": "EmitMessage",
        "family": "sink_observe",
        "template_id": None,
        "payload_mode": "full",
        "payload_excerpt": "You catch fire",
        "payload_sha256": None,
    }


def test_module_cli_classify_treats_missing_runtime_dir_as_empty_report(tmp_path: Path) -> None:
    """The package CLI should support an empty runtime-evidence directory as a format check."""
    input_dir = tmp_path / "runtime"
    out = tmp_path / "triage.json"

    completed = subprocess.run(  # noqa: S603 -- test invokes the repo-local package module via the active interpreter.
        [
            sys.executable,
            "-m",
            "scripts.triage.cli",
            "classify",
            "--input-dir",
            str(input_dir),
            "--output",
            str(out),
        ],
        capture_output=True,
        text=True,
        cwd=REPO_ROOT,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data == {
        "summary": {
            "total": 0,
            "static_leaf": 0,
            "route_patch": 0,
            "logic_required": 0,
            "preserved_english": 0,
            "unexpected_translation_of_preserved_token": 0,
            "unresolved": 0,
        },
        "by_classification": {
            "static_leaf": [],
            "route_patch": [],
            "logic_required": [],
            "preserved_english": [],
            "unexpected_translation_of_preserved_token": [],
            "unresolved": [],
        },
        "by_route": {},
        "phase_f": {
            "summary": {
                "total": 0,
                "dynamic_text_probe": 0,
                "sink_observe": 0,
            },
            "entries": [],
        },
    }


def test_module_cli_classify_keeps_no_context_split_explicit(tmp_path: Path) -> None:
    """The package CLI should preserve the mixed <no-context> outcomes instead of collapsing them."""
    input_dir = tmp_path / "runtime"
    out = tmp_path / "triage.json"
    _write_log(
        input_dir / "Player.log",
        [
            "[QudJP] Translator: missing key 'Inventory' (hit 1).",
            "[QudJP] Translator: missing key 'Level: 2' (hit 1).",
            "[QudJP] Translator: missing key 'Nigashrowar' (hit 1).",
        ],
    )

    completed = subprocess.run(  # noqa: S603 -- test invokes the repo-local package module via the active interpreter.
        [
            sys.executable,
            "-m",
            "scripts.triage.cli",
            "classify",
            "--input-dir",
            str(input_dir),
            "--output",
            str(out),
        ],
        capture_output=True,
        text=True,
        cwd=REPO_ROOT,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    data = json.loads(out.read_text(encoding="utf-8"))
    assert set(data["by_route"]) == {"<no-context>"}
    assert [entry["text"] for entry in data["by_route"]["<no-context>"]["static_leaf"]] == ["Inventory"]
    assert [entry["text"] for entry in data["by_route"]["<no-context>"]["logic_required"]] == ["Level: 2"]
    assert [entry["text"] for entry in data["by_route"]["<no-context>"]["unresolved"]] == ["Nigashrowar"]
    assert data["summary"] == {
        "total": 3,
        "static_leaf": 1,
        "route_patch": 0,
        "logic_required": 1,
        "preserved_english": 0,
        "unexpected_translation_of_preserved_token": 0,
        "unresolved": 1,
    }


def test_cli_classifies_preserved_english_separately(tmp_path: Path) -> None:
    """Preserved English appears in its own actionable report bucket."""
    log = tmp_path / "Player.log"
    out = tmp_path / "triage.json"
    _write_log(
        log,
        [
            "[QudJP] Translator: missing key 'STR' (hit 1). (context: CharacterAttributeLineTranslationPatch)",
            "[QudJP] Translator: missing key '123/200 lbs.' (hit 1). (context: UnknownWeightRoute)",
        ],
    )

    result = main(["--log", str(log), "--output", str(out)])
    assert result == 0

    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["summary"]["preserved_english"] == 1
    assert data["summary"]["logic_required"] == 1
    assert [entry["text"] for entry in data["by_classification"]["preserved_english"]] == ["STR"]


def test_module_cli_classify_returns_error_when_output_parent_cannot_be_created(tmp_path: Path) -> None:
    """The package CLI should flatten output-path OSErrors into a single-line failure."""
    input_dir = tmp_path / "runtime"
    blocked_parent = tmp_path / "occupied"
    blocked_parent.write_text("not a directory", encoding="utf-8")

    result = subprocess.run(  # noqa: S603 -- test invokes the repo-local package module via the active interpreter.
        [
            sys.executable,
            "-m",
            "scripts.triage.cli",
            "classify",
            "--input-dir",
            str(input_dir),
            "--output",
            str(blocked_parent / "triage.json"),
        ],
        capture_output=True,
        text=True,
        cwd=REPO_ROOT,
        check=False,
    )

    assert result.returncode == 1
    assert "Error:" in result.stderr


def test_cli_preserves_grouping_when_structured_values_contain_delimiter_like_text(tmp_path: Path) -> None:
    """Escaped Phase F values do not hijack actionable grouping or Phase F routes."""
    log = tmp_path / "Player.log"
    out = tmp_path / "triage.json"
    _write_log(log, [_MISSING_KEY_ESCAPED, _DYNAMIC_PROBE_ESCAPED])

    result = main(["--log", str(log), "--output", str(out)])
    assert result == 0

    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["summary"]["total"] == 1
    assert set(data["by_route"]) == {"ExactKey"}
    actionable_entries = [entry for entries in data["by_route"]["ExactKey"].values() for entry in entries]
    assert len(actionable_entries) == 1
    actionable_entry = actionable_entries[0]
    assert actionable_entry["phase_f"] == {
        "route": "ExactKey",
        "family": "missing_key",
        "template_id": None,
        "rendered_text_sample": "Put away; route=Spoofed; family=spoof=value",
    }
    assert data["phase_f"]["summary"] == {"total": 1, "dynamic_text_probe": 1, "sink_observe": 0}
    assert data["phase_f"]["entries"][0]["route"] == "DoesVerbRoute"
    assert data["phase_f"]["entries"][0]["phase_f"]["payload_excerpt"] == (
        "You catch fire; route=Spoofed; family=spoof=value"
    )


def test_sample_log_smoke(tmp_path: Path) -> None:
    """A frozen sample log can be parsed into structured Phase F expectations."""
    log = tmp_path / "Player.log"
    lines = [_MISSING_KEY_NEW, _NO_PATTERN_NEW, _DYNAMIC_PROBE_NEW, _SINK_OBSERVE_NEW]
    _write_log(log, lines)

    structured_output = []
    for entry in parse_log(log):
        payload = {
            "kind": entry.kind.value,
            "route": entry.route,
            "text": entry.text,
            "hits": entry.hits,
            "line_number": entry.line_number,
        }
        if entry.family is not None:
            payload["family"] = entry.family
        if entry.translated_text is not None:
            payload["translated_text"] = entry.translated_text
        if entry.changed is not None:
            payload["changed"] = entry.changed
        structured = {
            key: value
            for key, value in {
                "route": entry.route,
                "family": entry.family,
                "template_id": entry.template_id,
                "rendered_text_sample": entry.rendered_text_sample,
                "payload_mode": entry.payload_mode,
                "payload_excerpt": entry.payload_excerpt,
                "payload_sha256": entry.payload_sha256,
            }.items()
            if key == "route" or (key == "family" and entry.family is not None) or entry.has_structured_field(key)
        }
        payload["structured"] = structured
        structured_output.append(payload)

    assert structured_output == [
        {
            "kind": "missing_key",
            "route": "ExactKey",
            "text": "Put away",
            "hits": 3,
            "line_number": 1,
            "family": "missing_key",
            "structured": {
                "route": "ExactKey",
                "family": "missing_key",
                "template_id": None,
                "rendered_text_sample": "Put away",
            },
        },
        {
            "kind": "no_pattern",
            "route": "MessagePattern",
            "text": "You catch fire",
            "hits": 2,
            "line_number": 2,
            "family": "message_pattern",
            "structured": {
                "route": "MessagePattern",
                "family": "message_pattern",
                "template_id": None,
                "rendered_text_sample": "You catch fire",
            },
        },
        {
            "kind": "dynamic_text_probe",
            "route": "DoesVerbRoute",
            "text": "You catch fire",
            "hits": 1,
            "line_number": 3,
            "family": "verb",
            "translated_text": "あなたは燃え上がる",
            "changed": True,
            "structured": {
                "route": "DoesVerbRoute",
                "family": "verb",
                "template_id": None,
                "payload_mode": "full",
                "payload_excerpt": "You catch fire",
                "payload_sha256": None,
            },
        },
        {
            "kind": "sink_observe",
            "route": "EmitMessage",
            "text": "You catch fire",
            "hits": None,
            "line_number": 4,
            "family": "sink_observe",
            "structured": {
                "route": "EmitMessage",
                "family": "sink_observe",
                "template_id": None,
                "payload_mode": "full",
                "payload_excerpt": "You catch fire",
                "payload_sha256": None,
            },
        },
    ]


@pytest.mark.skipif(not _PLAYER_LOG.exists(), reason="No Player.log available")
def test_cli_real_player_log(tmp_path: Path) -> None:
    """Smoke test against the real Player.log."""
    out = tmp_path / "triage.json"
    result = main(["--log", str(_PLAYER_LOG), "--output", str(out)])
    assert result == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    assert "summary" in data
    assert data["summary"]["total"] >= 0
