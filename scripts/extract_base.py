"""Extract game Base XML files to local references/ directory."""

import argparse
import shutil
import sys
from pathlib import Path

_GAME_BASE_DIR = (
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
    / "Base"
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


def extract_xml_files(
    source: Path,
    destination: Path,
    *,
    force: bool = False,
) -> list[Path]:
    """Copy XML files from source to destination preserving directory structure.

    Args:
        source: Source directory containing XML files.
        destination: Destination directory to copy to.
        force: If True, overwrite existing files.

    Returns:
        List of destination paths for copied files.

    Raises:
        FileNotFoundError: If source directory does not exist.
    """
    if not source.is_dir():
        msg = f"Source directory not found: {source}"
        raise FileNotFoundError(msg)

    copied: list[Path] = []
    for xml_path in sorted(source.rglob("*.xml")):
        relative = xml_path.relative_to(source)
        dest_path = destination / relative

        if dest_path.exists() and not force:
            continue

        dest_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(xml_path, dest_path)
        copied.append(dest_path)

    return copied


def main(argv: list[str] | None = None) -> int:
    """Run the Base XML extraction CLI.

    Args:
        argv: Command-line arguments. Defaults to sys.argv[1:].

    Returns:
        Exit code: 0 on success, 1 on failure.
    """
    parser = argparse.ArgumentParser(
        description="Extract game Base XML files to local references/ directory.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite existing files in the destination.",
    )
    args = parser.parse_args(argv)

    try:
        project_root = _find_project_root()
    except FileNotFoundError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    destination = project_root / "references" / "Base"
    try:
        copied = extract_xml_files(_GAME_BASE_DIR, destination, force=args.force)
    except FileNotFoundError as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    total_xml = sum(1 for _ in _GAME_BASE_DIR.rglob("*.xml")) if _GAME_BASE_DIR.is_dir() else 0
    skipped = total_xml - len(copied)
    print(f"Copied {len(copied)} files, skipped {skipped} existing.")  # noqa: T201
    return 0


if __name__ == "__main__":
    sys.exit(main())
