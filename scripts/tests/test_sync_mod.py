"""Tests for the sync_mod module."""

from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from scripts.sync_mod import (
    _RSYNC_EXCLUDES,
    _RSYNC_INCLUDES,
    build_rsync_command,
    run_sync,
)


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
        """manifest.json, Assemblies/QudJP.dll, and Localization/** are included."""
        cmd = build_rsync_command(Path("/src"), Path("/dst"))
        assert "--include=manifest.json" in cmd
        assert "--include=Assemblies/QudJP.dll" in cmd
        assert "--include=Localization/**" in cmd

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
        with patch(
            "scripts.sync_mod.subprocess.run",
            return_value=mock_result,
        ) as mock_run:
            run_sync(source, tmp_path / "dest")
            mock_run.assert_called_once()
            cmd = mock_run.call_args[0][0]
            assert cmd[0] == "rsync"

    def test_dry_run_passed_to_subprocess(self, tmp_path: Path) -> None:
        """Dry-run flag is forwarded to the subprocess command."""
        source = tmp_path / "source"
        source.mkdir()
        mock_result = MagicMock(stdout="", stderr="", returncode=0)
        with patch(
            "scripts.sync_mod.subprocess.run",
            return_value=mock_result,
        ) as mock_run:
            run_sync(source, tmp_path / "dest", dry_run=True)
            cmd = mock_run.call_args[0][0]
            assert "--dry-run" in cmd
