"""Sync QudJP mod files to the Caves of Qud game directory."""

import argparse
import json
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
_MACOS_HELPER_COMMAND_NAMES: tuple[str, ...] = (
    "Launch CavesOfQud (Rosetta).command",
    "Install Native Apple Silicon Harmony.command",
    "Restore Game Harmony.command",
)

_RSYNC_INCLUDES: tuple[str, ...] = (
    "manifest.json",
    "preview.png",
    "Bootstrap.cs",
    *_MACOS_HELPER_COMMAND_NAMES,
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
    "QudTest/",
    "QudTest/fixtures/",
    "QudTest/fixtures/*.json",
    "Fonts/",
    "Fonts/**",
)

_RSYNC_EXCLUDES: tuple[str, ...] = ("*",)
_MACOS_HELPER_COMMANDS: tuple[Path, ...] = tuple(
    Path(helper_name) for helper_name in _MACOS_HELPER_COMMAND_NAMES
)
_LOCAL_ONLY_FILES: tuple[Path, ...] = (Path("workshop.json"),)
_LOCALIZATION_ASSET_SUFFIXES = {".json", ".txt", ".xml"}
_WINDOWS_DRIVE_PREFIX_LENGTH = 2
DEFAULT_DEV_VERSION_SUFFIX = ""
DEFAULT_DEV_TITLE_SUFFIX = " (local dev)"

_MACOS_STABLE_REF_MODS_SUFFIX = (
    Path("Games")
    / "CavesOfQud-stable-ref"
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
        return current_home / _MACOS_STABLE_REF_MODS_SUFFIX

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
        *_MACOS_HELPER_COMMANDS,
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

    qudtest_fixtures_dir = source / "QudTest" / "fixtures"
    if qudtest_fixtures_dir.is_dir():
        file_paths.extend(
            file_path
            for file_path in sorted(qudtest_fixtures_dir.glob("*.json"))
            if file_path.is_file()
        )

    return file_paths


def _rewrite_manifest_metadata(
    manifest_data: dict[str, object],
    *,
    version_suffix: str | None,
    title_suffix: str | None,
) -> dict[str, object]:
    """Return manifest data with local-only display metadata applied."""
    rewritten = dict(manifest_data)

    if version_suffix:
        version = rewritten.get("Version")
        if not isinstance(version, str) or not version:
            msg = "manifest.json must contain a non-empty string Version"
            raise ValueError(msg)
        rewritten["Version"] = (
            version if version.endswith(version_suffix) else f"{version}{version_suffix}"
        )

    if title_suffix:
        title = rewritten.get("Title")
        if not isinstance(title, str) or not title:
            msg = "manifest.json must contain a non-empty string Title"
            raise ValueError(msg)
        rewritten["Title"] = (
            title if title.endswith(title_suffix) else f"{title}{title_suffix}"
        )

    return rewritten


def _write_manifest_metadata(
    source_manifest: Path,
    destination_manifest: Path,
    *,
    version_suffix: str | None,
    title_suffix: str | None,
) -> None:
    """Write destination manifest metadata without mutating the source manifest."""
    manifest_data = json.loads(source_manifest.read_text(encoding="utf-8"))
    if not isinstance(manifest_data, dict):
        msg = "manifest.json must contain a JSON object"
        raise TypeError(msg)

    rewritten = _rewrite_manifest_metadata(
        manifest_data,
        version_suffix=version_suffix,
        title_suffix=title_suffix,
    )
    destination_manifest.parent.mkdir(parents=True, exist_ok=True)
    destination_manifest.write_text(
        f"{json.dumps(rewritten, ensure_ascii=False, indent=2)}\n",
        encoding="utf-8",
    )


def _append_stdout_line(
    result: subprocess.CompletedProcess[str],
    line: str,
) -> subprocess.CompletedProcess[str]:
    """Return a completed process result with one extra stdout line."""
    stdout = f"{result.stdout.rstrip()}\n{line}\n" if result.stdout else f"{line}\n"
    return subprocess.CompletedProcess(
        args=result.args,
        returncode=result.returncode,
        stdout=stdout,
        stderr=result.stderr,
    )


def _run_python_sync(
    source: Path,
    destination: Path,
    *,
    dry_run: bool,
    exclude_fonts: bool,
    manifest_version_suffix: str | None,
    manifest_title_suffix: str | None,
) -> subprocess.CompletedProcess[str]:
    """Synchronize files with a pure-Python copy fallback."""
    file_paths = _iter_sync_files(source, exclude_fonts=exclude_fonts)
    lines = ["Using Python copy fallback."]
    rewrite_manifest = bool(manifest_version_suffix or manifest_title_suffix)

    if dry_run:
        action = "replace" if destination.exists() else "create"
        lines.append(f"Would {action} {destination}")
        lines.extend(
            f"Would copy {file_path.relative_to(source)}"
            for file_path in file_paths
        )
        if rewrite_manifest:
            lines.append("Would rewrite manifest.json display metadata")
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
        if relative_path == Path("manifest.json") and rewrite_manifest:
            _write_manifest_metadata(
                file_path,
                target_path,
                version_suffix=manifest_version_suffix,
                title_suffix=manifest_title_suffix,
            )
        else:
            shutil.copy2(file_path, target_path)

    lines.append(f"Copied {len(file_paths)} files to {destination}")
    return subprocess.CompletedProcess(
        args=["python-copy"],
        returncode=0,
        stdout="\n".join(lines),
        stderr="",
    )


def _capture_local_only_files(destination: Path) -> dict[Path, bytes]:
    """Read destination-only files that must survive deployment refreshes."""
    preserved: dict[Path, bytes] = {}
    for relative_path in _LOCAL_ONLY_FILES:
        target_path = destination / relative_path
        if target_path.is_file():
            preserved[relative_path] = target_path.read_bytes()
    return preserved


def _restore_local_only_files(
    destination: Path,
    preserved: Mapping[Path, bytes],
) -> None:
    """Restore destination-only files removed by rsync/delete refreshes."""
    for relative_path, contents in preserved.items():
        target_path = destination / relative_path
        target_path.parent.mkdir(parents=True, exist_ok=True)
        target_path.write_bytes(contents)


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
    cmd = ["rsync", "-av", "--delete", "--delete-excluded"]
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
    manifest_version_suffix: str | None = None,
    manifest_title_suffix: str | None = None,
) -> subprocess.CompletedProcess[str]:
    """Execute sync to copy mod files into the game directory.

    Args:
        source: Source directory to sync from.
        destination: Destination directory to sync to.
        dry_run: If True, perform a dry run without copying.
        exclude_fonts: If True, exclude Fonts/ directory.
        manifest_version_suffix: Optional local-only Version suffix to apply at
            destination.
        manifest_title_suffix: Optional local-only Title suffix to apply at
            destination.

    Returns:
        Completed process result from rsync or the Python fallback.

    Raises:
        FileNotFoundError: If source directory does not exist, or if dev
            manifest rewriting is enabled but source manifest.json is missing.
        subprocess.CalledProcessError: If rsync fails.
    """
    if not source.is_dir():
        msg = f"Source directory not found: {source}"
        raise FileNotFoundError(msg)

    rewrite_manifest = bool(manifest_version_suffix or manifest_title_suffix)
    if rewrite_manifest and not dry_run:
        source_manifest = source / "manifest.json"
        if not source_manifest.is_file():
            msg = f"manifest.json not found: {source_manifest}"
            raise FileNotFoundError(msg)

    preserved = {} if dry_run else _capture_local_only_files(destination)
    try:
        if shutil.which("rsync") is None:
            return _run_python_sync(
                source,
                destination,
                dry_run=dry_run,
                exclude_fonts=exclude_fonts,
                manifest_version_suffix=manifest_version_suffix,
                manifest_title_suffix=manifest_title_suffix,
            )

        cmd = build_rsync_command(
            source,
            destination,
            dry_run=dry_run,
            exclude_fonts=exclude_fonts,
        )
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)  # noqa: S603 -- trusted rsync call
        if rewrite_manifest and dry_run:
            return _append_stdout_line(
                result,
                "Would rewrite manifest.json display metadata",
            )
        if rewrite_manifest and not dry_run:
            _write_manifest_metadata(
                source / "manifest.json",
                destination / "manifest.json",
                version_suffix=manifest_version_suffix,
                title_suffix=manifest_title_suffix,
            )
        return result
    finally:
        if preserved:
            _restore_local_only_files(destination, preserved)


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
        "--dev",
        action="store_true",
        help=(
            "Mark the synced destination manifest as a local development build "
            "without changing the source manifest."
        ),
    )
    parser.add_argument(
        "--dev-version-suffix",
        default=DEFAULT_DEV_VERSION_SUFFIX,
        help=(
            "Version suffix used with --dev. Defaults to an empty suffix because "
            "Caves of Qud rejects prerelease strings such as '-dev'."
        ),
    )
    parser.add_argument(
        "--dev-title-suffix",
        default=DEFAULT_DEV_TITLE_SUFFIX,
        help="Title suffix used with --dev. Defaults to ' (local dev)'.",
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

    if not args.dev and (
        args.dev_version_suffix != DEFAULT_DEV_VERSION_SUFFIX
        or args.dev_title_suffix != DEFAULT_DEV_TITLE_SUFFIX
    ):
        parser.error("--dev-version-suffix and --dev-title-suffix require --dev")

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
    manifest_version_suffix = args.dev_version_suffix if args.dev else None
    manifest_title_suffix = args.dev_title_suffix if args.dev else None
    try:
        result = run_sync(
            source,
            destination,
            dry_run=args.dry_run,
            exclude_fonts=args.exclude_fonts,
            manifest_version_suffix=manifest_version_suffix,
            manifest_title_suffix=manifest_title_suffix,
        )
    except (FileNotFoundError, TypeError, ValueError) as exc:
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
