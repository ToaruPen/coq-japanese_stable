# Issue #420: HSE Narrative Pattern Auto-Extraction Pipeline — Design Spec

> **Scope:** PR1 of issue #420 — pipeline + Resheph 16 files. Subsequent PRs (PR2+) will extend coverage to the remaining 30 Annals files (sultans/babe/marriage/death/guild etc.).

> **Source of truth note:** This spec captures the design as of 2026-04-26. If implementation reveals contradictions, update tests/code first and reconcile this doc.

---

## 1. Goal & Scope

### Goal

Translate Caves of Qud's Sultan history (the Resheph entry in the Sultan Histories journal) into Japanese. Build on top of the translation pipeline established by issue #408 (HSE → walker → `JournalPatternTranslator`) by extracting regex/template pairs mechanically from the decompiled `XRL.Annals/Resheph*.cs` and translating them via the existing Codex CLI corpus pipeline.

### In Scope (PR1)

- Four-script Python pipeline (`extract` / `validate` / `translate` / `merge`)
- Repo-local C# console project (`scripts/tools/AnnalsPatternExtractor/`) for Roslyn AST extraction
- `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` (new sibling to `journal-patterns.ja.json`)
- `JournalPatternTranslator` extended to ordered multi-file load
- Test additions: 7 Python files, 4 L1 files, 1 L2 file
- Live-runtime evidence via the deterministic `wish reshephgospel` flow
- Documentation updates: `scripts/AGENTS.md`, `Localization/Dictionaries/AGENTS.md`, `CHANGELOG.md`, `.gitignore`
- Glossary additions: `Rebekah` / `Gyre` / `Qud` / `Tomb of the Eaters` / `Omonporch` / `Spindle`

### Out of Scope (deferred to PR2+)

- The remaining 30 Annals files (`FoundAsBabe`, `BloodyBattle`, `ChallengeSultan`, marriage/death/guild)
- Reorganization of the existing `world-gospels.ja.json` (issue #420 Open Question 4)
- CI integration of the **pipeline body** (`extract` / `validate` / `translate` / `merge` runtime invocation in CI) — issue #420 Open Question 3. Note: the Roslyn console project's `dotnet build` IS added to CI so its csproj does not silently rot — see §5.6.
- Automatic glossary construction (e.g. extracting from `world-factions.ja.json`)
- Pattern dictionary i18n beyond ja

### Non-Goals

- Issue-level AC "80% no pattern reduction" (achieved across PR2/PR3, not PR1)
- Fully automated translation pipeline — human review gate is permanent
- Roslyn semantic model / cross-file analysis — same-method local resolution only

---

## 2. Architecture

### System Overview

```text
[Build time, developer host]                              [Runtime, player game session]
─────────────────────────────────                         ──────────────────────────────────
  XRL.Annals/Resheph*.cs (decompiled)                      Generated History (post-HSE-expansion of <spice...>)
        │                                                   │
        ▼ extract_annals_patterns.py                        ▼ Harmony Postfix on
        │   → dotnet run AnnalsPatternExtractor              QudHistoryFactory.GenerateVillageEraHistory (#408)
        │                                                       │
        ▼ candidate JSON (status="pending")                     ▼ HistoricNarrativeDictionaryWalker
        │                                                       │   walks History.events
        ▼ human edit (status=accepted, ja_template)             ▼
        │                                                   HistoricNarrativeTextTranslator
        ▼ validate_candidate_schema.py                          │
        │   (placeholder count, status, ja_template required)   ▼
        ▼ translate_annals_patterns.py                       JournalPatternTranslator
        │   (Codex CLI batch, candidate_id+source_hash resume)  │  multi-file ordered load
        ▼ merge_annals_patterns.py                              │  [journal-patterns, annals-patterns]
        │   (raw + normalized collision; on conflict            │
        │    write merge_conflicts.json + nonzero exit)         ▼ regex match → {tN} per-capture
        ▼                                                       │   via Translator.Translate()
  Mods/QudJP/Localization/Dictionaries/                         ▼
  ├── journal-patterns.ja.json (existing 86)             Japanese gospel rendered
  └── annals-patterns.ja.json {"entries":[], "patterns":[...]}
```

### Key separation

- **Build-time and runtime are completely decoupled.** Build-time produces JSON only; the shipped DLL contains no Roslyn / tree-sitter code.
- Runtime change: `JournalPatternTranslator.LoadPatterns()` goes from single-file to **ordered list** `[journal-patterns.ja.json, annals-patterns.ja.json]` with hard-fail-on-malformed (matches existing single-file behavior, per `JournalPatternTranslator.cs:158-161`).
- The `{tN}` per-capture lookup mechanism (`JournalPatternTranslator.cs:677` `TranslateTemplateCapture()`) is reused unchanged. Captures are resolved through `Translator.Translate()` with ASCII-lowercase fallback. No re-entry into the pattern engine; no infinite-loop risk.

### Multi-file load patch

```csharp
// JournalPatternTranslator
internal static readonly string[] DefaultPatternAssetPaths = {
    "Dictionaries/journal-patterns.ja.json",
    "Dictionaries/annals-patterns.ja.json",
};

internal static void SetPatternFilesForTests(string[]? paths) {
    // null = reset to defaults; non-null = override list
    ...
}

// Existing API kept as a wrapper for backwards compatibility:
internal static void SetPatternFileForTests(string? path) =>
    SetPatternFilesForTests(path == null ? null : new[] { path });
```

`null` semantics for both APIs is "reset to defaults" — preserves the existing `ResetForTests()` contract (`JournalPatternTranslator.cs:87`).

### Dictionary schema (annals-patterns.ja.json)

```json
{
  "entries": [],
  "patterns": [
    {"pattern": "^...$", "template": "...", "route": "annals"}
  ]
}
```

`"entries": []` is **mandatory**: `Translator` loads all `Dictionaries/*.ja.json` and expects an `entries` array on every file (`Translator.cs:271, 274, 359`). Missing `entries` would cause load-time errors when `{tN}` lookups fire.

### Build-time pipeline directionality

Each script reads previous output, writes next. Stateless between runs. Re-runnable.

```text
extract → candidates_pending.json
        (human edits in place; status, ja_template, reason)
validate → read-only check
translate → in-place update of candidates_pending.json (resume via candidate_id + en_template_hash)
merge → emits annals-patterns.ja.json (or merge_conflicts.json + nonzero exit)
```

### Idempotency

- `extract` is deterministic given the same `--source-root` and `--include` glob; refuses to overwrite existing artifact unless `--force` (which creates a `.bak-YYYYMMDDHHMMSS`).
- `translate` skips candidates whose stored `en_template_hash` matches the current hash; only stale or empty ja_template trigger re-translation.
- `merge` produces deterministic output ordering (sort by `id` ascending). Re-run with same input produces byte-identical `annals-patterns.ja.json`.

---

## 3. Pipeline Components

### 3.0 Operator prerequisites

The build-time pipeline runs on a developer host, not in CI. An operator running the full pipeline needs:

- **.NET SDK** matching `10.0.x` (CI uses `10.0.x` matrix; local dev typically `10.0.100+`). No `global.json` pins a specific patch.
- **Python** `3.12+` with `pytest` and `ruff` available
- **Node.js** with `@ast-grep/cli` (already required by repo for other scanners)
- **`codex` CLI** authenticated via `codex login` (used by `translate_annals_patterns.py`; CI runners deliberately do not have this — see §5.6)
- **Decompiled game source** at `~/dev/coq-decompiled_stable/XRL.Annals/Resheph*.cs`. This directory is not part of the repo and must be regenerated via `scripts/decompile_game_dll.sh` if absent.
- **Apple Silicon hosts only**: Rosetta is required to run the live verification flow (Caves of Qud is x64-only; Rosetta is the standard target environment per `docs/RULES.md`).

### 3.1 AnnalsPatternExtractor (C# console)

- **Location:** `scripts/tools/AnnalsPatternExtractor/`
- **Stack:** net10.0, `Microsoft.CodeAnalysis.CSharp` NuGet
- **Precedent:** `Mods/QudJP/Assemblies/QudJP.Analyzers/QudJP.Analyzers.csproj` already uses Roslyn.
- **CLI:**

  ```bash
  dotnet run --project scripts/tools/AnnalsPatternExtractor -- \
      --source-root <decompiled-XRL.Annals-path> \
      --include "Resheph*.cs" \
      --output <json-path>
  ```

- **Logic:**
  1. Parse each `.cs` with `CSharpSyntaxTree.ParseText`
  2. Visitor finds `SetEventProperty("gospel"|"tombInscription", value)` calls inside `Generate()`
  3. Per-call AST analysis of the value expression (see PR1 AST subset below)
  4. Classify each slot: `spice` / `entity-property` / `grammar-helper` / `string-format-arg`
  5. **Anything not in the PR1 AST subset → emit candidate with `status: "needs_manual"` and `reason` describing the unsupported shape**
  6. Write JSON to `--output` path (NOT stdout — stdout/stderr reserved for diagnostic logs)

- **PR1 AST subset (required):**
  - Single-literal value: `SetEventProperty("gospel", "<spice...> ...")`
  - `+` concatenation of literals and local-variable references whose initializer is a literal in the same `Generate()` method (e.g. `string text = "..."; ... value = text + "..." + property;`)
  - Local variable references where the initializer is a literal, an `entity.GetProperty(...)` call, or a year-like `int Random(...)` call. Unresolved/non-literal initializers → `needs_manual`.
  - HSE marker expansion: `LiteralPercent + identifier + LiteralPercent` triples mirror `HistoricStringExpander` runtime substitution; the `year` slot emits `(.+?)\ (?:BR|AR)` and is typed `hse-expansion`. Other HSE variables deferred to PR2+.
  - Resheph 16 files are dominated by these shapes (Codex review confirmed: mostly fixed-literal compositions with a `year` and one `newRegion`).
- **PR1 AST out of scope (degrade to `needs_manual`):**
  - `switch`/`case` decomposition (none of the Resheph 16 files use this; deferred to #422 PR2+)
  - `string.Format(...)` with multiple positional args (deferred to #422 PR2+)
  - Helper-call resolution beyond a single hop (e.g. `QudHistoryHelpers.GetNewRegion(...)` chains)
  - Ternary / conditional expressions in the value
  - Class fields, static helpers, closure captures
- **Architectural extensibility:** the visitor and slot classifier are designed so that PR2+ can add support for the deferred shapes without changing the candidate JSON schema or the Python pipeline.
- **Output schema:** see §3.5

### 3.2 extract_annals_patterns.py

- **CLI:**

  ```bash
  python3.12 scripts/extract_annals_patterns.py \
      --source-root ~/dev/coq-decompiled_stable/XRL.Annals \
      --include "Resheph*.cs" \
      --output scripts/_artifacts/annals/candidates_pending.json
      [--force]
  ```

- **Behavior:**
  - Wraps `dotnet run` invocation; forwards CLI args to C# tool
  - Refuses to overwrite existing `--output` unless `--force` (which creates `.bak-YYYYMMDDHHMMSS` first)
  - Validates basic JSON shape after C# tool returns
  - Reports candidate count summary

### 3.3 validate_candidate_schema.py

- **CLI:** `python3.12 scripts/validate_candidate_schema.py <candidates-json>`
- **Checks:**
  - `schema_version` present and matches expected version (currently `"1"`)
  - JSON parseable
  - Each candidate has all required fields
  - Unknown top-level fields → fail (allowlist explicit: `review_notes` permitted)
  - `status` is enum `pending|accepted|needs_manual|skip`
  - `ja_template` non-empty when `status == accepted`
  - `extracted_pattern` is a valid regex (compile succeeds)
  - Placeholder index in `ja_template` (`{N}` and `{tN}`) does not exceed `extracted_pattern` capture count
  - `id` is unique within the file
- **Exit:** `0` on success, `1` on any handled failure (matching repo convention: `check_encoding.py:176`, `validate_xml.py:239`)

### 3.4 translate_annals_patterns.py

- **CLI:** `python3.12 scripts/translate_annals_patterns.py <candidates-json>`
- **Behavior:**
  - Library-imports glossary loader from `translate_corpus_batch.py`
  - Selects candidates where `status=accepted` AND (`ja_template == ""` OR stored `en_template_hash` mismatches recomputed hash)
  - Chunks into **5–8 candidates per chunk**, source order
  - Each chunk prompt prefixes a short summary of all Resheph candidates as context (backstory cohesion)
  - Invokes Codex CLI per chunk
  - **100% match required** (not the existing 80% threshold)
  - Retry strategy:
    - Whole JSON un-parseable → re-send entire chunk (max 3 retries)
    - Parsed but missing/invalid IDs → next retry sends only failed IDs
    - Token budget exceeded → split chunk and retry
    - Single-candidate budget exceeded → mark `needs_manual`
  - Per-candidate response validation: id match, `ja_template` non-empty, regex compile, sample match, capture count vs placeholder index, slot/placeholder set parity, no unknown placeholders
  - **Saves valid candidates immediately** (partial-success preserves idempotency)
  - Updates `ja_template` and `en_template_hash` in place
  - On exhaustion of retries, the failing candidate's status is downgraded to `needs_manual`
- **Exit:** `0` on success, `1` on any handled failure (Codex CLI absent, auth expired, retries exhausted)

### 3.5 merge_annals_patterns.py

- **CLI:** `python3.12 scripts/merge_annals_patterns.py <candidates-json>`
- **Behavior:**
  1. Calls `validate_candidate_schema.py` logic as a pure function (idempotency hole defense)
  2. Filters `status=accepted` AND `ja_template != ""`
  3. Collision detection: for each candidate, test both:
     - Raw `sample_source` against all existing `journal-patterns.ja.json` regexes
     - **Normalized** `sample_source` (with slot raws replaced by `SLOT0`, `SLOT1`, …) against same
  4. On any collision → write `scripts/_artifacts/annals/merge_conflicts.json` and exit `1`
  5. On clean → emit `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` with deterministic ordering, schema `{"entries":[], "patterns":[...]}`
  6. Empty accepted list (no candidates) → emit `{"entries":[], "patterns":[]}` and exit `0`
  7. Stale `merge_conflicts.json` is deleted on clean run
- **Output formatting:** `json.dumps(..., ensure_ascii=False, indent=2) + "\n"` (matches `validate_xml.py:98`, `translate_corpus_batch.py:339`)

### 3.6 Candidate JSON schema

```jsonc
{
  "schema_version": "1",
  "candidates": [
    {
      "id": "ReshephIsBorn#case0",            // <annal_class>#<switch-case-index | "default">
      "source_file": "ReshephIsBorn.cs",
      "annal_class": "ReshephIsBorn",
      "switch_case": "default",                // null | "0" | "1" | "default"
      "event_property": "gospel",              // "gospel" | "tombInscription"
      "sample_source": "<spice...> Resheph was born...",  // synthesized from C# composition
      "extracted_pattern": "^... (.+?) ...\\.$",
      "slots": [
        {
          "index": 0,
          "type": "spice",                     // spice | entity-property | grammar-helper | string-format-arg
          "raw": "<spice.history.gospels.ReshephLocation.!random>",
          "default": "{t0}"
        }
      ],
      "status": "pending",                    // extractor emits pending or needs_manual
      "reason": "",                            // human-readable note (extractor or human)
      "ja_template": "",                       // human-edited; final translation source of truth
      "review_notes": "",                      // optional human notes (allowlisted unknown field)
      "route": "annals",                       // fixed
      "en_template_hash": "sha256:..."        // sha256(canonical_json({extracted_pattern, slots, sample_source, event_property, switch_case}))
    }
  ]
}
```

### 3.7 merge_conflicts.json schema

```jsonc
{
  "schema_version": "1",
  "conflicts": [
    {
      "candidate_id": "ReshephIsBorn#case0",
      "candidate_pattern": "^...$",
      "candidate_pattern_normalized": "^...SLOT0...$",
      "candidate_template": "...",
      "sample_source": "...",
      "conflict_type": "raw" | "normalized",
      "conflicts": [
        {
          "file": "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json",
          "pattern_index": 42,
          "pattern": "^...$",
          "pattern_normalized": "^...$",
          "template": "..."
        }
      ],
      "resolution_options": [
        "skip_candidate",
        "narrow_candidate_pattern",
        "replace_existing_after_review"
      ]
    }
  ]
}
```

`resolution_options` are mechanical suggestions: raw collision → `skip_candidate` first; normalized collision → `narrow_candidate_pattern` first.

### 3.8 Glossary integration

- Reuse `scripts/translation_glossary.txt` (existing canonical glossary)
- PR1 adds proper nouns required for Resheph backstory:
  - `Rebekah` → `レベカ`
  - `Gyre` → `ジャイア`
  - `Qud` → `クッド` (already present)
  - `Tomb of the Eaters` → `喰らう者の墓`
  - `Omonporch` → `オモンポーチ`
  - `Spindle` → `スピンドル`
  - (Final translations subject to the user's preference; spec captures the additions, exact ja forms are PR-time decisions)
- `translate_annals_patterns.py` library-imports `load_glossary` from `translate_corpus_batch.py`

### 3.9 Artifact directory & gitignore

```text
scripts/_artifacts/annals/
├── candidates_pending.json     # extract output, human edits in place
├── candidates_pending.json.bak-YYYYMMDDHHMMSS  # extract --force backups
└── merge_conflicts.json        # merge writes on conflict; deletes on clean
```

`.gitignore` add: `scripts/_artifacts/annals/`

---

## 4. Data Flow & Error Handling

### 4.1 Operator workflow (typical session)

```bash
# 1. Extract candidates from decompiled source
python3.12 scripts/extract_annals_patterns.py \
  --source-root ~/dev/coq-decompiled_stable/XRL.Annals \
  --include "Resheph*.cs" \
  --output scripts/_artifacts/annals/candidates_pending.json

# 2. Human review (edit JSON in editor)
$EDITOR scripts/_artifacts/annals/candidates_pending.json

# 3. Schema validation
python3.12 scripts/validate_candidate_schema.py \
  scripts/_artifacts/annals/candidates_pending.json

# 4. Translate (Codex CLI required)
python3.12 scripts/translate_annals_patterns.py \
  scripts/_artifacts/annals/candidates_pending.json

# 5. Translation review (optional refinement loop: 2 ↔ 4)
$EDITOR scripts/_artifacts/annals/candidates_pending.json

# 6. Merge into annals-patterns.ja.json
python3.12 scripts/merge_annals_patterns.py \
  scripts/_artifacts/annals/candidates_pending.json

# 7. Build & test
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj
```

### 4.2 Idempotency & re-runs

| Re-run trigger | Behavior |
|---|---|
| `extract` with existing artifact, no `--force` | refuse + exit `1` |
| `extract --force` with existing artifact | back up to `.bak-YYYYMMDDHHMMSS`, write new |
| `validate` twice → identical result | OK |
| `translate` twice on same candidates | hash matches → skip; nothing written |
| `translate` after human edits to `extracted_pattern` | hash mismatch → re-translate just affected candidate |
| `merge` twice on same input | byte-identical output |
| `merge` with conflict → fix → re-run clean | stale `merge_conflicts.json` deleted |

### 4.3 Error matrix

| Stage | Error | Detection | Exit |
|---|---|---|---|
| extract | `dotnet` not installed | subprocess errno | `1` ("dotnet 10.0.x SDK required; install via standard means") |
| extract | source-root missing | os check | `1` |
| extract | C# parse error in some `.cs` | Roslyn diagnostics | `0` (warn + skip + summary) |
| extract | unresolved local variable | Roslyn visitor | `0` (emit `status=needs_manual` + reason) |
| extract | existing artifact (no `--force`) | os.path.exists | `1` |
| validate | JSON parse fail | json.JSONDecodeError | `1` (line/field) |
| validate | `schema_version` missing/wrong | schema check | `1` |
| validate | unknown top-level field | schema check | `1` |
| validate | status enum violation | schema check | `1` |
| validate | empty `ja_template` when accepted | schema check | `1` |
| validate | placeholder index out of range | regex compile + scan | `1` |
| validate | regex compile fail | re.compile | `1` |
| translate | Codex CLI absent | subprocess errno | `1` ("`codex` command required") |
| translate | Codex auth expired | subprocess output | `1` ("`codex login` required") |
| translate | response un-parseable JSON (after retries) | json.JSONDecodeError | `1` |
| translate | match rate < 100% (after retries) | id-set comparison | `1` |
| translate | placeholder/slot violation (after retries) | post-validate | `0` (mark needs_manual) |
| merge | shadow against `journal-patterns.ja.json` | regex match (raw + normalized) | `1` (write `merge_conflicts.json`) |
| merge | existing `annals-patterns.ja.json` malformed | json schema check | `1` |
| merge | output write failure | OSError | `1` |

`argparse` usage errors (`--help`, missing args) exit `2` per Python convention.

### 4.4 Logging

- Stdout: lifecycle messages (input/output paths, candidate counts, per-id status changes)
- Stderr: warnings (skipped files, recoverable errors)
- No `[QudJP]` structured logging (build-time scripts; Phase F observability is runtime-only per `docs/RULES.md:17, 191`)
- Asset provenance & duplicate/broad rejection per `Mods/QudJP/Localization/AGENTS.md:28` — handled by `merge`'s collision detection

---

## 5. Tests

### 5.1 Test layering

| Layer | Project | Purpose |
|---|---|---|
| **Python pytest** | `scripts/tests/` | Pipeline scripts (extract / validate / translate / merge) and tooling |
| **L1 (pure C#)** | `QudJP.Tests/L1/` | Pure logic, no `Assembly-CSharp.dll` dependency |
| **L2 (dummy seam)** | `QudJP.Tests/L2/` | DummyTarget-based behavioral verification |
| **L2G (game DLL signature)** | `QudJP.Tests/L2G/` | Verify against real `Assembly-CSharp.dll` types/methods (no new tests in PR1) |

### 5.2 Python tests (new, in `scripts/tests/`)

| File | Coverage |
|---|---|
| `test_extract_annals_patterns.py` | Fixture-driven (`scripts/tests/fixtures/annals/*.cs`, tiny synthetic): `simple_concat.cs`, `string_format.cs`, `switch_cases.cs`, `unresolved_variable.cs`. Golden candidate JSON comparison. |
| `test_validate_candidate_schema.py` | enum / required field / placeholder index / regex / `schema_version` / unknown field. Positive + each negative. |
| `test_translate_annals_patterns.py` | Mock `subprocess.run` for Codex CLI. `en_template_hash` stale detection. 100% match retry. Partial-save on chunk failure. Token budget chunk shrink. |
| `test_merge_annals_patterns.py` | Clean merge / raw collision / normalized collision / accepted=0 / existing malformed / merge calls validate internally. |
| `test_roslyn_extractor_smoke.py` | `dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj` succeeds. |
| `test_pipeline_roundtrip.py` | extract → validate → mock `ja_template` → merge → output JSON loadable. |
| `test_artifact_gitignore.py` | `.gitignore` contains `scripts/_artifacts/annals/`. |

Naming: `test_<behavior>_<condition>` snake_case (existing pattern from `test_validate_pattern_routes.py:18`).

Fixtures use plain `.cs` extension (existing convention `scripts/tests/fixtures/scanner/Demo/PatternCoverage.cs`); decompiled source MUST NOT be copied (commit forbidden).

### 5.3 L1 tests (new, in `QudJP.Tests/L1/`)

| File | Coverage |
|---|---|
| `JournalPatternTranslatorMultiFileTests.cs` | Ordered load (journal first, annals second). `null` reset → defaults. Empty list → defaults. First file missing → `FileNotFoundException`. Second file missing → `FileNotFoundException`. Malformed JSON in second file → `InvalidDataException`. Invalid regex in second file → `InvalidDataException`. Duplicate pattern across files → warn + first-file wins (per existing `JournalPatternTranslator.cs:181, 247`). |
| `AnnalsPatternsMarkupInvariantTests.cs` | Load `annals-patterns.ja.json`. Each pattern: regex compile, placeholder index ≤ capture count, multiset parity of markup tokens (`&W`, `^k`, `{{X|y}}`, `<color=...>`, `\n`, `=name=` etc.) between source/template, `entries` field present. |
| `AnnalsPatternsCollisionTests.cs` | Annals samples (test fixture) do not shadow existing 86 `journal-patterns.ja.json` patterns (raw + normalized). |
| `AnnalsPatternsAssetReachabilityTests.cs` | `LocalizationAssetResolver` resolves the path; `patterns` array non-empty. (Moved from L2G — asset path resolution is game-DLL-independent per `LocalizationAssetResolver.cs:12`.) |

Naming: `Subject_Condition_Behavior` (existing pattern from `JournalPatternTranslatorTests.cs:129`).

### 5.4 L2 tests (new, in `QudJP.Tests/L2/`)

| File | Coverage |
|---|---|
| `ReshephHistoryTranslationTests.cs` | Build dummy `History`/`HistoricEntity` with Resheph-related `eventProperties` content. Run walker. Assert each accepted candidate's synthetic sample is translated. Per-candidate-id coverage: N accepted → N translated. |

L2 reads the committed `annals-patterns.ja.json` directly; the Roslyn extractor is NOT invoked at test time. Synthetic samples live in `QudJP.Tests/L2/Fixtures/annals-samples.json` with the schema below.

**`annals-samples.json` schema:**

```jsonc
{
  "schema_version": "1",
  "samples": [
    {
      "candidate_id": "ReshephIsBorn#default",      // matches a candidate in annals-patterns.ja.json provenance (informational)
      "event_property": "gospel",                    // gospel | tombInscription
      "input_source": "Resheph was born in the salt marsh.",  // post-HSE-expansion English text the walker would receive
      "expected_japanese_contains": ["レシェフ"]      // substring-set assertion; loose match tolerates template variation
    }
  ]
}
```

Test asserts that running `input_source` through the walker (per #408 path) produces output containing every string in `expected_japanese_contains`. Substring-set assertion is intentional — exact-string equality is brittle against future template edits.

### 5.5 L2G tests

No new L2G tests in PR1. Existing `HistoricNarrativePatchPresenceTests.cs` already covers `JournalPatternTranslator` reachability paths through the `#if HAS_GAME_DLL`-gated regression guards.

### 5.6 CI integration

Existing `.github/workflows/ci.yml` already runs:

- `dotnet build` (Release) for `QudJP.csproj` and `QudJP.Tests.csproj`
- `dotnet test` for `QudJP.Tests.csproj` (`--no-build`)
- `actions/setup-python@v6` with Python 3.12, plus `ruff check scripts/` and `pytest scripts/tests/` (each gated on file existence)
- `npm install -g @ast-grep/cli`
- `actions/setup-dotnet@v5` installs both `8.0.x` and `10.0.x` SDKs (no `global.json`; the highest available is selected at build time)

**PR1 additions to CI** (only these two):

1. Build the Roslyn extractor so its csproj does not silently rot:

   ```yaml
   - name: Build AnnalsPatternExtractor
     run: dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj --configuration Release
   ```

2. New pytest files under `scripts/tests/` are picked up by the existing `pytest scripts/tests/` step automatically; new C# tests under `QudJP.Tests/L1/` and `QudJP.Tests/L2/` are picked up by the existing `dotnet test` step automatically — no workflow edit beyond (1).

**Explicitly out of CI scope for PR1:**

- The pipeline body (`extract` / `validate` / `translate` / `merge`) is dev-local only. `translate` requires `codex` CLI access which CI runners lack.
- `scripts/check_encoding.py`, `scripts/validate_xml.py`, `scripts/sync_mod.py --dry-run` are existing manual-run developer tooling; adding them to CI is its own initiative (issue #420 OQ3 follow-up, not in PR1 or in #422).

L2G tests skip naturally on game-DLL-absent CI runners via `#if HAS_GAME_DLL`.

### 5.7 Coverage targets

PR1 does not introduce a coverage gate (the repo has no existing coverage tooling). Required:

- Each new Python script's primary path (happy + main error) covered by pytest
- L1: all branches of multi-file load + markup invariant per pattern + collision per sample
- L2: per-candidate-id coverage (N accepted → N translated)

A coverage gate may be introduced in a future PR.

---

## 6. Acceptance Criteria (PR1)

### 6.1 Pipeline implementation

- [ ] `scripts/tools/AnnalsPatternExtractor/` — net10.0 console with `Microsoft.CodeAnalysis.CSharp`, `--source-root` / `--include` / `--output`, JSON written to `--output` path (not stdout)
- [ ] `scripts/extract_annals_patterns.py` — `--source-root` / `--include` / `--output` / `--force` (with `.bak-` backup)
- [ ] `scripts/validate_candidate_schema.py` — exit 0/1, full schema check
- [ ] `scripts/translate_annals_patterns.py` — 5–8 candidate chunks, 100% match, `en_template_hash` stale detection, partial-save, retry-failed-IDs-only
- [ ] `scripts/merge_annals_patterns.py` — raw + normalized collision, internal validate, `merge_conflicts.json` artifact, deterministic output

### 6.2 Dictionary deliverable

- [ ] `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` new file, `{"entries":[], "patterns":[...]}` schema
- [ ] All accepted candidates from Resheph 16 files translated and merged

### 6.3 Runtime change

- [ ] `JournalPatternTranslator.SetPatternFilesForTests(string[]?)` added; old `SetPatternFileForTests(string?)` becomes a wrapper preserving null-resets-to-default semantics
- [ ] `DefaultPatternAssetPaths` static array (journal first, annals second)
- [ ] Existing 86 patterns behavior unchanged
- [ ] Hard-fail-on-malformed for both files (matches existing single-file behavior)

### 6.4 Tests

- [ ] Python: 7 new files (`test_extract_annals_patterns.py`, `test_validate_candidate_schema.py`, `test_translate_annals_patterns.py`, `test_merge_annals_patterns.py`, `test_roslyn_extractor_smoke.py`, `test_pipeline_roundtrip.py`, `test_artifact_gitignore.py`)
- [ ] L1: 4 new files (`JournalPatternTranslatorMultiFileTests.cs`, `AnnalsPatternsMarkupInvariantTests.cs`, `AnnalsPatternsCollisionTests.cs`, `AnnalsPatternsAssetReachabilityTests.cs`)
- [ ] L2: 1 new file (`ReshephHistoryTranslationTests.cs`)
- [ ] L2G: no new files (existing presence tests sufficient)

### 6.5 Live-runtime verification

- [ ] Rosetta CoQ launch → new Joppa world → `wish reshephgospel` → Journal > Sultan Histories > Resheph display
- [ ] Screenshot evidence ≥1 image showing Resheph gospel ≥1 line in Japanese
- [ ] Per-candidate-id coverage: every accepted candidate is verified translated in L1 sample tests; ≥1 candidate verified in actual runtime evidence

### 6.6 Quality gates

- [ ] All existing L1/L2/L2G tests green
- [ ] No new symbolic-key-pollution errors (`spice reference <日本語> wasn't a node`) in runtime log
- [ ] CI all-pass (existing steps + PR1 addition):
  - existing: `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` (Release)
  - existing: `dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` (Release)
  - existing: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --no-build`
  - existing: `ruff check scripts/`
  - existing: `pytest scripts/tests/`
  - **new (PR1):** `dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj` (Release)
- [ ] Existing 86 patterns regression: `JournalPatternTranslatorMultiFileTests` includes a parametrized assertion that every existing pattern in `journal-patterns.ja.json` continues to match its current behaviour after the multi-file load change

### 6.7 Documentation

- [ ] `scripts/AGENTS.md` (or `scripts/README.md`) updated with annals pipeline operator workflow
- [ ] `Mods/QudJP/Localization/Dictionaries/AGENTS.md` (if present) updated with `annals-patterns.ja.json` role
- [ ] `CHANGELOG.md` Unreleased entry: "Sultan Resheph history translation"
- [ ] `.gitignore` adds `scripts/_artifacts/annals/`

### 6.8 Glossary additions

- [ ] `scripts/translation_glossary.txt` adds: `Rebekah`, `Gyre`, `Qud` (if not present), `Tomb of the Eaters`, `Omonporch`, `Spindle` (final ja forms decided at PR review time)

---

## 7. References

- Issue: <https://github.com/ToaruPen/coq-japanese_stable/issues/420>
- Parent: #400, depends-on: #408 (PR #419 merged 2026-04-26)
- Existing translators:
  - `Mods/QudJP/Assemblies/src/Translation/JournalPatternTranslator.cs` (regex/template engine, `{tN}` mechanism at line 677)
  - `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs`
  - `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs`
  - `Mods/QudJP/Assemblies/src/Translation/Translator.cs` (loads `Dictionaries/*.ja.json`, requires `entries` field)
- Existing patches: `Mods/QudJP/Assemblies/src/Patches/GenerateVillageEraHistoryTranslationPatch.cs`
- Existing dictionaries:
  - `Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json` (86 patterns)
  - `Mods/QudJP/Localization/Dictionaries/world-gospels.ja.json` (1286 fragments)
- Existing scripts: `scripts/translate_corpus_batch.py`, `scripts/translation_glossary.txt`, `scripts/sync_mod.py`, `scripts/build_release.py`
- Decompiled source (NOT committed): `~/dev/coq-decompiled_stable/XRL.Annals/Resheph*.cs` (16 files), `XRL.World.Capabilities/Wishing.cs:2989` (`reshephgospel` wish), `Qud.API/JournalAPI.cs:292` (`GetNotesForResheph`)
- Test architecture: `docs/test-architecture.md`
- Operating rules: `docs/RULES.md`
