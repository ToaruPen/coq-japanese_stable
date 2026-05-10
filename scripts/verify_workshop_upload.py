"""Verify generated Steam Workshop upload files before running steamcmd."""

from __future__ import annotations

import argparse
import re
import sys
import zipfile
from pathlib import Path
from typing import TYPE_CHECKING

try:
    from scripts.build_workshop_upload import (
        WORKSHOP_APP_ID,
        WORKSHOP_PUBLISHED_FILE_ID,
        validate_workshop_changenote,
        verify_workshop_upload_staging,
    )
except ModuleNotFoundError:
    from build_workshop_upload import (
        WORKSHOP_APP_ID,
        WORKSHOP_PUBLISHED_FILE_ID,
        validate_workshop_changenote,
        verify_workshop_upload_staging,
    )

if TYPE_CHECKING:
    from collections.abc import Sequence

_VDF_FIELD_PATTERN = re.compile(r'^\s*"(?P<key>[^"]+)"\s+"(?P<value>.*)"\s*$', re.MULTILINE)
_VDF_TEXT_FIELD_KEYS = ("title", "description", "changenote")


def _parse_vdf_fields(vdf_text: str) -> dict[str, str]:
    """Parse simple quoted top-level VDF fields emitted by build_workshop_upload.py."""
    fields: dict[str, str] = {}
    for match in _VDF_FIELD_PATTERN.finditer(vdf_text):
        fields[match.group("key")] = match.group("value")
    return fields


def _vdf_unescape(value: str) -> str:
    """Unescape the subset emitted by build_workshop_upload.vdf_escape."""
    return value.replace(r"\\", "\\").replace(r"\"", '"')


def _extract_vdf_field_value(vdf_text: str, key: str) -> str | None:
    """Extract a generated VDF value, including literal multiline text fields."""
    marker = f'"{key}" "'
    start = vdf_text.find(marker)
    if start == -1:
        return None

    index = start + len(marker)
    value: list[str] = []
    escaped = False
    while index < len(vdf_text):
        char = vdf_text[index]
        if escaped:
            value.append(f"\\{char}")
            escaped = False
        elif char == "\\":
            escaped = True
        elif char == '"':
            return "".join(value)
        else:
            value.append(char)
        index += 1
    return None


def _append_vdf_mismatch(
    findings: list[str],
    fields: dict[str, str],
    key: str,
    expected: str,
    *,
    unescape_actual: bool = False,
) -> None:
    """Append a standard VDF field mismatch finding."""
    actual = fields.get(key, "<missing>")
    comparable_actual = _vdf_unescape(actual) if unescape_actual and actual != "<missing>" else actual
    if comparable_actual != expected:
        findings.append(f"VDF {key} mismatch: expected {expected}, got {actual}")


def verify_workshop_vdf(vdf_path: Path, *, content_folder: Path) -> list[str]:
    """Verify VDF identity and fragile Steam KeyValues text before upload."""
    findings: list[str] = []
    if not vdf_path.is_file():
        return [f"Workshop VDF not found: {vdf_path}"]

    vdf_text = vdf_path.read_text(encoding="utf-8")
    fields = _parse_vdf_fields(vdf_text)
    _append_vdf_mismatch(findings, fields, "appid", WORKSHOP_APP_ID)
    _append_vdf_mismatch(findings, fields, "publishedfileid", WORKSHOP_PUBLISHED_FILE_ID)
    expected_content_folder = str(content_folder.resolve())
    _append_vdf_mismatch(findings, fields, "contentfolder", expected_content_folder, unescape_actual=True)
    expected_preview_file = str((content_folder / "preview.png").resolve())
    _append_vdf_mismatch(findings, fields, "previewfile", expected_preview_file, unescape_actual=True)
    if r"\"" in vdf_text:
        findings.append('VDF contains escaped double quote sequences (\\"); remove double quotes from text fields')
    text_field_values = {
        key: _extract_vdf_field_value(vdf_text, key) or fields.get(key, "") for key in _VDF_TEXT_FIELD_KEYS
    }
    if any(r"\n" in value for value in text_field_values.values()):
        findings.append(r"VDF contains escaped newline sequences (\n); use literal multiline text")
    changenote = text_field_values.get("changenote", "")
    if changenote:
        findings.extend(validate_workshop_changenote(_vdf_unescape(changenote)))
    return findings


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Verify QudJP Workshop upload staging before steamcmd upload.")
    parser.add_argument("--release-zip", type=Path, required=True)
    parser.add_argument("--content-folder", type=Path, default=Path("dist/workshop/QudJP"))
    parser.add_argument("--vdf", type=Path, default=Path("dist/workshop/workshop_item.vdf"))
    parser.add_argument("--expected-version", required=True)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Run the Workshop upload preflight CLI."""
    parser = _build_parser()
    args = parser.parse_args(argv)

    try:
        findings = [
            *verify_workshop_upload_staging(
                args.release_zip,
                args.content_folder,
                expected_version=args.expected_version,
            ),
            *verify_workshop_vdf(args.vdf, content_folder=args.content_folder),
        ]
    except (FileNotFoundError, KeyError, OSError, TypeError, ValueError, zipfile.BadZipFile) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1
    if findings:
        for finding in findings:
            print(f"Error: {finding}", file=sys.stderr)  # noqa: T201
        return 1

    print(  # noqa: T201
        "Workshop upload preflight verified: "
        f"version={args.expected_version} release_zip={args.release_zip} content_folder={args.content_folder}",
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
