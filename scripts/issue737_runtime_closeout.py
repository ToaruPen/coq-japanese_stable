"""Check fresh Player.log evidence for Issue #737 runtime closeout."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any

_DEFAULT_LOG = Path.home() / "Library" / "Logs" / "Freehold Games" / "CavesOfQud" / "Player.log"
_DEFAULT_SOURCE_MOD_ROOT = Path(__file__).resolve().parents[1] / "Mods" / "QudJP"
_DEFAULT_DEPLOYMENT_FILES = (
    Path("Assemblies") / "QudJP.dll",
    Path("Localization") / "Dictionaries" / "Scoped" / "historyspice-common.ja.json",
    Path("Localization") / "Dictionaries" / "annals-patterns.ja.json",
    Path("Localization") / "Dictionaries" / "journal-patterns.ja.json",
)
_VISIBLE_PROBE_FIELD_PATTERN = re.compile(
    r"\b(?:final|translated)='(?P<single>(?:\\'|[^'])*)'|"
    r'\b(?:final|translated)="(?P<double>(?:\\"|[^"])*)"',
)


@dataclass(frozen=True)
class CloseoutCheck:
    """One Issue #737 runtime closeout check."""

    id: str
    description: str
    observed_patterns: tuple[str, ...]
    failure_patterns: tuple[tuple[str, str], ...]
    runtime_required: bool = False


_CHECKS: tuple[CloseoutCheck, ...] = (
    CloseoutCheck(
        id="startup_health",
        description="QudJP startup has no obvious compile/runtime warnings.",
        observed_patterns=("[QudJP] Build marker:",),
        failure_patterns=(
            ("MODWARN", "MODWARN"),
            ("QudJP compile error", "QudJP compile error"),
            ("MissingMethodException", "MissingMethodException"),
            ("QudJP-owned exception", "[QudJP] Exception"),
        ),
    ),
    CloseoutCheck(
        id="campfire_meal_ingredients",
        description="Campfire meal descriptions no longer expose Issue #737 English ingredient fragments.",
        observed_patterns=("CampfireDescribeMealTranslationPatch", "You toss ", "鍋に放り込み"),
        failure_patterns=(
            ("glass berries", "glass berries"),
            ("nip of joined paprika", "nip of joined paprika"),
            ("chameleon horn", "chameleon horn"),
        ),
    ),
    CloseoutCheck(
        id="campfire_preserve_frame",
        description="Campfire preserve output no longer exposes Some/into/serving frame residue.",
        observed_patterns=("CampfirePreserveTranslationPatch", "You preserved", "保存した"),
        failure_patterns=(("preserve-frame residue", "PRESERVE_FRAME_RESIDUE"),),
    ),
    CloseoutCheck(
        id="sultan_journal_history",
        description="Sultan/journal history no longer exposes Issue #737 generated header/body/date residue.",
        observed_patterns=("JournalSultanNote", "JournalVillageNote", "Sultan Histories", "スルタン", "HISTORY OF"),
        failure_patterns=(
            ("HISTORY OF", "HISTORY OF"),
            ("with malicious soldering", "with malicious soldering"),
            ("shining visage", "shining visage"),
            ("On the 22nd of Tishru i Ux", "On the 22nd of Tishru i Ux"),
        ),
    ),
    CloseoutCheck(
        id="journal_map_note_location",
        description="Journal map-note generated locations no longer expose English settlement/distance residue.",
        observed_patterns=("JournalMapNote", "最後に訪れた", "parasangs", "Stargazerhome"),
        failure_patterns=(
            ("Stargazerhome", "Stargazerhome"),
            ("parasangs-distance", "parasangs "),
        ),
    ),
    CloseoutCheck(
        id="journal_relationship_title",
        description="Generated relationship/title fragments no longer expose leader-of-the residue.",
        observed_patterns=("leader of the ", "の指導者"),
        failure_patterns=(("leader of the", "leader of the "),),
    ),
    CloseoutCheck(
        id="textfilters_runtime_required",
        description="TextFilters Angry/Lallated stay runtime-required unless concrete owner-route output appears.",
        observed_patterns=("TextFilters.Angry", "TextFilters.Lallated", "Lallated", "Angry"),
        failure_patterns=(),
        runtime_required=True,
    ),
)


def analyze_log(
    *,
    log_path: Path,
    min_mtime: datetime | None = None,
    source_mod_root: Path | None = None,
    deployed_mod_root: Path | None = None,
    deployment_files: tuple[Path, ...] = _DEFAULT_DEPLOYMENT_FILES,
) -> dict[str, Any]:
    """Analyze a Player.log for Issue #737 closeout evidence."""
    if not log_path.exists():
        msg = f"Player.log not found: {log_path}"
        raise FileNotFoundError(msg)

    lines = log_path.read_text(encoding="utf-8", errors="replace").splitlines()
    mtime = datetime.fromtimestamp(log_path.stat().st_mtime).astimezone()
    deployment = _deployment_report(
        source_mod_root=source_mod_root,
        deployed_mod_root=deployed_mod_root,
        deployment_files=deployment_files,
    )
    freshness = {
        "log_path": str(log_path),
        "mtime": mtime.isoformat(),
        "min_mtime": min_mtime.isoformat() if min_mtime is not None else None,
        "is_fresh": min_mtime is None or mtime > min_mtime,
    }
    if not freshness["is_fresh"]:
        return {
            "status": "stale",
            "freshness": freshness,
            "deployment": deployment,
            "checks": [],
        }

    checks = [_analyze_check(check, lines) for check in _CHECKS]
    status = _overall_status(checks, deployment=deployment)
    return {
        "status": status,
        "freshness": freshness,
        "deployment": deployment,
        "checks": checks,
    }


def main(argv: list[str] | None = None) -> int:
    """Run the Issue #737 runtime closeout checker."""
    parser = argparse.ArgumentParser(description="Check Player.log evidence for Issue #737 runtime closeout.")
    parser.add_argument(
        "--log",
        type=Path,
        default=_DEFAULT_LOG,
        help=f"Path to Player.log (default: {_DEFAULT_LOG})",
    )
    parser.add_argument(
        "--min-mtime",
        type=_parse_datetime,
        default=None,
        help="Require Player.log mtime to be after this ISO-8601 timestamp.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Output path for JSON report (default: stdout).",
    )
    parser.add_argument(
        "--require-passed",
        action="store_true",
        help="Exit non-zero unless the report status is passed.",
    )
    parser.add_argument(
        "--source-mod-root",
        type=Path,
        default=_DEFAULT_SOURCE_MOD_ROOT,
        help=f"Source Mods/QudJP root for optional deployment hash checks (default: {_DEFAULT_SOURCE_MOD_ROOT}).",
    )
    parser.add_argument(
        "--deployed-mod-root",
        type=Path,
        default=None,
        help="Optional deployed Mods/QudJP root to compare against the source mod files.",
    )
    parser.add_argument(
        "--deployment-file",
        type=Path,
        action="append",
        default=None,
        help="Relative mod file to compare for --deployed-mod-root. May be repeated.",
    )
    args = parser.parse_args(argv)

    try:
        report = analyze_log(
            log_path=args.log,
            min_mtime=args.min_mtime,
            source_mod_root=args.source_mod_root,
            deployed_mod_root=args.deployed_mod_root,
            deployment_files=tuple(args.deployment_file or _DEFAULT_DEPLOYMENT_FILES),
        )
    except (FileNotFoundError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    report_json = json.dumps(report, ensure_ascii=False, indent=2)
    if args.output is None:
        print(report_json)  # noqa: T201
        return _exit_code(report, require_passed=args.require_passed)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(report_json, encoding="utf-8")
    print(f"Runtime closeout report written to {args.output}", file=sys.stderr)  # noqa: T201
    return _exit_code(report, require_passed=args.require_passed)


def _analyze_check(check: CloseoutCheck, lines: list[str]) -> dict[str, Any]:
    matches = _failure_matches(check, lines)
    observed = _is_observed(check, lines)
    if check.runtime_required:
        status = "runtime_required"
    elif matches:
        status = "failed"
    elif observed:
        status = "passed"
    else:
        status = "unobserved"
    return {
        "id": check.id,
        "description": check.description,
        "status": status,
        "observed": observed,
        "matches": matches,
    }


def _failure_matches(check: CloseoutCheck, lines: list[str]) -> list[dict[str, object]]:
    matches: list[dict[str, object]] = []
    for line_number, line in enumerate(lines, start=1):
        for text in _failure_search_texts(line):
            for label, needle in check.failure_patterns:
                if needle == "PRESERVE_FRAME_RESIDUE":
                    if _has_preserve_frame_residue(text):
                        matches.append({"line": line_number, "pattern": label, "excerpt": _excerpt(text)})
                    continue
                if needle in text:
                    matches.append({"line": line_number, "pattern": label, "excerpt": _excerpt(text)})
    return matches


def _failure_search_texts(line: str) -> list[str]:
    """Return final/translated text fields when a probe logs source text too."""
    if "Probe/" not in line:
        return [line]

    visible_values = [
        _unescape_probe_value(match.group("single") or match.group("double") or "")
        for match in _VISIBLE_PROBE_FIELD_PATTERN.finditer(line)
    ]
    return visible_values or [line]


def _unescape_probe_value(value: str) -> str:
    unescaped = (
        value.replace(r"\'", "'")
        .replace(r"\"", '"')
        .replace(r"\n", "\n")
        .replace(r"\r", "\r")
        .replace(r"\t", "\t")
    )
    return re.sub(r"\\u([0-9A-Fa-f]{4})", lambda match: chr(int(match.group(1), 16)), unescaped)


def _is_observed(check: CloseoutCheck, lines: list[str]) -> bool:
    return any(pattern in line for pattern in check.observed_patterns for line in lines)


def _has_preserve_frame_residue(line: str) -> bool:
    if "You preserved" not in line and "保存した" not in line and "CampfirePreserveTranslationPatch" not in line:
        return False
    return "Some " in line or " into " in line or " serving" in line


def _overall_status(checks: list[dict[str, Any]], *, deployment: dict[str, Any]) -> str:
    if deployment["status"] == "failed":
        return "deployment_mismatch"
    active_statuses = [check["status"] for check in checks if check["status"] != "runtime_required"]
    if any(status == "failed" for status in active_statuses):
        return "failed"
    if any(status == "unobserved" for status in active_statuses):
        return "unobserved"
    return "passed"


def _deployment_report(
    *,
    source_mod_root: Path | None,
    deployed_mod_root: Path | None,
    deployment_files: tuple[Path, ...],
) -> dict[str, Any]:
    if deployed_mod_root is None:
        return {
            "status": "not_checked",
            "source_mod_root": str(source_mod_root) if source_mod_root is not None else None,
            "deployed_mod_root": None,
            "files": [],
        }
    if source_mod_root is None:
        msg = "--source-mod-root is required when --deployed-mod-root is provided."
        raise ValueError(msg)

    files = [
        _deployment_file_report(
            source_mod_root=source_mod_root,
            deployed_mod_root=deployed_mod_root,
            relative_path=relative_path,
        )
        for relative_path in deployment_files
    ]
    status = "passed" if all(file_report["status"] == "passed" for file_report in files) else "failed"
    return {
        "status": status,
        "source_mod_root": str(source_mod_root),
        "deployed_mod_root": str(deployed_mod_root),
        "files": files,
    }


def _deployment_file_report(*, source_mod_root: Path, deployed_mod_root: Path, relative_path: Path) -> dict[str, Any]:
    source_path = source_mod_root / relative_path
    deployed_path = deployed_mod_root / relative_path
    source_sha256 = _sha256(source_path) if source_path.is_file() else None
    deployed_sha256 = _sha256(deployed_path) if deployed_path.is_file() else None
    status = "passed" if source_sha256 is not None and source_sha256 == deployed_sha256 else "failed"
    return {
        "path": str(relative_path),
        "status": status,
        "source_exists": source_sha256 is not None,
        "deployed_exists": deployed_sha256 is not None,
        "source_sha256": source_sha256,
        "deployed_sha256": deployed_sha256,
    }


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _exit_code(report: dict[str, Any], *, require_passed: bool) -> int:
    if not require_passed or report.get("status") == "passed":
        return 0
    print(f"Runtime closeout status is {report.get('status')}; expected passed.", file=sys.stderr)  # noqa: T201
    return 2


def _parse_datetime(value: str) -> datetime:
    try:
        parsed = datetime.fromisoformat(value)
    except ValueError as exc:
        msg = f"Invalid ISO-8601 timestamp: {value}"
        raise argparse.ArgumentTypeError(msg) from exc
    if parsed.tzinfo is None:
        msg = f"Timestamp must include a timezone offset: {value}"
        raise argparse.ArgumentTypeError(msg)
    return parsed


def _excerpt(line: str, *, limit: int = 240) -> str:
    if len(line) <= limit:
        return line
    return line[: limit - 1] + "…"


if __name__ == "__main__":
    raise SystemExit(main())
