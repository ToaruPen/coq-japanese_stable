"""Sync QudJP mod files to the Caves of Qud game directory."""

import argparse
import os
import platform
import shutil
import subprocess
import sys
from collections.abc import Mapping
from pathlib import Path

# Only game-essential files are deployed.  The game's Unity/Mono compiler will
# attempt to compile any .cs file it finds, so source code must never reach the
# Mods directory — with one exception: Bootstrap.cs is a thin loader shim that
# the game compiles to discover and initialize QudJP.dll.  We use an
# include-first strategy: explicitly allow the needed files, then exclude
# everything else.
_RSYNC_INCLUDES: tuple[str, ...] = (
    "manifest.json",
    "preview.png",
    "Bootstrap.cs",
    "Assemblies/",
    "Assemblies/QudJP.dll",
    "Localization/",
    "Localization/**/",
    "Localization/*.xml",
    "Localization/*.json",
    "Localization/*.txt",
    "Localization/**/*.xml",
    "Localization/**/*.json",
    "Localization/**/*.txt",
    "Fonts/",
    "Fonts/**",
)

_RSYNC_EXCLUDES: tuple[str, ...] = ("*",)
_LOCALIZATION_ASSET_SUFFIXES = {".json", ".txt", ".xml"}
_WINDOWS_DRIVE_PREFIX_LENGTH = 2

_MACOS_MODS_SUFFIX = (
    Path("Library")
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

_WINDOWS_MODS_SUFFIX = (
    Path("AppData")
    / "LocalLow"
    / "Freehold Games"
    / "CavesOfQud"
    / "Mods"
    / "QudJP"
)

_LINUX_MODS_SUFFIX = (
    Path(".config")
    / "unity3d"
    / "Freehold Games"
    / "CavesOfQud"
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


def _is_wsl(release: str | None = None) -> bool:
    """Return whether the current Linux environment is WSL."""
    release_name = (release or platform.uname().release).lower()
    return "microsoft" in release_name or "wsl" in release_name


def _translate_windows_path_for_wsl(raw_path: str) -> Path:
    r"""Translate a Windows path like ``C:\\Users\\name`` to WSL style."""
    normalized = raw_path.strip().replace("\\", "/")
    if len(normalized) >= _WINDOWS_DRIVE_PREFIX_LENGTH and normalized[1] == ":":
        drive = normalized[0].lower()
        remainder = normalized[_WINDOWS_DRIVE_PREFIX_LENGTH :].lstrip("/")
        return Path("/mnt") / drive / remainder
    return Path(normalized)


def _resolve_windows_home(
    env: Mapping[str, str],
    *,
    wsl: bool,
) -> Path | None:
    """Resolve the Windows home directory from environment variables."""
    user_profile = env.get("USERPROFILE")
    if user_profile:
        return (
            _translate_windows_path_for_wsl(user_profile)
            if wsl
            else Path(user_profile)
        )

    home_drive = env.get("HOMEDRIVE")
    home_path = env.get("HOMEPATH")
    if home_drive and home_path:
        combined = f"{home_drive}{home_path}"
        return _translate_windows_path_for_wsl(combined) if wsl else Path(combined)
    return None


def resolve_default_destination(
    *,
    system: str | None = None,
    home: Path | None = None,
    env: Mapping[str, str] | None = None,
    release: str | None = None,
) -> Path:
    """Resolve the default mod destination for the current platform.

    Args:
        system: Optional platform override for tests.
        home: Optional home directory override for tests.
        env: Optional environment override for tests.
        release: Optional kernel release override for WSL detection.

    Returns:
        The default destination path for the detected platform.

    Raises:
        ValueError: If the platform is unsupported or Windows home cannot be
            determined in WSL/native Windows.
    """
    detected_system = system or platform.system()
    current_home = home or Path.home()
    current_env = env or os.environ

    if detected_system == "Darwin":
        return current_home / _MACOS_MODS_SUFFIX

    if detected_system == "Windows":
        windows_home = _resolve_windows_home(current_env, wsl=False)
        if windows_home is None:
            msg = "Could not determine %USERPROFILE%; pass --destination explicitly."
            raise ValueError(msg)
        return windows_home / _WINDOWS_MODS_SUFFIX

    if detected_system == "Linux":
        if _is_wsl(release):
            windows_home = _resolve_windows_home(current_env, wsl=True)
            if windows_home is None:
                msg = (
                    "Could not determine Windows home from WSL environment; "
                    "pass --destination explicitly."
                )
                raise ValueError(msg)
            return windows_home / _WINDOWS_MODS_SUFFIX
        return current_home / _LINUX_MODS_SUFFIX

    msg = f"Unsupported platform: {detected_system}"
    raise ValueError(msg)


def _iter_sync_files(source: Path, *, exclude_fonts: bool) -> list[Path]:
    """Collect files that should be deployed to the game Mods directory."""
    file_paths: list[Path] = []

    for relative in (
        Path("manifest.json"),
        Path("preview.png"),
        Path("Bootstrap.cs"),
        Path("Assemblies") / "QudJP.dll",
    ):
        candidate = source / relative
        if candidate.is_file():
            file_paths.append(candidate)

    localization_dir = source / "Localization"
    if localization_dir.is_dir():
        file_paths.extend(
            file_path
            for file_path in sorted(localization_dir.rglob("*"))
            if file_path.is_file() and file_path.suffix in _LOCALIZATION_ASSET_SUFFIXES
        )

    if not exclude_fonts:
        fonts_dir = source / "Fonts"
        if fonts_dir.is_dir():
            file_paths.extend(
                file_path
                for file_path in sorted(fonts_dir.rglob("*"))
                if file_path.is_file()
            )

    return file_paths


def _run_python_sync(
    source: Path,
    destination: Path,
    *,
    dry_run: bool,
    exclude_fonts: bool,
) -> subprocess.CompletedProcess[str]:
    """Synchronize files with a pure-Python copy fallback."""
    file_paths = _iter_sync_files(source, exclude_fonts=exclude_fonts)
    lines = ["Using Python copy fallback."]

    if dry_run:
        action = "replace" if destination.exists() else "create"
        lines.append(f"Would {action} {destination}")
        lines.extend(
            f"Would copy {file_path.relative_to(source)}"
            for file_path in file_paths
        )
        return subprocess.CompletedProcess(
            args=["python-copy"],
            returncode=0,
            stdout="\n".join(lines),
            stderr="",
        )

    if destination.exists():
        if destination.is_dir():
            shutil.rmtree(destination)
        else:
            destination.unlink()

    for file_path in file_paths:
        relative_path = file_path.relative_to(source)
        target_path = destination / relative_path
        target_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file_path, target_path)

    lines.append(f"Copied {len(file_paths)} files to {destination}")
    return subprocess.CompletedProcess(
        args=["python-copy"],
        returncode=0,
        stdout="\n".join(lines),
        stderr="",
    )


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
    """Execute sync to copy mod files into the game directory.

    Args:
        source: Source directory to sync from.
        destination: Destination directory to sync to.
        dry_run: If True, perform a dry run without copying.
        exclude_fonts: If True, exclude Fonts/ directory.

    Returns:
        Completed process result from rsync or the Python fallback.

    Raises:
        FileNotFoundError: If source directory does not exist.
        subprocess.CalledProcessError: If rsync fails.
    """
    if not source.is_dir():
        msg = f"Source directory not found: {source}"
        raise FileNotFoundError(msg)

    if shutil.which("rsync") is None:
        return _run_python_sync(
            source,
            destination,
            dry_run=dry_run,
            exclude_fonts=exclude_fonts,
        )

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
    parser.add_argument(
        "--destination",
        "--dest",
        type=Path,
        default=None,
        help=(
            "Override the destination Mods/QudJP directory. If omitted, the "
            "platform default path is used."
        ),
    )
    args = parser.parse_args(argv)

    try:
        project_root = _find_project_root()
    except FileNotFoundError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    try:
        destination = args.destination or resolve_default_destination()
    except ValueError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    source = project_root / "Mods" / "QudJP"
    try:
        result = run_sync(
            source,
            destination,
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
