from __future__ import annotations

import json
import zipfile
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Iterator, Mapping

REPO_ROOT = Path(__file__).resolve().parents[2]
DICTIONARIES_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
VALID_RELEASE_DLL_MARKER_PAYLOAD = b"\0".join(
    [
        b"Unity.TextMeshPro",
        b"TextMeshProUguiFontPatch",
        b"TmpInputFieldFontPatch",
        b"InventoryLineFontFixer",
        b"DelayedInventoryLineRepairScheduler",
        b"ShouldPreserveActiveReplacementForTests",
    ],
)


def write_workshop_release_zip(
    path: Path,
    *,
    version: str = "0.2.0",
    dll_payload: bytes = VALID_RELEASE_DLL_MARKER_PAYLOAD,
) -> None:
    """Create a minimal QudJP release ZIP fixture for Workshop tests."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        zf.writestr(
            "QudJP/manifest.json",
            json.dumps({"Version": version, "PreviewImage": "preview.png"}),
        )
        zf.writestr("QudJP/preview.png", b"png")
        zf.writestr("QudJP/LICENSE", "MIT License")
        zf.writestr("QudJP/NOTICE.md", "# NOTICE")
        zf.writestr("QudJP/Bootstrap.cs", "public static class Bootstrap {}")
        launcher_info = zipfile.ZipInfo("QudJP/Launch CavesOfQud (Rosetta).command")
        launcher_info.external_attr = 0o100755 << 16
        zf.writestr(launcher_info, "#!/usr/bin/env bash\n")
        zf.writestr("QudJP/Assemblies/QudJP.dll", dll_payload)
        zf.writestr("QudJP/Localization/ui.json", "{}")
        zf.writestr("QudJP/Fonts/OFL.txt", "SIL Open Font License")


def iter_dictionary_entries(path: Path) -> Iterator[tuple[int, Mapping[str, object]]]:
    """Yield 1-based dictionary entry indexes with object entries from a JSON asset."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        rel = path.relative_to(REPO_ROOT) if path.is_relative_to(REPO_ROOT) else path
        msg = f"Failed to read/parse {rel}: {exc}"
        raise ValueError(msg) from exc

    raw_entries = data.get("entries", []) if isinstance(data, dict) else data
    if not isinstance(raw_entries, list):
        return

    for index, entry in enumerate(raw_entries, start=1):
        if isinstance(entry, dict):
            yield index, entry
