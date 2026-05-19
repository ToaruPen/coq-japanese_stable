from __future__ import annotations

import json
import os
from datetime import UTC, datetime
from pathlib import Path
from typing import TYPE_CHECKING

from scripts import issue737_runtime_closeout as closeout

if TYPE_CHECKING:
    import pytest


def _write_log(path: Path, text: str, *, mtime: datetime) -> None:
    path.write_text(text, encoding="utf-8")
    timestamp = mtime.timestamp()
    path.touch()
    os.utime(path, (timestamp, timestamp))


def test_analyze_log_reports_stale_without_claiming_runtime_result(tmp_path: Path) -> None:
    """A pre-deploy log is context only and must not prove pass or fail."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        "source='You toss glass berries into a pot and stir.'\n",
        mtime=datetime(2026, 5, 18, 0, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    assert report["status"] == "stale"
    assert report["freshness"]["is_fresh"] is False
    assert report["deployment"]["status"] == "not_checked"
    assert report["checks"] == []


def test_analyze_log_detects_issue737_runtime_residue_in_fresh_log(tmp_path: Path) -> None:
    """Fresh logs with original Issue #737 residue fail the closeout check."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        "source='You toss glass berries into a pot and stir.'\n"
        "[QudJP] FinalOutputProbe/v1: "
        "source='&yYou preserved:\\n\\nSome &r肉&y into 3 serving of 肉ジャーキー.'\n"
        "[QudJP] FinalOutputProbe/v1: source='{{W|HISTORY OF ナレドゥクフト}}'\n"
        "[QudJP] FinalOutputProbe/v1: source='カルクヘタラ, Stargazerhome'\n"
        "[QudJP] FinalOutputProbe/v1: source='leader of the シャッガンナ Pest Flock'",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    assert report["status"] == "failed"
    failed_checks = {check["id"]: check for check in report["checks"] if check["status"] == "failed"}
    assert failed_checks.keys() == {
        "campfire_meal_ingredients",
        "campfire_preserve_frame",
        "sultan_journal_history",
        "journal_map_note_location",
        "journal_relationship_title",
    }
    assert failed_checks["campfire_preserve_frame"]["matches"][0]["pattern"] == "preserve-frame residue"


def test_analyze_log_detects_preserve_residue_after_outer_frame_translation(tmp_path: Path) -> None:
    """Preserve residue can remain after the outer frame is already Japanese."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] FinalOutputProbe/v1: "
        "final='保存した:\\n\\nSome {{r|生肉}}を3 servingの肉ジャーキーに保存した。'",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    failed_checks = {check["id"]: check for check in report["checks"] if check["status"] == "failed"}
    assert report["status"] == "failed"
    assert failed_checks["campfire_preserve_frame"]["matches"][0]["pattern"] == "preserve-frame residue"


def test_analyze_log_ignores_dynamic_probe_source_when_translated_text_is_clean(tmp_path: Path) -> None:
    """Dynamic probe source text is route evidence, but residue checks use translated/final fields."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        "source='You toss glass berries, a nip of joined paprika, and chameleon horn into a pot and stir.' "
        "translated='ガラスベリー、結ばれたパプリカ少量とカメレオンの角を鍋に放り込み、かき混ぜた。'\n",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    checks = {check["id"]: check for check in report["checks"]}
    assert checks["campfire_meal_ingredients"]["status"] == "passed"
    assert checks["campfire_meal_ingredients"]["matches"] == []


def test_analyze_log_ignores_final_output_source_when_final_text_is_clean(tmp_path: Path) -> None:
    """Final-output source text is not itself residue when the final visible text is clean."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalSultanNote' "
        "source='{{W|HISTORY OF ナレドゥクフト}}' translated='{{W|ナレドゥクフトの歴史}}' "
        "final='{{W|ナレドゥクフトの歴史}}'\n",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    checks = {check["id"]: check for check in report["checks"]}
    assert checks["sultan_journal_history"]["status"] == "passed"
    assert checks["sultan_journal_history"]["matches"] == []


def test_analyze_log_parses_escaped_quotes_before_visible_residue(tmp_path: Path) -> None:
    """Escaped apostrophes in probe values must not hide later visible residue."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalSultanNote' "
        "final='ウーヒム IV wasn\\'t forgotten. HISTORY OF ナレドゥクフト'\n",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    failed_checks = {check["id"]: check for check in report["checks"] if check["status"] == "failed"}
    assert report["status"] == "failed"
    assert failed_checks["sultan_journal_history"]["matches"][0]["pattern"] == "HISTORY OF"


def test_analyze_log_decodes_unicode_escapes_before_visible_residue(tmp_path: Path) -> None:
    """Control-character escapes in probe values are normalized before matching."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalSultanNote' "
        "final='\\u0048ISTORY OF ナレドゥクフト'\n",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    failed_checks = {check["id"]: check for check in report["checks"] if check["status"] == "failed"}
    assert report["status"] == "failed"
    assert failed_checks["sultan_journal_history"]["matches"][0]["pattern"] == "HISTORY OF"


def test_analyze_log_uses_double_quoted_visible_probe_fields(tmp_path: Path) -> None:
    """Double-quoted final/translated fields are treated like single-quoted fields."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalSultanNote' "
        "source='{{W|HISTORY OF ナレドゥクフト}}' final=\"{{W|ナレドゥクフトの歴史}}\"\n"
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        'translated="glass berriesを鍋に放り込み、かき混ぜた。"\n',
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    checks = {check["id"]: check for check in report["checks"]}
    assert checks["sultan_journal_history"]["status"] == "passed"
    assert checks["sultan_journal_history"]["matches"] == []
    assert checks["campfire_meal_ingredients"]["status"] == "failed"
    assert checks["campfire_meal_ingredients"]["matches"][0]["pattern"] == "glass berries"


def test_analyze_log_distinguishes_observed_pass_from_unobserved_route(tmp_path: Path) -> None:
    """A clean fresh log can pass observed routes while leaving absent routes unobserved."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        "translated='ガラスベリーを鍋に放り込み、かき混ぜた。'\n"
        "[QudJP] FinalOutputProbe/v1: source='保存した:\\n\\n{{r|生肉}}を3食分の肉ジャーキーに保存した。'",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    assert report["status"] == "unobserved"
    checks = {check["id"]: check["status"] for check in report["checks"]}
    assert checks["campfire_meal_ingredients"] == "passed"
    assert checks["campfire_preserve_frame"] == "passed"
    assert checks["sultan_journal_history"] == "unobserved"
    assert checks["textfilters_runtime_required"] == "runtime_required"


def test_analyze_log_does_not_treat_unrelated_journal_output_as_sultan_route(tmp_path: Path) -> None:
    """Generic journal output is not enough to prove the sultan history route."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalEntryDisplayTextPatch' final='一般的な日誌行'",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    checks = {check["id"]: check["status"] for check in report["checks"]}
    assert checks["sultan_journal_history"] == "unobserved"


def test_analyze_log_passes_when_all_non_textfilter_routes_are_observed_without_residue(tmp_path: Path) -> None:
    """A fresh replay that observes every non-TextFilters route can close runtime evidence."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        "translated='ガラスベリーを鍋に放り込み、かき混ぜた。'\n"
        "[QudJP] FinalOutputProbe/v1: source='保存した:\\n\\n{{r|生肉}}を3食分の肉ジャーキーに保存した。'\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalSultanNote' final='スルタンの歴史'\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalMapNote' final='最後に訪れた: 星見の家'\n"
        "[QudJP] FinalOutputProbe/v1: source='シャッガンナ Pest Flockの指導者'",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    assert report["status"] == "passed"
    checks = {check["id"]: check["status"] for check in report["checks"]}
    assert {status for check_id, status in checks.items() if check_id != "textfilters_runtime_required"} == {"passed"}
    assert checks["textfilters_runtime_required"] == "runtime_required"


def test_analyze_log_records_observed_textfilters_without_closing_runtime_required(tmp_path: Path) -> None:
    """TextFilters output remains a manual runtime-required follow-up even when observed."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] DynamicTextProbe/v1: route='TextFilters.Angry' source='Stop there.' final='STOP THERE!'\n",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
    )

    textfilters = next(check for check in report["checks"] if check["id"] == "textfilters_runtime_required")
    assert textfilters["status"] == "runtime_required"
    assert textfilters["observed"] is True


def test_analyze_log_reports_deployment_hash_match_when_requested(tmp_path: Path) -> None:
    """Optional deployment evidence ties the runtime check to the deployed mod files."""
    log = tmp_path / "Player.log"
    source_mod = tmp_path / "source" / "QudJP"
    deployed_mod = tmp_path / "deployed" / "QudJP"
    relative = Path("Assemblies") / "QudJP.dll"
    (source_mod / relative.parent).mkdir(parents=True)
    (deployed_mod / relative.parent).mkdir(parents=True)
    (source_mod / relative).write_text("same dll", encoding="utf-8")
    (deployed_mod / relative).write_text("same dll", encoding="utf-8")
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n",
        mtime=datetime(2026, 5, 18, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
        source_mod_root=source_mod,
        deployed_mod_root=deployed_mod,
        deployment_files=(relative,),
    )

    assert report["status"] == "stale"
    assert report["deployment"]["status"] == "passed"
    compared = report["deployment"]["files"][0]
    assert compared["path"] == "Assemblies/QudJP.dll"
    assert compared["source_sha256"] == compared["deployed_sha256"]


def test_analyze_log_rejects_passed_runtime_when_deployment_hash_mismatches(tmp_path: Path) -> None:
    """A clean log cannot close the goal if it is not tied to the deployed files."""
    log = tmp_path / "Player.log"
    source_mod = tmp_path / "source" / "QudJP"
    deployed_mod = tmp_path / "deployed" / "QudJP"
    relative = Path("Assemblies") / "QudJP.dll"
    (source_mod / relative.parent).mkdir(parents=True)
    (deployed_mod / relative.parent).mkdir(parents=True)
    (source_mod / relative).write_text("source dll", encoding="utf-8")
    (deployed_mod / relative).write_text("old deployed dll", encoding="utf-8")
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        "translated='ガラスベリーを鍋に放り込み、かき混ぜた。'\n"
        "[QudJP] FinalOutputProbe/v1: source='保存した:\\n\\n{{r|生肉}}を3食分の肉ジャーキーに保存した。'\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalSultanNote' final='スルタンの歴史'\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalMapNote' final='最後に訪れた: 星見の家'\n"
        "[QudJP] FinalOutputProbe/v1: source='シャッガンナ Pest Flockの指導者'",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    report = closeout.analyze_log(
        log_path=log,
        min_mtime=datetime(2026, 5, 19, 0, 0, tzinfo=UTC),
        source_mod_root=source_mod,
        deployed_mod_root=deployed_mod,
        deployment_files=(relative,),
    )

    assert report["status"] == "deployment_mismatch"
    assert report["deployment"]["status"] == "failed"
    compared = report["deployment"]["files"][0]
    assert compared["source_exists"] is True
    assert compared["deployed_exists"] is True
    assert compared["source_sha256"] != compared["deployed_sha256"]


def test_main_writes_json_report(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """The CLI writes deterministic JSON for automation handoff."""
    log = tmp_path / "Player.log"
    output = tmp_path / "report.json"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    exit_code = closeout.main(
        [
            "--log",
            str(log),
            "--min-mtime",
            "2026-05-19T00:00:00+00:00",
            "--output",
            str(output),
        ],
    )

    assert exit_code == 0
    assert "Runtime closeout report written" in capsys.readouterr().err
    assert json.loads(output.read_text(encoding="utf-8"))["status"] == "unobserved"


def test_main_require_passed_rejects_stale_report(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """Strict mode is a completion gate: stale evidence must fail the command."""
    log = tmp_path / "Player.log"
    output = tmp_path / "report.json"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n",
        mtime=datetime(2026, 5, 18, 1, 0, tzinfo=UTC),
    )

    exit_code = closeout.main(
        [
            "--log",
            str(log),
            "--min-mtime",
            "2026-05-19T00:00:00+00:00",
            "--output",
            str(output),
            "--require-passed",
        ],
    )

    assert exit_code == 2
    assert json.loads(output.read_text(encoding="utf-8"))["status"] == "stale"
    assert "expected passed" in capsys.readouterr().err


def test_main_require_passed_accepts_passed_report(tmp_path: Path) -> None:
    """Strict mode succeeds when every non-TextFilters route is observed and clean."""
    log = tmp_path / "Player.log"
    _write_log(
        log,
        "[QudJP] Build marker: issue737-test, Version: 0.1.0.0\n"
        "[QudJP] DynamicTextProbe/v1: route='CampfireDescribeMealTranslationPatch' "
        "translated='ガラスベリーを鍋に放り込み、かき混ぜた。'\n"
        "[QudJP] FinalOutputProbe/v1: source='保存した:\\n\\n{{r|生肉}}を3食分の肉ジャーキーに保存した。'\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalSultanNote' final='スルタンの歴史'\n"
        "[QudJP] FinalOutputProbe/v1: route='JournalMapNote' final='最後に訪れた: 星見の家'\n"
        "[QudJP] FinalOutputProbe/v1: source='シャッガンナ Pest Flockの指導者'",
        mtime=datetime(2026, 5, 19, 1, 0, tzinfo=UTC),
    )

    exit_code = closeout.main(
        [
            "--log",
            str(log),
            "--min-mtime",
            "2026-05-19T00:00:00+00:00",
            "--require-passed",
        ],
    )

    assert exit_code == 0
