# Changelog

All notable changes to QudJP will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [0.1.0] — 2026-03-11

### Added

**Project scaffolding**
- Repository structure: `Mods/QudJP/`, `scripts/`, `docs/`, `.github/`
- `manifest.json` with mod metadata (Id, Title, Author, Version, Tags)
- `pyproject.toml` with Ruff `select = ["ALL"]` and pytest configuration
- `.editorconfig` for consistent formatting across editors
- GitHub Actions CI pipeline (Ubuntu 24.04, .NET 8.0.x + 10.0.x, Python 3.12)

**C# translation infrastructure**
- `Translator` — JSON dictionary loader with lazy initialization and `ConcurrentDictionary` cache (net48 compatible)
- `ColorCodePreserver` — Preserves and restores `{{W|...}}`, `&X`, `^Y` color codes around translation
- `QudJPMod` — Harmony patch entry point with fail-fast initialization

**Harmony patches (11 patches, 8 Grammar methods)**
- `OptionsLocalizationPatch` — Translates options screen labels via Postfix on `Show()`
- `MainMenuLocalizationPatch` — Translates main menu button labels via Postfix on `Show()`
- `PopupTranslationPatch` — Translates popup titles, body text, and button labels via `HarmonyTargetMethods` Prefix
- `UITextSkinTranslationPatch` — Common UI text translation via `UITextSkin`
- `GrammarPatch` — Neutralizes English grammar for Japanese: removes articles, disables pluralization, appends `の`, and adapts list formatting. Covers 8 methods: `A`, `Pluralize`, `MakePossessive`, `MakeAndList`, `MakeOrList`, `SplitOfSentenceList`, `InitCaps`, `CardinalNumber`
- `ConversationDisplayTextPatch` — Translates conversation node display text via Postfix
- `GetDisplayNamePatch` — Translates item/creature display names
- `CharGenLocalizationPatch` — Translates character generation UI
- `InventoryLocalizationPatch` — Translates inventory screen
- `MessageLogPatch` — Translates message log entries using regex pattern matching
- `ProceduralTextPatch` — Translates `HistoricStringExpander` output

**Roslyn analyzers**
- `EnableNETAnalyzers`, `AnalysisLevel=latest-all` — full .NET analyzer suite enabled
- SonarAnalyzer.CSharp — additional static analysis rules
- Custom analyzers: QJ001 (catch-all suppression), QJ002 (null-coalescing in TargetMethod), QJ003 (empty catch block)

**Python tooling**
- `check_encoding.py` — UTF-8 BOM detection, CRLF detection, mojibake character detection
- `validate_xml.py` — XML parse validation, color code balance check, duplicate ID/Name detection, empty `<text>` detection
- `diff_localization.py` — Translation coverage comparison between base game XML and localized XML, with `--summary` and `--missing-only` modes
- `extract_base.py` — Copies game `StreamingAssets/Base/` XML to local `references/Base/`
- `sync_mod.py` — Include-first deployment via rsync (87 files, excludes source code)
- `build_release.py` — Builds Release DLL and creates `dist/QudJP-v{version}.zip`

**Test suite**
- 101 C# NUnit tests (L1 pure logic + L2 Harmony integration)
- 83 Python pytest tests across all scripts
- 3-layer test architecture: L1 (no HarmonyLib), L2 (no UnityEngine), L3 (manual game smoke)
- DummyTarget pattern for L2 tests — no direct instantiation of `Assembly-CSharp.dll` types

**Legacy XML migration**
- 35 XML translation files (66,306 lines) migrated from legacy project
- 35 JSON dictionary files (32,836 lines) migrated from legacy project
- All files normalized to UTF-8 without BOM, LF line endings, zero mojibake characters

**Fail-fast error handling**
- Initialization failures (`LoadTranslations`, `ApplyHarmonyPatches`) raise exceptions immediately
- `TargetMethod()` resolution failures log `Trace.TraceError` and return `null`
- Runtime translation failures log `Trace.TraceError` and return original text (no exception thrown)

**Documentation**
- `docs/glossary.csv` — 84-entry terminology glossary with English, Japanese, Short, Notes, Status columns
- `docs/game-data-analysis.md` — Analysis of base game XML structure and translatable string counts
- `docs/ilspy-analysis.md` — ILSpy decompilation findings for key game types
- `docs/poc-results.md` — Proof-of-concept results for Harmony + NUnit on macOS ARM64
- `docs/migration-plan.md` — Legacy XML migration plan and execution record
- `docs/translation-coverage-report.md` — Translation coverage by category (ObjectBlueprints 77%, Conversations 97.5%)
- `docs/deployment.md` — Deployment guide

---

[0.1.0]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.1.0
