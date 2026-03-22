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

## Existing Infrastructure: Keep As-Is

| Component | Role | Change Needed |
|-----------|------|---------------|
| `ColorAwareTranslationComposer` | Strip/Restore color markup | None |
| `MessagePatternTranslator` | Regex message log routes | Extend with new patterns from inventory |
| `GetDisplayNameRouteTranslator` | DisplayName suffix decomposition | None |
| `UITextSkinTemplateTranslator` | Template translation at sink | None |
| `StatusLineTranslationHelpers` | Status line translation | None |
| `Translator` + 38 dictionaries | Leaf lookup | Add entries from inventory |
| 50 committed patches on main | Various screen/effect/description translations | Register in inventory as `translated` |
| `UITextSkinTranslationPatch` | Sink-level translation | Keep for now; Phase 2 may convert to audit |

## Scanner Implementation

### Technology

- **Python 3.12+** (consistent with existing `scripts/` tooling)
- **ast-grep** for structural C# pattern matching (installed, v0.42.0)
- **LLM calls** for steps 5-6 classification (decompiled source reading)
- Output: JSON (candidate-inventory.json)

### Scanner Components

```
scripts/
  scan_text_producers.py       # Main scanner entry point
  scanner/
    __init__.py
    ast_grep_patterns.py       # ast-grep pattern definitions for 5 sink families
    classifier.py              # Rule-based classification (steps 1-4)
    llm_classifier.py          # LLM-assisted classification (steps 5-6)
    inventory.py               # Inventory data model and JSON I/O
    cross_reference.py         # Cross-ref with existing patches/dictionaries
```

### Cross-Reference with Existing Work

The scanner must know what's ALREADY translated to avoid duplicate work.
Cross-reference sources:

1. **Existing patches** (`src/Patches/*.cs`) — grep for target methods → mark as `translated`
2. **Existing dictionaries** (`Localization/Dictionaries/*.ja.json`) — keys → mark matching Leaf sites
3. **Existing XML translations** (`Localization/*.jp.xml`) — IDs → mark matching blueprint text

## Success Criteria

- [ ] candidate-inventory.json contains ALL unique sink call sites (~1677 deduped)
- [ ] Auto-classification (steps 1-4) covers ≥75% with high confidence
- [ ] LLM-assisted classification (steps 5-6) brings total to ≥90%
- [ ] Unresolved sites are <10% of total
- [ ] AGENTS.md references candidate-inventory.json as the workflow gate
- [ ] Agent can consume inventory and generate translation artifacts without manual guidance
- [ ] Existing translations (50 patches + 38 dictionaries) are cross-referenced and marked `translated`

## Open Questions

1. **LLM classifier cost**: Steps 5-6 may require reading 1000+ files. Should the LLM
   classifier run as a batch job or on-demand per-file?
2. **Inventory freshness**: When game updates, how to detect which sites changed?
   Diff the decompiled source against previous version.
3. **MessageFrame priority**: 548 call sites is large. Should we build SVO→SOV rewrite
   infrastructure first, or handle each one as a regex pattern in MessagePatternTranslator?
