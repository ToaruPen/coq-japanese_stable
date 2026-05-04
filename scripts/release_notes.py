"""Validate and render QudJP release-note fragments."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Self

FRAGMENTS_DIR = Path("docs/release-notes/unreleased")
LOCALIZATION_PREFIX = "Mods/QudJP/Localization/"
SECTION_ORDER = ("Added", "Changed", "Fixed", "Removed", "Deprecated", "Security")


class ReleaseNoteError(ValueError):
    """Raised when release-note fragments are missing or malformed."""


@dataclass(frozen=True)
class ReleaseNoteFragments:
    """Grouped release-note bullets."""

    sections: dict[str, list[str]] = field(default_factory=dict)

    def has_entries(self) -> bool:
        """Return whether any release-note bullet was collected."""
        return any(self.sections.values())


def _fragment_prefix(fragments_dir: Path) -> str:
    """Return the changed-file prefix for a fragment directory."""
    return f"{fragments_dir.as_posix().rstrip('/')}/"


def _is_fragment_path(path: str, *, fragment_prefix: str) -> bool:
    """Return whether a changed file path is an unreleased fragment."""
    return path.startswith(fragment_prefix) and path.endswith(".md") and not path.endswith("/README.md")


def _is_localization_path(path: str) -> bool:
    """Return whether a changed file path is a localization asset."""
    return path.startswith(LOCALIZATION_PREFIX)


def check_fragment_requirement(changed_files: list[str], *, fragments_dir: Path = FRAGMENTS_DIR) -> None:
    """Require an unreleased fragment when localization assets change."""
    has_localization_change = any(_is_localization_path(path) for path in changed_files)
    fragment_prefix = _fragment_prefix(fragments_dir)
    changed_fragment_paths = [
        Path(path) for path in changed_files if _is_fragment_path(path, fragment_prefix=fragment_prefix)
    ]
    if not has_localization_change:
        return
    if not changed_fragment_paths:
        msg = (
            f"Localization changes require a release-note fragment under {fragments_dir}/*.md."
        )
        raise ReleaseNoteError(msg)
    _validate_changed_fragments(changed_fragment_paths)
    fragments = collect_fragments(fragments_dir)
    if not fragments.has_entries():
        msg = f"No release-note fragments found under {fragments_dir}"
        raise ReleaseNoteError(msg)


def collect_fragments(fragments_dir: Path = FRAGMENTS_DIR) -> ReleaseNoteFragments:
    """Collect unreleased markdown fragments grouped by changelog section."""
    sections: dict[str, list[str]] = {section: [] for section in SECTION_ORDER}
    if not fragments_dir.exists():
        return ReleaseNoteFragments(sections={})

    for path in sorted(fragments_dir.glob("*.md")):
        if path.name == "README.md":
            continue
        _collect_fragment(path, sections)

    return ReleaseNoteFragments(sections={section: entries for section, entries in sections.items() if entries})


def _collect_fragment(path: Path, sections: dict[str, list[str]]) -> int:
    entry_count = 0
    current_section: str | None = None
    for line_number, raw_line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        line = raw_line.strip()
        if not line:
            continue
        if line.startswith("### "):
            section = line.removeprefix("### ").strip()
            if section not in SECTION_ORDER:
                msg = f"{path}:{line_number}: unsupported release-note section: {section}"
                raise ReleaseNoteError(msg)
            current_section = section
            continue
        if line.startswith("- "):
            if current_section is None:
                msg = f"{path}:{line_number}: bullet appears before a section heading"
                raise ReleaseNoteError(msg)
            bullet = line.removeprefix("- ").strip()
            if not bullet:
                msg = f"{path}:{line_number}: empty release-note bullet"
                raise ReleaseNoteError(msg)
            sections[current_section].append(bullet)
            entry_count += 1
            continue
        msg = f"{path}:{line_number}: expected a '### Section' heading or '- ' bullet"
        raise ReleaseNoteError(msg)
    return entry_count


def _validate_changed_fragments(paths: list[Path]) -> None:
    """Validate the fragment files that were part of the change set."""
    for path in paths:
        if not path.is_file():
            msg = f"Changed release-note fragment not found: {path}"
            raise ReleaseNoteError(msg)
        sections: dict[str, list[str]] = {section: [] for section in SECTION_ORDER}
        if _collect_fragment(path, sections) == 0:
            msg = f"Changed release-note fragment has no entries: {path}"
            raise ReleaseNoteError(msg)


def render_changelog_entry(
    *,
    version: str,
    release_date: str,
    fragments: ReleaseNoteFragments,
) -> str:
    """Render a Keep a Changelog entry from collected fragments."""
    lines = [f"## [{version}] - {release_date}", ""]
    lines.extend(_render_section_lines(fragments))
    return "\n".join(lines)


def render_workshop_changenote(
    *,
    version: str,
    git_hash: str,
    fragments: ReleaseNoteFragments,
) -> str:
    """Render a Steam Workshop changenote from collected fragments."""
    lines = [f"v{version} / {git_hash}", "", "更新内容:"]
    for section in SECTION_ORDER:
        lines.extend(f"- {bullet}" for bullet in fragments.sections.get(section, []))
    lines.append("")
    return "\n".join(lines)


def _render_section_lines(fragments: ReleaseNoteFragments) -> list[str]:
    lines: list[str] = []
    for section in SECTION_ORDER:
        entries = fragments.sections.get(section, [])
        if not entries:
            continue
        lines.extend([f"### {section}", ""])
        lines.extend(f"- {entry}" for entry in entries)
        lines.append("")
    return lines


def git_changed_files(base_ref: str, head_ref: str) -> list[str]:
    """Return changed file paths between two git refs."""
    git = shutil.which("git")
    if git is None:
        msg = "git executable not found"
        raise ReleaseNoteError(msg)
    try:
        result = subprocess.run(  # noqa: S603
            [git, "diff", "--name-only", f"{base_ref}...{head_ref}"],
            check=True,
            capture_output=True,
            text=True,
        )
    except subprocess.CalledProcessError as exc:
        detail = (exc.stderr or "").strip() or str(exc)
        msg = f"git diff failed for {base_ref}...{head_ref}: {detail}"
        raise ReleaseNoteError(msg) from exc
    return [line for line in result.stdout.splitlines() if line]


def _write_output(path: Path, text: str) -> None:
    """Write rendered release-note output to a UTF-8 file."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _require_fragments(fragments_dir: Path) -> ReleaseNoteFragments:
    """Collect fragments and fail if no release-note entries exist."""
    fragments = collect_fragments(fragments_dir)
    if not fragments.has_entries():
        msg = f"No release-note fragments found under {fragments_dir}"
        raise ReleaseNoteError(msg)
    return fragments


class _Parser(argparse.ArgumentParser):
    def error(self: Self, message: str) -> None:
        """Print argparse errors without a traceback."""
        raise ReleaseNoteError(message)


def build_parser() -> argparse.ArgumentParser:
    """Build the release-notes CLI parser."""
    parser = _Parser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    check_parser = subparsers.add_parser(
        "check-fragment",
        help="Require an unreleased release-note fragment for localization changes.",
    )
    check_parser.add_argument("--base-ref", default="origin/main")
    check_parser.add_argument("--head-ref", default="HEAD")
    check_parser.add_argument("--fragments-dir", type=Path, default=FRAGMENTS_DIR)
    check_parser.add_argument(
        "--changed-file",
        action="append",
        default=[],
        help="Changed file path. If provided, git diff is not invoked.",
    )

    render_parser = subparsers.add_parser(
        "render",
        help="Render changelog and Workshop changenote drafts from unreleased fragments.",
    )
    render_parser.add_argument("--version", required=True)
    render_parser.add_argument("--git-hash", required=True)
    render_parser.add_argument("--date", required=True)
    render_parser.add_argument("--fragments-dir", type=Path, default=FRAGMENTS_DIR)
    render_parser.add_argument("--changelog-output", type=Path)
    render_parser.add_argument("--workshop-output", type=Path)

    return parser


def main(argv: list[str] | None = None) -> int:
    """Run the release-notes CLI."""
    parser = build_parser()
    try:
        args = parser.parse_args(argv)
        if args.command == "check-fragment":
            changed_files = args.changed_file or git_changed_files(args.base_ref, args.head_ref)
            check_fragment_requirement(changed_files, fragments_dir=args.fragments_dir)
            return 0
        if args.command == "render":
            fragments = _require_fragments(args.fragments_dir)
            changelog = render_changelog_entry(version=args.version, release_date=args.date, fragments=fragments)
            workshop = render_workshop_changenote(version=args.version, git_hash=args.git_hash, fragments=fragments)
            if args.changelog_output is None and args.workshop_output is None:
                print(changelog)  # noqa: T201
                print("---")  # noqa: T201
                print(workshop)  # noqa: T201
                return 0
            if args.changelog_output is not None:
                _write_output(args.changelog_output, changelog)
            if args.workshop_output is not None:
                _write_output(args.workshop_output, workshop)
            return 0
    except ReleaseNoteError as exc:
        print(f"error: {exc}", file=sys.stderr)  # noqa: T201
        return 1
    raise AssertionError(args.command)


if __name__ == "__main__":
    raise SystemExit(main())
