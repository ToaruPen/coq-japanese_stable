# QudJP

Japanese localization mod for Caves of Qud (`2.0.4`).

## Contributor index

Read the scoped guide for the area you are changing:

- `Mods/QudJP/Assemblies/AGENTS.md` — C# patches, helpers, and tests
- `Mods/QudJP/Localization/AGENTS.md` — XML/JSON localization assets
- `scripts/AGENTS.md` — Python and shell tooling

## Source of truth

- **Behavior:** tests in `Mods/QudJP/Assemblies/QudJP.Tests/`
- **Layer boundaries:** `docs/test-architecture.md`
- **Runtime evidence:** current game logs under `~/Library/Logs/Freehold Games/CavesOfQud/`

If a stale design note conflicts with tests or runtime evidence, follow tests and fresh runtime evidence from current logs.

## Core commands

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
ruff check scripts/
pytest scripts/tests/
python3.12 scripts/sync_mod.py
```

## Translation workflow

- Before adding dictionary or XML entries, confirm the string is a true stable leaf using existing tests and current runtime evidence.
- If a route is dynamic, procedural, or observation-only at the sink, prefer producer-side or mid-pipeline fixes over sink-side dictionary translation.
- Trace the upstream producer first when the owning route is unclear.
- Treat `Player.log` as runtime evidence, not as the behavior source of truth.

## Runtime evidence

- Current log: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- Previous log: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log`
- Build log: `~/Library/Application Support/Freehold Games/CavesOfQud/build_log.txt`
- L3 launch helper: `scripts/launch_rosetta.sh`

Useful markers: `[QudJP] Build marker`, `DynamicTextProbe/v1`, `missing key`, `MODWARN`.

## Game source reference

Decompiled game source lives in `~/Dev/coq-decompiled/` and must never be committed.

```bash
scripts/decompile_game_dll.sh
scripts/decompile_game_dll.sh --list
COQ_DECOMPILED_DIR=/path/to/dir scripts/decompile_game_dll.sh
```

## Repo constraints

- Never commit `Assembly-CSharp.dll` or other game binaries.
- Contributors must own the game locally for DLL-assisted work.
- Blueprint and conversation IDs must match game version `2.0.4`.
- `.cs` source files are not deployed; only the built DLL and localization assets ship.
