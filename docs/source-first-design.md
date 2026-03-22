# Source-First Architecture Design (DRAFT)

## Premise

Full decompilation of Assembly-CSharp.dll (5474 files, 40MB) makes incremental route
discovery obsolete. Static analysis of decompiled source can classify ~85-90% of all
translatable text without ever running the game. The remaining ~10-15% falls back to
runtime log analysis.

This replaces the producer-first design (archived as `producer-first-design-old.md`),
which assumed only 26 known hook points and incremental discovery.

## Validated Site Counts (deduplicated, 2026-03-22)

Measured against `~/Dev/coq-decompiled/` with deduplication: 5,474 raw → **5,371** unique
`.cs` files (excluded: 32 empty, 37 dot-namespace flat dupes, 26 underscore-namespace
flat dupes, 8 retry/msgprobe artifacts).

### Sink Call Sites (11 families)

| # | Family | Call Sites | Files | Notes |
|---|--------|----------:|------:|-------|
| 1 | SetText | 316 | 85 | |
| 2 | AddPlayerMessage (instance + MessageQueue) | 585 | 220 | |
| 3 | Popup family (Show, ShowFail, ShowYesNo, etc. — 9 variants) | 1,460 | 421 | Largest family |
| 4 | DidX family (DidX, DidXToY, DidXToYWithZ, XDidY, XDidYToZ, WDidXToYWithZ) | 272 | 145 | |
| 5 | GetDisplayName | 147 | 70 | |
| 6 | Does() | 307 | 136 | Subject+verb composition |
| 7 | EmitMessage | 238 | 82 | Direct message emission |
| 8 | GetShort/LongDescription | 7 | 5 | Negligible — mostly override producers |
| 9 | JournalAPI (AddAccomplishment, AddMapNote, AddObservation) | 130 | 82 | Narrative/chronicle text |
| 10 | HistoricStringExpander.ExpandString | 242 | 58 | Procedural lore text |
| 11 | ReplaceBuilder (StartReplace) | ~52 | ~21 | Template-variable text composition |
| | **Sink subtotal** | **~3,756** | | |

### Override Producers

| Producer | Overrides | Files |
|----------|----------:|------:|
| Effects: GetDescription | 180 | 180 |
| Effects: GetDetails | 171 | 171 |
| Mutations: GetDescription | 127 | 127 |
| Mutations: GetLevelText | 131 | 131 |
| Parts: GetShortDescription | ~265 | ~265 |
| **Producer subtotal** | **~874** | |

### Grand Total: **~4,630** translatable sites

Note: DidX family count (272) needs re-measurement after ast-grep pattern fix (B1).
Total will shift accordingly. All counts are post-dedup approximations.

### Additional Data

- **Unique verb strings** in DidX family: **118** (appear, beat, burn, convince, die, disarm, give, roll, stare, etc.)

### Coverage Estimate (revised)

| Method | Estimated Coverage | Sites |
|--------|-------------------|------:|
| Mechanical (ast-grep + rule classifier, steps 1-4) | ~70-75% | ~3,000-3,200 |
| + LLM reading decompiled source (steps 5-6) | ~85-90% | ~3,700-3,900 |
| Requires runtime log analysis | ~10-15% | ~400-600 |

The ~10-15% that genuinely require runtime evidence:
- Event parameter injection — `SourceDescription` depends on event firing context
- Runtime payload UI — `Notification.Data` displays arbitrary notification payloads
- Save/persisted text — score details are records from past runs
- Generic popup/message wrappers — `Popup.Show(Message)` where producer is external
- Blueprint/tag strings — text from XML tags not visible in C# source
- Ability/data asset strings — display text loaded from non-code assets
- HistoricStringExpander procedural lore (~242 sites) — deep template expansion

## Two-Layer Problem This Solves

1. **Why isn't everything translated?** — Decompiled source wasn't fully available.
   Now it is. LLM agents can scan all 4,600+ translatable sites and generate translation
   artifacts (dictionary entries or patches) directly from source.

2. **Why do agents misclassify dynamic text as dictionary entries?** — They couldn't
   distinguish static from dynamic. Now the source scanner classifies every call site
   before any agent touches it.

## Architecture Overview

```
Phase 1: Static Scan (Python + ast-grep + LLM)
  ~/Dev/coq-decompiled/ (5371 files after dedup)
    → Scanner classifies all ~4,630 translatable sites
    → Generates candidate-inventory.json (all routes, with confidence)
    → LLM agents consume inventory to produce translations

Phase 2: Runtime Verification (C# + Harmony, only for unresolved ~10-15%)
  Player.log + audit sink
    → Captures text that static analysis couldn't classify
    → Agent traces upstream in decompiled source → resolves
```

## Phase 1: Source Scanner

### Input

All `.cs` files in `~/Dev/coq-decompiled/`, organized by namespace.

### Scan Targets (11 sink families + 5 override producers)

See "Validated Site Counts" section above for the full table with deduplicated numbers.

**ast-grep patterns for sink families:**

Note: IComponent subclasses call DidX/DidXToY/EmitMessage without qualifier (`this.` implicit).
Both qualified (`$_.DidX($$$)`) and unqualified (`DidX($$$)`) patterns are needed.
`Does()` has ~9 false positives from non-messaging `.Does()` methods — filter in rule_classifier.

| # | Family | ast-grep Pattern(s) |
|---|--------|-------------------|
| 1 | SetText | `$_.SetText($$$)` |
| 2 | AddPlayerMessage | `$_.AddPlayerMessage($$$)`, `MessageQueue.AddPlayerMessage($$$)` |
| 3 | Popup (9 variants) | `Popup.Show($$$)`, `Popup.ShowFail($$$)`, `Popup.ShowBlock($$$)`, `Popup.ShowYesNo($$$)`, `Popup.ShowYesNoCancel($$$)`, `Popup.PickOption($$$)`, `Popup.AskString($$$)`, `Popup.ShowAsync($$$)`, `Popup.WarnYesNo($$$)` |
| 4 | DidX (6 variants) | `$_.DidX($$$)`, `DidX($$$)`, `$_.DidXToY($$$)`, `DidXToY($$$)`, `$_.DidXToYWithZ($$$)`, `DidXToYWithZ($$$)`, `Messaging.XDidY($$$)`, `Messaging.XDidYToZ($$$)`, `Messaging.WDidXToYWithZ($$$)` |
| 5 | GetDisplayName | `$_.GetDisplayName($$$)` |
| 6 | Does() | `$_.Does($$$)` (filter non-IComponent.Does in rule_classifier) |
| 7 | EmitMessage | `$_.EmitMessage($$$)`, `EmitMessage($$$)`, `Messaging.EmitMessage($$$)` |
| 8 | GetShort/LongDescription | `$_.GetShortDescription($$$)`, `$_.GetLongDescription($$$)` |
| 9 | JournalAPI | `JournalAPI.AddAccomplishment($$$)`, `JournalAPI.AddMapNote($$$)`, `JournalAPI.AddObservation($$$)` |
| 10 | HistoricStringExpander | `HistoricStringExpander.ExpandString($$$)` |
| 11 | ReplaceBuilder | `$_.StartReplace($$$)` (template-variable text composition, ~52 post-dedup sites) |

**Override producers** (grep-based, not ast-grep):

| Producer | Pattern | Overrides |
|----------|---------|----------:|
| Effects: GetDescription | `override.*GetDescription` in Effects/ | 180 |
| Effects: GetDetails | `override.*GetDetails` in Effects/ | 171 |
| Mutations: GetDescription | `override.*GetDescription` in Mutations/ | 127 |
| Mutations: GetLevelText | `override.*GetLevelText` in Mutations/ | 131 |
| Parts: GetShortDescription | `override.*GetShortDescription` in Parts/ | ~265 |

### Classification Algorithm

For each sink call site, classify by argument pattern:

```
1. Literal string → Leaf
   SetText("Inventory")  →  { type: "Leaf", confidence: "high", key: "Inventory" }

2. string.Format / interpolation → Template
   SetText(string.Format("Level: {0}", x))  →  { type: "Template", confidence: "high", slots: ["level"] }

3. GetDisplayName result → Builder
   SetText(go.GetDisplayName())  →  { type: "Builder", confidence: "high" }

4a. DidX/DidXToY → MessageFrame (HandleMessage path)
    ParentObject.DidX("block", ...)  →  { type: "MessageFrame", confidence: "high", path: "HandleMessage" }

4b. Does() → VerbComposition (caller-assembled path)
    obj.Does("begin") + " flying."  →  { type: "VerbComposition", confidence: "high", path: "caller" }
    NOTE: Does() returns a string; translation must happen at the call site or at
    the downstream sink (AddPlayerMessage/Popup.Show), NOT via HandleMessage.

4c. ReplaceBuilder → VariableTemplate
    "=subject.T= =verb:strike=".StartReplace()  →  { type: "VariableTemplate", confidence: "high" }
    Uses =variable= template syntax, distinct from string.Format.

4d. HistoricStringExpander → ProceduralText
    HistoricStringExpander.ExpandString(...)  →  { type: "ProceduralText", confidence: "low", needs_runtime: true }
    Deep <spice.*> template expansion — cannot be statically resolved.

4e. JournalAPI → NarrativeTemplate
    JournalAPI.AddAccomplishment("On the " + Calendar.GetDay() + ...)
    →  { type: "NarrativeTemplate", confidence: "medium", needs_review: true }
    Calendar/string concatenation patterns, partially traceable.

5. StringBuilder.ToString() → Template (needs slot analysis)
   SetText(sb.ToString())  →  { type: "Template", confidence: "medium", needs_review: true }

6. Variable from known producer → trace upstream
   SetText(node.Description)
     → trace: node.Description comes from SPNode.Description
     → trace: SPNode.Description is loaded from XML blueprint
     → { type: "Leaf", confidence: "medium", source: "xml-blueprint" }

7. Variable from unknown/complex path → Unresolved
   SetText(ComputeSomething())  →  { type: "Unresolved", confidence: "low", needs_runtime: true }
```

Steps 1-4e are mechanical (ast-grep + regex). ~3,000-3,200 sites, ~70-75%.
Steps 5-6 require LLM reading decompiled source. ~700-900 additional sites, ~15-20%.
Step 7 is the remainder. ~400-600 sites, ~10-15%.

### Output: candidate-inventory.json

```json
{
  "version": "1.0",
  "game_version": "2.0.4",
  "scan_date": "2026-03-22",
  "stats": {
    "total_sites": 4630,
    "total_deduped_files": 5371,
    "sink_sites": 3756,
    "override_producers": 874,
    "auto_classified": null,
    "llm_classified": null,
    "unresolved": null
  },
  "sites": [
    {
      "id": "Qud.UI.CharacterStatusScreen::UpdateViewFromData::L227",
      "file": "Qud.UI/CharacterStatusScreen.cs",
      "line": 227,
      "sink": "SetText",
      "type": "Template",
      "confidence": "high",
      "pattern": "string.Format(\"Level: {0} ... HP: {1}/{2} ... XP: {3}/{4} ... Weight: {5}#\", ...)",
      "slots": ["level", "hp_current", "hp_max", "xp_current", "xp_next", "weight"],
      "status": "needs_translation",
      "existing_patch": "CharacterStatusScreenTranslationPatch"
    },
    {
      "id": "Qud.UI.SkillsAndPowersStatusScreen::UpdateDetailsFromNode::L91",
      "file": "Qud.UI/SkillsAndPowersStatusScreen.cs",
      "line": 91,
      "sink": "SetText",
      "type": "Leaf",
      "confidence": "medium",
      "pattern": "learnedText.SetText(\"{{R|[Unlearned]}}\")",
      "key": "{{R|[Unlearned]}}",
      "status": "needs_translation",
      "existing_patch": "SkillsAndPowersStatusScreenTranslationPatch"
    },
    {
      "id": "XRL.World.Parts.Combat::HandleEvent::L1015",
      "file": "XRL.World.Parts/Combat.cs",
      "line": 1015,
      "sink": "AddPlayerMessage",
      "type": "MessageFrame",
      "confidence": "high",
      "pattern": "DidX(\"block\", ...)",
      "status": "needs_patch",
      "existing_patch": null
    },
    {
      "id": "XRL.World.Effects.Confused::GetDescription",
      "file": "XRL.World.Effects/Confused.cs",
      "line": null,
      "sink": "GetDescription_override",
      "type": "Leaf",
      "confidence": "high",
      "pattern": "return \"Confused\"",
      "key": "Confused",
      "status": "needs_translation",
      "existing_patch": "EffectDescriptionPatch"
    }
  ]
}
```

### Status Values

| Status | Meaning | Agent Action |
|--------|---------|-------------|
| `translated` | Already handled by existing patch/dictionary | Skip |
| `needs_translation` | Classified, ready for translation work | Generate artifact |
| `needs_patch` | Logic-required, needs Harmony patch | Implement patch |
| `needs_review` | Medium confidence, LLM should verify | Read source, reclassify |
| `unresolved` | Static analysis failed | Fall back to runtime log |
| `excluded` | Intentionally untranslated (procedural, dev-only) | Skip |

## Phase 1 Workflow: Agent Translation Pipeline

```
1. Scanner generates candidate-inventory.json
2. Agent reads inventory, filters by status=needs_translation or needs_patch
3. For each site:
   a. type=Leaf → add dictionary entry (key → Japanese)
   b. type=Template / VariableTemplate / NarrativeTemplate → create template pattern with slot mapping
   c. type=Builder → verify existing GetDisplayNameRouteTranslator handles it
   d. type=MessageFrame → translated by HandleMessage Prefix (verb dictionary)
   e. type=VerbComposition → translate at call site or downstream sink (see Does() strategy)
   f. type=ProceduralText → defer to Phase 2 (runtime)
   g. type=needs_review → read decompiled source, reclassify, then act
4. Agent updates inventory status to `translated`
5. Repeat until needs_translation/needs_patch count → 0
```

### Priority Order (by translation impact)

| Priority | Domain | Sites | Rationale |
|----------|--------|------:|-----------|
| P0 | UI screen labels (SetText literals) | ~30 | Immediately visible, Leaf, trivial |
| P1 | Effect/mutation descriptions (overrides) | ~609 | High volume, mostly Leaf. Effects 180+171, Mutations 127+131 |
| P2 | Screen-specific templates (CharacterStatus, Skills, Factions) | ~50 | Already partially patched |
| P3 | Popup family literals (Show, ShowFail, ShowYesNo, etc.) | ~1,460 | Largest family, many Leaf |
| P4 | AddPlayerMessage literals | ~585 | Message log text |
| P5 | Parts.GetShortDescription overrides | ~265 | Item/creature descriptions, mostly Leaf |
| P6a | MessageFrame (DidX 6 variants) | ~272 | SVO→SOV via HandleMessage Prefix patch |
| P6b | VerbComposition (Does()) | ~307 | Separate strategy — Does() does NOT flow through HandleMessage |
| P7 | GetDisplayName / Trade / Tinkering / Book screens | ~227 | Mixed Template/Leaf |
| P8 | JournalAPI / EmitMessage / ReplaceBuilder | ~432 | Narrative + direct messages + template vars |
| P9 | Conversation text | ~40 | Template with =variable= slots |
| P10 | HistoricStringExpander (procedural lore) | ~242 | Deep template expansion, likely runtime |
| P11 | Unresolved (runtime fallback) | ~400-600 | Phase 2 |

## Phase 2: Runtime Verification (for Unresolved ~10-15%)

Only needed for sites where static analysis couldn't determine the text content or
composition pattern. Uses the existing Player.log infrastructure.

### Approach

1. Deploy mod with audit-mode UITextSkin.SetText (logs unclaimed text)
2. Play game, triggering unresolved code paths
3. Agent matches log entries to unresolved inventory sites
4. Agent reads decompiled source for the matched site → classifies → generates artifact
5. Update inventory status

### Runtime Infrastructure (minimal, deferred)

The old plan's ContractRegistry/ClaimRegistry/audit sink may still be useful here,
but it is NOT a prerequisite for Phase 1. It only matters for the ~400-600 unresolved sites
that need runtime evidence.

Decision: defer runtime infrastructure until Phase 1 is substantially complete and
the actual unresolved count is known.

## Existing Infrastructure: Triage

The existing 50 patches were written without full decompilation — agents had limited
visibility into upstream text producers, resulting in primitive pattern matching and
incomplete classification. With full source now available, most translation logic
patches should be rewritten to leverage the scanner's classification data.

**Exception**: Inventory/tooltip rendering patches (fonts, layout, display formatting)
are UI-presentation concerns unaffected by decompilation depth. Keep as-is.

### Tier 1: Keep As-Is (rendering/display)

| Component (actual filename) | Role | Rationale |
|-----------|------|-----------|
| `StatusScreenFontPatch` (multiple files) | CJK font injection per screen | Pure rendering, no translation logic |
| `TextMeshProFontPatch.cs` | TextMeshPro font | Pure rendering |
| `TmpInputFieldFontPatch.cs` | Input field font | Pure rendering |
| `InventoryLineRenderProbePatch.cs` | Inventory row rendering | Display probe |
| `EquipmentLineRenderProbePatch.cs` | Equipment row rendering | Display probe |
| `BaseLineWithTooltipStartTooltipPatch.cs` | Tooltip rendering | Display formatting |
| `SelectableTextMenuItemProbePatch.cs` | Menu item rendering | Display formatting |
| `ColorAwareTranslationComposer` | Strip/Restore color markup | Utility, reusable as-is |

### Tier 2: Rewrite with Source-First Knowledge (translation logic)

| Component | Current Approach | Source-First Improvement |
|-----------|-----------------|------------------------|
| `UITextSkinTranslationPatch` | Stack inspection + context guessing | Scanner classifies every SetText site upfront; route is known before runtime |
| `MessagePatternTranslator` | 30+ hand-written regex patterns | Scanner extracts all AddPlayerMessage patterns; regex generated from inventory |
| `PopupTranslationPatch` | 3 methods (ShowBlock, ShowOptionList, ShowConversation) | Expand to all Popup variants; classification from inventory |
| `MessageLogPatch` | Delegates to MessagePatternTranslator | Rewrite with HandleMessage Prefix patch for SVO→SOV |
| `GetDisplayNamePatch` / `GetDisplayNameRouteTranslator` | Suffix decomposition heuristics | Source-traced name composition; inventory knows exact Builder patterns |
| `InventoryLocalizationPatch` | GetDisplayNameRouteTranslator delegation | Contains translation logic (was misclassified as Tier 1) |
| `InventoryAndEquipmentStatusScreenTranslationPatch` | Inventory/Equipment screen | Scanner classifies all sites in these screens |
| `EffectDescriptionPatch` / `EffectDetailsPatch` | Override hook + dictionary | Scanner identifies all 180+171 Effect overrides; generates complete dictionary |
| `MutationDescriptionPatch` / `MutationLevelTextPatch` | Override hook + dictionary | Scanner identifies all 127+132 overrides; generates complete dictionary |
| `SkillDescriptionPatch` | Override hook + dictionary | Scanner identifies all skill descriptions; generates complete dictionary |
| `CharacterStatusScreenTranslationPatch` | Manual template extraction | Scanner classifies all Template sites in CharacterStatusScreen |
| `SkillsAndPowersStatusScreenTranslationPatch` | Manual template extraction | Scanner classifies all sites in SkillsAndPowersStatusScreen |
| `FactionsStatusScreenTranslationPatch` | Manual template extraction | Scanner classifies all sites in FactionsStatusScreen |
| `GrammarPatch.cs` (1 file, 8 inner classes) | Individual method patches | Keep structure, but enhance with source-informed rules |
| `MainMenuTranslationPatch` | Hard-coded string mapping | Scanner identifies all Leaf sites; dictionary-driven |
| `OptionsScreenTranslationPatch` | Hard-coded string mapping | Scanner identifies all Leaf sites; dictionary-driven |
| `ConversationDisplayTextPatch` / `ConversationTextDynamicPatch` | Pattern matching | Scanner classifies conversation text composition |

### Migration Strategy

Tier 2 rewrites follow the scanner output priority (P0→P11):

1. Scanner classifies all sites in the relevant domain
2. Agent compares scanner output vs existing patch implementation
3. New implementation replaces the patch, covering ALL sites (not just the ones the old patch knew about)
4. Old patch is deleted; inventory marks all sites as `translated`
5. Tests verify: new implementation covers everything the old patch covered + new sites

This means Tier 2 patches are NOT immediately obsolete — they continue to function
until the scanner-driven replacement is ready and tested for that domain. The transition
is per-domain, not a big-bang rewrite.

## Scanner Implementation

### Technology

- **Python 3 (>=3.12)** — use `python3`, not version-pinned `python3.12`
- **ast-grep** for structural C# pattern matching (installed, v0.42.0)
- **Interactive LLM classification** for steps 5-6 (run `--phase=1c` within Claude Code session, or `claude -p` headless mode, or Anthropic API)
- Output: JSON (`candidate-inventory.json` — committed to repo; intermediate files `.gitignore`d)

### Pipeline Phases (Approach C: Hybrid)

```
Phase 1a — ast-grep batch scan (mechanical, seconds)
  11 sink families (30+ patterns) × ~/Dev/coq-decompiled/ → .scanner-cache/raw_hits.jsonl
  + 5 override producer greps → .scanner-cache/override_hits.jsonl
  ~4,630 total hits extracted with file, line, matched code
  Dedup: exclude flat namespace duplicates + empty files (5,474 → 5,371 files)

Phase 1b — Python rule classifier (mechanical, seconds)
  raw_hits.jsonl → rule classifier → .scanner-cache/inventory_draft.json
  Steps 1-4: Leaf/Template/Builder/MessageFrame at high confidence (~78%)

Phase 1c — LLM-assisted classification (targeted, interactive)
  Filter: confidence=medium or low from inventory_draft (~300-400 sites)
  Execution: run `scan_text_producers.py --phase=1c` within a Claude Code session.
    Script presents unresolved sites → human/LLM reads the relevant decompiled
    source file and classifies → result written back to inventory_draft.json.
  NOTE: No Python→Claude Code subagent API exists. For automation, use either
    Anthropic API (separate cost) or Claude Code CLI headless mode (`claude -p`).
  Steps 5-6: variable tracing, call chain analysis (~15-20% additional)

Phase 1d — Cross-reference (mechanical, seconds)
  Match against existing patches (Mods/QudJP/Assemblies/src/Patches/*.cs),
  dictionaries (Localization/Dictionaries/*.ja.json),
  and XML translations (Localization/*.jp.xml)
  → Mark matched sites as `translated`

Output: candidate-inventory.json (committed to docs/)
```

Intermediate files (`.scanner-cache/`) are `.gitignore`d — each contributor regenerates locally.

### Deduplication Algorithm (Phase 1a)

The decompiler produces multiple copies of some classes. The scanner MUST deduplicate
before counting or classifying:

```
1. Exclude empty files (0 bytes)
2. Exclude .retry.cs and .msgprobe.cs files
3. For flat namespace files (e.g., XRL.World.GameObject.cs, XRL_World_GameObject.cs):
   - If a namespace-directory counterpart exists (e.g., XRL.World/GameObject.cs),
     EXCLUDE the flat file and keep the directory version
   - Match rule: strip dots/underscores from prefix, compare to directory path
4. Prefer namespace-directory files over flat files in all cases
```

This reduces 5,474 → ~5,371 files. All site counts in this document are post-dedup.

### Scanner Components

```
scripts/
  scan_text_producers.py       # Main entry: orchestrates phases 1a-1d
  scanner/
    __init__.py
    ast_grep_runner.py         # Phase 1a: ast-grep batch execution per sink family
    rule_classifier.py         # Phase 1b: mechanical classification (steps 1-4)
    llm_classifier.py          # Phase 1c: present unresolved sites for interactive LLM classification
    cross_reference.py         # Phase 1d: match existing patches/dictionaries
    inventory.py               # Inventory data model, JSON I/O, diffing
```

### Cross-Reference with Existing Work

The scanner must know what's ALREADY translated to avoid duplicate work.
Cross-reference sources:

1. **Existing dictionaries** (`Localization/Dictionaries/*.ja.json`, 38 files) — **primary source**.
   Match dictionary keys against Leaf site string values. This is the most reliable
   cross-reference because most existing patches translate via dictionary lookup, not
   by patching individual call sites.
2. **Existing XML translations** (`Localization/*.jp.xml`, ~21 files) — match IDs against
   blueprint-sourced text.
3. **Existing patches** (`Mods/QudJP/Assemblies/src/Patches/*.cs`, 50 files) — parse
   `[HarmonyPatch]` attributes to identify which game methods are already hooked.
   Note: most patches route to dictionary lookup, so the dictionary cross-reference
   (source 1) catches what they cover. Patch cross-reference catches structural patches
   (e.g., Template reformatting, SVO rewriting) that don't use dictionary keys.

## Implementation Policy: TDD

All scanner components and translation engine code MUST follow Test-Driven Development:

1. **Write a failing test first** — before implementing any scanner phase or translator
2. **Implement the minimum code** to make the test pass
3. **Refactor** — clean up while keeping tests green

### Test boundaries per component

| Component | Test Type | Example |
|-----------|-----------|---------|
| `ast_grep_runner.py` | L1 unit | Given a known .cs snippet, assert correct raw hits extraction |
| `rule_classifier.py` | L1 unit | Given a raw hit, assert correct classification (Leaf/Template/etc.) |
| `llm_classifier.py` | L1 unit | Given a medium-confidence site, assert correct presentation format and result write-back |
| `cross_reference.py` | L1 unit | Given existing patch list + inventory, assert correct `translated` marking |
| `inventory.py` | L1 unit | JSON round-trip, dedup, diff operations |
| `MessageFrameTranslator` | L1 unit | Given assembled English DidX output, assert correct SOV Japanese |
| Scanner integration | L2 | Run full pipeline on test fixture directory, assert inventory schema |

### Rationale

The scanner produces a JSON artifact that all downstream agents depend on. Incorrect
classification cascades into wrong translation artifacts. TDD ensures each classification
step is verified against known inputs before the pipeline is assembled.

## Success Criteria

- [ ] candidate-inventory.json contains ALL ~4,630 translatable sites (deduped)
- [ ] Auto-classification (steps 1-4) covers ≥75% with high confidence
- [ ] LLM-assisted classification (steps 5-6) brings total to ≥90%
- [ ] Unresolved sites are <15% of total (estimate: 10-15%)
- [ ] AGENTS.md references candidate-inventory.json as the workflow gate
- [ ] Agent can consume inventory and generate translation artifacts without manual guidance
- [ ] Existing translations (50 patches + 38 dictionaries) are cross-referenced and marked `translated`
- [ ] All scanner components have passing tests before integration

## MessageFrame Translation: SVO→SOV Rewrite Engine

272 DidX-family + 307 Does() call sites make per-pattern regex infeasible. Instead,
build a generalized SVO→SOV rewrite engine via HandleMessage Prefix patch.

### Problem

English: `The bear blocks the attack with its shield.` (Subject-Verb-Object)
Japanese: `クマは盾で攻撃を防いだ。` (Subject-Object-Verb)

DidX/DidXToY frames produce English word-order messages at runtime. Each call site
uses a verb string + optional object + optional preposition phrase. The combinations
are too numerous for individual regex patterns.

### Approach

XDidY/XDidYToZ/WDidXToYWithZ are all **void methods** — a postfix patch cannot
intercept the assembled message (it's already been sent to `HandleMessage` →
`MessageQueue.AddPlayerMessage()` or `Popup.Show()` by postfix time).

Instead, use a **Prefix patch on `Messaging.HandleMessage()`** — the single funnel
point through which ALL DidX/EmitMessage output passes (confirmed in Messaging.cs
at lines 87, 197, 284, 431, 564, 735). This is a `private static` method but Harmony
can patch it via `AccessTools.Method`.

```csharp
[HarmonyPatch]
static class MessageFrameTranslatorPatch
{
    // HandleMessage has 2 overloads (string vs StringBuilder) — must specify param types
    static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(Messaging), "HandleMessage",
            new[] { typeof(GameObject), typeof(string), typeof(char),
                    typeof(bool), typeof(bool), typeof(GameObject), typeof(GameObject) });

    // Prefix receives ref string Msg — rewrite SVO→SOV in place
    static void Prefix(ref string Msg, GameObject Source)
    { ... }
}
```

The `MessageFrameTranslator` Prefix:

1. **Receives** `ref string Msg` (the assembled English sentence) from HandleMessage
2. **Parses** into semantic slots: `{subject, verb, extra}` (note: "Extra" is freeform English — not just object/instrument)
3. **Looks up** verb → Japanese verb mapping (dictionary-driven, **118 unique verbs**)
4. **Recomposes** in SOV order with Japanese particles
5. For freeform `Extra` phrases, falls back to per-verb-Extra pair dictionary or LLM-generated translation at scan time
6. **Discriminates** DidX-originated messages from other EmitMessage calls (via pattern matching or `__state` flag from a companion XDidY Prefix)
7. **Marks translated messages** so downstream patches (MessagePatternTranslator, PopupTranslationPatch) skip them — prevents double-translation

### Does() Translation: Separate Strategy Required

**Does() does NOT flow through HandleMessage.** `obj.Does("verb")` returns a string
that callers concatenate via `+` before passing to AddPlayerMessage or Popup.Show.

```
DidX path:  DidX("block") → Messaging.XDidY() → HandleMessage() → AddPlayerMessage()
                                                   ↑ Prefix patch here

Does path:  obj.Does("begin") + " flying." → AddPlayerMessage(result)
            ↑ returns string               ↑ already concatenated, no HandleMessage
```

Two viable strategies:

**Option A: Patch `Does()` itself** — Harmony Prefix/Postfix on `IComponent.Does(string)`
to return Japanese subject+verb. Caller still appends freeform English via `+`.
Pros: Catches all 307 sites at the source. Cons: Freeform tail text remains English.

**Option B: Catch at downstream sink** — The concatenated string reaches
`AddPlayerMessage` or `Popup.Show`, where existing patches (MessageLogPatch,
PopupTranslationPatch) already intercept. Add Does()-aware pattern matching there.
Pros: Full sentence available. Cons: Must reverse-engineer the concatenation pattern.

Decision: deferred to implementation. The scanner classifies Does() sites as
`type: "VerbComposition"` with the verb string extracted, enabling either approach.

### Double-Translation Prevention

With HandleMessage Prefix active, translated DidX messages flow downstream:

```
HandleMessage Prefix (SVO→SOV) → AddPlayerMessage → MessageLogPatch → MessagePatternTranslator
                                                                       ↑ sees Japanese, must skip
```

Prevention mechanism: HandleMessage Prefix prepends a non-visible marker (e.g., `\x01`)
to translated messages. Downstream patches check for the marker, strip it, and skip
translation. This is lightweight and avoids coupling between patches.

### Inventory Integration

The scanner classifies DidX (272 sites) and Does() (307 sites) separately:

- DidX → `type: "MessageFrame"`, translated via HandleMessage Prefix
- Does() → `type: "VerbComposition"`, strategy decided at implementation

The inventory records the verb string for each site, enabling bulk extraction of the
verb dictionary (118 unique verbs from DidX; Does() verbs may partially overlap):

```json
{
  "id": "XRL.World.Parts.Combat::HandleEvent::L1015",
  "type": "MessageFrame",
  "verb": "block",
  "frame": "DidX",
  "has_object": true,
  "has_with_phrase": true
}
```

### Priority

P6 in the translation order. Implementation depends on:
- Inventory completion (know all 579 DidX + Does() sites)
- Verb dictionary extraction (118 unique verbs, automated from inventory)
- HandleMessage Prefix patch (intercepts at message generation, not per call site)
- Freeform Extra phrase handling (per-verb-Extra pair dictionary + LLM fallback)

## Freshness Management: Decompile Diff

When the game updates (e.g., 2.0.4 → 2.0.5):

### Strategy

```
1. Decompile new Assembly-CSharp.dll → ~/Dev/coq-decompiled-new/
2. diff -rq ~/Dev/coq-decompiled/ ~/Dev/coq-decompiled-new/ → changed_files.txt
3. Re-run scanner phases 1a-1b on changed files only
4. Diff old vs new inventory → report:
   - New sites (added in update)
   - Removed sites (deleted in update)
   - Modified sites (same location, different pattern)
   - Broken translations (translated site changed upstream)
5. Human reviews broken translations, agents handle new sites
```

### Tooling

Add `--diff` flag to `scan_text_producers.py`:

```bash
# Full scan (initial or rebuild)
python scripts/scan_text_producers.py

# Incremental scan after game update
python scripts/scan_text_producers.py --diff ~/Dev/coq-decompiled-new/
```

### Current Status

Game version is fixed at `2.0.4`. This strategy is designed but not implemented
until an update ships. The scanner should be built diff-aware from the start so
the `--diff` flag is a filter, not a separate code path.

## Resolved Design Decisions

| Question | Decision | Rationale |
|----------|----------|-----------|
| LLM classifier execution model | Interactive within Claude Code session (or `claude -p` headless) | No Python→subagent API exists; interactive mode or Anthropic API for automation |
| Intermediate file management | `.gitignore`d, only `candidate-inventory.json` committed | Reproducible locally, review-friendly final output |
| MessageFrame strategy | HandleMessage Prefix patch | void methods make postfix impossible; HandleMessage is the single funnel point |
| Inventory freshness | Decompile diff | Automated detection of changed sites on game update |
