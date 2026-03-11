"""Sync QudJP mod files to the Caves of Qud game directory via rsync."""

import argparse
import subprocess
import sys
from pathlib import Path

# Only game-essential files are deployed.  The game's Unity/Mono compiler will
# attempt to compile any .cs file it finds, so source code must never reach the
# Mods directory — with one exception: Bootstrap.cs is a thin loader shim that
# the game compiles to discover and initialize QudJP.dll.  We use an
# include-first strategy: explicitly allow the needed files, then exclude
# everything else.
_RSYNC_INCLUDES: tuple[str, ...] = (
    "manifest.json",
    "Bootstrap.cs",
    "Assemblies/",
    "Assemblies/QudJP.dll",
    "Localization/",
    "Localization/**",
)

_RSYNC_EXCLUDES: tuple[str, ...] = ("*",)

_GAME_MODS_DIR = (
    Path.home()
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


def _find_project_root() -> Path:
    """Locate the project root by traversing up to find pyproject.toml.

    Returns:
        Path to the project root directory.

    Raises:
        FileNotFoundError: If pyproject.toml cannot be found in any parent.
    """
    current = Path(__file__).resolve().parent
    while current != current.parent:
        if (current / "pyproject.toml").exists():
            return current
        current = current.parent
    msg = "Could not find project root (no pyproject.toml found)"
    raise FileNotFoundError(msg)


def build_rsync_command(
    source: Path,
    destination: Path,
    *,
    dry_run: bool = False,
    exclude_fonts: bool = False,
) -> list[str]:
    """Build the rsync command list for syncing mod files.

    Args:
        source: Source directory to sync from.
        destination: Destination directory to sync to.
        dry_run: If True, add --dry-run flag to rsync.
        exclude_fonts: If True, exclude Fonts/ directory.

    Returns:
        List of command arguments for subprocess.run.
    """
    cmd = ["rsync", "-av", "--delete"]
    if dry_run:
        cmd.append("--dry-run")
    if exclude_fonts:
        cmd.append("--exclude=Fonts/")
    cmd.extend(f"--include={p}" for p in _RSYNC_INCLUDES)
    cmd.extend(f"--exclude={p}" for p in _RSYNC_EXCLUDES)
    cmd.extend([f"{source}/", f"{destination}/"])
    return cmd


def run_sync(
    source: Path,
    destination: Path,
    *,
    dry_run: bool = False,
    exclude_fonts: bool = False,
) -> subprocess.CompletedProcess[str]:
    """Execute rsync to sync mod files to the game directory.

    Args:
        source: Source directory to sync from.
        destination: Destination directory to sync to.
        dry_run: If True, perform a dry run without copying.
        exclude_fonts: If True, exclude Fonts/ directory.

    Returns:
        Completed process result from rsync.

    Raises:
        FileNotFoundError: If source directory does not exist.
        subprocess.CalledProcessError: If rsync fails.
    """
    if not source.is_dir():
        msg = f"Source directory not found: {source}"
        raise FileNotFoundError(msg)

    cmd = build_rsync_command(
        source,
        destination,
        dry_run=dry_run,
        exclude_fonts=exclude_fonts,
    )
    return subprocess.run(cmd, capture_output=True, text=True, check=True)  # noqa: S603 -- trusted rsync call


def main(argv: list[str] | None = None) -> int:
    """Run the mod sync CLI.

    Args:
        argv: Command-line arguments. Defaults to sys.argv[1:].

    Returns:
        Exit code: 0 on success, 1 on failure.
    """
    parser = argparse.ArgumentParser(
        description="Sync QudJP mod files to the Caves of Qud game directory.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would be copied without copying.",
    )
    parser.add_argument(
        "--exclude-fonts",
        action="store_true",
        help="Exclude Fonts/ directory from sync.",
    )
    args = parser.parse_args(argv)

    try:
        project_root = _find_project_root()
    except FileNotFoundError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    source = project_root / "Mods" / "QudJP"
    try:
        result = run_sync(
            source,
            _GAME_MODS_DIR,
            dry_run=args.dry_run,
            exclude_fonts=args.exclude_fonts,
        )
    except FileNotFoundError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1
    except subprocess.CalledProcessError as exc:
        print(f"rsync failed: {exc.stderr}", file=sys.stderr)  # noqa: T201
        return 1

    if result.stdout:
        print(result.stdout.rstrip())  # noqa: T201

    return 0


if __name__ == "__main__":
    sys.exit(main())
