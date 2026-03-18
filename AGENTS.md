# QudJP — Caves of Qud Japanese Localization Mod

**Repository**: ToaruPen/Caves-of-Qud_Japanese
**Game version target**: 2.0.4

## Project Overview

QudJP is a Japanese localization mod for Caves of Qud. It ships:
- A Harmony-patched DLL that intercepts grammar/text rendering at runtime
- XML translation files loaded by the game's merge system
- Python scripts for translation tooling and validation

## Directory Structure

```
Caves-of-Qud_Japanese/
├── Mods/QudJP/
│   ├── Assemblies/          # C# Harmony patch DLL + tests
│   │   ├── src/             # Production code
│   │   ├── QudJP.csproj     # net48 mod DLL project
│   │   └── QudJP.Tests/     # net10.0 test project
│   ├── Localization/        # XML translation files (*.jp.xml)
│   └── manifest.json        # Mod metadata
├── scripts/                 # Python tooling
│   └── tests/               # pytest test suite
├── pyproject.toml           # Python project config (Ruff, pytest)
└── .editorconfig            # Editor formatting rules
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Mod DLL | C# / .NET 4.8 (Unity Mono) |
| Harmony patches | HarmonyLib 0Harmony 2.2.2.0 (game-bundled, runtime) |
| Test Harmony | HarmonyLib NuGet 2.4.2 (test-only) |
| Translations | XML with game merge system |
| Tooling | Python 3.12+ |
| Linter (C#) | Roslyn analyzers, TreatWarningsAsErrors |
| Linter (Python) | Ruff select=ALL, McCabe C≤10 |

## Build Commands

```bash
# Build mod DLL
dotnet build Mods/QudJP/Assemblies/QudJP.csproj

# Run all tests (when test project exists)
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj

# Pure logic tests only (no Harmony, no Unity)
dotnet test --filter TestCategory=L1

# Harmony integration tests only (no Unity)
dotnet test --filter TestCategory=L2

# Lint Python
ruff check scripts/

# Test Python
pytest scripts/tests/
```

## Test Architecture (3 Layers)

### L1 — Pure Logic
- No HarmonyLib dependency
- No UnityEngine dependency
- Tests pure C# logic: string manipulation, grammar rules, data parsing
- Tag: `[Category("L1")]`
- Fast, run on every commit

### L2 — Harmony Integration
- HarmonyLib NuGet 2.4.2 allowed
- No UnityEngine dependency
- Tests that patches apply correctly to DummyTarget classes and, where safe, against real `Assembly-CSharp.dll` method resolution/static behavior without Unity runtime
- Tag: `[Category("L2")]`
- Run on every commit

### L3 — Game Smoke
- Requires actual game launch
- Manual only, never in CI
- Verifies end-to-end rendering in-game

## Testing Rules

**DummyTarget pattern** (critical):
- NEVER instantiate types from Assembly-CSharp.dll in tests
- Assembly-CSharp.dll may be referenced in tests for target resolution, signature checks, and Unity-runtime-free static behavior
- Create test doubles with matching method signatures instead
- Example: `class DummyGrammar { public string Pluralize(string s) => s + "s"; }`

**Layer boundaries**:
- L1 tests: zero references to HarmonyLib
- L2 tests: zero references to UnityEngine; direct Assembly-CSharp instantiation remains forbidden
- L3 tests: manual game launch only, no automation

## Code Style

### C#
- `<Nullable>enable</Nullable>` — null safety enforced
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — zero warnings
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` — style in CI
- All game DLL references: `<Private>false</Private>` (don't copy to output)

### Python
- Python 3.12+
- Ruff: `select = ["ALL"]`, `max-complexity = 10`
- Type hints required on all public functions
- Docstrings: Google style
- Script naming: `verb_noun.py` (e.g., `sync_translations.py`)

## Commit Convention

Conventional Commits in English:
```
type(scope): description

Types: feat, fix, docs, style, refactor, test, chore
Scopes: patch, xml, scripts, ci, deps
```

Examples:
- `feat(patch): add grammar postfix for verb conjugation`
- `fix(xml): correct creature name encoding in Creatures.jp.xml`
- `test(patch): add L2 test for GrammarPatch prefix`

## Mod Deployment

See [docs/deployment.md](docs/deployment.md) for full deployment instructions.

**Key points**:
- Deploy with `python3 scripts/sync_mod.py` (recommended)
- The game needs `manifest.json` + `Bootstrap.cs` + `Assemblies/QudJP.dll` + `Localization/` + `Fonts/`
- NEVER deploy `.cs` source files (the game's Unity/Mono compiler will attempt to compile them and fail)

## Runtime Logs

- macOS Steam runtime logs are under `~/Library/Logs/Freehold Games/CavesOfQud/`
- Current run: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- Previous run: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log`
- Mod build logs are under `~/Library/Application Support/Freehold Games/CavesOfQud/` (`build_log.txt`, `build_log.txt.prev`)
- On Apple Silicon, collect L3 evidence from Rosetta launches only; use `scripts/launch_rosetta.sh` or `Launch CavesOfQud (Rosetta).command`
- When verifying the mod, check `Player.log` for `[QudJP] Build marker`, probe lines like `[QudJP] PopupTitleProbe` / `[QudJP] DescriptionInventoryActionProbe`, and errors such as `MODWARN` or `Missing glyph`

## Constraints

- For rendering, localization, and UI regressions, prefer root-cause investigation and durable fixes over stopgap workarounds. Temporary fallbacks are acceptable only to gather evidence or unblock diagnosis, and should be removed once the underlying failure path is understood.
- No code from the legacy project (clean-room implementation)
- Assembly-CSharp.dll must NOT be committed to the repo
- Distribution via GitHub only (no Steam Workshop)
- Game DLL references are local paths; contributors must own the game
- Blueprint/Conversation IDs must match game version 2.0.4 exactly
