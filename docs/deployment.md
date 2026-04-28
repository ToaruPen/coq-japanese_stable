# Mod Deployment Guide

How to deploy the QudJP mod to the Caves of Qud game directory.

For Steam Workshop publishing, use `docs/release.md` after local deployment and validation pass.

---

## Prerequisites

- Caves of Qud installed (Steam, macOS / Windows / WSL2 / Linux)
- `QudJP.dll` built via `dotnet build`

---

## Deployment Methods

### Method 1: sync_mod.py (Recommended)

Always clean + full rebuild before deploying. Incremental builds may ship stale DLL artifacts.

```bash
dotnet clean Mods/QudJP/Assemblies/QudJP.csproj
dotnet build Mods/QudJP/Assemblies/QudJP.csproj --no-incremental
python3.12 scripts/sync_mod.py
```

`sync_mod.py` requires Python `>=3.12` per `pyproject.toml`. `python3` may resolve to an older interpreter on macOS, so prefer `python3.12` unless your shell already points `python3` at Python 3.12+.

`sync_mod.py` resolves a platform-appropriate default destination on macOS / Windows / WSL2 / Linux. It uses `rsync` when available and otherwise falls back to a pure-Python copy implementation.

**Dry run** (preview without copying):

```bash
python3.12 scripts/sync_mod.py --dry-run
```

**Exclude fonts** (faster when fonts have not changed):

```bash
python3.12 scripts/sync_mod.py --exclude-fonts
```

**Override the destination** (non-standard install paths):

```bash
python3.12 scripts/sync_mod.py --destination /path/to/Mods/QudJP
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
available, use `python3.12 scripts/sync_mod.py` instead; it applies the same
deployment filtering.

---

## Deployed Files

The game requires these deployed files:

| File | Purpose |
|------|---------|
| `manifest.json` | Mod metadata (ID, title, version) |
| `preview.png` | Workshop/mod-manager preview image referenced by `manifest.json` |
| `Bootstrap.cs` | Game-compiled loader shim — discovers and initializes QudJP.dll |
| `Assemblies/QudJP.dll` | Pre-compiled Harmony patch DLL |
| `Localization/` | XML translation files, JSON dictionaries, and text corpus assets |
| `Fonts/` | CJK font for TextMeshPro rendering + SIL OFL license |

### Files That Must NOT Be Deployed

| File | Reason |
|------|--------|
| `*.cs` (except `Bootstrap.cs`) | Game's Unity/Mono compiler attempts to compile them — `Bootstrap.cs` is the intentional exception as it IS meant to be game-compiled |
| `*.csproj`, `*.sln` | Build configuration files (not needed by the game) |
| `*.pdb` | Debug symbols (not needed by the game) |
| `bin/`, `obj/` | Build artifacts |
| `src/` | Source code directory |
| `QudJP.Tests/` | Test project |
| `QudJP.Analyzers/` | Roslyn analyzer project |
| `AGENTS.md` | Development documentation |

> **Critical**: The game's mod system automatically attempts to compile any `.cs` file found in the mod directory. `Bootstrap.cs` is intentionally game-compiled (it uses C# ≤9 syntax to bootstrap the pre-built DLL). All other `.cs` source files must NOT be deployed, as C# 10+ syntax will cause compilation errors (CS8652, CS1514).

---

## Deployment Target Paths

- macOS Steam: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\QudJP\`
- WSL2: `/mnt/c/Users/<name>/AppData/LocalLow/Freehold Games/CavesOfQud/Mods/QudJP/`
- Linux: `~/.config/unity3d/Freehold Games/CavesOfQud/Mods/QudJP/`

---

## Post-Deployment Verification

1. Launch the game
2. Confirm **"Caves of Qud 日本語化"** appears in the Mod Manager
3. Set the mod to ENABLED
4. Restart the game and verify the Options screen displays Japanese text

Inventory / equipment を含む表示確認は、`docs/RULES.md` の runtime evidence ルールに従って fresh log と再現メモを残してください。

### Apple Silicon / Rosetta

- On Apple Silicon, in-game verification must run under Rosetta 2
- Use `scripts/launch_rosetta.sh` or `Launch CavesOfQud (Rosetta).command`
- Do not use native ARM64 runtime logs as localization observability evidence

### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| FAILED + CS8652/CS1514 errors | `.cs` source files were deployed | Re-deploy with `sync_mod.py` (excludes source files) |
| Mod not listed | `manifest.json` not deployed | Verify `manifest.json` exists at the deploy target |
| Japanese text shows as □ (tofu) | CJK font not bundled | Verify Fonts directory is deployed |
| DLL load error | `QudJP.dll` not built | Run `dotnet build` then re-deploy |
| No QudJP traces in Player.log | Bootstrap.cs not deployed or failed to compile | Verify `Bootstrap.cs` exists in game `Mods/QudJP/` directory; check Player.log for compile errors |

---

## L3 Testing (In-Game Verification)

Manual checks that cannot be covered by automated tests (L1/L2):

- [ ] On Apple Silicon, launch via Rosetta before collecting evidence
- [ ] "Caves of Qud 日本語化" appears in the Mod Manager
- [ ] Options screen displays Japanese text
- [ ] Character creation screen is localized
- [ ] Japanese characters render correctly (no □ tofu)
- [ ] Player.log contains no Missing glyph / encoding errors
- [ ] Inventory / equipment 表示確認の fresh log と再現メモを残した
