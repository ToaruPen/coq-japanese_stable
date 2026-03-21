# QudJP AGENTS.md

## WHY

- This repo is a Japanese localization mod for Caves of Qud, targeting game version `2.0.4`.
- The shipped mod consists of a Harmony-patched DLL, XML localization assets, and Python tooling for validation and sync.

## WHAT

### Start Here

- Read this root file first, then the scoped `AGENTS.md` for the area you will edit:
  - `Mods/QudJP/Assemblies/AGENTS.md`
  - `Mods/QudJP/Localization/AGENTS.md`
  - `scripts/AGENTS.md`
- Tasks that touch dynamic or procedural text use `docs/logic-required-policy.md` as the default workflow.
  - This includes work that starts from `Player.log`, `missing key`, `no pattern`, display-name composition, popup templates, inventory suffixes, or message-log templates.
  - Read `docs/logic-required-policy.md` before analyzing logs or adding route/template logic.
- Manual L3 verification on Apple Silicon uses Rosetta only.
  - Launch with `scripts/launch_rosetta.sh` or `Launch CavesOfQud (Rosetta).command`.

### Repo Map

- `Mods/QudJP/Assemblies/`
  - C# mod DLL (`QudJP.csproj`, `net48`) and test project (`QudJP.Tests`, `net10.0`).
- `Mods/QudJP/Localization/`
  - XML translation files and JSON dictionaries loaded by the mod/game.
- `scripts/`
  - Python utilities for sync, validation, extraction, and tests.
- `docs/`
  - Canonical process and verification docs.

### Canonical Docs

- `README.md`
  - project overview, install/build/test commands
- `docs/contributing.md`
  - contributor workflow and CI expectations
- `docs/logic-required-policy.md`
  - required policy for dynamic/procedural text
- `docs/test-architecture.md`
  - L1/L2/L2G/L3 test boundaries
- `docs/deployment.md`
  - deployment procedure
- `docs/inventory-verification.md`
  - inventory/equipment L3 verification flow

## HOW

### Build And Test

- Mod build: `dotnet build Mods/QudJP/Assemblies/QudJP.csproj`
- C# tests: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
- L1 only: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1`
- L2 only: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2`
- Python lint: `ruff check scripts/`
- Python tests: `pytest scripts/tests/`
- Runtime-facing C# or localization changes normally stay incomplete until the built mod is re-deployed to the game directory.
- The canonical deploy path is `python3.12 scripts/sync_mod.py` after a successful build, or another Python `>=3.12` interpreter that satisfies `pyproject.toml`; see `docs/deployment.md` for the shipped file set and target path.

### Dynamic-Text Workflow

- `Player.log` is evidence, not the source of truth for closing dynamic strings.
- Do not treat one observed log line as the translation unit by default; name the family first, then confirm that family from upstream evidence.
- For logic-required text, first locate the upstream generator or asset, then implement the smallest slot-aware route/template change, then add L1/L2 coverage that matches that composition boundary.
- Stable leaf strings, fixed labels, and atomic names can still be handled as dictionary/XML assets when the text is genuinely static.

### Runtime Evidence

- Current log: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- Previous log: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log`
- Build logs: `~/Library/Application Support/Freehold Games/CavesOfQud/build_log.txt`
- Useful runtime markers:
  - `[QudJP] Build marker`
  - `DynamicTextProbe/v1`
  - `MessagePatternTranslator: no pattern`
  - `missing key`
  - `MODWARN`
  - `Missing glyph`

### Repo Facts That Affect Edits

- `Assembly-CSharp.dll` must not be committed.
- Game DLL references are local contributor paths; contributors must own the game.
- Blueprint and Conversation IDs must match game version `2.0.4`.
- Deployment ships built assets and localization data; `.cs` source files are not deployed into the game mod directory.

### Working Conventions

- Commit messages use Conventional Commits in English, typically with scopes such as `patch`, `xml`, `scripts`, `ci`, or `deps`.
- Detailed C# patch/test guidance lives in `Mods/QudJP/Assemblies/AGENTS.md`.
- Detailed XML/encoding guidance lives in `Mods/QudJP/Localization/AGENTS.md`.
- Detailed Python guidance lives in `scripts/AGENTS.md`.
