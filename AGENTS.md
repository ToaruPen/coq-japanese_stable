# QudJP

## Why

QudJP is the Japanese localization mod for Caves of Qud `2.0.4`. The repo contains the shipped DLL, localization assets, and the tooling used to validate and deploy them.

## What

- Read the scoped guide for the area you are changing:
  - `Mods/QudJP/Assemblies/AGENTS.md` for C# patches, helpers, and tests
  - `Mods/QudJP/Localization/AGENTS.md` for XML and JSON localization assets
  - `scripts/AGENTS.md` for Python and shell tooling
- Source of truth:
  - behavior: `Mods/QudJP/Assemblies/QudJP.Tests/`
  - layer boundaries: `docs/test-architecture.md`
  - workflow rules and operating constraints: `docs/RULES.md`
  - runtime evidence: fresh logs under `~/Library/Logs/Freehold Games/CavesOfQud/`
  - Steam Workshop release procedure: `docs/release.md`

## How

- If a stale note conflicts with tests or fresh runtime evidence, follow tests first.
- Core commands:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
ruff check scripts/
uv run pytest scripts/tests/
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/sync_mod.py
```

- Decompiled game source lives in `~/dev/coq-decompiled_stable/` and must never be committed.
- Do not commit `Assembly-CSharp.dll` or other game binaries.
- Only the built DLL and localization assets ship; `.cs` source files do not.
