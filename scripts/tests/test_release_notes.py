"""Tests for release-note fragment validation and rendering."""

from __future__ import annotations

import subprocess
from pathlib import Path
from typing import TYPE_CHECKING

import pytest

from scripts import release_notes
from scripts.release_notes import (
    ReleaseNoteError,
    check_fragment_requirement,
    collect_fragments,
    git_changed_files,
    main,
    render_changelog_entry,
    render_workshop_changenote,
)

if TYPE_CHECKING:
    from collections.abc import Sequence


def test_collect_fragments_groups_keep_a_changelog_sections(tmp_path: Path) -> None:
    """Unreleased fragments are grouped by Keep a Changelog section headings."""
    fragments_dir = tmp_path / "docs" / "release-notes" / "unreleased"
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "ui-update.md").write_text(
        "### Added\n\n"
        "- Add Japanese labels to the game summary screen.\n\n"
        "### Fixed\n\n"
        "- Fix untranslated popup button text.\n",
        encoding="utf-8",
    )

    fragments = collect_fragments(fragments_dir)

    assert fragments.sections == {
        "Added": ["Add Japanese labels to the game summary screen."],
        "Fixed": ["Fix untranslated popup button text."],
    }


def test_collect_fragments_rejects_bullets_before_heading(tmp_path: Path) -> None:
    """Fragments must declare an explicit release-note section."""
    fragments_dir = tmp_path / "docs" / "release-notes" / "unreleased"
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "bad.md").write_text("- Missing section heading.\n", encoding="utf-8")

    with pytest.raises(ReleaseNoteError, match="section heading"):
        collect_fragments(fragments_dir)


def test_render_changelog_entry_uses_version_date_and_sections(tmp_path: Path) -> None:
    """Generated changelog entries are suitable for CHANGELOG.md insertion."""
    fragments_dir = tmp_path / "docs" / "release-notes" / "unreleased"
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "translations.md").write_text(
        "### Changed\n\n- Improve conversation and trade UI translations.\n",
        encoding="utf-8",
    )

    entry = render_changelog_entry(
        version="0.1.0",
        release_date="2026-05-04",
        fragments=collect_fragments(fragments_dir),
    )

    assert (
        entry
        == "## [0.1.0] - 2026-05-04\n\n"
        "### Changed\n\n"
        "- Improve conversation and trade UI translations.\n"
    )


def test_render_workshop_changenote_is_user_facing(tmp_path: Path) -> None:
    """Workshop changenotes include version, git hash, and user-visible bullets."""
    fragments_dir = tmp_path / "docs" / "release-notes" / "unreleased"
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "runtime.md").write_text(
        "### Fixed\n\n- Fix untranslated sleep and game-summary messages.\n",
        encoding="utf-8",
    )

    changenote = render_workshop_changenote(
        version="0.1.0",
        git_hash="abc1234def56",
        fragments=collect_fragments(fragments_dir),
    )

    assert (
        changenote
        == "v0.1.0 / abc1234def56\n\n"
        "更新内容:\n"
        "- Fix untranslated sleep and game-summary messages.\n"
    )


def test_check_fragment_requirement_requires_fragment_for_localization_change() -> None:
    """Localization updates must carry a release-note fragment."""
    changed_files = [
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
    ]

    with pytest.raises(ReleaseNoteError, match="release-note fragment"):
        check_fragment_requirement(changed_files)


def test_check_fragment_requirement_reports_configured_fragment_dir() -> None:
    """Missing-fragment errors point at the configured fragment directory."""
    changed_files = ["Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json"]

    with pytest.raises(ReleaseNoteError, match=r"custom-release-notes/\*.md"):
        check_fragment_requirement(changed_files, fragments_dir=Path("custom-release-notes"))


def test_check_fragment_requirement_passes_when_fragment_changes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Localization updates are accepted when an unreleased fragment is present."""
    monkeypatch.chdir(tmp_path)
    fragments_dir = Path("custom-release-notes")
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "ui-popup.md").write_text("### Fixed\n\n- Fix popup translation coverage.\n", encoding="utf-8")
    changed_files = [
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        "custom-release-notes/ui-popup.md",
    ]

    check_fragment_requirement(changed_files, fragments_dir=fragments_dir)


def test_check_fragment_requirement_rejects_malformed_fragment(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Localization PRs must include a parseable release-note fragment."""
    monkeypatch.chdir(tmp_path)
    fragments_dir = Path("custom-release-notes")
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "ui-popup.md").write_text("### Unknown\n\n- Fix popup translation coverage.\n", encoding="utf-8")
    changed_files = [
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        "custom-release-notes/ui-popup.md",
    ]

    with pytest.raises(ReleaseNoteError, match="unsupported release-note section"):
        check_fragment_requirement(changed_files, fragments_dir=fragments_dir)


def test_check_fragment_requirement_rejects_missing_changed_fragment(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The changed fragment path itself must still exist."""
    monkeypatch.chdir(tmp_path)
    fragments_dir = Path("custom-release-notes")
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "other.md").write_text("### Fixed\n\n- Fix another translation.\n", encoding="utf-8")
    changed_files = [
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        "custom-release-notes/ui-popup.md",
    ]

    with pytest.raises(ReleaseNoteError, match="Changed release-note fragment not found"):
        check_fragment_requirement(changed_files, fragments_dir=fragments_dir)


def test_check_fragment_requirement_rejects_empty_changed_fragment(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The changed fragment path must contain at least one release-note bullet."""
    monkeypatch.chdir(tmp_path)
    fragments_dir = Path("custom-release-notes")
    fragments_dir.mkdir(parents=True)
    (fragments_dir / "ui-popup.md").write_text("", encoding="utf-8")
    (fragments_dir / "other.md").write_text("### Fixed\n\n- Fix another translation.\n", encoding="utf-8")
    changed_files = [
        "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
        "custom-release-notes/ui-popup.md",
    ]

    with pytest.raises(ReleaseNoteError, match="Changed release-note fragment has no entries"):
        check_fragment_requirement(changed_files, fragments_dir=fragments_dir)


def test_check_fragment_requirement_ignores_non_localization_changes() -> None:
    """Non-localization PRs do not need release-note fragments."""
    check_fragment_requirement(["scripts/release_notes.py"])


def test_git_changed_files_wraps_git_diff_errors(monkeypatch: pytest.MonkeyPatch) -> None:
    """Git diff failures are reported as release-note CLI errors."""

    def fake_run(
        _args: Sequence[str],
        **_kwargs: object,
    ) -> subprocess.CompletedProcess[str]:
        raise subprocess.CalledProcessError(
            returncode=128,
            cmd="git diff --name-only bad...HEAD",
            stderr="fatal: bad revision 'bad...HEAD'",
        )

    monkeypatch.setattr(release_notes.shutil, "which", lambda _name: "git")
    monkeypatch.setattr(release_notes.subprocess, "run", fake_run)

    with pytest.raises(ReleaseNoteError, match="git diff failed for bad\\.\\.\\.HEAD"):
        git_changed_files("bad", "HEAD")


def test_main_reports_parse_errors_without_traceback(capsys: pytest.CaptureFixture[str]) -> None:
    """CLI argument errors are normalized into the release-note error format."""
    assert main(["render"]) == 1

    captured = capsys.readouterr()
    assert captured.out == ""
    assert "error: the following arguments are required" in captured.err
    assert "Traceback" not in captured.err
