# Steam Workshop Release Guide

This guide covers publishing QudJP to Steam Workshop with `steamcmd`.

## Agent Quick Path

When asked to update the Steam Workshop item, do this first:

1. Read this file, `steam/workshop_metadata.json`, and
   `steam/changenote_template.txt`.
2. Confirm the target Workshop item is `3718988020`.
3. Inspect tags, release range, and head identity:

   ```bash
   git status --short --branch
   git tag --sort=-creatordate | head -20
   git describe --tags --abbrev=0 --match 'v[0-9]*'
   git log --oneline <previous-tag>..HEAD
   git rev-parse --short=12 HEAD
   ```

4. Confirm or create the release tag according to the Tag and Release Policy
   below. The tag, manifest version, release ZIP, and staged Workshop content
   must all identify the same release.
5. Draft a user-facing changenote from the accumulated commits. Keep internal
   implementation names secondary; lead with visible translation, UI, runtime,
   and packaging changes.
6. Run the Preflight recipe below.
7. Generate `dist/workshop/QudJP/` and `dist/workshop/workshop_item.vdf` with
   the Workshop staging recipe.
8. Stop before running `steamcmd` unless the user explicitly confirms upload
   credentials and permission to publish.

Use `just` recipes for release commands so local runs match the repo task
runner. The recipes execute Python through `uv run python`; do not rewrite the
documented workflow just because a local shell lacks `python3.12`.

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

## Tag and Release Policy

Steam Workshop updates, Git tags, and GitHub Releases are separate publication
surfaces:

- Git tag: immutable source identity for a release commit.
- GitHub Release: optional GitHub distribution page attached to a tag.
- Steam Workshop update: the `steamcmd` upload to published file ID
  `3718988020`.

Normal Workshop shipping uses an annotated Git tag named `vX.Y.Z` before upload.
The tag must point at the exact commit used to build the release ZIP and
Workshop VDF. The `Mods/QudJP/manifest.json` `Version`, release ZIP name, release
report, changenote first line, and Git tag must all use the same version.

Do not create or move tags as a hidden side effect. Creating a tag, pushing a
tag, deleting a tag, or retagging requires explicit current user confirmation.
If a tag already exists for the target version, stop unless it points at the
release commit.

Recommended command sequence:

```bash
git status --short --branch
git tag --sort=-creatordate | head -20
git describe --tags --abbrev=0 --match 'v[0-9]*'
git log --oneline <previous-tag>..HEAD
git rev-parse --short=12 HEAD
```

Record `<previous-tag>` or the explicit previous release range before creating
the new release tag. After `vX.Y.Z` exists, `git describe --tags --abbrev=0`
returns the current release tag from `HEAD`, so it is no longer a valid way to
discover the previous release.

After release notes, changelog, manifest, and other release files are final and
committed, create the release tag:

```bash
git tag -a vX.Y.Z -m "QudJP vX.Y.Z"
git rev-list -n1 vX.Y.Z
git rev-parse HEAD
```

Push the tag only after explicit confirmation:

```bash
git push origin vX.Y.Z
```

If no prior local tag exists, do not invent the previous release range. Use one
of these sources and record which one was used in the release evidence report:

- remote tag evidence, checked without mutating local refs:
  `git ls-remote --tags origin`
- an existing Workshop release evidence report
- an existing changelog/GitHub release record that identifies a release commit
- an explicit user-provided commit range

If the prior release identity still cannot be established, stop before building
Workshop staging.

## Preflight

Run this from the repository root before generating the Workshop upload VDF:

```bash
just workshop-preflight X.Y.Z
```

The preflight recipe requires a clean worktree, verifies `vX.Y.Z` points at
`HEAD`, then runs build, Python lint/tests, localization checks,
translation-token checks, and `scripts/build_release.py`. Do not build release
artifacts from a dirty worktree; uncommitted files can make the ZIP differ from
the tagged source.

## Release Notes

Localization PRs that change `Mods/QudJP/Localization/` must include at least
one release-note fragment under `docs/release-notes/unreleased/*.md`. CI checks
this on pull requests.

Fragments use Keep a Changelog section headings and user-facing bullets:

```markdown
### Changed

- Improve Japanese text in the trade and conversation UI.
```

Before release, render the fragments into drafts for `CHANGELOG.md` and the
Steam Workshop changenote:

```bash
git rev-parse --short=12 HEAD
just render-release-notes X.Y.Z <short-git-hash> YYYY-MM-DD
```

Review `/tmp/qudjp-changelog-entry.md`, copy it into `CHANGELOG.md`, and use
`/tmp/qudjp-workshop-changenote.txt` as the `build_workshop_upload.py`
`--changenote-file`. Do not publish to Steam until the upload gate below is
explicitly confirmed.

Spot-check the latest release ZIP:

```bash
just release-zip-check
```

For a specific release archive:

```bash
just release-zip-check dist/QudJP-vX.Y.Z.zip
```

## Generate Workshop Upload Files

Use the rendered Workshop changenote draft from the Release Notes step as the
baseline. If needed, enrich it with commit-range context before upload:

```bash
git log --oneline <recorded-previous-release-ref>..vX.Y.Z
$EDITOR /tmp/qudjp-workshop-changenote.txt
```

Use the previous release ref recorded before tag creation or documented in the
release evidence report. Do not recompute it with `git describe` after the new
release tag exists.

Build the generated content folder and steamcmd VDF from the latest
`dist/QudJP-v*.zip`:

```bash
just build-workshop-upload
```

For a specific release archive:

```bash
just build-workshop-upload dist/QudJP-vX.Y.Z.zip /tmp/qudjp-workshop-changenote.txt
```

The script regenerates `dist/workshop/QudJP/` and writes
`dist/workshop/workshop_item.vdf`. `dist/` is ignored by git; do not commit
generated upload files.

Steam renders Workshop description and changenote text from literal newline
characters in the generated VDF. Do not escape newlines as the two-character
sequence `\n`; Steam will show that text verbatim in Change Notes. After
publishing, verify the rendered Workshop page and changelog in the target
locale, for example `?l=japanese`, because Steam stores localized Change Notes
separately.

## Upload Or Update

Run steamcmd with the generated VDF:

```bash
steamcmd +login "$STEAM_USER" +workshop_build_item dist/workshop/workshop_item.vdf +quit
```

Do not commit Steam credentials, 2FA material, or login scripts. The
`publishedfileid` is public and intentionally committed in
`steam/workshop_metadata.json`; credentials are local operator state.

The release tag must point at the commit that produced the staged content. If
the repo changes after generating `dist/workshop/`, regenerate the release ZIP
and Workshop VDF before uploading.

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
