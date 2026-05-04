# Changelog

All notable changes to QudJP will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

---

## [0.2.1] - 2026-05-05

### Fixed

- Improve Japanese coverage for generated ability names, active effects,
  cooking effects, message log text, and village history descriptions.
- Improve Japanese translation coverage for multiline item descriptions, popup
  messages, and slimy/statue messages.

---

## [0.2.0] - 2026-05-04

### Added

- Broad runtime translation coverage for ability bars, status screens, trade,
  tinkering, quests, journals, mod management, world map, save/load, death,
  score, achievement, popup, and conversation UI routes.
- Japanese procedural text coverage for Markov corpus text, procedural names,
  titles, body-part variants, world parts, world mods, cooking/status effects,
  death reasons, historical narratives, and Sultan Resheph annals.
- CJK font packaging and runtime fallback setup for Workshop/mod-manager use.
- Release, Steam Workshop, translation-token, glossary-consistency, XML source
  markup, runtime smoke, and agent workflow validation tooling.
- Sultan Resheph history translation in `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` (#420 PR1).
- Build-time pipeline at `scripts/{extract,validate,translate,merge}_annals_patterns.py` and `scripts/tools/AnnalsPatternExtractor/` for extracting and translating regex/template pairs from `XRL.Annals/*.cs` (#420 PR1).
- `JournalPatternTranslator` now supports ordered multi-file pattern dictionary load (`SetPatternFilesForTests(params string[])`).

### Changed

- Moved many dynamic translation routes from sink-side fallback toward
  producer/owner-side translation so rendered text keeps markup and route
  context more reliably.
- Normalized terminology and glossary usage across item names, factions,
  mutations, books, cooking text, UI labels, and static XML/JSON dictionaries.
- Expanded C# L1/L2/L2G and Python test coverage for runtime routes,
  placeholder parity, markup preservation, scanner output, release packaging,
  and Workshop staging.

### Fixed

- Preserved color markup, placeholder tokens, literal escape markers, and
  information-bearing variables across translated XML and JSON assets.
- Repaired remaining untranslated or unstable UI routes found through runtime
  log audits, including popup handoffs, player status bar refresh, chargen
  summaries, display-name tails, journal/lair text, and ability HUD text.
- Cleaned XML validation false positives and stale generated-artifact scanning
  behavior.

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

[0.2.1]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.1
[0.2.0]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.0
[0.1.0]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.1.0
