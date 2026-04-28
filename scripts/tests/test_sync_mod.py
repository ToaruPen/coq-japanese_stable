"""Tests for the sync_mod module."""

import subprocess
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from scripts.sync_mod import (
    _RSYNC_EXCLUDES,
    _RSYNC_INCLUDES,
    build_rsync_command,
    main,
    resolve_default_destination,
    run_sync,
)

LOCALIZATION_DOC_NAMES = ("AGENTS.md", "CLAUDE.md", "README.md")


class TestBuildRsyncCommand:
    """Tests for build_rsync_command."""

    def test_basic_command_structure(self) -> None:
        """Basic command includes rsync, -av, --delete, and trailing-slash paths."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert cmd[0] == "rsync"
        assert "-av" in cmd
        assert "--delete" in cmd
        assert "/src/" in cmd
        assert "/dst/" in cmd

    def test_dry_run_flag(self) -> None:
        """Dry-run adds --dry-run to the command."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"), dry_run=True)
        assert "--dry-run" in cmd

    def test_no_dry_run_by_default(self) -> None:
        """Dry-run flag is absent by default."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert "--dry-run" not in cmd

    def test_exclude_fonts_flag(self) -> None:
        """Exclude-fonts adds --exclude=Fonts/ to the command."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"), exclude_fonts=True)
        assert "--exclude=Fonts/" in cmd

    def test_no_exclude_fonts_by_default(self) -> None:
        """Exclude-fonts flag is absent by default."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert "--exclude=Fonts/" not in cmd

    def test_both_flags_combined(self) -> None:
        """Both flags can be used together."""
        cmd = build_rsync_command(
            Path("/src"),
            Path("/dst"),
            dry_run=True,
            exclude_fonts=True,
        )
        assert "--dry-run" in cmd
        assert "--exclude=Fonts/" in cmd

    def test_include_patterns_present(self) -> None:
        """All _RSYNC_INCLUDES patterns appear as --include= args."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        for pattern in _RSYNC_INCLUDES:
            assert f"--include={pattern}" in cmd

    def test_exclude_patterns_present(self) -> None:
        """All _RSYNC_EXCLUDES patterns appear as --exclude= args."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        for pattern in _RSYNC_EXCLUDES:
            assert f"--exclude={pattern}" in cmd

    def test_includes_before_excludes(self) -> None:
        """--include= args must appear before --exclude= args for rsync to work."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        first_include = next(i for i, a in enumerate(cmd) if a.startswith("--include="))
        first_exclude = next(i for i, a in enumerate(cmd) if a.startswith("--exclude="))
        assert first_include < first_exclude

    def test_wildcard_exclude_present(self) -> None:
        """--exclude=* is present to block all non-included files."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert "--exclude=*" in cmd

    def test_essential_files_included(self) -> None:
        """Core files and explicit localization asset patterns are included."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert "--include=manifest.json" in cmd
        assert "--include=preview.png" in cmd
        assert "--include=Assemblies/QudJP.dll" in cmd
        assert "--include=Localization/" in cmd
        assert "--include=Localization/**/" in cmd
        assert "--include=Localization/*.xml" in cmd
        assert "--include=Localization/*.json" in cmd
        assert "--include=Localization/*.txt" in cmd
        assert "--include=Localization/**/*.xml" in cmd
        assert "--include=Localization/**/*.json" in cmd
        assert "--include=Localization/**/*.txt" in cmd
        assert "--include=Localization/**" not in cmd

    def test_exclude_fonts_before_wildcard_exclude(self) -> None:
        """--exclude=Fonts/ appears before --exclude=* when exclude_fonts is set."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"), exclude_fonts=True)
        fonts_idx = cmd.index("--exclude=Fonts/")
        wildcard_idx = cmd.index("--exclude=*")
        assert fonts_idx < wildcard_idx

    def test_rsync_includes_contains_bootstrap_cs(self) -> None:
        """Bootstrap.cs is in the include list for game-compiled loader deployment."""
        assert "Bootstrap.cs" in _RSYNC_INCLUDES

    def test_build_rsync_command_includes_bootstrap(self) -> None:
        """--include=Bootstrap.cs appears in the built rsync command."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert "--include=Bootstrap.cs" in cmd

    def test_rsync_includes_contains_fonts(self) -> None:
        """Fonts/ and Fonts/** are in the include list for font deployment."""
        assert "Fonts/" in _RSYNC_INCLUDES
        assert "Fonts/**" in _RSYNC_INCLUDES

    def test_build_rsync_command_includes_fonts(self) -> None:
        """--include=Fonts/ and --include=Fonts/** appear in the built command."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert "--include=Fonts/" in cmd
        assert "--include=Fonts/**" in cmd


class TestRunSync:
    """Tests for run_sync."""

    def test_nonexistent_source_raises(self) -> None:
        """Running sync with nonexistent source raises FileNotFoundError."""
        with pytest.raises(FileNotFoundError, match="Source directory not found"):
            run_sync(Path("/nonexistent/src/abc123"), Path("/dst"))

    def test_calls_subprocess_run(self, tmp_path: Path) -> None:
        """run_sync delegates to subprocess.run with the built command."""
        source = tmp_path / "source"
        source.mkdir()
        mock_result = MagicMock(stdout="", stderr="", returncode=0)
        with (
            patch("scripts.sync_mod.shutil.which", return_value="/usr/bin/rsync"),
            patch(
                "scripts.sync_mod.subprocess.run",
                return_value=mock_result,
            ) as mock_run,
        ):
            run_sync(source, tmp_path / "dest")
            mock_run.assert_called_once()
            cmd = mock_run.call_args[0][0]
            assert cmd[0] == "rsync"

    def test_dry_run_passed_to_subprocess(self, tmp_path: Path) -> None:
        """Dry-run flag is forwarded to the subprocess command."""
        source = tmp_path / "source"
        source.mkdir()
        mock_result = MagicMock(stdout="", stderr="", returncode=0)
        with (
            patch("scripts.sync_mod.shutil.which", return_value="/usr/bin/rsync"),
            patch(
                "scripts.sync_mod.subprocess.run",
                return_value=mock_result,
            ) as mock_run,
        ):
            run_sync(source, tmp_path / "dest", dry_run=True)
            cmd = mock_run.call_args[0][0]
            assert "--dry-run" in cmd

    def test_python_fallback_copies_expected_files(self, tmp_path: Path) -> None:
        """Fallback mode copies only deployable files when rsync is unavailable."""
        source = tmp_path / "source"
        destination = tmp_path / "dest"
        (source / "Assemblies").mkdir(parents=True)
        (source / "Localization").mkdir()
        (source / "Fonts").mkdir()
        (source / "manifest.json").write_text("{}", encoding="utf-8")
        (source / "preview.png").write_bytes(b"preview")
        (source / "Bootstrap.cs").write_text("// bootstrap", encoding="utf-8")
        (source / "Assemblies" / "QudJP.dll").write_bytes(b"dll")
        (source / "Localization" / "Creatures.jp.xml").write_text(
            "<objects/>",
            encoding="utf-8",
        )
        (source / "Localization" / "ui.ja.json").write_text(
            "{}",
            encoding="utf-8",
        )
        (source / "Localization" / "Text.jp.txt").write_text(
            "main text",
            encoding="utf-8",
        )
        corpus = source / "Localization" / "Corpus"
        corpus.mkdir()
        (corpus / "Library-excerpt.jp.txt").write_text(
            "corpus text",
            encoding="utf-8",
        )
        for doc_name in LOCALIZATION_DOC_NAMES:
            (source / "Localization" / doc_name).write_text("# docs", encoding="utf-8")
        (source / "Fonts" / "Font.otf").write_bytes(b"font")
        (source / "src.cs").write_text("// do not copy", encoding="utf-8")
        destination.mkdir()
        (destination / "stale.txt").write_text("stale", encoding="utf-8")

        with patch("scripts.sync_mod.shutil.which", return_value=None):
            result = run_sync(source, destination)

        assert result.returncode == 0
        assert (destination / "manifest.json").exists()
        assert (destination / "preview.png").exists()
        assert (destination / "Bootstrap.cs").exists()
        assert (destination / "Assemblies" / "QudJP.dll").exists()
        assert (destination / "Localization" / "Creatures.jp.xml").exists()
        assert (destination / "Localization" / "ui.ja.json").exists()
        assert (destination / "Localization" / "Text.jp.txt").exists()
        assert (
            destination / "Localization" / "Corpus" / "Library-excerpt.jp.txt"
        ).exists()
        for doc_name in LOCALIZATION_DOC_NAMES:
            assert not (destination / "Localization" / doc_name).exists()
        assert (destination / "Fonts" / "Font.otf").exists()
        assert not (destination / "src.cs").exists()
        assert not (destination / "stale.txt").exists()

    def test_python_fallback_respects_exclude_fonts(self, tmp_path: Path) -> None:
        """Fallback mode skips Fonts/ when exclude_fonts is requested."""
        source = tmp_path / "source"
        destination = tmp_path / "dest"
        (source / "Fonts").mkdir(parents=True)
        (source / "Fonts" / "Font.otf").write_bytes(b"font")

        with patch("scripts.sync_mod.shutil.which", return_value=None):
            run_sync(source, destination, exclude_fonts=True)

        assert not (destination / "Fonts").exists()

    def test_python_fallback_dry_run_does_not_modify_destination(
        self,
        tmp_path: Path,
    ) -> None:
        """Dry-run fallback reports planned work without writing files."""
        source = tmp_path / "source"
        destination = tmp_path / "dest"
        source.mkdir()

        with patch("scripts.sync_mod.shutil.which", return_value=None):
            result = run_sync(source, destination, dry_run=True)

        assert result.returncode == 0
        assert "Would create" in result.stdout
        assert not destination.exists()

    def test_python_fallback_dry_run_reports_replace_for_existing_destination(
        self,
        tmp_path: Path,
    ) -> None:
        """Dry-run fallback reports replacement when destination already exists."""
        source = tmp_path / "source"
        destination = tmp_path / "dest"
        source.mkdir()
        destination.mkdir()

        with patch("scripts.sync_mod.shutil.which", return_value=None):
            result = run_sync(source, destination, dry_run=True)

        assert result.returncode == 0
        assert "Would replace" in result.stdout


class TestResolveDefaultDestination:
    """Tests for platform-specific default destination resolution."""

    def test_macos_uses_streaming_assets_mods(self, tmp_path: Path) -> None:
        """MacOS defaults to the Steam app bundle Mods directory."""
        destination = resolve_default_destination(system="Darwin", home=tmp_path)
        assert destination == (
            tmp_path
            / "Library"
            / "Application Support"
            / "Steam"
            / "steamapps"
            / "common"
            / "Caves of Qud"
            / "CoQ.app"
            / "Contents"
            / "Resources"
            / "Data"
            / "StreamingAssets"
            / "Mods"
            / "QudJP"
        )

    def test_windows_uses_locallow_mods(self) -> None:
        """Windows defaults to the LocalLow mods directory."""
        destination = resolve_default_destination(
            system="Windows",
            env={"USERPROFILE": r"C:\Users\TestUser"},
        )
        assert str(destination).replace("\\", "/").endswith(
            "C:/Users/TestUser/AppData/LocalLow/Freehold Games/CavesOfQud/Mods/QudJP",
        )

    def test_wsl_uses_translated_windows_profile(self) -> None:
        """WSL defaults to the Windows LocalLow mods directory."""
        destination = resolve_default_destination(
            system="Linux",
            env={"USERPROFILE": r"C:\Users\TestUser"},
            release="5.15.167.4-microsoft-standard-WSL2",
        )
        assert destination == (
            Path("/mnt/c/Users/TestUser")
            / "AppData"
            / "LocalLow"
            / "Freehold Games"
            / "CavesOfQud"
            / "Mods"
            / "QudJP"
        )

    def test_linux_uses_unity3d_mods(self, tmp_path: Path) -> None:
        """Native Linux defaults to the Unity user-data Mods directory."""
        destination = resolve_default_destination(
            system="Linux",
            home=tmp_path,
            release="6.8.0-generic",
        )
        assert destination == (
            tmp_path
            / ".config"
            / "unity3d"
            / "Freehold Games"
            / "CavesOfQud"
            / "Mods"
            / "QudJP"
        )


class TestMain:
    """Tests for the sync_mod CLI entry point."""

    def test_destination_override_is_forwarded(self, tmp_path: Path) -> None:
        """--dest forwards the chosen path to run_sync."""
        project_root = tmp_path / "repo"
        source = project_root / "Mods" / "QudJP"
        source.mkdir(parents=True)
        destination = tmp_path / "custom-dest"

        with (
            patch("scripts.sync_mod._find_project_root", return_value=project_root),
            patch(
                "scripts.sync_mod.run_sync",
                return_value=subprocess.CompletedProcess(
                    args=["sync"],
                    returncode=0,
                    stdout="",
                    stderr="",
                ),
            ) as mock_run,
        ):
            result = main(["--dest", str(destination)])

        assert result == 0
        assert mock_run.call_args.args[0] == source
        assert mock_run.call_args.args[1] == destination
