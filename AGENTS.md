# QudJP

Japanese localization mod for Caves of Qud (game version `2.0.4`).
Harmony-patched DLL + XML localization assets + Python tooling.

## Start Here

1. Read this file, then the scoped AGENTS.md for the area you edit:
   - `Mods/QudJP/Assemblies/AGENTS.md` (C# patches and tests)
   - `Mods/QudJP/Localization/AGENTS.md` (XML/JSON dictionaries)
   - `scripts/AGENTS.md` (Python tooling)
2. For dynamic/procedural text: read `docs/logic-required-policy.md` first.
3. For architecture decisions: read `docs/producer-first-design.md`.

## Build and Test

```bash
# Mod build
dotnet build Mods/QudJP/Assemblies/QudJP.csproj

# C# tests (all / L1 only / L2 only)
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2

# Python
ruff check scripts/
pytest scripts/tests/

# Deploy built mod to game directory
python3.12 scripts/sync_mod.py
```

## Translation Workflow

- **Before adding any translation**: check `docs/contract-inventory.json` for the route's contract type.
  - Leaf/MarkupLeaf → dictionary entry is appropriate.
  - Template/Builder/MessageFrame → implement translation logic, not a dictionary entry.
  - Route not registered → investigate upstream producer first. Do NOT add a dictionary entry.
- `Player.log` is evidence, not the source of truth. Name the family first, then confirm from upstream.
- Stable leaf strings and fixed labels can use dictionary/XML when the text is genuinely static.

## Game Source Reference

Decompiled game source lives in `~/Dev/coq-decompiled/` (outside repo, never committed).

```bash
# Regenerate decompiled sources (37 text-pipeline classes)
scripts/decompile_game_dll.sh

# List classes without decompiling
scripts/decompile_game_dll.sh --list

# Override output directory
COQ_DECOMPILED_DIR=/path/to/dir scripts/decompile_game_dll.sh
```

Use these files to trace upstream text producers, verify method signatures, and investigate unclaimed routes.

## Runtime Evidence

- Current log: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- Previous log: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log`
- Build log: `~/Library/Application Support/Freehold Games/CavesOfQud/build_log.txt`
- L3 verification: `scripts/launch_rosetta.sh` (Apple Silicon requires Rosetta)
- Key log markers: `[QudJP] Build marker`, `DynamicTextProbe/v1`, `MessagePatternTranslator: no pattern`, `missing key`, `MODWARN`

## Repo Constraints

- `Assembly-CSharp.dll` must NOT be committed (copyrighted game binary).
- Game DLL references are local contributor paths; contributors must own the game.
- Blueprint and Conversation IDs must match game version `2.0.4`.
- `.cs` source files are not deployed — only built DLL and localization data ship.

## Conventions

- Commit messages: Conventional Commits in English (scopes: `patch`, `xml`, `scripts`, `ci`, `deps`, `arch`).
- Tests: L1 (unit) / L2 (Harmony integration) / L2G (DLL-assisted) / L3 (runtime). See `docs/test-architecture.md`.
- Never delete or disable existing tests.

## Canonical Docs

| Doc | Purpose |
|-----|---------|
| `docs/producer-first-design.md` | Architecture: ContractRegistry + ClaimRegistry + audit sink |
| `docs/logic-required-policy.md` | Policy for dynamic/procedural text classification |
| `docs/test-architecture.md` | L1/L2/L2G/L3 test boundaries |
| `docs/contributing.md` | Contributor workflow and CI |
| `docs/deployment.md` | Deployment procedure |
