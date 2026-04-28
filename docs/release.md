# Steam Workshop Release Guide

This guide covers publishing QudJP to Steam Workshop through the Caves of Qud in-game uploader. The uploader is GUI-driven; this repo does not use `steamcmd` for Workshop publishing.

## Release Scope

The Workshop upload source is a local `Mods/QudJP/` deployment that contains only shipped mod files. Do not upload source trees, test projects, build directories, decompiled game files, or game binaries.

Required files:

| Path | Required for |
| --- | --- |
| `manifest.json` | Mod ID, title, description, version, tags, and `PreviewImage` metadata |
| `preview.png` | Workshop/mod-manager preview image; referenced by `manifest.json` |
| `LICENSE` | License compliance in the release ZIP |
| `NOTICE.md` | Third-party and project notices in the release ZIP |
| `Bootstrap.cs` | Game-compiled loader shim |
| `Assemblies/QudJP.dll` | Built Harmony patch DLL |
| `Localization/` | XML overlays, JSON dictionaries, and text corpus assets |
| `Fonts/` | CJK font assets and font license |

## Preflight

Run these checks from the repository root before opening the uploader:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
ruff check scripts/
uv run pytest scripts/tests/test_build_release.py scripts/tests/test_sync_mod.py scripts/tests/test_tokenize_corpus.py -q
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/build_release.py
python3.12 scripts/sync_mod.py
```

Spot-check the release ZIP:

```bash
python3.12 - <<'PY'
import zipfile
from pathlib import Path

release_archives = list(Path("dist").glob("QudJP-v*.zip"))
if not release_archives:
    raise SystemExit("dist/: no QudJP-v*.zip release archive found")
zip_path = max(release_archives, key=lambda path: path.stat().st_mtime)
required = {
    "QudJP/manifest.json",
    "QudJP/preview.png",
    "QudJP/LICENSE",
    "QudJP/NOTICE.md",
    "QudJP/Bootstrap.cs",
    "QudJP/Assemblies/QudJP.dll",
}
with zipfile.ZipFile(zip_path) as zf:
    names = set(zf.namelist())
missing = sorted(required - names)
if missing:
    raise SystemExit(f"{zip_path}: missing {missing}")
print(f"{zip_path}: required release files present")
PY
```

## Upload Or Update

1. Launch Caves of Qud through Steam. On Apple Silicon, use Rosetta for the same runtime path used by QudJP verification.
2. Open the Mod Manager and confirm `Caves of Qud 日本語化` / `QudJP` is listed from the synced local mod directory.
3. Open the Workshop management/uploader view for `QudJP`.
4. For a first upload, create the Workshop item and let the game write `workshop.json`.
5. Set or confirm:
   - title: `Caves of Qud 日本語化`
   - description: summarize the current Japanese localization coverage and note that Caves of Qud `2.0.4` is the supported game version
   - tags: `Localization`, `UI`, `Japanese`
   - visibility: use private/friends-only until smoke checks pass; switch to public only after verification
6. Select `Mods/QudJP/preview.png` as the Workshop image if the uploader does not already show it. The game stores this choice in local `workshop.json` as `ImagePath`.
7. Enter a concise changelist for the upload/update.
8. Submit the item update and wait for the uploader to report completion.

The checked-in `manifest.json` `PreviewImage` supports the in-game mod listing. The Workshop uploader itself uses the local `workshop.json` `ImagePath`, so the GUI image selection step is still required when that file is absent or stale.

## Post-Publish Smoke

After Steam finishes processing the item:

1. Open the Workshop page and confirm the title, description, tags, preview image, and visibility.
2. Subscribe to the item from a clean Steam client state or unsubscribe/resubscribe if updating an existing item.
3. Launch the game, enable only QudJP for the smoke pass, and restart.
4. Confirm the Mod Manager lists QudJP with the expected version and preview.
5. Confirm the Options screen and one short conversation render Japanese text and CJK glyphs correctly.
6. Check fresh logs under `~/Library/Logs/Freehold Games/CavesOfQud/` for QudJP build markers, missing glyph warnings, compile errors, or `MODWARN`.
7. Record the smoke result in `docs/reports/` when the release decision depends on runtime evidence.

## Rollback

Steam Workshop updates cannot be atomically rolled back from this repository. If a published item is bad:

1. Set Workshop visibility to private or friends-only.
2. Rebuild and resync the last known good repo state.
3. Submit a new Workshop update with a changelist explaining the rollback.
4. If the release ZIP was already distributed elsewhere, replace it with the last known good ZIP and keep the bad ZIP for internal diagnosis only.

## Known Limitations

- The Workshop item ID and uploader image path live in local `workshop.json`; do not commit that file unless a maintainer explicitly decides to track public Workshop metadata.
- The first git tag matching a manifest version is a release-management decision and is intentionally separate from the metadata/docs slice.
- Fresh runtime smoke requires an unlocked macOS console session when using the automated checker.
