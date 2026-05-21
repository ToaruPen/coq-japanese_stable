"""Tests for QudTest runtime artifact inspection."""

from __future__ import annotations

import json
import subprocess
import sys
from datetime import UTC, datetime, timedelta
from pathlib import Path

SCRIPT = Path("scripts/qudtest_inspect.py")


def _write_fixture(root: Path, *, expected: str = "インベントリ") -> None:
    root.mkdir(parents=True, exist_ok=True)
    (root / "runtime-smoke.json").write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "suite": "runtime",
                "description": "runtime smoke",
                "cases": [
                    {
                        "id": "message-log.inventory",
                        "route": "message-log",
                        "input": "Inventory",
                        "expected": expected,
                    },
                ],
            },
        ),
        encoding="utf-8",
    )


def _write_suite_fixture(root: Path, *, suite: str, case_id: str, expected: str) -> None:
    root.mkdir(parents=True, exist_ok=True)
    (root / f"{suite}-smoke.json").write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "suite": suite,
                "description": f"{suite} smoke",
                "cases": [
                    {
                        "id": case_id,
                        "route": "message-log",
                        "input": expected,
                        "expected": expected,
                    },
                ],
            },
        ),
        encoding="utf-8",
    )


def _write_binding_fixture(root: Path, *, expected_targets: list[str]) -> None:
    root.mkdir(parents=True, exist_ok=True)
    (root / "bindings-smoke.json").write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "suite": "bindings",
                "description": "binding smoke",
                "cases": [
                    {
                        "id": "binding.campfire-preserve",
                        "route": "patch-binding",
                        "patch": "QudJP.Patches.CampfirePreserveTranslationPatch",
                        "expectedTargets": expected_targets,
                    },
                ],
            },
        ),
        encoding="utf-8",
    )


def _write_result(  # noqa: PLR0913 - test fixtures need explicit artifact fields.
    root: Path,
    *,
    ended_at: datetime | None = None,
    passed: bool = True,
    expected: str = "インベントリ",
    actual: str = "インベントリ",
    language: str = "ja",
    suite: str = "all",
    case_id: str = "message-log.inventory",
) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    ended = ended_at or datetime.now(UTC)
    result = {
        "schemaVersion": 1,
        "command": f"qudtest:{suite}",
        "suite": suite,
        "modLanguage": language,
        "startedAtUtc": ended.isoformat(),
        "endedAtUtc": ended.isoformat(),
        "passed": passed,
        "totalCount": 1,
        "passCount": 1 if passed else 0,
        "failCount": 0 if passed else 1,
        "cases": [
            {
                "id": case_id,
                "route": "message-log",
                "input": actual,
                "expected": expected,
                "actual": actual,
                "passed": passed,
                "diagnostic": "" if passed else "Expected インベントリ.",
            },
        ],
    }
    path = root / "results.json"
    path.write_text(json.dumps(result), encoding="utf-8")
    return path


def _run(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(  # noqa: S603 - test helper executes the local inspector with controlled arguments.
        [sys.executable, str(SCRIPT), *args],
        check=False,
        capture_output=True,
        text=True,
    )


def _run_inspection(fixtures: Path, results: Path, player_log: Path) -> subprocess.CompletedProcess[str]:
    return _run(
        "--fixtures",
        str(fixtures),
        "--results",
        str(results / "results.json"),
        "--player-log",
        str(player_log),
    )


def test_qudtest_inspect_passes_for_matching_fresh_results(tmp_path: Path) -> None:
    """Accepts a fresh QudTest result matching repository fixture expectations."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_fixture(fixtures)
    _write_result(results)
    player_log.write_text("[QudJP] Build marker: test\n", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 0, completed.stderr
    assert "QudTest passed" in completed.stdout


def test_qudtest_inspect_fails_when_fixture_case_is_missing(tmp_path: Path) -> None:
    """Every fixture case must appear in the runtime result artifact."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_fixture(fixtures)
    _write_result(results)
    player_log.write_text("", encoding="utf-8")
    payload = json.loads((results / "results.json").read_text(encoding="utf-8"))
    payload["cases"] = []
    (results / "results.json").write_text(json.dumps(payload), encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 1
    assert "missing result case: message-log.inventory" in completed.stderr


def test_qudtest_inspect_compares_only_the_result_suite_fixtures(tmp_path: Path) -> None:
    """Focused qudtest:runtime and qudtest:wish runs validate only their suite fixture files."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_suite_fixture(fixtures, suite="runtime", case_id="runtime.case", expected="runtime text")
    _write_suite_fixture(fixtures, suite="wish", case_id="wish.case", expected="wish text")
    player_log.write_text("", encoding="utf-8")

    _write_result(results, suite="runtime", case_id="runtime.case", expected="runtime text", actual="runtime text")
    runtime_completed = _run_inspection(fixtures, results, player_log)

    _write_result(results, suite="wish", case_id="wish.case", expected="wish text", actual="wish text")
    wish_completed = _run_inspection(fixtures, results, player_log)

    assert runtime_completed.returncode == 0, runtime_completed.stderr
    assert wish_completed.returncode == 0, wish_completed.stderr


def test_qudtest_inspect_normalizes_binding_expected_targets(tmp_path: Path) -> None:
    """Binding fixtures compare structured expectedTargets against artifact expected strings."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    expected_targets = [
        "XRL.World.Parts.Campfire|Preserve|System.Boolean",
        "XRL.World.Parts.Campfire|PreserveExotic|System.Boolean",
    ]
    expected = "\n".join(sorted(expected_targets))
    _write_binding_fixture(fixtures, expected_targets=expected_targets)
    _write_result(results, suite="bindings", case_id="binding.campfire-preserve", expected=expected, actual=expected)
    player_log.write_text("", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 0, completed.stderr


def test_qudtest_inspect_accepts_dynamic_binding_suite_without_fixture_cases(tmp_path: Path) -> None:
    """Dynamic suites validate artifact failures directly instead of comparing fixture expectations."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_result(
        results,
        suite="bindings-all",
        case_id="binding-all.PopupAskStringTranslationPatch",
        expected="one or more resolved target signatures",
        actual="XRL.UI.Popup|AskString|System.String",
    )
    player_log.write_text("", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 0, completed.stderr


def test_qudtest_inspect_detects_binding_expected_target_mismatch(tmp_path: Path) -> None:
    """Binding fixture expectations must catch stale target signatures in artifacts."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_binding_fixture(
        fixtures,
        expected_targets=[
            "XRL.World.Parts.Campfire|Preserve|System.Boolean",
            "XRL.World.Parts.Campfire|PreserveExotic|System.Boolean",
        ],
    )
    _write_result(
        results,
        suite="bindings",
        case_id="binding.campfire-preserve",
        expected="XRL.World.Parts.Campfire|OldPreserve|System.Boolean",
        actual="XRL.World.Parts.Campfire|OldPreserve|System.Boolean",
    )
    player_log.write_text("", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 1
    assert "fixture expected mismatch: binding.campfire-preserve" in completed.stderr


def test_qudtest_inspect_fails_for_stale_results(tmp_path: Path) -> None:
    """Old result artifacts must not be reused as fresh runtime evidence."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_fixture(fixtures)
    _write_result(results, ended_at=datetime.now(UTC) - timedelta(hours=2))
    player_log.write_text("", encoding="utf-8")

    completed = _run(
        "--fixtures",
        str(fixtures),
        "--results",
        str(results / "results.json"),
        "--player-log",
        str(player_log),
        "--max-age-seconds",
        "60",
    )

    assert completed.returncode == 1
    assert "stale results" in completed.stderr


def test_qudtest_inspect_fails_for_expected_mismatch(tmp_path: Path) -> None:
    """Result artifacts must preserve the same expected values as fixtures."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_fixture(fixtures, expected="インベントリ")
    _write_result(results, expected="持ち物")
    player_log.write_text("", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 1
    assert "fixture expected mismatch: message-log.inventory" in completed.stderr


def test_qudtest_inspect_fails_for_failed_case(tmp_path: Path) -> None:
    """Failed runtime cases are reported with expected and actual values."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_fixture(fixtures)
    _write_result(results, passed=False, actual="Inventory")
    player_log.write_text("", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 1
    assert "failed case: message-log.inventory" in completed.stderr
    assert "result document reports passed=false" in completed.stderr


def test_qudtest_inspect_fails_for_wrong_active_language(tmp_path: Path) -> None:
    """Runtime artifacts must record the expected active language."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_fixture(fixtures)
    _write_result(results, language="en")
    player_log.write_text("", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 1
    assert "wrong mod language: expected ja, got en" in completed.stderr


def test_qudtest_inspect_can_skip_player_log(tmp_path: Path) -> None:
    """Headless or artifact-only checks may skip Player.log validation explicitly."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    _write_fixture(fixtures)
    _write_result(results)

    completed = _run("--fixtures", str(fixtures), "--results", str(results / "results.json"), "--skip-player-log")

    assert completed.returncode == 0, completed.stderr


def test_qudtest_inspect_fails_for_fatal_player_log_marker(tmp_path: Path) -> None:
    """In-game inspections fail when Player.log contains known fatal mod markers."""
    fixtures = tmp_path / "fixtures"
    results = tmp_path / "QudTest"
    player_log = tmp_path / "Player.log"
    _write_fixture(fixtures)
    _write_result(results)
    player_log.write_text("Exception compiling mod assembly: boom\n", encoding="utf-8")

    completed = _run_inspection(fixtures, results, player_log)

    assert completed.returncode == 1
    assert "fatal Player.log pattern: Exception compiling mod assembly:" in completed.stderr


def test_qudtest_inspect_lists_recent_runs(tmp_path: Path) -> None:
    """Run history is listed newest-first with pass/fail counts."""
    runs = tmp_path / "QudTest" / "runs"
    for name, passed in (("20260520T0100000000000Z", True), ("20260520T0200000000000Z", False)):
        run_dir = runs / name
        _write_result(run_dir, passed=passed)

    completed = _run("--list-runs", "--runs-dir", str(runs), "--limit", "2")

    assert completed.returncode == 0, completed.stderr
    assert completed.stdout.splitlines() == [
        "20260520T0200000000000Z passed=false total=1 failed=1",
        "20260520T0100000000000Z passed=true total=1 failed=0",
    ]
