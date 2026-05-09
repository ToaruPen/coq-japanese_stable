"""Collect and summarize Caves of Qud startup timing logs."""

from __future__ import annotations

import argparse
import json
import re
import shlex
import shutil
import subprocess
import sys
import time
from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from pathlib import Path
from statistics import mean, median
from typing import TYPE_CHECKING

_PROJECT_ROOT = Path(__file__).resolve().parents[1]
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

from scripts.sync_mod import resolve_default_destination  # noqa: E402

if TYPE_CHECKING:
    from collections.abc import Iterable, Sequence

_DEFAULT_LOG = Path.home() / "Library" / "Logs" / "Freehold Games" / "CavesOfQud" / "Player.log"
_DEFAULT_ARTIFACT_DIR = Path(".sisyphus") / "evidence" / f"issue-604-{datetime.now(UTC):%Y%m%dT%H%M%SZ}"
_STARTUP_TIMING_MARKER = "[QudJP] StartupTiming/v1:"
_GAME_LOADING_TASK_PATTERN = re.compile(r"INFO - Finished 'Loading (?P<task>[^']+)' task in (?P<elapsed>\d+)ms")
_BUILD_MARKER = "[QudJP] Build marker:"
_HARMONY_COMPLETE_MARKER = "[QudJP] Harmony patching complete:"
_ERROR_MARKERS = (
    "MissingMethodException",
    "MODWARN",
    "[QudJP] Bootstrap failed",
    "[QudJP] FontManager failed",
)


@dataclass(frozen=True)
class StartupTimingEntry:
    """A single `StartupTiming/v1` timing entry from Player.log."""

    phase: str
    elapsed_ms: float
    detail: str | None
    line_number: int


@dataclass(frozen=True)
class ParsedStartupLog:
    """Structured timing and marker data extracted from one Player.log."""

    timings: tuple[StartupTimingEntry, ...]
    build_marker_seen: bool
    harmony_complete_seen: bool
    error_lines: tuple[str, ...]


@dataclass(frozen=True)
class IterationMetadata:
    """Runner-side metadata for one startup iteration."""

    profile: str
    iteration: int
    command: tuple[str, ...]
    started_at: str
    status: str
    elapsed_until_ready_ms: float | None
    exit_code: int | None


@dataclass(frozen=True)
class IterationResult:
    """Combined runner metadata and parsed log data for one iteration."""

    metadata: IterationMetadata
    parsed: ParsedStartupLog


@dataclass(frozen=True)
class PhaseSummary:
    """Aggregate timing statistics for one phase in one profile."""

    phase: str
    count: int
    mean_ms: float
    median_ms: float
    min_ms: float
    max_ms: float


@dataclass(frozen=True)
class ProfileSummary:
    """Aggregate timing statistics for one measurement profile."""

    profile: str
    iterations: int
    ready_iterations: int
    phases: tuple[PhaseSummary, ...]


@dataclass(frozen=True)
class PhaseComparison:
    """Comparison for a phase shared by two profiles."""

    phase: str
    baseline_median_ms: float
    candidate_median_ms: float
    delta_median_ms: float


def parse_startup_log_text(text: str) -> ParsedStartupLog:
    """Parse QudJP startup timing markers from Player.log text."""
    timings: list[StartupTimingEntry] = []
    error_lines: list[str] = []
    build_marker_seen = False
    harmony_complete_seen = False

    for index, line in enumerate(text.splitlines(), start=1):
        if _BUILD_MARKER in line:
            build_marker_seen = True
        if _HARMONY_COMPLETE_MARKER in line:
            harmony_complete_seen = True
        if any(marker in line for marker in _ERROR_MARKERS):
            error_lines.append(line)
        marker_index = line.find(_STARTUP_TIMING_MARKER)
        if marker_index != -1:
            fields = _parse_timing_fields(line[marker_index + len(_STARTUP_TIMING_MARKER) :].strip())
            phase = fields.get("phase")
            elapsed_ms = fields.get("elapsed_ms")
            if phase is None or elapsed_ms is None:
                continue
            try:
                elapsed = float(elapsed_ms)
            except ValueError:
                continue
            timings.append(
                StartupTimingEntry(
                    phase=phase,
                    elapsed_ms=elapsed,
                    detail=fields.get("detail"),
                    line_number=index,
                ),
            )
            continue

        game_loading_match = _GAME_LOADING_TASK_PATTERN.search(line)
        if game_loading_match is not None:
            timings.append(
                StartupTimingEntry(
                    phase="game.loading." + _normalize_phase_fragment(game_loading_match.group("task")),
                    elapsed_ms=float(game_loading_match.group("elapsed")),
                    detail=game_loading_match.group("task"),
                    line_number=index,
                ),
            )

    return ParsedStartupLog(
        timings=tuple(timings),
        build_marker_seen=build_marker_seen,
        harmony_complete_seen=harmony_complete_seen,
        error_lines=tuple(error_lines),
    )


def summarize_iterations(results: Iterable[IterationResult]) -> tuple[ProfileSummary, ...]:
    """Build per-profile phase summaries from iteration results."""
    grouped: dict[str, list[IterationResult]] = {}
    for result in results:
        grouped.setdefault(result.metadata.profile, []).append(result)

    summaries: list[ProfileSummary] = []
    for profile, profile_results in sorted(grouped.items()):
        phase_values: dict[str, list[float]] = {}
        for result in profile_results:
            if result.metadata.elapsed_until_ready_ms is not None:
                phase_values.setdefault("runner.elapsed_until_ready", []).append(result.metadata.elapsed_until_ready_ms)
            for timing in result.parsed.timings:
                phase_values.setdefault(timing.phase, []).append(timing.elapsed_ms)
        phases = tuple(
            PhaseSummary(
                phase=phase,
                count=len(values),
                mean_ms=round(mean(values), 3),
                median_ms=round(median(values), 3),
                min_ms=round(min(values), 3),
                max_ms=round(max(values), 3),
            )
            for phase, values in sorted(phase_values.items())
        )
        summaries.append(
            ProfileSummary(
                profile=profile,
                iterations=len(profile_results),
                ready_iterations=sum(1 for result in profile_results if result.metadata.status == "ready"),
                phases=phases,
            ),
        )
    return tuple(summaries)


def compare_profiles(
    summaries: Sequence[ProfileSummary],
    *,
    baseline_profile: str,
    candidate_profile: str,
) -> tuple[PhaseComparison, ...]:
    """Compare median phase timings between two profiles."""
    by_profile = {summary.profile: summary for summary in summaries}
    baseline = by_profile[baseline_profile]
    candidate = by_profile[candidate_profile]
    baseline_phases = {phase.phase: phase for phase in baseline.phases}
    candidate_phases = {phase.phase: phase for phase in candidate.phases}

    comparisons: list[PhaseComparison] = []
    for phase in sorted(set(baseline_phases) & set(candidate_phases)):
        baseline_median = baseline_phases[phase].median_ms
        candidate_median = candidate_phases[phase].median_ms
        comparisons.append(
            PhaseComparison(
                phase=phase,
                baseline_median_ms=baseline_median,
                candidate_median_ms=candidate_median,
                delta_median_ms=round(candidate_median - baseline_median, 3),
            ),
        )
    return tuple(comparisons)


def write_summary_markdown(summaries: Sequence[ProfileSummary], path: Path) -> None:
    """Write a compact Markdown summary table."""
    lines = ["# Startup Measurement Summary", ""]
    for summary in summaries:
        lines.extend(
            [
                f"## {summary.profile}",
                "",
                f"- iterations: {summary.iterations}",
                f"- ready iterations: {summary.ready_iterations}",
                "",
                "| phase | count | median ms | mean ms | min ms | max ms |",
                "| --- | ---: | ---: | ---: | ---: | ---: |",
            ],
        )
        lines.extend(
            f"| {phase.phase} | {phase.count} | {phase.median_ms:.3f} | {phase.mean_ms:.3f} | "
            f"{phase.min_ms:.3f} | {phase.max_ms:.3f} |"
            for phase in summary.phases
        )
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def write_comparison_markdown(comparisons: Sequence[PhaseComparison], path: Path) -> None:
    """Write a compact Markdown comparison table."""
    lines = [
        "# Startup Measurement Comparison",
        "",
        "| phase | baseline median ms | candidate median ms | delta median ms |",
        "| --- | ---: | ---: | ---: |",
    ]
    lines.extend(
        f"| {item.phase} | {item.baseline_median_ms:.3f} | {item.candidate_median_ms:.3f} | "
        f"{item.delta_median_ms:.3f} |"
        for item in comparisons
    )
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def write_top_phases_markdown(
    summaries: Sequence[ProfileSummary],
    path: Path,
    *,
    prefix: str,
    limit: int,
) -> None:
    """Write the highest-median phases matching a prefix for each profile."""
    lines = ["# Startup Measurement Top Phases", "", f"- prefix: `{prefix}`", f"- limit: {limit}", ""]
    for summary in summaries:
        matching = [phase for phase in summary.phases if phase.phase.startswith(prefix)]
        matching.sort(key=lambda phase: phase.median_ms, reverse=True)
        lines.extend(
            [
                f"## {summary.profile}",
                "",
                "| rank | phase | count | median ms | mean ms | min ms | max ms |",
                "| ---: | --- | ---: | ---: | ---: | ---: | ---: |",
            ],
        )
        if not matching:
            lines.append("|  | _no matching phases_ |  |  |  |  |  |")
        else:
            lines.extend(
                f"| {rank} | {phase.phase} | {phase.count} | {phase.median_ms:.3f} | "
                f"{phase.mean_ms:.3f} | {phase.min_ms:.3f} | {phase.max_ms:.3f} |"
                for rank, phase in enumerate(matching[:limit], start=1)
            )
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def _parse_timing_fields(text: str) -> dict[str, str]:
    fields: dict[str, str] = {}
    for token in _split_escaped_tokens(text):
        if "=" not in token:
            continue
        key, value = token.split("=", maxsplit=1)
        fields[key] = value
    return fields


def _split_escaped_tokens(text: str) -> list[str]:
    tokens: list[str] = []
    current: list[str] = []
    escaped = False
    for char in text:
        if escaped:
            current.append(char)
            escaped = False
            continue
        if char == "\\":
            escaped = True
            continue
        if char == " ":
            if current:
                tokens.append("".join(current))
                current = []
            continue
        current.append(char)
    if escaped:
        current.append("\\")
    if current:
        tokens.append("".join(current))
    return tokens


def _normalize_phase_fragment(value: str) -> str:
    normalized = re.sub(r"[^a-z0-9]+", "_", value.lower()).strip("_")
    return normalized or "unknown"


def _as_jsonable(value: object) -> object:
    if isinstance(value, tuple):
        return [_as_jsonable(item) for item in value]
    if isinstance(value, list):
        return [_as_jsonable(item) for item in value]
    if hasattr(value, "__dataclass_fields__"):
        return {key: _as_jsonable(item) for key, item in asdict(value).items()}
    if isinstance(value, dict):
        return {key: _as_jsonable(item) for key, item in value.items()}
    return value


def _write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(_as_jsonable(value), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _read_iteration_result(iteration_dir: Path) -> IterationResult:
    metadata = json.loads((iteration_dir / "metadata.json").read_text(encoding="utf-8"))
    parsed = parse_startup_log_text((iteration_dir / "Player.log").read_text(encoding="utf-8"))
    return IterationResult(
        metadata=IterationMetadata(
            profile=metadata["profile"],
            iteration=int(metadata["iteration"]),
            command=tuple(metadata["command"]),
            started_at=metadata["started_at"],
            status=metadata["status"],
            elapsed_until_ready_ms=metadata["elapsed_until_ready_ms"],
            exit_code=metadata["exit_code"],
        ),
        parsed=parsed,
    )


def _find_iteration_dirs(input_dir: Path) -> list[Path]:
    return sorted(path for path in input_dir.glob("profiles/*/iteration-*") if (path / "metadata.json").exists())


def _read_profile_summaries(summary_path: Path) -> tuple[ProfileSummary, ...]:
    summaries = json.loads(summary_path.read_text(encoding="utf-8"))
    return tuple(
        ProfileSummary(
            profile=summary["profile"],
            iterations=summary["iterations"],
            ready_iterations=summary["ready_iterations"],
            phases=tuple(PhaseSummary(**phase) for phase in summary["phases"]),
        )
        for summary in summaries
    )


def _wait_for_ready_marker(log_path: Path, timeout_seconds: float, ready_marker: str) -> tuple[str, float | None]:
    started = time.monotonic()
    deadline = started + timeout_seconds
    while time.monotonic() < deadline:
        if log_path.exists():
            text = log_path.read_text(encoding="utf-8", errors="replace")
            if ready_marker in text:
                return "ready", time.monotonic() - started
            if "Harmony patched zero methods" in text or "mprotect returned EACCES" in text:
                return "harmony_failed", time.monotonic() - started
        time.sleep(0.25)
    return "timeout", None


def _stop_process(process: subprocess.Popen[bytes]) -> None:
    if process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)


def _disable_mod(mod_dir: Path) -> Path | None:
    if not mod_dir.exists():
        return None
    disabled_root = mod_dir.parent.parent / ".qudjp-startup-measure-disabled"
    disabled_dir = disabled_root / mod_dir.name
    if disabled_dir.exists():
        msg = f"Refusing to overwrite existing disabled mod directory: {disabled_dir}"
        raise FileExistsError(msg)
    disabled_root.mkdir(parents=True, exist_ok=True)
    mod_dir.rename(disabled_dir)
    return disabled_dir


def _restore_mod(mod_dir: Path, disabled_dir: Path | None) -> None:
    if disabled_dir is None:
        return
    if mod_dir.exists():
        msg = (
            f"Refusing to restore disabled mod over existing directory: {mod_dir}. "
            f"The disabled copy remains at: {disabled_dir}"
        )
        raise FileExistsError(msg)
    mod_dir.parent.mkdir(parents=True, exist_ok=True)
    disabled_dir.rename(mod_dir)


def _resolve_ready_marker(*, disable_mod: bool, ready_marker: str | None) -> str:
    if ready_marker is not None:
        return ready_marker
    if disable_mod:
        msg = (
            "--disable-mod requires --ready-marker because the default QudJP Harmony "
            "completion marker will not appear while QudJP is disabled."
        )
        raise ValueError(msg)
    return _HARMONY_COMPLETE_MARKER


def _collect(args: argparse.Namespace) -> int:
    command = tuple(shlex.split(args.launch_cmd))
    artifact_dir = args.artifact_dir
    mod_dir = args.mod_dir or resolve_default_destination()
    ready_marker = _resolve_ready_marker(disable_mod=args.disable_mod, ready_marker=args.ready_marker)
    disabled_dirs: list[tuple[Path, Path | None]] = []
    try:
        if args.disable_mod:
            disabled_dirs.append((mod_dir, _disable_mod(mod_dir)))
        disabled_dirs.extend((extra_mod_dir, _disable_mod(extra_mod_dir)) for extra_mod_dir in args.disable_mod_dir)
        results: list[IterationResult] = []
        for iteration in range(1, args.iterations + 1):
            iteration_dir = artifact_dir / "profiles" / args.profile / f"iteration-{iteration:02d}"
            iteration_dir.mkdir(parents=True, exist_ok=True)
            previous_log = iteration_dir / "Player.before.log"
            if args.log.exists():
                shutil.copy2(args.log, previous_log)
                args.log.unlink()
            started_at = datetime.now(UTC).isoformat()
            with (iteration_dir / "stdout.txt").open("wb") as stdout_file, (iteration_dir / "stderr.txt").open(
                "wb",
            ) as stderr_file:
                process = subprocess.Popen(  # noqa: S603 -- command is explicit CLI input for local measurement.
                    command,
                    stdout=stdout_file,
                    stderr=stderr_file,
                )
                status, elapsed = _wait_for_ready_marker(args.log, args.timeout, ready_marker)
                if status == "ready" and args.settle_seconds > 0:
                    time.sleep(args.settle_seconds)
                _stop_process(process)
            if args.log.exists():
                shutil.copy2(args.log, iteration_dir / "Player.log")
            else:
                (iteration_dir / "Player.log").write_text("", encoding="utf-8")

            metadata = IterationMetadata(
                profile=args.profile,
                iteration=iteration,
                command=command,
                started_at=started_at,
                status=status,
                elapsed_until_ready_ms=None if elapsed is None else round(elapsed * 1000, 3),
                exit_code=process.returncode,
            )
            parsed = parse_startup_log_text((iteration_dir / "Player.log").read_text(encoding="utf-8"))
            result = IterationResult(metadata=metadata, parsed=parsed)
            results.append(result)
            _write_json(iteration_dir / "metadata.json", metadata)
            _write_json(iteration_dir / "parsed.json", parsed)

        summaries = summarize_iterations(results)
        artifact_dir.mkdir(parents=True, exist_ok=True)
        _write_json(artifact_dir / f"{args.profile}-summary.json", summaries)
        write_summary_markdown(summaries, artifact_dir / f"{args.profile}-summary.md")
        return 0
    finally:
        for enabled_dir, disabled_dir in reversed(disabled_dirs):
            _restore_mod(enabled_dir, disabled_dir)


def _parse(args: argparse.Namespace) -> int:
    results = [_read_iteration_result(path) for path in _find_iteration_dirs(args.input_dir)]
    summaries = summarize_iterations(results)
    _write_json(args.output, summaries)
    if args.markdown is not None:
        write_summary_markdown(summaries, args.markdown)
    return 0


def _compare(args: argparse.Namespace) -> int:
    profile_summaries = _read_profile_summaries(args.summary)
    comparisons = compare_profiles(
        profile_summaries,
        baseline_profile=args.baseline,
        candidate_profile=args.candidate,
    )
    _write_json(args.output, comparisons)
    if args.markdown is not None:
        write_comparison_markdown(comparisons, args.markdown)
    return 0


def _top(args: argparse.Namespace) -> int:
    profile_summaries = _read_profile_summaries(args.summary)
    selected_profiles = set(args.profile)
    if selected_profiles:
        profile_summaries = tuple(summary for summary in profile_summaries if summary.profile in selected_profiles)

    top_by_profile: dict[str, list[PhaseSummary]] = {}
    for summary in profile_summaries:
        matching = [phase for phase in summary.phases if phase.phase.startswith(args.prefix)]
        matching.sort(key=lambda phase: phase.median_ms, reverse=True)
        top_by_profile[summary.profile] = matching[: args.limit]

    _write_json(args.output, top_by_profile)
    if args.markdown is not None:
        write_top_phases_markdown(profile_summaries, args.markdown, prefix=args.prefix, limit=args.limit)
    return 0


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    collect = subparsers.add_parser("collect", help="Launch the game repeatedly and preserve Player.log artifacts.")
    collect.add_argument("--profile", required=True, help="Profile name, for example qudjp-enabled or qudjp-disabled.")
    collect.add_argument("--iterations", type=int, default=3, help="Number of launch iterations.")
    collect.add_argument("--artifact-dir", type=Path, default=_DEFAULT_ARTIFACT_DIR, help="Artifact output directory.")
    collect.add_argument(
        "--launch-cmd",
        default="scripts/launch_rosetta.sh",
        help="Command used to launch Caves of Qud.",
    )
    collect.add_argument("--log", type=Path, default=_DEFAULT_LOG, help="Path to Player.log.")
    collect.add_argument("--timeout", type=float, default=90.0, help="Seconds to wait for the ready marker.")
    collect.add_argument(
        "--settle-seconds",
        type=float,
        default=0.0,
        help="Seconds to keep the game running after the ready marker before preserving Player.log.",
    )
    collect.add_argument(
        "--ready-marker",
        default=None,
        help=(
            "Player.log marker that ends an iteration. Defaults to the QudJP Harmony "
            "completion marker unless --disable-mod is used."
        ),
    )
    collect.add_argument("--mod-dir", type=Path, default=None, help="Mods/QudJP directory to rename for --disable-mod.")
    collect.add_argument(
        "--disable-mod-dir",
        type=Path,
        action="append",
        default=[],
        help="Additional mod directory to temporarily rename during collection.",
    )
    collect.add_argument("--disable-mod", action="store_true", help="Temporarily disable QudJP for this profile.")
    collect.set_defaults(func=_collect)

    parse = subparsers.add_parser("parse", help="Parse preserved iteration artifacts into a summary.")
    parse.add_argument(
        "--input-dir",
        type=Path,
        required=True,
        help="Artifact directory containing profiles/*/iteration-*.",
    )
    parse.add_argument("--output", type=Path, required=True, help="JSON summary output path.")
    parse.add_argument("--markdown", type=Path, default=None, help="Optional Markdown summary output path.")
    parse.set_defaults(func=_parse)

    compare = subparsers.add_parser("compare", help="Compare two profiles from a summary JSON file.")
    compare.add_argument("--summary", type=Path, required=True, help="JSON summary from the parse command.")
    compare.add_argument("--baseline", required=True, help="Baseline profile name.")
    compare.add_argument("--candidate", required=True, help="Candidate profile name.")
    compare.add_argument("--output", type=Path, required=True, help="JSON comparison output path.")
    compare.add_argument("--markdown", type=Path, default=None, help="Optional Markdown comparison output path.")
    compare.set_defaults(func=_compare)

    top = subparsers.add_parser("top", help="Report highest-median phases matching a prefix.")
    top.add_argument("--summary", type=Path, required=True, help="JSON summary from the parse command.")
    top.add_argument("--prefix", required=True, help="Phase prefix to include, for example harmony.patch_apply.")
    top.add_argument("--limit", type=int, default=20, help="Maximum rows per profile.")
    top.add_argument("--profile", action="append", default=[], help="Optional profile to include; may be repeated.")
    top.add_argument("--output", type=Path, required=True, help="JSON top phases output path.")
    top.add_argument("--markdown", type=Path, default=None, help="Optional Markdown top phases output path.")
    top.set_defaults(func=_top)

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Run the startup measurement CLI."""
    parser = _build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
