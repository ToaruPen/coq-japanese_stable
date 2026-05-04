"""Build Steam Workshop staging files and a steamcmd VDF for QudJP."""

from __future__ import annotations

import argparse
import json
import shutil
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Sequence

WORKSHOP_APP_ID = "333640"
WORKSHOP_PUBLISHED_FILE_ID = "3718988020"
_DEFAULT_STAGING_DIR = Path("dist/workshop")
_DEFAULT_VDF_OUTPUT = _DEFAULT_STAGING_DIR / "workshop_item.vdf"
_DEFAULT_METADATA_PATH = Path("steam/workshop_metadata.json")


@dataclass(frozen=True)
class WorkshopMetadata:
    """Steam Workshop metadata needed by ``workshop_build_item``."""

    appid: str
    publishedfileid: str
    title: str
    visibility: str
    description_file: Path | None


def _find_project_root() -> Path:
    """Locate the project root by traversing up to find pyproject.toml."""
    current = Path(__file__).resolve().parent
    while current != current.parent:
        if (current / "pyproject.toml").exists():
            return current
        current = current.parent
    msg = "Could not find project root (no pyproject.toml found)"
    raise FileNotFoundError(msg)


def _require_string(data: dict[str, object], key: str, *, source: Path) -> str:
    """Read a required non-empty string from metadata."""
    value = data.get(key)
    if not isinstance(value, str) or not value.strip():
        msg = f"Workshop metadata field {key!r} must be a non-empty string: {source}"
        raise ValueError(msg)
    return value.strip()


def _resolve_metadata_path(path: Path) -> Path:
    """Resolve a metadata path relative to the project root."""
    if path.is_absolute():
        return path
    return _find_project_root() / path


def _resolve_description_file(metadata_path: Path, value: object) -> Path | None:
    """Resolve an optional description file path from metadata."""
    if value is None:
        return None
    if not isinstance(value, str) or not value.strip():
        msg = f"Workshop metadata field 'description_file' must be a non-empty string: {metadata_path}"
        raise ValueError(msg)

    description_file = Path(value.strip())
    if not description_file.is_absolute():
        description_file = metadata_path.parent / description_file
    if not description_file.is_file():
        msg = f"Workshop description file not found: {description_file}"
        raise FileNotFoundError(msg)
    return description_file


def load_metadata(metadata_path: Path) -> WorkshopMetadata:
    """Load and validate Steam Workshop metadata JSON."""
    resolved_metadata_path = _resolve_metadata_path(metadata_path)
    if not resolved_metadata_path.is_file():
        msg = f"Workshop metadata file not found: {resolved_metadata_path}"
        raise FileNotFoundError(msg)

    data = json.loads(resolved_metadata_path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        msg = f"Workshop metadata must be a JSON object: {resolved_metadata_path}"
        raise ValueError(msg)  # noqa: TRY004 -- CLI reports metadata validation failures uniformly.

    appid = _require_string(data, "appid", source=resolved_metadata_path)
    publishedfileid = _require_string(data, "publishedfileid", source=resolved_metadata_path)
    title = _require_string(data, "title", source=resolved_metadata_path)
    visibility = _require_string(data, "visibility", source=resolved_metadata_path)
    description_file = _resolve_description_file(resolved_metadata_path, data.get("description_file"))

    if not appid.isdigit():
        msg = f"Workshop metadata appid must be numeric: {resolved_metadata_path}"
        raise ValueError(msg)
    if not publishedfileid.isdigit():
        msg = f"Workshop metadata publishedfileid must be numeric: {resolved_metadata_path}"
        raise ValueError(msg)
    if visibility not in {"0", "1", "2"}:
        msg = f"Workshop metadata visibility must be 0, 1, or 2: {resolved_metadata_path}"
        raise ValueError(msg)

    return WorkshopMetadata(
        appid=appid,
        publishedfileid=publishedfileid,
        title=title,
        visibility=visibility,
        description_file=description_file,
    )


def vdf_escape(value: str) -> str:
    """Escape a string for a quoted steamcmd VDF value."""
    normalized = value.replace("\r\n", "\n").replace("\r", "\n")
    return normalized.replace("\\", "\\\\").replace('"', '\\"')


def render_vdf(
    metadata: WorkshopMetadata,
    *,
    content_folder: Path,
    preview_file: Path,
    changenote: str,
    description: str | None,
) -> str:
    """Render a steamcmd ``workshop_build_item`` VDF."""
    if not changenote.strip():
        msg = "Workshop changenote must be non-empty"
        raise ValueError(msg)

    fields = [
        ("appid", metadata.appid),
        ("publishedfileid", metadata.publishedfileid),
        ("contentfolder", str(content_folder.resolve())),
        ("previewfile", str(preview_file.resolve())),
        ("visibility", metadata.visibility),
        ("title", metadata.title),
    ]
    if description is not None:
        fields.append(("description", description))
    fields.append(("changenote", changenote.strip()))

    lines = ['"workshopitem"', "{"]
    lines.extend(f'  "{key}" "{vdf_escape(value)}"' for key, value in fields)
    lines.append("}")
    return "\n".join(lines) + "\n"


def find_latest_release_zip(dist_dir: Path) -> Path:
    """Find the newest ``QudJP-v*.zip`` release archive under ``dist_dir``."""
    release_archives = sorted(
        dist_dir.glob("QudJP-v*.zip"),
        key=lambda path: (path.stat().st_mtime, path.name),
    )
    if not release_archives:
        msg = f"No QudJP-v*.zip release archive found under {dist_dir}"
        raise FileNotFoundError(msg)
    return release_archives[-1]


def _validate_release_zip_members(zip_path: Path, members: list[str]) -> None:
    """Validate that the release ZIP has the expected QudJP archive root."""
    if not members or any(not member.startswith("QudJP/") for member in members):
        msg = f"Workshop release ZIP must contain only files under QudJP/: {zip_path}"
        raise ValueError(msg)

    required = {
        "QudJP/LICENSE",
        "QudJP/NOTICE.md",
        "QudJP/manifest.json",
        "QudJP/preview.png",
        "QudJP/Bootstrap.cs",
        "QudJP/Assemblies/QudJP.dll",
    }
    required_prefixes = {
        "QudJP/Localization/",
        "QudJP/Fonts/",
    }
    missing = sorted(required - set(members))
    missing_prefixes = sorted(
        prefix for prefix in required_prefixes if not any(member.startswith(prefix) for member in members)
    )
    if missing or missing_prefixes:
        missing_entries = [*missing, *missing_prefixes]
        msg = f"Workshop release ZIP is missing required files: {', '.join(missing_entries)}"
        raise ValueError(msg)


def create_workshop_staging(release_zip: Path, staging_root: Path) -> tuple[Path, Path]:
    """Extract a release ZIP into the generated Workshop staging directory."""
    resolved_release_zip = release_zip.resolve()
    if not resolved_release_zip.is_file():
        msg = f"Release ZIP not found: {resolved_release_zip}"
        raise FileNotFoundError(msg)

    staging_root = staging_root.resolve()
    content_folder = staging_root / "QudJP"
    if content_folder.exists():
        shutil.rmtree(content_folder)
    staging_root.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(resolved_release_zip) as zf:
        members = zf.namelist()
        _validate_release_zip_members(resolved_release_zip, members)
        for info in zf.infolist():
            target = (staging_root / info.filename).resolve()
            if not target.is_relative_to(staging_root):
                msg = f"Release ZIP member escapes staging directory: {info.filename}"
                raise ValueError(msg)
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(info) as source, target.open("wb") as destination:
                shutil.copyfileobj(source, destination)

    preview_file = content_folder / "preview.png"
    if not preview_file.is_file():
        msg = f"Workshop preview file not found after staging: {preview_file}"
        raise FileNotFoundError(msg)
    return content_folder, preview_file


def _read_description(metadata: WorkshopMetadata) -> str | None:
    """Read the optional Workshop description text."""
    if metadata.description_file is None:
        return None
    return metadata.description_file.read_text(encoding="utf-8").strip()


def build_workshop_upload(
    *,
    release_zip: Path | None,
    metadata_path: Path,
    staging_dir: Path,
    vdf_output: Path,
    changenote: str,
) -> tuple[Path, Path]:
    """Build Workshop staging contents and write the steamcmd VDF."""
    project_root = _find_project_root()
    resolved_release_zip = release_zip
    if resolved_release_zip is None:
        resolved_release_zip = find_latest_release_zip(project_root / "dist")
    elif not resolved_release_zip.is_absolute():
        resolved_release_zip = project_root / resolved_release_zip

    metadata = load_metadata(metadata_path)
    content_folder, preview_file = create_workshop_staging(resolved_release_zip, project_root / staging_dir)
    vdf = render_vdf(
        metadata,
        content_folder=content_folder,
        preview_file=preview_file,
        changenote=changenote,
        description=_read_description(metadata),
    )

    output = vdf_output if vdf_output.is_absolute() else project_root / vdf_output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(vdf, encoding="utf-8")
    return content_folder, output


def _read_changenote_file(changenote_file: Path) -> str:
    """Read a changenote text file for the Workshop VDF."""
    path = changenote_file if changenote_file.is_absolute() else _find_project_root() / changenote_file
    if not path.is_file():
        msg = f"Workshop changenote file not found: {path}"
        raise FileNotFoundError(msg)
    changenote = path.read_text(encoding="utf-8").strip()
    if not changenote:
        msg = f"Workshop changenote file is empty: {path}"
        raise ValueError(msg)
    return changenote


def _build_parser() -> argparse.ArgumentParser:
    """Build the command line parser."""
    parser = argparse.ArgumentParser(
        description="Create Steam Workshop staging files and a steamcmd VDF for QudJP.",
    )
    parser.add_argument(
        "--release-zip",
        type=Path,
        default=None,
        help="Release ZIP to stage. Defaults to newest dist/QudJP-v*.zip.",
    )
    parser.add_argument(
        "--metadata",
        type=Path,
        default=_DEFAULT_METADATA_PATH,
        help=f"Workshop metadata JSON. Default: {_DEFAULT_METADATA_PATH}",
    )
    parser.add_argument(
        "--staging-dir",
        type=Path,
        default=_DEFAULT_STAGING_DIR,
        help=f"Generated Workshop staging directory. Default: {_DEFAULT_STAGING_DIR}",
    )
    parser.add_argument(
        "--vdf-output",
        type=Path,
        default=_DEFAULT_VDF_OUTPUT,
        help=f"Generated steamcmd VDF path. Default: {_DEFAULT_VDF_OUTPUT}",
    )
    changenote_group = parser.add_mutually_exclusive_group(required=True)
    changenote_group.add_argument(
        "--changenote",
        help="Steam Workshop changenote for this upload.",
    )
    changenote_group.add_argument(
        "--changenote-file",
        type=Path,
        help="UTF-8 text file containing the Steam Workshop changenote for this upload.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Command line entry point."""
    parser = _build_parser()
    args = parser.parse_args(argv)

    try:
        changenote = args.changenote
        if args.changenote_file is not None:
            changenote = _read_changenote_file(args.changenote_file)
        content_folder, vdf_output = build_workshop_upload(
            release_zip=args.release_zip,
            metadata_path=args.metadata,
            staging_dir=args.staging_dir,
            vdf_output=args.vdf_output,
            changenote=changenote,
        )
    except (FileNotFoundError, ValueError, zipfile.BadZipFile) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    print(f"Workshop content folder: {content_folder}")  # noqa: T201
    print(f"steamcmd VDF: {vdf_output}")  # noqa: T201
    print(f"Upload command: steamcmd +login <user> +workshop_build_item {vdf_output} +quit")  # noqa: T201
    return 0


if __name__ == "__main__":
    sys.exit(main())
