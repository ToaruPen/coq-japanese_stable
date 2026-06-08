# Mod Deployment Guide

How to deploy the QudJP mod to the Caves of Qud game directory.

For Steam Workshop publishing, use `docs/release.md` after local deployment and validation pass.

---

## Prerequisites

- Caves of Qud installed (Steam, macOS / Windows / WSL2 / Linux)
- `QudJP.dll` built via `just build` or `just rebuild`
- `just` and `uv` installed for the recommended deployment recipes

---

## Build Flavors

`just deploy-mod` is the normal local deployment path. It rebuilds the same
shipping-style DLL used for Workshop packaging and keeps verbose runtime probe
logs disabled.

Use `just deploy-dev` only when you are actively investigating runtime behavior
and need dev-only probe logs. Development builds are for local diagnosis; do not
use them for Steam Workshop staging, release ZIPs, or player-facing uploads.

When deploying a merged PR, review fix, or any other specific commit for
runtime verification, create or use a clean worktree at that commit before
running `just deploy-dev`. Do not deploy from a dirty coordination checkout:
stale local edits can produce a DLL that does not match the commit being
verified.

```bash
# Shipping-style local deployment
just deploy-mod

# Local diagnostic deployment with dev-only probes enabled
just deploy-dev
```

## Deployment Methods

### Method 1: just deploy-mod (Recommended)

Always clean + full rebuild before deploying. Incremental builds may ship stale DLL artifacts.

```bash
just deploy-mod
```

`just deploy-mod` wraps `sync_mod.py`, which requires Python `>=3.12` per
`pyproject.toml`.

`sync_mod.py` resolves a platform-appropriate default destination on macOS / Windows / WSL2 / Linux. It uses `rsync` when available and otherwise falls back to a pure-Python copy implementation.

**Dry run** (preview without copying):

```bash
just sync-mod-dry-run
```

**Exclude fonts** (faster when fonts have not changed):

```bash
just sync-mod-exclude-fonts
```

**Override the destination** (non-standard install paths):

```bash
just deploy-mod-to /path/to/Mods/QudJP
```

### Method 2: Manual Copy

```bash
GAME_MODS="$HOME/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods"

# Remove previous deployment
rm -rf "$GAME_MODS/QudJP"

# Copy only required files
mkdir -p "$GAME_MODS/QudJP/Assemblies"
cp Mods/QudJP/manifest.json "$GAME_MODS/QudJP/"
cp Mods/QudJP/preview.png "$GAME_MODS/QudJP/"
cp Mods/QudJP/Bootstrap.cs "$GAME_MODS/QudJP/"
cp Mods/QudJP/Assemblies/QudJP.dll "$GAME_MODS/QudJP/Assemblies/"
mkdir -p "$GAME_MODS/QudJP/Localization"
rsync -a --prune-empty-dirs \
  --include='*/' \
  --include='*.xml' \
  --include='*.json' \
  --include='*.txt' \
  --exclude='*' \
  Mods/QudJP/Localization/ "$GAME_MODS/QudJP/Localization/"
```

The filtered `rsync` step copies only shipped localization assets and skips
development-only markdown such as `AGENTS.md` and `README.md`. If `rsync` is not
available, use `just sync-mod` instead; it applies the same deployment
filtering through `sync_mod.py`.

---

## Deployed Files

The game requires these deployed files:

| File | Purpose |
|------|---------|
| `manifest.json` | Mod metadata (ID, title, version) |
| `preview.png` | Workshop/mod-manager preview image referenced by `manifest.json` |
| `Bootstrap.cs` | Game-compiled loader shim - discovers and initializes QudJP.dll |
| `Assemblies/QudJP.dll` | Pre-compiled Harmony patch DLL |
| `Localization/` | XML translation files, JSON dictionaries, and text corpus assets |
| `Fonts/` | CJK font for TextMeshPro rendering + SIL OFL license |

### Files That Must NOT Be Deployed

| File | Reason |
|------|--------|
| `*.cs` (except `Bootstrap.cs`) | Game's Unity/Mono compiler attempts to compile them - `Bootstrap.cs` is the intentional exception as it IS meant to be game-compiled |
| `*.csproj`, `*.sln` | Build configuration files (not needed by the game) |
| `*.pdb` | Debug symbols (not needed by the game) |
| `bin/`, `obj/` | Build artifacts |
| `src/` | Source code directory |
| `QudJP.Tests/` | Test project |
| `QudJP.Analyzers/` | Roslyn analyzer project |
| `AGENTS.md` | Development documentation |

> **Critical**: The game's mod system automatically attempts to compile any `.cs` file found in the mod directory. `Bootstrap.cs` is intentionally game-compiled (it uses C# <=9 syntax to bootstrap the pre-built DLL). All other `.cs` source files must NOT be deployed, as C# 10+ syntax will cause compilation errors (CS8652, CS1514).

---

## Deployment Target Paths

- macOS Steam: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\QudJP\`
- WSL2: `/mnt/c/Users/<name>/AppData/LocalLow/Freehold Games/CavesOfQud/Mods/QudJP/`
- Linux: `~/.config/unity3d/Freehold Games/CavesOfQud/Mods/QudJP/`

---

## Post-Deployment Verification

1. Launch the game
2. Confirm **"Caves of Qud Japanese Mod"** appears in the Mod Manager
3. Set the mod to ENABLED
4. Restart the game and verify the Options screen displays Japanese text

For inventory / equipment display checks, follow the runtime evidence rules in
`docs/RULES.md` and keep a fresh log plus reproduction notes.

### Apple Silicon / Rosetta

- On Apple Silicon, repo-owned in-game verification must run under Rosetta 2
- For local repo verification, use `scripts/launch_rosetta.sh` or the root
  `Launch CavesOfQud (Rosetta).command`
- For player-facing builds, the release ZIP and Workshop content include
  `QudJP/Launch CavesOfQud (Rosetta).command`; launch through that wrapper
  from the installed `QudJP` folder
- The Workshop launcher is intended for Finder double-click use. If Rosetta 2
  is missing, it offers to install Rosetta 2 through a macOS dialog. It first
  checks the default Steam library and the Steam library implied by the
  downloaded Workshop folder. If it is launched from an installed
  `CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP` folder, it also
  infers the parent `CoQ.app` directly. If none of those paths contains Caves of
  Qud, it opens a file picker so the user can select `CoQ.app`.
- On macOS with the default Steam library, the subscribed Workshop item is
  normally under:
  `~/Library/Application Support/Steam/steamapps/workshop/content/333640/3718988020/`
  If Steam uses another library, look under that library's
  `steamapps/workshop/content/333640/3718988020/` directory instead.
- `arch -x86_64 .../CoQ` is a one-shot launch path. It does not persist to
  future Steam launches, so Apple Silicon users should use the wrapper each
  time unless they have separately configured a reliable Rosetta launch path.
- QudJP does not automatically bundle or overwrite the game's `0Harmony.dll`;
  Rosetta 2 is the recommended player-facing workaround. Release and Workshop
  builds may include opt-in `Install Native Apple Silicon Harmony.command` and
  `Restore Game Harmony.command` helpers. These helpers must only mutate the
  game file after explicit user confirmation, must create a backup first, must
  restore only from the QudJP-named backup, and must fall back to a file picker
  if they cannot infer the target from their installed location or the default
  Steam path. Advanced users may choose to back up the game file and replace
  `CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll` with the
  `net48/0Harmony.dll` from Harmony `v2.4.2.0` to run native ARM64. Treat this
  as a user-managed game-file change; Steam verification, reinstall, or game
  updates may revert it.
- Do not use native ARM64 runtime logs as localization observability evidence
- If `Player.log` contains `mprotect returned EACCES` or QudJP reports
  `Harmony patching complete: 0 method(s) patched`, treat that run as native
  ARM64 with an unsupported game-bundled Harmony runtime and ask for a
  Rosetta-backed retry before triaging localization routes.

### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| FAILED + CS8652/CS1514 errors | `.cs` source files were deployed | Re-deploy with `just deploy-mod` (excludes source files) |
| Mod not listed | `manifest.json` not deployed | Verify `manifest.json` exists at the deploy target |
| Japanese text shows as tofu squares | CJK font not bundled | Verify Fonts directory is deployed |
| DLL load error | `QudJP.dll` not built | Run `just deploy-mod` |
| No QudJP traces in Player.log | Bootstrap.cs not deployed or failed to compile | Verify `Bootstrap.cs` exists in game `Mods/QudJP/` directory; check Player.log for compile errors |
| Apple Silicon: title/UI text stays English and `mprotect returned EACCES` appears | Native ARM64 launch blocked Harmony patching through the game-bundled `0Harmony.dll` | Launch through `Launch CavesOfQud (Rosetta).command` or `arch -x86_64 .../CoQ`; advanced users may back up and replace the game `0Harmony.dll` with Harmony 2.4.2 |

---

## L3 Testing (In-Game Verification)

Manual checks that cannot be covered by automated tests (L1/L2):

- [ ] On Apple Silicon, launch via Rosetta before collecting evidence
- [ ] "Caves of Qud Japanese Mod" appears in the Mod Manager
- [ ] Options screen displays Japanese text
- [ ] Character creation screen is localized
- [ ] Japanese characters render correctly (no tofu squares)
- [ ] Player.log contains no Missing glyph / encoding errors
- [ ] Kept a fresh log and reproduction notes for inventory / equipment display checks
