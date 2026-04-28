"""Build a release ZIP for the QudJP mod.

Runs ``dotnet build -c Release``, reads the version from ``manifest.json``,
and produces ``dist/QudJP-v{version}.zip`` containing only the files the game
needs:

- ``QudJP/manifest.json``
- ``QudJP/preview.png`` when referenced by ``manifest.json`` ``PreviewImage``
- ``QudJP/LICENSE``
- ``QudJP/NOTICE.md``
- ``QudJP/Bootstrap.cs``
- ``QudJP/Assemblies/QudJP.dll``
- ``QudJP/Localization/**/*.xml``
- ``QudJP/Localization/**/*.json``
- ``QudJP/Localization/**/*.txt``
- ``QudJP/Fonts/*`` (CJK font and license)

The ZIP root is ``QudJP/`` so users can extract it directly into their Mods
directory.
"""

import json
import re
import subprocess
import sys
import zipfile
from pathlib import Path

RELEASE_VERSION = "0.2.0"
_LOCALIZATION_ASSET_SUFFIXES = {".json", ".txt", ".xml"}


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


def read_version(manifest_path: Path) -> str:
    """Read the mod version from manifest.json.

    Args:
        manifest_path: Path to the manifest.json file.

    Returns:
        Version string (e.g. ``"0.1.0"``).

    Raises:
        FileNotFoundError: If manifest.json does not exist.
        ValueError: If the ``Version`` key is missing, empty, or not simple semver.
    """
    if not manifest_path.exists():
        msg = f"manifest.json not found: {manifest_path}"
        raise FileNotFoundError(msg)

    data: dict[str, object] = json.loads(manifest_path.read_text(encoding="utf-8"))
    if "Version" not in data:
        msg = f"Version field is missing in manifest.json: {manifest_path}"
        raise ValueError(msg)
    version = str(data["Version"]).strip()
    if not version:
        msg = f"Version field is empty in manifest.json: {manifest_path}"
        raise ValueError(msg)
    if re.fullmatch(r"\d+\.\d+\.\d+", version) is None:
        msg = (
            "Version field must be simple semver X.Y.Z in manifest.json: "
            f"{manifest_path} (got {version!r})"
        )
        raise ValueError(msg)
    return version


def read_preview_image_path(manifest_path: Path) -> Path | None:
    """Read and validate the optional PreviewImage asset path.

    Args:
        manifest_path: Path to ``manifest.json``.

    Returns:
        Absolute path to the preview image, or ``None`` when unset.

    Raises:
        FileNotFoundError: If the referenced preview image does not exist.
        ValueError: If PreviewImage is absolute or escapes the mod directory.
    """
    data: dict[str, object] = json.loads(manifest_path.read_text(encoding="utf-8"))
    preview = data.get("PreviewImage")
    if not isinstance(preview, str) or not preview.strip():
        return None

    relative_path = Path(preview.strip())
    if relative_path.is_absolute() or ".." in relative_path.parts:
        msg = f"PreviewImage must be a relative mod-local path: {manifest_path} (got {preview!r})"
        raise ValueError(msg)

    preview_path = manifest_path.parent / relative_path
    if not preview_path.is_file():
        msg = f"PreviewImage file not found: {preview_path}"
        raise FileNotFoundError(msg)
    return preview_path


def build_dll(project_root: Path) -> Path:
    """Run ``dotnet build -c Release`` and return the output DLL path.

    Args:
        project_root: Root directory of the repository.

    Returns:
        Path to the built ``QudJP.dll``.

    Raises:
        subprocess.CalledProcessError: If the build fails.
        FileNotFoundError: If the DLL is not found after a successful build.
    """
    csproj = project_root / "Mods" / "QudJP" / "Assemblies" / "QudJP.csproj"
    cmd = ["dotnet", "build", str(csproj), "-c", "Release"]
    print(f"Running: {' '.join(cmd)}")  # noqa: T201
    subprocess.run(cmd, check=True)  # noqa: S603 -- trusted dotnet call

    dll_path = project_root / "Mods" / "QudJP" / "Assemblies" / "QudJP.dll"
    if not dll_path.exists():
        msg = f"DLL not found after build: {dll_path}"
        raise FileNotFoundError(msg)
    return dll_path


def collect_localization_files(localization_dir: Path) -> list[Path]:
    """Collect all game-loaded localization assets under the Localization directory.

    Args:
        localization_dir: Path to ``Mods/QudJP/Localization/``.

    Returns:
        Sorted list of absolute paths to ``.xml``, ``.json``, and ``.txt`` files.

    Raises:
        FileNotFoundError: If the localization directory does not exist.
    """
    if not localization_dir.is_dir():
        msg = f"Localization directory not found: {localization_dir}"
        raise FileNotFoundError(msg)

    return sorted(
        {
            file_path
            for file_path in localization_dir.rglob("*")
            if file_path.suffix in _LOCALIZATION_ASSET_SUFFIXES
        },
    )


def create_zip(
    output_path: Path,
    manifest_path: Path,
    dll_path: Path,
    localization_dir: Path,
    localization_files: list[Path],
    *,
    legal_files: list[Path] | None = None,
) -> list[str]:
    """Create the release ZIP archive.

    The ZIP root is ``QudJP/`` so users can extract directly into Mods/.

    Args:
        output_path: Destination path for the ZIP file.
        manifest_path: Path to ``manifest.json``.
        dll_path: Path to the built ``QudJP.dll``.
        localization_dir: Path to the ``Localization/`` directory.
        localization_files: List of localization files to include.
        legal_files: Optional repository-level compliance files to include.

    Returns:
        List of archive member names added to the ZIP.
    """
    output_path.parent.mkdir(parents=True, exist_ok=True)
    members: list[str] = []

    with zipfile.ZipFile(output_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        # manifest.json
        arc_manifest = "QudJP/manifest.json"
        zf.write(manifest_path, arc_manifest)
        members.append(arc_manifest)

        # Optional Workshop/mod-manager preview image referenced by manifest.json
        preview_path = read_preview_image_path(manifest_path)
        if preview_path is not None:
            relative = preview_path.relative_to(manifest_path.parent)
            arc_preview = f"QudJP/{relative}"
            zf.write(preview_path, arc_preview)
            members.append(arc_preview)

        # Compliance files
        for legal_file in legal_files or []:
            if not legal_file.exists():
                msg = f"Missing required compliance file: {legal_file}"
                raise FileNotFoundError(msg)
            arc_name = f"QudJP/{legal_file.name}"
            zf.write(legal_file, arc_name)
            members.append(arc_name)

        # DLL
        arc_dll = "QudJP/Assemblies/QudJP.dll"
        zf.write(dll_path, arc_dll)
        members.append(arc_dll)

        # Bootstrap.cs
        bootstrap_path = manifest_path.parent / "Bootstrap.cs"
        if bootstrap_path.exists():
            arc_bootstrap = "QudJP/Bootstrap.cs"
            zf.write(bootstrap_path, arc_bootstrap)
            members.append(arc_bootstrap)

        # Localization files
        for file_path in localization_files:
            relative = file_path.relative_to(localization_dir.parent)
            arc_name = f"QudJP/{relative}"
            zf.write(file_path, arc_name)
            members.append(arc_name)

        # Font files
        fonts_dir = manifest_path.parent / "Fonts"
        if fonts_dir.is_dir():
            for font_file in sorted(fonts_dir.rglob("*")):
                if font_file.is_file():
                    relative = font_file.relative_to(manifest_path.parent)
                    arc_name = f"QudJP/{relative}"
                    zf.write(font_file, arc_name)
                    members.append(arc_name)

    return members


def build_release() -> None:
    """Build the release ZIP for QudJP.

    Raises:
        FileNotFoundError: If required files are missing.
        subprocess.CalledProcessError: If the dotnet build fails.
        ValueError: If the version string is invalid.
    """
    project_root = _find_project_root()
    mod_dir = project_root / "Mods" / "QudJP"
    manifest_path = mod_dir / "manifest.json"
    localization_dir = mod_dir / "Localization"

    version = read_version(manifest_path)
    print(f"Building release for version: {version}")  # noqa: T201

    dll_path = build_dll(project_root)
    print(f"DLL built: {dll_path}")  # noqa: T201

    localization_files = collect_localization_files(localization_dir)
    print(f"Localization files found: {len(localization_files)}")  # noqa: T201

    legal_files = [project_root / "LICENSE", project_root / "NOTICE.md"]
    output_path = project_root / "dist" / f"QudJP-v{version}.zip"
    members = create_zip(
        output_path,
        manifest_path,
        dll_path,
        localization_dir,
        localization_files,
        legal_files=legal_files,
    )

    print(f"\nCreated: {output_path}")  # noqa: T201
    print(f"Contents ({len(members)} files):")  # noqa: T201
    for member in members:
        print(f"  {member}")  # noqa: T201


def main() -> int:
    """Entry point for the build_release script.

    Returns:
        Exit code: 0 on success, 1 on failure.
    """
    try:
        build_release()
    except (FileNotFoundError, ValueError, subprocess.CalledProcessError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
