# Release Guide

This guide covers publishing QudJP through GitHub Releases and Steam Workshop.
The GitHub Release ZIP is the source artifact for the manual Steam Workshop
upload.

## Agent Quick Path

When asked to update the Steam Workshop item, do this first:

1. Read this file, `steam/workshop_metadata.json`, and
   `steam/changenote_template.txt`.
2. Confirm the target Workshop item is `3718988020`.
3. Establish what "current changes" means before choosing the release version.
   Do not reuse the newest existing tag just because it exists. Compare the
   previous shipped tag with `origin/main`, including `Mods/QudJP/Localization/`,
   `Mods/QudJP/Assemblies/`, `docs/release-notes/unreleased/`, `CHANGELOG.md`,
   and `Mods/QudJP/manifest.json`. If `origin/main` contains unreleased
   user-visible localization or runtime changes, prepare the next version so
   the shipped ZIP and Workshop changenote describe the same content.
4. Prepare and merge a release PR that updates `Mods/QudJP/manifest.json`,
   `CHANGELOG.md`, and release-note fragments.
5. After the release PR is merged, update local `main` and inspect tags,
   release range, and head identity:

   ```bash
   git switch main
   git pull --ff-only origin main
   git status --short --branch
   git tag --sort=-creatordate | head -20
   git describe --tags --abbrev=0 --match 'v[0-9]*'
   git log --oneline <previous-tag>..HEAD
   git diff --name-only <previous-tag>..HEAD -- \
     Mods/QudJP/Localization Mods/QudJP/Assemblies docs/release-notes CHANGELOG.md Mods/QudJP/manifest.json
   git rev-parse --short=12 HEAD
   ```

6. Create and push the release tag from `main` according to the Tag and Release
   Policy below. The tag, manifest version, GitHub Release ZIP, staged Workshop
   content, and changenote must all identify the same release.
7. Wait for the tag-triggered `Release` GitHub Actions workflow to create a
   draft GitHub Release with `QudJP-vX.Y.Z.zip` and
   `QudJP-vX.Y.Z.zip.sha256`.
8. Draft a user-facing changenote from the accumulated commits. Keep internal
   implementation names secondary; lead with visible translation, UI, runtime,
   and packaging changes. If the release includes localization changes, the
   first substantive section of the Steam changenote must describe those
   translation changes in user-facing terms before pipeline or tooling notes.
9. Download and verify the GitHub Release ZIP with the local release ZIP
   download recipe.
   `just release-zip-check` also runs the release DLL marker gate: the shipped
   DLL must contain required runtime markers and must not contain dev-only probe
   patch markers or verbose probe markers such as `DynamicTextProbe/v1`,
   `FinalOutputProbe/v1`, and `SinkObserve/v1`.
10. Generate `dist/workshop/QudJP/` and `dist/workshop/workshop_item.vdf` with
   the Workshop staging recipe, passing the downloaded GitHub Release ZIP
   explicitly.
11. Stop before running `steamcmd` unless the user explicitly confirms upload
   credentials and permission to publish.
12. After Steam upload and smoke checks, publish the draft GitHub Release and
    commit the Workshop release evidence report outside the release tag.

Use `just` recipes for release commands so local runs match the repo task
runner. The recipes execute Python through `uv run python`; do not rewrite the
documented workflow just because a local shell lacks a versioned Python
executable.

For each Workshop update, copy
`docs/reports/templates/workshop-release.md` to a dated file under
`docs/reports/` and fill it as release evidence, including GitHub Release,
preflight, upload, and post-publish smoke results. The release evidence report
records observed publication results and normally stays outside the release
tag. Commit it after upload with the actual Steam manifest ID, public metadata
check, and download validation results.

## Release Scope

The GitHub Release ZIP and Workshop upload source contain only the shipped mod
files. Do not upload source trees, test projects, build directories, decompiled
game files, or game binaries.

Public Workshop metadata:

| Field | Value |
| --- | --- |
| Steam app ID | `333640` |
| Workshop item ID | `3718988020` |
| Metadata source | `steam/workshop_metadata.json` |
| Description source | `steam/workshop_description.ja.txt` |
| Changenote template | `steam/changenote_template.txt` |
| GitHub Release ZIP | `QudJP-vX.Y.Z.zip` |
| GitHub Release checksum | `QudJP-vX.Y.Z.zip.sha256` |
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
| `Launch CavesOfQud (Rosetta).command` | macOS Apple Silicon helper launcher |
| `Assemblies/QudJP.dll` | Built Harmony patch DLL |
| `Localization/` | XML overlays, JSON dictionaries, and text corpus assets |
| `Fonts/` | CJK font assets and font license |

## Tag and Release Policy

Steam Workshop updates, Git tags, and GitHub Releases are separate publication
surfaces:

- Git tag: immutable source identity for a release commit.
- GitHub Release: draft distribution page attached to a tag. The ZIP asset is
  the source artifact for Steam Workshop staging.
- Steam Workshop update: the `steamcmd` upload to published file ID
  `3718988020`.

Normal shipping uses an annotated Git tag named `vX.Y.Z` after the release PR is
merged to `main`. The tag must point at a commit reachable from `origin/main`;
do not tag a PR branch. The `Mods/QudJP/manifest.json` `Version`, GitHub
Release ZIP name, release report, changenote first line, and Git tag must all
use the same version.

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

After release notes, changelog, manifest, and other release files are final,
merged, and pulled locally on `main`, create the release tag:

```bash
git switch main
git pull --ff-only origin main
git tag -a vX.Y.Z -m "QudJP vX.Y.Z"
git rev-list -n1 vX.Y.Z
git rev-parse HEAD
```

Confirm that the tag target is on `origin/main`, then push the tag only after
explicit confirmation:

```bash
git merge-base --is-ancestor "$(git rev-list -n1 vX.Y.Z)" origin/main
git push origin vX.Y.Z
```

Pushing `vX.Y.Z` triggers `.github/workflows/release.yml`. That workflow fails
before draft release creation if the tag is not reachable from `origin/main`, if
the tag and manifest versions differ, or if `CHANGELOG.md` has no matching
entry.

If no prior local tag exists, do not invent the previous release range. Use one
of these sources and record which one was used in the release evidence report:

- remote tag evidence, checked without mutating local refs:
  `git ls-remote --tags origin`
- an existing Workshop release evidence report
- an existing changelog/GitHub release record that identifies a release commit
- an explicit user-provided commit range

If the prior release identity still cannot be established, stop before building
Workshop staging.

## GitHub Release Artifact

The tag-triggered GitHub Actions `Release` workflow builds and verifies
`QudJP-vX.Y.Z.zip`, renders GitHub Release notes from `CHANGELOG.md`, writes a
SHA256 checksum file, and creates a draft GitHub Release. The created release is
explicitly marked as GitHub's `Latest` release for the tag version so the
repository release page follows the newest shipped release when the draft is
published.

The workflow is intentionally tag-only:

```yaml
on:
  push:
    tags:
      - "v*.*.*"
```

PRs, `main` pushes, documentation edits, and workflow maintenance do not create
GitHub Releases. The workflow does not run `steamcmd` and must not contain Steam
credentials.

## Local Preflight

The GitHub Release workflow is the source of the release ZIP, but a local
operator can still run the same preflight before tagging or when diagnosing a
release:

```bash
just workshop-preflight X.Y.Z
```

The preflight recipe requires a clean worktree, verifies `vX.Y.Z` points at
`HEAD`, then runs build, Python lint/tests, localization checks,
translation-token checks, and `scripts/build_release.py`. Do not upload release
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
just release-zip-check dist/release-assets/vX.Y.Z/QudJP-vX.Y.Z.zip
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

Download and verify the draft GitHub Release ZIP:

```bash
just download-release-zip X.Y.Z
```

This writes the verified ZIP under `dist/release-assets/vX.Y.Z/`. Build the
generated content folder and steamcmd VDF from that explicit archive:

```bash
just build-workshop-upload \
  dist/release-assets/vX.Y.Z/QudJP-vX.Y.Z.zip \
  /tmp/qudjp-workshop-changenote.txt
```

The script regenerates `dist/workshop/QudJP/` and writes
`dist/workshop/workshop_item.vdf`. `dist/` is ignored by git; do not commit
generated upload files. Do not rely on the default latest-zip lookup for
shipping; pass the GitHub Release ZIP path explicitly.

Steam renders Workshop description and changenote text from literal newline
characters in the generated VDF. Do not escape newlines as the two-character
sequence `\n`; Steam will show that text verbatim in Change Notes. After
publishing, verify the rendered Workshop page and changelog in the target
locale, for example `?l=japanese`, because Steam stores localized Change Notes
separately.

Steam's KeyValues parser can reject escaped double quotes inside long
description or changenote fields. Keep Workshop text free of literal `"`
characters; prefer backticks, Japanese corner quotes, or plain text instead.

## Upload Or Update

Run steamcmd with the generated VDF:

```bash
steamcmd +login "$STEAM_USER" +workshop_build_item dist/workshop/workshop_item.vdf +quit
```

Before upload, confirm that the same `steamcmd` executable you will use for
`workshop_build_item` has usable cached credentials. The desktop Steam client
being logged in is not enough; Homebrew or other `steamcmd` installs can use a
different credential store. Check the executable path and run a login probe with
that exact command:

```bash
command -v steamcmd
steamcmd +login "$STEAM_USER" +quit
```

If the upload command will use an explicit executable path, use that same path
for the login probe too:

```bash
/opt/homebrew/bin/steamcmd +login "$STEAM_USER" +quit
```

If `steamcmd` reports `Cached credentials not found`, have the operator log in
interactively through that same executable before upload. Do not pipe, commit,
or record Steam passwords, Steam Guard codes, or login scripts.

Do not commit Steam credentials, 2FA material, or login scripts. The
`publishedfileid` is public and intentionally committed in
`steam/workshop_metadata.json`; credentials are local operator state.

The release tag must point at the commit that produced the staged content. If
the repo changes after generating `dist/workshop/`, regenerate the release ZIP
and Workshop VDF before uploading.

Immediately before `steamcmd +workshop_build_item`, verify that the generated
content folder and VDF still match the intended release ZIP:

```bash
just workshop-upload-preflight \
  dist/release-assets/vX.Y.Z/QudJP-vX.Y.Z.zip \
  X.Y.Z
```

This catches stale `dist/workshop/QudJP/` contents, mismatched Workshop item
IDs, mismatched `contentfolder`, escaped newline sequences, and escaped double
quotes before Steam can reject or publish the wrong upload.

## Post-Publish Smoke

After Steam finishes processing the item:

1. Open the Workshop page and confirm the title, description, tags, preview
   image, visibility, file size, and change note.
   - For Apple Silicon support, confirm the description mentions
     `Launch CavesOfQud (Rosetta).command`, Rosetta 2, Finder double-click use,
     the `CoQ.app` picker fallback, and a quote-free `arch -x86_64` launch
     example for advanced users.
2. Subscribe to the item from a clean Steam client state or unsubscribe and
   resubscribe if updating an existing item.
3. Validate the downloaded Workshop item against the staged content:

   ```bash
   steamcmd +login "$STEAM_USER" +workshop_download_item 333640 3718988020 validate +quit
   just workshop-download-check X.Y.Z dist/release-assets/vX.Y.Z/QudJP-vX.Y.Z.zip
   ```

   This verifies that the downloaded `manifest.json` reports the expected
   version and that the downloaded `Assemblies/QudJP.dll` SHA256 matches the
   release ZIP DLL. Do not mark the release GO if the public Workshop download
   still contains an older DLL.
   Also confirm the downloaded `Launch CavesOfQud (Rosetta).command` exists and
   is executable in the downloaded `QudJP` folder. The launcher should guide
   player-facing failures with macOS dialogs rather than requiring manual script
   edits.
   On the default macOS Steam library, the downloaded Workshop folder is usually
   `~/Library/Application Support/Steam/steamapps/workshop/content/333640/3718988020/`.
4. Launch the game, enable only QudJP for the smoke pass, and restart.
   - On Apple Silicon, launch through the downloaded Rosetta wrapper. A native
     ARM64 run with `mprotect returned EACCES` or zero patched Harmony methods
     does not count as a valid smoke pass.
5. Confirm the Mod Manager lists QudJP with the expected version and preview.
6. Confirm the Options screen and one short conversation render Japanese text
   and CJK glyphs correctly.
7. Check fresh logs under `~/Library/Logs/Freehold Games/CavesOfQud/` for QudJP
   build markers, missing glyph warnings, compile errors, or `MODWARN`.
   Platform SDK noise such as local `GalaxyCSharpGlue` load failures may be
   recorded in notes, but do not treat it as a QudJP release blocker without a
   QudJP-owned marker, compile error, warning, or exception in the same fresh
   log.
8. Record the smoke result in the dated release evidence file copied from
   `docs/reports/templates/workshop-release.md`.
9. Publish the draft GitHub Release only after the Steam Workshop upload and
   minimum post-publish checks are acceptable.

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
