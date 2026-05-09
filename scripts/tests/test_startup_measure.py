"""Tests for startup measurement tooling."""

from __future__ import annotations

import json
from typing import TYPE_CHECKING

import pytest

from scripts.startup_measure import (
    IterationMetadata,
    IterationResult,
    PhaseSummary,
    ProfileSummary,
    _resolve_ready_marker,
    _restore_mod,
    compare_profiles,
    main,
    parse_startup_log_text,
    summarize_iterations,
)

if TYPE_CHECKING:
    from pathlib import Path


def test_parse_startup_log_text_extracts_timing_markers() -> None:
    """StartupTiming lines are parsed with escaped detail fields."""
    log = (
        "[QudJP] Build marker: marker\n"
        "[QudJP] StartupTiming/v1: phase=harmony.prepare_patch_types elapsed_ms=12.346 "
        r"detail=patch_types\=140\;prepared\=139"
        "\nINFO - Finished 'Loading Naming.xml' task in 42ms"
        "\n[QudJP] Harmony patching complete: 590 method(s) patched."
    )

    parsed = parse_startup_log_text(log)

    assert parsed.build_marker_seen is True
    assert parsed.harmony_complete_seen is True
    assert len(parsed.timings) == 2
    assert parsed.timings[0].phase == "harmony.prepare_patch_types"
    assert parsed.timings[0].elapsed_ms == 12.346
    assert parsed.timings[0].detail == "patch_types=140;prepared=139"
    assert parsed.timings[1].phase == "game.loading.naming_xml"
    assert parsed.timings[1].elapsed_ms == 42.0
    assert parsed.timings[1].detail == "Naming.xml"


def test_summarize_iterations_groups_by_profile_and_phase() -> None:
    """Iteration summaries report per-profile median and ready counts."""
    results = [
        _result("enabled", 1, "ready", {"font.initialize": 100.0, "harmony.setup_and_apply": 20.0}),
        _result("enabled", 2, "ready", {"font.initialize": 120.0, "harmony.setup_and_apply": 30.0}),
        _result("disabled", 1, "ready", {"bootstrap.total": 50.0}),
    ]

    summaries = summarize_iterations(results)
    enabled = next(summary for summary in summaries if summary.profile == "enabled")
    font = next(phase for phase in enabled.phases if phase.phase == "font.initialize")
    runner = next(phase for phase in enabled.phases if phase.phase == "runner.elapsed_until_ready")

    assert enabled.iterations == 2
    assert enabled.ready_iterations == 2
    assert font.count == 2
    assert font.median_ms == 110.0
    assert font.mean_ms == 110.0
    assert runner.count == 2
    assert runner.median_ms == 1000.0


def test_compare_profiles_reports_median_delta() -> None:
    """Profile comparisons use shared phase medians."""
    summaries = (
        ProfileSummary(
            profile="disabled",
            iterations=1,
            ready_iterations=1,
            phases=(PhaseSummary("bootstrap.total", 1, 50.0, 50.0, 50.0, 50.0),),
        ),
        ProfileSummary(
            profile="enabled",
            iterations=1,
            ready_iterations=1,
            phases=(PhaseSummary("bootstrap.total", 1, 80.0, 80.0, 80.0, 80.0),),
        ),
    )

    comparisons = compare_profiles(summaries, baseline_profile="disabled", candidate_profile="enabled")

    assert len(comparisons) == 1
    assert comparisons[0].phase == "bootstrap.total"
    assert comparisons[0].delta_median_ms == 30.0


def test_parse_cli_reads_preserved_iteration_artifacts(tmp_path: Path) -> None:
    """The parse command summarizes saved Player.log and metadata files."""
    iteration_dir = tmp_path / "profiles" / "enabled" / "iteration-01"
    iteration_dir.mkdir(parents=True)
    (iteration_dir / "metadata.json").write_text(
        json.dumps(
            {
                "profile": "enabled",
                "iteration": 1,
                "command": ["scripts/launch_rosetta.sh"],
                "started_at": "2026-05-09T00:00:00+00:00",
                "status": "ready",
                "elapsed_until_ready_ms": 1000.0,
                "exit_code": 0,
            },
        ),
        encoding="utf-8",
    )
    (iteration_dir / "Player.log").write_text(
        "[QudJP] StartupTiming/v1: phase=font.initialize elapsed_ms=25\n",
        encoding="utf-8",
    )
    output = tmp_path / "summary.json"
    markdown = tmp_path / "summary.md"

    exit_code = main(["parse", "--input-dir", str(tmp_path), "--output", str(output), "--markdown", str(markdown)])

    summary = json.loads(output.read_text(encoding="utf-8"))
    assert exit_code == 0
    assert summary[0]["profile"] == "enabled"
    assert summary[0]["phases"][0]["phase"] == "font.initialize"
    assert markdown.read_text(encoding="utf-8").startswith("# Startup Measurement Summary")


def test_top_cli_reports_highest_matching_phases(tmp_path: Path) -> None:
    """The top command ranks matching phase medians per profile."""
    summary = [
        {
            "profile": "enabled",
            "iterations": 1,
            "ready_iterations": 1,
            "phases": [
                {
                    "phase": "harmony.patch_apply.FastPatch",
                    "count": 1,
                    "mean_ms": 5.0,
                    "median_ms": 5.0,
                    "min_ms": 5.0,
                    "max_ms": 5.0,
                },
                {
                    "phase": "harmony.patch_apply.SlowPatch",
                    "count": 1,
                    "mean_ms": 50.0,
                    "median_ms": 50.0,
                    "min_ms": 50.0,
                    "max_ms": 50.0,
                },
                {
                    "phase": "font.initialize",
                    "count": 1,
                    "mean_ms": 100.0,
                    "median_ms": 100.0,
                    "min_ms": 100.0,
                    "max_ms": 100.0,
                },
            ],
        },
    ]
    summary_path = tmp_path / "summary.json"
    output = tmp_path / "top.json"
    markdown = tmp_path / "top.md"
    summary_path.write_text(json.dumps(summary), encoding="utf-8")

    exit_code = main(
        [
            "top",
            "--summary",
            str(summary_path),
            "--prefix",
            "harmony.patch_apply.",
            "--limit",
            "1",
            "--output",
            str(output),
            "--markdown",
            str(markdown),
        ],
    )

    top = json.loads(output.read_text(encoding="utf-8"))
    assert exit_code == 0
    assert top["enabled"][0]["phase"] == "harmony.patch_apply.SlowPatch"
    assert "SlowPatch" in markdown.read_text(encoding="utf-8")


def test_restore_mod_refuses_to_delete_recreated_destination(tmp_path: Path) -> None:
    """Restoring a disabled mod never removes a concurrently recreated destination."""
    mod_dir = tmp_path / "Mods" / "QudJP"
    disabled_dir = tmp_path / ".qudjp-startup-measure-disabled" / "QudJP"
    mod_dir.mkdir(parents=True)
    disabled_dir.mkdir(parents=True)
    (mod_dir / "new-file.txt").write_text("new", encoding="utf-8")
    (disabled_dir / "original-file.txt").write_text("original", encoding="utf-8")

    with pytest.raises(FileExistsError, match="Refusing to restore disabled mod"):
        _restore_mod(mod_dir, disabled_dir)

    assert (mod_dir / "new-file.txt").read_text(encoding="utf-8") == "new"
    assert (disabled_dir / "original-file.txt").read_text(encoding="utf-8") == "original"


def test_disable_mod_requires_explicit_ready_marker() -> None:
    """A disabled QudJP profile cannot wait on QudJP's own completion marker."""
    with pytest.raises(ValueError, match="--disable-mod requires --ready-marker"):
        _resolve_ready_marker(disable_mod=True, ready_marker=None)


def _result(profile: str, iteration: int, status: str, timings: dict[str, float]) -> IterationResult:
    parsed = parse_startup_log_text(
        "\n".join(
            f"[QudJP] StartupTiming/v1: phase={phase} elapsed_ms={elapsed}"
            for phase, elapsed in timings.items()
        ),
    )
    return IterationResult(
        metadata=IterationMetadata(
            profile=profile,
            iteration=iteration,
            command=("scripts/launch_rosetta.sh",),
            started_at="2026-05-09T00:00:00+00:00",
            status=status,
            elapsed_until_ready_ms=1000.0,
            exit_code=0,
        ),
        parsed=parsed,
    )
