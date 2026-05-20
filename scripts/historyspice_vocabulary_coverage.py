"""Report direct dictionary-key coverage for Base/HistorySpice.json leaves."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

DEFAULT_GROUPS = (
    "spice.history.gospels.*",
    "spice.typeOfVillage*",
    "spice.cooking.*",
    "spice.cooking.recipeNames.*",
    "spice.cooking.terrain.*",
    "spice.items.*",
    "spice.elements.*",
    "spice.commonPhrases.*",
    "spice.instancesOf.*",
    "spice.extradimensional.*",
    "spice.gossip.*",
    "spice.proverbs*",
)

DEFAULT_HSE_DICTIONARIES = (
    Path("Scoped/historyspice-common.ja.json"),
    Path("world-gospels.ja.json"),
)

DEFAULT_MISSING_EXAMPLE_LIMIT = 12


@dataclass(frozen=True)
class LeafRecord:
    """One string leaf extracted from HistorySpice."""

    path: str
    text: str


@dataclass(frozen=True)
class CoverageSummary:
    """Coverage counters for one leaf set."""

    unique_leaves: int
    covered: int
    missing: int
    coverage_percent: float


def extract_historyspice_leaves(payload: object) -> list[LeafRecord]:
    """Extract string leaves from a decoded HistorySpice JSON object."""
    leaves: list[LeafRecord] = []
    _walk_historyspice(payload, path=(), leaves=leaves)
    return leaves


def load_dictionary_keys(dictionary_paths: list[Path]) -> set[str]:
    """Load dictionary keys from QudJP JSON dictionary files."""
    keys: set[str] = set()
    for path in dictionary_paths:
        payload = json.loads(path.read_text(encoding="utf-8"))
        for entry in _iter_dictionary_entries(payload):
            key = entry.get("key")
            if isinstance(key, str):
                keys.add(key)
                if key.isascii():
                    keys.add(key.lower())
    return keys


def summarize_coverage(leaves: list[LeafRecord], dictionary_keys: set[str]) -> CoverageSummary:
    """Summarize direct exact/lowercase coverage for a leaf list."""
    unique = sorted({leaf.text for leaf in leaves})
    covered = sum(1 for leaf in unique if _is_covered(leaf, dictionary_keys))
    missing = len(unique) - covered
    return CoverageSummary(
        unique_leaves=len(unique),
        covered=covered,
        missing=missing,
        coverage_percent=_percent(covered, len(unique)),
    )


def summarize_groups(
    leaves: list[LeafRecord],
    dictionary_keys: set[str],
    groups: tuple[str, ...],
) -> dict[str, CoverageSummary]:
    """Summarize coverage for configured HistorySpice path groups."""
    grouped: dict[str, CoverageSummary] = {}
    for group in groups:
        group_leaves = [leaf for leaf in leaves if _matches_group(leaf.path, group)]
        grouped[group] = summarize_coverage(group_leaves, dictionary_keys)
    return grouped


def missing_leaf_examples(
    leaves: list[LeafRecord],
    dictionary_keys: set[str],
    *,
    limit: int = DEFAULT_MISSING_EXAMPLE_LIMIT,
) -> list[LeafRecord]:
    """Return deterministic examples of leaves not covered by dictionary keys."""
    missing: dict[str, LeafRecord] = {}
    for leaf in leaves:
        if _is_covered(leaf.text, dictionary_keys):
            continue
        missing.setdefault(leaf.text, leaf)
    return sorted(missing.values(), key=lambda leaf: (leaf.path, leaf.text))[:limit]


def build_report(
    *,
    historyspice_path: Path,
    dictionaries_root: Path,
    hse_dictionary_paths: list[Path],
    groups: tuple[str, ...] = DEFAULT_GROUPS,
) -> dict[str, object]:
    """Build the complete vocabulary coverage report."""
    leaves = extract_historyspice_leaves(json.loads(historyspice_path.read_text(encoding="utf-8")))
    all_dictionary_paths = sorted(dictionaries_root.rglob("*.json"))
    hse_dictionary_keys = load_dictionary_keys(hse_dictionary_paths)
    all_dictionary_keys = load_dictionary_keys(all_dictionary_paths)
    all_group_summaries = summarize_groups(leaves, all_dictionary_keys, groups)

    return {
        "historyspice_path": str(historyspice_path),
        "leaf_occurrences": len(leaves),
        "unique_leaf_strings": len({leaf.text for leaf in leaves}),
        "hse_dictionary_paths": [str(path) for path in hse_dictionary_paths],
        "hse_dictionary_keys": len(hse_dictionary_keys),
        "hse_dictionary_coverage": _summary_dict(summarize_coverage(leaves, hse_dictionary_keys)),
        "all_dictionary_keys": len(all_dictionary_keys),
        "all_dictionary_coverage": _summary_dict(summarize_coverage(leaves, all_dictionary_keys)),
        "groups": {
            group: {
                **_summary_dict(summary),
                "missing_examples": [
                    {"path": leaf.path, "text": leaf.text}
                    for leaf in missing_leaf_examples(
                        [leaf for leaf in leaves if _matches_group(leaf.path, group)],
                        all_dictionary_keys,
                    )
                ],
            }
            for group, summary in all_group_summaries.items()
        },
    }


def main(argv: list[str] | None = None) -> int:
    """Run the HistorySpice vocabulary coverage CLI."""
    args = _parse_args(argv)
    dictionaries_root = args.dictionaries_root
    hse_dictionary_paths = [dictionaries_root / relative for relative in DEFAULT_HSE_DICTIONARIES]
    report = build_report(
        historyspice_path=args.historyspice_json,
        dictionaries_root=dictionaries_root,
        hse_dictionary_paths=hse_dictionary_paths,
    )

    if args.format == "json":
        json.dump(report, sys.stdout, ensure_ascii=False, indent=2)
        sys.stdout.write("\n")
        return 0

    sys.stdout.write(_format_markdown(report))
    return 0


def _walk_historyspice(value: object, *, path: tuple[str, ...], leaves: list[LeafRecord]) -> None:
    if isinstance(value, str):
        leaves.append(LeafRecord(path=_format_path(path), text=value))
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _walk_historyspice(item, path=(*path, f"[{index}]"), leaves=leaves)
        return
    if isinstance(value, dict):
        for key, item in value.items():
            _walk_historyspice(item, path=(*path, str(key)), leaves=leaves)


def _format_path(parts: tuple[str, ...]) -> str:
    formatted = ""
    for part in parts:
        if part.startswith("["):
            formatted += part
            continue
        formatted = part if not formatted else f"{formatted}.{part}"
    return formatted


def _iter_dictionary_entries(payload: object) -> list[dict[str, Any]]:
    if not isinstance(payload, dict):
        return []
    entries = payload.get("entries")
    if not isinstance(entries, list):
        return []
    return [entry for entry in entries if isinstance(entry, dict)]


def _is_covered(leaf: str, dictionary_keys: set[str]) -> bool:
    return leaf in dictionary_keys or (leaf.isascii() and leaf.lower() in dictionary_keys)


def _matches_group(path: str, group: str) -> bool:
    if group.endswith(".*"):
        return path.startswith(group[:-1])
    if group.endswith("*"):
        return path.startswith(group[:-1])
    return path == group


def _percent(numerator: int, denominator: int) -> float:
    if denominator == 0:
        return 0.0
    return round(numerator / denominator * 100, 2)


def _summary_dict(summary: CoverageSummary) -> dict[str, object]:
    return {
        "unique_leaves": summary.unique_leaves,
        "covered": summary.covered,
        "missing": summary.missing,
        "coverage_percent": summary.coverage_percent,
    }


def _format_markdown(report: dict[str, object]) -> str:
    lines = [
        "# HistorySpice Vocabulary Coverage",
        "",
        f"- leaf occurrences: `{report['leaf_occurrences']}`",
        f"- unique leaf strings: `{report['unique_leaf_strings']}`",
        f"- HSE dictionary keys: `{report['hse_dictionary_keys']}`",
        f"- all JSON dictionary keys: `{report['all_dictionary_keys']}`",
        "",
        "## Coverage",
        "",
        "| Dictionary set | Unique leaves | Covered | Missing | Coverage |",
        "| --- | ---: | ---: | ---: | ---: |",
    ]
    hse = report["hse_dictionary_coverage"]
    all_json = report["all_dictionary_coverage"]
    if isinstance(hse, dict):
        lines.append(_coverage_row("HSE dictionaries", hse))
    if isinstance(all_json, dict):
        lines.append(_coverage_row("All JSON dictionaries", all_json))

    lines.extend(
        [
            "",
            "## Focused Groups",
            "",
            "| HistorySpice path group | Unique leaves | Covered | Missing | Coverage |",
            "| --- | ---: | ---: | ---: | ---: |",
        ],
    )
    groups = report["groups"]
    if isinstance(groups, dict):
        for group, summary in groups.items():
            if isinstance(summary, dict):
                lines.append(_coverage_row(f"`{group}`", summary))
    lines.extend(_format_missing_examples(groups))
    return "\n".join(lines) + "\n"


def _coverage_row(label: str, summary: dict[str, object]) -> str:
    return (
        f"| {label} | {summary['unique_leaves']} | {summary['covered']} | "
        f"{summary['missing']} | {summary['coverage_percent']:.2f}% |"
    )


def _format_missing_examples(groups: object) -> list[str]:
    lines = ["", "## Missing Examples", ""]
    if not isinstance(groups, dict):
        return lines

    for group, summary in groups.items():
        if not isinstance(summary, dict):
            continue
        examples = summary.get("missing_examples")
        if not isinstance(examples, list) or not examples:
            continue
        lines.extend([f"### `{group}`", ""])
        lines.extend(_format_missing_example_rows(examples))
        lines.append("")
    return lines


def _format_missing_example_rows(examples: list[object]) -> list[str]:
    return [
        f"- `{example.get('path')}`: `{example.get('text')}`"
        for example in examples
        if isinstance(example, dict)
    ]


def _parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("historyspice_json", type=Path, help="Path to Base/HistorySpice.json")
    parser.add_argument(
        "--dictionaries-root",
        type=Path,
        default=Path("Mods/QudJP/Localization/Dictionaries"),
        help="Path to QudJP Localization/Dictionaries",
    )
    parser.add_argument("--format", choices=("json", "markdown"), default="markdown")
    return parser.parse_args(argv)


if __name__ == "__main__":
    raise SystemExit(main())
