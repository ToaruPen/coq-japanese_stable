# Source-First Architecture Design (DRAFT)

## Premise

Full decompilation of Assembly-CSharp.dll (5474 files, 40MB) makes incremental route
discovery obsolete. Static analysis of decompiled source can classify ~90%+ of all
translatable text without ever running the game. The remaining ~5-10% falls back to
runtime log analysis.

This replaces the producer-first design (archived as `producer-first-design-old.md`),
which assumed only 26 known hook points and incremental discovery.

## Validated Coverage Numbers (Codex + Claude independent audit, 2026-03-22)

| Method | Coverage | Sites |
|--------|----------|------:|
| Sink-line-only (literal + inline template) | **78.2%** | 1460/1866 |
| + LLM reading decompiled source (variable tracing, call chains) | **91.8%** | 1540/1677 (deduped) |
| Requires runtime log analysis | **~8%** | 250-300 |

The ~8% that genuinely require runtime evidence:
- Event parameter injection — `SourceDescription` depends on event firing context
- Runtime payload UI — `Notification.Data` displays arbitrary notification payloads
- Save/persisted text — score details are records from past runs
- Generic popup/message wrappers — `Popup.Show(Message)` where producer is external
- Blueprint/tag strings — text from XML tags not visible in C# source
- Ability/data asset strings — display text loaded from non-code assets

## Two-Layer Problem This Solves

1. **Why isn't everything translated?** — Decompiled source wasn't fully available.
   Now it is. LLM agents can scan all 1885+ sink call sites and generate translation
   artifacts (dictionary entries or patches) directly from source.

2. **Why do agents misclassify dynamic text as dictionary entries?** — They couldn't
   distinguish static from dynamic. Now the source scanner classifies every call site
   before any agent touches it.

## Architecture Overview

```
Phase 1: Static Scan (Python + ast-grep + LLM)
  ~/Dev/coq-decompiled/ (5474 files)
    → Scanner classifies all sink call sites
    → Generates candidate-inventory.json (all routes, with confidence)
    → LLM agents consume inventory to produce translations

Phase 2: Runtime Verification (C# + Harmony, only for unresolved ~5-10%)
  Player.log + audit sink
    → Captures text that static analysis couldn't classify
    → Agent traces upstream in decompiled source → resolves
```

## Phase 1: Source Scanner

### Input

All `.cs` files in `~/Dev/coq-decompiled/`, organized by namespace.

### Scan Targets (5 sink families)

| Sink | Pattern | Files | Call Sites |
|------|---------|------:|----------:|
| UI text | `$_.SetText($$$)` | 359 | ~500+ |
| Message log | `$_.AddPlayerMessage($$$)` | 671 | ~800+ |
| Popup | `Popup.Show($$$)` | 843 | ~1000+ |
| Action narration | `DidX/DidXToY` | 303 | 548 |
| Display name | `$_.GetDisplayName($$$)` | 191 | ~250+ |

Additionally, text-producing overrides:

| Producer | Pattern | Files |
|----------|---------|------:|
| Effect descriptions | `override GetDescription/GetDetails` | 180 + 171 |
| Mutation descriptions | `override GetDescription/GetLevelText` | 127 + 132 |
| Skill descriptions | in `XRL.World.Parts.Skill/` | 173 |

### Classification Algorithm

For each sink call site, classify by argument pattern:

```
1. Literal string → Leaf
   SetText("Inventory")  →  { type: "Leaf", confidence: "high", key: "Inventory" }

2. string.Format / interpolation → Template
   SetText(string.Format("Level: {0}", x))  →  { type: "Template", confidence: "high", slots: ["level"] }

3. GetDisplayName result → Builder
   SetText(go.GetDisplayName())  →  { type: "Builder", confidence: "high" }

4. DidX/DidXToY → MessageFrame
   ParentObject.DidX("block", ...)  →  { type: "MessageFrame", confidence: "high" }

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

Steps 1-4 are mechanical (ast-grep + regex). ~1460 sites, ~78%.
Steps 5-6 require LLM reading decompiled source. ~80-250 additional sites, ~14%.
Step 7 is the remainder. ~250-300 sites, ~8%.

### Output: candidate-inventory.json

```json
{
  "version": "1.0",
  "game_version": "2.0.4",
  "scan_date": "2026-03-22",
  "stats": {
    "total_sites": 1677,
    "total_raw": 1866,
    "auto_classified": 1460,
    "llm_classified": 80,
    "unresolved": 137
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
   b. type=Template → create template pattern with slot mapping
   c. type=Builder → verify existing GetDisplayNameRouteTranslator handles it
   d. type=MessageFrame → implement SVO→SOV rewrite (or add to MessagePatternTranslator)
   e. type=needs_review → read decompiled source, reclassify, then act
4. Agent updates inventory status to `translated`
5. Repeat until needs_translation/needs_patch count → 0
```

### Priority Order (by translation impact)

| Priority | Domain | Sites | Rationale |
|----------|--------|------:|-----------|
| P0 | UI screen labels (Qud.UI SetText literals) | ~27 | Immediately visible, Leaf, trivial |
| P1 | Effect/mutation/skill descriptions | ~600 | High volume, mostly Leaf/Template |
| P2 | Screen-specific templates (CharacterStatus, Skills, Factions) | ~50 | Already partially patched |
| P3 | Popup.Show literals | ~262 | High volume, Leaf |
| P4 | AddPlayerMessage literals | ~126 | Message log text |
| P5 | Conversation text | ~40 | Template with =variable= slots |
| P6 | MessageFrame (DidX/DidXToY) | ~548 | Highest complexity, SVO→SOV |
| P7 | Trade/Tinkering/Book screens | ~80 | Mixed Template/Leaf |
| P8 | StringBuilder compositions | ~117 | Need slot analysis |
| P9 | Unresolved (runtime fallback) | ~265 | Phase 2 |

## Phase 2: Runtime Verification (for Unresolved ~5-10%)

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
but it is NOT a prerequisite for Phase 1. It only matters for the ~265 unresolved sites
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

| Component | Role | Rationale |
|-----------|------|-----------|
| `StatusScreenFontPatch` (multiple) | CJK font injection per screen | Pure rendering, no translation logic |
| `PopupFontPatch` | Popup font consistency | Pure rendering |
| `TextMeshProTextTranslationPatch` | TextMeshPro rendering | Pure rendering |
| `UITextFieldTranslationPatch` | Input field font | Pure rendering |
| `InventoryLocalizationPatch` | Inventory display name | Rendering + GetDisplayNameRouteTranslator delegation |
| `InventoryLineTranslationPatch` | Inventory row formatting | Display formatting |
| `InventoryScreenTranslationPatch` | Inventory screen layout | Display formatting |
| `EquipmentLineTranslationPatch` | Equipment row formatting | Display formatting |
| `EquipmentScreenTranslationPatch` | Equipment screen layout | Display formatting |
| `BaseLineWithTooltipTranslationPatch` | Tooltip rendering | Display formatting |
| `SelectableTextMenuItemTranslationPatch` | Menu item rendering | Display formatting |
| `ColorAwareTranslationComposer` | Strip/Restore color markup | Utility, reusable as-is |

### Tier 2: Rewrite with Source-First Knowledge (translation logic)

| Component | Current Approach | Source-First Improvement |
|-----------|-----------------|------------------------|
| `UITextSkinTranslationPatch` | Stack inspection + context guessing | Scanner classifies every SetText site upfront; route is known before runtime |
| `MessagePatternTranslator` | 30+ hand-written regex patterns | Scanner extracts all AddPlayerMessage patterns; regex generated from inventory |
| `PopupTranslationPatch` | 3 methods (ShowBlock, ShowOptionList, ShowConversation) | Expand to all Popup variants; classification from inventory |
| `MessageLogPatch` | Delegates to MessagePatternTranslator | Rewrite with Messaging-class-level postfix patch for SVO→SOV |
| `GetDisplayNamePatch` / `GetDisplayNameRouteTranslator` | Suffix decomposition heuristics | Source-traced name composition; inventory knows exact Builder patterns |
| `EffectDescriptionPatch` / `EffectDetailsPatch` | Override hook + dictionary | Scanner identifies all 347 GetDescription overrides; generates complete dictionary |
| `MutationDescriptionPatch` / `MutationLevelTextPatch` | Override hook + dictionary | Scanner identifies all 127+132 overrides; generates complete dictionary |
| `SkillDescriptionPatch` | Override hook + dictionary | Scanner identifies all skill descriptions; generates complete dictionary |
| `CharacterStatusScreenTranslationPatch` | Manual template extraction | Scanner classifies all Template sites in CharacterStatusScreen |
| `SkillsAndPowersStatusScreenTranslationPatch` | Manual template extraction | Scanner classifies all sites in SkillsAndPowersStatusScreen |
| `FactionsStatusScreenTranslationPatch` | Manual template extraction | Scanner classifies all sites in FactionsStatusScreen |
| `GrammarPatch` family (8 patches) | Individual method patches | Keep structure, but enhance with source-informed rules |
| `MainMenuTranslationPatch` | Hard-coded string mapping | Scanner identifies all Leaf sites; dictionary-driven |
| `OptionsScreenTranslationPatch` | Hard-coded string mapping | Scanner identifies all Leaf sites; dictionary-driven |
| `ConversationDisplayTextPatch` / `ConversationTextDynamicPatch` | Pattern matching | Scanner classifies conversation text composition |

### Migration Strategy

Tier 2 rewrites follow the scanner output priority (P0→P9):

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

- **Python 3.12+** (consistent with existing `scripts/` tooling)
- **ast-grep** for structural C# pattern matching (installed, v0.42.0)
- **Claude Code subagents** for steps 5-6 classification (reads decompiled source in-session, no API cost)
- Output: JSON (`candidate-inventory.json` — committed to repo; intermediate files `.gitignore`d)

### Pipeline Phases (Approach C: Hybrid)

```
Phase 1a — ast-grep batch scan (mechanical, seconds)
  5 sink patterns × ~/Dev/coq-decompiled/ → .scanner-cache/raw_hits.jsonl
  ~1866 raw hits extracted with file, line, matched code

Phase 1b — Python rule classifier (mechanical, seconds)
  raw_hits.jsonl → rule classifier → .scanner-cache/inventory_draft.json
  Steps 1-4: Leaf/Template/Builder/MessageFrame at high confidence (~78%)

Phase 1c — Claude Code subagent classifier (targeted, minutes)
  Filter: confidence=medium or low from inventory_draft (~300-400 sites)
  Subagent reads relevant decompiled source → reclassifies
  Steps 5-6: variable tracing, call chain analysis (~14% additional)

Phase 1d — Cross-reference (mechanical, seconds)
  Match against existing patches (src/Patches/*.cs),
  dictionaries (Localization/Dictionaries/*.ja.json),
  and XML translations (Localization/*.jp.xml)
  → Mark matched sites as `translated`

Output: candidate-inventory.json (committed to docs/)
```

Intermediate files (`.scanner-cache/`) are `.gitignore`d — each contributor regenerates locally.

### Scanner Components

```
scripts/
  scan_text_producers.py       # Main entry: orchestrates phases 1a-1d
  scanner/
    __init__.py
    ast_grep_runner.py         # Phase 1a: ast-grep batch execution per sink family
    rule_classifier.py         # Phase 1b: mechanical classification (steps 1-4)
    llm_classifier.py          # Phase 1c: subagent dispatch for medium/low sites
    cross_reference.py         # Phase 1d: match existing patches/dictionaries
    inventory.py               # Inventory data model, JSON I/O, diffing
```

### Cross-Reference with Existing Work

The scanner must know what's ALREADY translated to avoid duplicate work.
Cross-reference sources:

1. **Existing patches** (`src/Patches/*.cs`) — grep for target methods → mark as `translated`
2. **Existing dictionaries** (`Localization/Dictionaries/*.ja.json`) — keys → mark matching Leaf sites
3. **Existing XML translations** (`Localization/*.jp.xml`) — IDs → mark matching blueprint text

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
| `llm_classifier.py` | L1 unit (mocked) | Given a medium-confidence site, assert correct subagent prompt |
| `cross_reference.py` | L1 unit | Given existing patch list + inventory, assert correct `translated` marking |
| `inventory.py` | L1 unit | JSON round-trip, dedup, diff operations |
| `MessageFrameTranslator` | L1 unit | Given assembled English DidX output, assert correct SOV Japanese |
| Scanner integration | L2 | Run full pipeline on test fixture directory, assert inventory schema |

### Rationale

The scanner produces a JSON artifact that all downstream agents depend on. Incorrect
classification cascades into wrong translation artifacts. TDD ensures each classification
step is verified against known inputs before the pipeline is assembled.

## Success Criteria

- [ ] candidate-inventory.json contains ALL unique sink call sites (deduped)
- [ ] Auto-classification (steps 1-4) covers ≥75% with high confidence
- [ ] LLM-assisted classification (steps 5-6) brings total to ≥90%
- [ ] Unresolved sites are <10% of total
- [ ] AGENTS.md references candidate-inventory.json as the workflow gate
- [ ] Agent can consume inventory and generate translation artifacts without manual guidance
- [ ] Existing translations (50 patches + 38 dictionaries) are cross-referenced and marked `translated`
- [ ] All scanner components have passing tests before integration

## MessageFrame Translation: SVO→SOV Rewrite Engine

548 DidX/DidXToY call sites make per-pattern regex infeasible. Instead, build a
generalized SVO→SOV rewrite engine.

### Problem

English: `The bear blocks the attack with its shield.` (Subject-Verb-Object)
Japanese: `クマは盾で攻撃を防いだ。` (Subject-Object-Verb)

DidX/DidXToY frames produce English word-order messages at runtime. Each call site
uses a verb string + optional object + optional preposition phrase. The combinations
are too numerous for individual regex patterns.

### Approach

Build a `MessageFrameTranslator` that:

1. **Parses** DidX/DidXToY output into semantic slots: `{subject, verb, object, instrument, extra}`
2. **Looks up** verb → Japanese verb mapping (dictionary-driven, ~100-150 unique verbs)
3. **Recomposes** in SOV order with Japanese particles: `{subject}は{instrument}で{object}を{verb_ja}`

### Inventory Integration

The scanner classifies all 548 MessageFrame sites. The inventory records the verb
string for each site, enabling bulk extraction of the verb dictionary:

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
- Inventory completion (know all 548 sites)
- Verb dictionary extraction (automated from inventory)
- SOV recomposition logic (the core engine)

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
| LLM classifier execution model | Claude Code subagents (in-session) | Zero API cost, leverages existing tooling, ~300-400 sites only |
| Intermediate file management | `.gitignore`d, only `candidate-inventory.json` committed | Reproducible locally, review-friendly final output |
| MessageFrame strategy | SVO→SOV rewrite engine | 548 sites too many for individual regex; generalized engine scales |
| Inventory freshness | Decompile diff | Automated detection of changed sites on game update |
