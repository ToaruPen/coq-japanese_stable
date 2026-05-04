# Steam Workshop Release Guide

This guide covers publishing QudJP to Steam Workshop with `steamcmd`.

## Agent Quick Path

When asked to update the Steam Workshop item, do this first:

1. Read this file, `steam/workshop_metadata.json`, and
   `steam/changenote_template.txt`.
2. Confirm the target Workshop item is `3718988020`.
3. Inspect the release range:

   ```bash
   git describe --tags --abbrev=0
   git log --oneline <previous-tag>..HEAD
   git rev-parse --short=12 HEAD
   ```

4. Draft a user-facing changenote from the accumulated commits. Keep internal
   implementation names secondary; lead with visible translation, UI, runtime,
   and packaging changes.
5. Run the Preflight commands below.
6. Generate `dist/workshop/QudJP/` and `dist/workshop/workshop_item.vdf`.
7. Stop before running `steamcmd` unless the user explicitly confirms upload
   credentials and permission to publish.

If `python3.12` is not available in the local shell, use `uv run python` for the
Python commands in this guide. Do not rewrite the documented command set just
because the local PATH differs.

For each Workshop update, copy
`docs/reports/templates/workshop-release.md` to a dated file under
`docs/reports/` and fill it as release evidence, including preflight, upload,
and post-publish smoke results.

## Release Scope

The Workshop upload source is a generated staging directory that contains only
the shipped mod files. Do not upload source trees, test projects, build
directories, decompiled game files, or game binaries.

Public Workshop metadata:

| Field | Value |
| --- | --- |
| Steam app ID | `333640` |
| Workshop item ID | `3718988020` |
| Metadata source | `steam/workshop_metadata.json` |
| Description source | `steam/workshop_description.ja.txt` |
| Changenote template | `steam/changenote_template.txt` |
| Generated content folder | `dist/workshop/QudJP/` |
| Generated VDF | `dist/workshop/workshop_item.vdf` |

Required files in the staged content folder:

| Path | Required for |
| --- | --- |
| `manifest.json` | Mod ID, title, description, version, tags, and `PreviewImage` metadata |
| `preview.png` | Workshop/mod-manager preview image; referenced by `manifest.json` |
| `LICENSE` | License compliance |
| `NOTICE.md` | Third-party and project notices |
| `Bootstrap.cs` | Game-compiled loader shim |
| `Assemblies/QudJP.dll` | Built Harmony patch DLL |
| `Localization/` | XML overlays, JSON dictionaries, and text corpus assets |
| `Fonts/` | CJK font assets and font license |

## Preflight

Run these checks from the repository root before generating the Workshop upload
VDF:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
ruff check scripts/
uv run pytest scripts/tests/test_build_release.py scripts/tests/test_build_workshop_upload.py scripts/tests/test_sync_mod.py scripts/tests/test_tokenize_corpus.py -q
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
python3.12 scripts/check_glossary_consistency.py Mods/QudJP/Localization
python3.12 scripts/check_translation_tokens.py Mods/QudJP/Localization
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/build_release.py
```

Spot-check the release ZIP:

```bash
python3.12 - <<'PY'
import zipfile
from pathlib import Path

release_archives = sorted(
    Path("dist").glob("QudJP-v*.zip"),
    key=lambda path: (path.stat().st_mtime, path.name),
)
if not release_archives:
    raise SystemExit("dist/: no QudJP-v*.zip release archive found")
zip_path = release_archives[-1]
required = {
    "QudJP/manifest.json",
    "QudJP/preview.png",
    "QudJP/LICENSE",
    "QudJP/NOTICE.md",
    "QudJP/Bootstrap.cs",
    "QudJP/Assemblies/QudJP.dll",
}
required_prefixes = {
    "QudJP/Localization/",
    "QudJP/Fonts/",
}
with zipfile.ZipFile(zip_path) as zf:
    names = set(zf.namelist())
missing = sorted(required - names)
missing_prefixes = sorted(
    prefix for prefix in required_prefixes if not any(name.startswith(prefix) for name in names)
)
if missing or missing_prefixes:
    raise SystemExit(f"{zip_path}: missing files={missing}, missing dirs={missing_prefixes}")
print(f"{zip_path}: required release files present")
PY
```

## Generate Workshop Upload Files

Draft the Workshop changenote from the template. Put the short git hash next to
the version, and summarize the accumulated commits as user-visible changes:

```bash
git describe --tags --abbrev=0
git log --oneline <previous-tag>..HEAD
git rev-parse --short=12 HEAD
cp steam/changenote_template.txt /tmp/qudjp-workshop-changenote.txt
$EDITOR /tmp/qudjp-workshop-changenote.txt
```

Build the generated content folder and steamcmd VDF from the latest
`dist/QudJP-v*.zip`:

```bash
python3.12 scripts/build_workshop_upload.py \
  --changenote-file /tmp/qudjp-workshop-changenote.txt
```

For a specific release archive:

```bash
python3.12 scripts/build_workshop_upload.py \
  --release-zip dist/QudJP-v0.2.0.zip \
  --changenote-file /tmp/qudjp-workshop-changenote.txt
```

The script regenerates `dist/workshop/QudJP/` and writes
`dist/workshop/workshop_item.vdf`. `dist/` is ignored by git; do not commit
generated upload files.

## Upload Or Update

Run steamcmd with the generated VDF:

```bash
steamcmd +login "$STEAM_USER" +workshop_build_item dist/workshop/workshop_item.vdf +quit
```

Do not commit Steam credentials, 2FA material, or login scripts. The
`publishedfileid` is public and intentionally committed in
`steam/workshop_metadata.json`; credentials are local operator state.

The release commit or tag must match the staged content. If the repo changes
after generating `dist/workshop/`, regenerate the release ZIP and Workshop VDF
before uploading.

## Post-Publish Smoke

After Steam finishes processing the item:

1. Open the Workshop page and confirm the title, description, tags, preview
   image, visibility, file size, and change note.
2. Subscribe to the item from a clean Steam client state or unsubscribe and
   resubscribe if updating an existing item.
3. Launch the game, enable only QudJP for the smoke pass, and restart.
4. Confirm the Mod Manager lists QudJP with the expected version and preview.
5. Confirm the Options screen and one short conversation render Japanese text
   and CJK glyphs correctly.
6. Check fresh logs under `~/Library/Logs/Freehold Games/CavesOfQud/` for QudJP
   build markers, missing glyph warnings, compile errors, or `MODWARN`.
7. Record the smoke result in the dated release evidence file copied from
   `docs/reports/templates/workshop-release.md`.

## Rollback

Steam Workshop updates cannot be atomically rolled back from this repository. If
a published item is bad:

1. Set Workshop visibility to private or friends-only.
2. Rebuild the last known good repo tag.
3. Regenerate the release ZIP and Workshop VDF from that tag.
4. Submit a new Workshop update with a changenote explaining the rollback.
5. If the release ZIP was already distributed elsewhere, replace it with the
   last known good ZIP and keep the bad ZIP for internal diagnosis only.

## Known Limitations

- `steamcmd` still requires an authenticated Steam account with permission to
  update Workshop item `3718988020`.
- Fresh runtime smoke requires an unlocked macOS console session when using the
  automated checker.
- Workshop moderation or compatibility warnings are Steam-side state; document
  them in the release report when they affect availability.
