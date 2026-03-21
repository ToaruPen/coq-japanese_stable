# Producer-First Architecture Design

## Two-Layer Architecture

This design has two layers serving different purposes:

1. **ContractRegistry** (design-time + agent workflow): Source of truth for "what routes exist,
   what contract type each uses, and how it should be translated." Agents consult this BEFORE
   adding any translation to determine dictionary vs template vs patch.

2. **ClaimRegistry** (runtime): Weak instance-based registry that tracks which string instances
   were already translated by a producer patch. The audit sink checks this to decide
   pass-through vs unclaimed.

These layers do not conflict — ContractRegistry answers "what SHOULD happen" and ClaimRegistry
records "what DID happen."

## Acceptance Criteria for This Branch

- [ ] `UITextSkin.SetText` is audit-primary, not translation-primary
- [ ] 3+ existing families migrated to contract-based rendering
- [ ] logic-required determination is per-contract, not per-string
- [ ] L1/L2G tests exist for contract units
- [ ] Player.log missing-key reports shift from "string listing" to "unknown contract / unknown owner"
- [ ] AuditRoute false positive rate is bounded: known fixed labels do NOT flood as unclaimed
- [ ] `docs/contract-inventory.json` is generated and referenced from AGENTS.md
- [ ] Agent workflow gate is documented: "check contract inventory before adding translations"

## Contract Types (6)

| Contract | Meaning | Translation Strategy |
|----------|---------|---------------------|
| `Leaf` | Stable literal, compile-time constant | Exact-match dictionary lookup |
| `MarkupLeaf` | Stable literal with markup boundaries (e.g., `{{W|Strength}}`) | Markup-preserving exact-match lookup |
| `LeafList` | Ordered list of stable literals (e.g., skill prerequisite names) | Per-item Leaf lookup, preserve list structure |
| `Template` | Format string with named/indexed slots | Slot extraction → template rendering |
| `Builder` | Ordered slot composition (adj+base+clause+tag) | Decompose → reorder for JP → recompose |
| `MessageFrame` | SVO sentence with actor/verb/object/prep | Frame extraction → SOV rewrite |

`Audit` is not a contract but an operating mode: UITextSkin.SetText runs in audit mode,
detecting strings that reached the sink without any contract claiming them.

### Boundary criteria

- **Leaf vs MarkupLeaf**: MarkupLeaf is used when the dictionary key must include color tags
  or TMP spans (e.g., `{{W|Strength}}`), and the translated output must preserve those markup
  boundaries. If the key is plain ASCII without markup, use Leaf.
- **LeafList vs Leaf**: LeafList is used when a field contains multiple Leaf items in a
  structured list (e.g., comma-separated skill names). The renderer iterates and translates
  each item individually.
- **Template vs Builder**: Template has indexed/named slots filled by `string.Format` or
  equivalent. Builder has semantically ordered slots (adj, base, clause, tag) that require
  JP word-order recomposition. If slots need reordering for Japanese, use Builder.

### Removed from contract types

`PostProcess` (grammar/filter transforms) is not a contract type. It is an internal pipeline
stage within each renderer. For example, a Template renderer may apply JP particle rules as
a post-render step. This is an implementation detail, not a registry-level classification.

## Renderer Interface

Each contract type has a corresponding renderer. The renderer interface is minimal:

```csharp
internal interface IContractRenderer
{
    string Render(string source, RouteContract contract);
}
```

Concrete renderers:

| Contract Type | Renderer | Derives From |
|---------------|----------|-------------|
| `Leaf` | `LeafRenderer` | Wraps `Translator.Translate()` exact-match |
| `MarkupLeaf` | `MarkupLeafRenderer` | Markup-preserving `Translator.Translate()` |
| `LeafList` | `LeafListRenderer` | Iterates items, delegates to `LeafRenderer` |
| `Template` | `TemplateRenderer` | Successor of `UITextSkinTemplateTranslator` |
| `Builder` | `BuilderRenderer` | Successor of `GetDisplayNameRouteTranslator` |
| `MessageFrame` | `MessageFrameRenderer` | New: SVO→SOV rewrite engine |

`MessagePatternTranslator` continues to handle regex-based message log patterns.
It operates at the message sink level and is NOT replaced by MessageFrameRenderer.
MessageFrameRenderer handles structured `XDidY`/`XDidYToZ` frames upstream;
`MessagePatternTranslator` handles free-form messages that bypass the frame API.

## Agent Workflow Gate

### The problem this solves

When an LLM agent encounters untranslated text, it must decide: add a dictionary entry,
add a regex pattern, or implement a Harmony patch. Without guidance, agents default to
dictionary entries — which is wrong for dynamically composed text. The user currently has
to manually instruct agents "this is dynamic, go find the upstream generator."

### The solution: contract inventory as a pre-work gate

ContractRegistry is exported as `docs/contract-inventory.json` at build time. AGENTS.md
references this file. Before adding ANY translation, agents must:

```
1. Check contract-inventory.json for the route that produces this text
2. If route is registered:
   - Leaf/MarkupLeaf → dictionary entry is appropriate
   - Template → add template pattern, not exact key
   - Builder → investigate slot structure, implement builder logic
   - MessageFrame → implement SVO→SOV frame rewrite
3. If route is NOT registered:
   - This is an unknown producer. Investigate upstream FIRST.
   - Do NOT add a dictionary entry until the route's contract type is determined.
```

This gate mechanically enforces the generator-first policy. The classification is
per-contract, not per-string — agents never need to guess whether a specific string
is dynamic.

### Contract inventory format

```json
{
  "routes": [
    {
      "routeId": "StatisticGetHelpTextPatch",
      "schemaId": "Statistic.HelpText",
      "fieldId": null,
      "contractType": "MarkupLeaf",
      "slots": [],
      "action": "Add markup-preserving dictionary entry"
    },
    {
      "routeId": "SkillsAndPowersStatusScreenDetailsPatch",
      "schemaId": "SkillsDetails.Requirements",
      "fieldId": "requirementsText",
      "contractType": "Template",
      "slots": ["skillName", "level"],
      "action": "Add template pattern with {skillName} and {level} slots"
    }
  ]
}
```

## Route Domains

### Phase 1: Infrastructure + 3 proven domains

#### 1. Display Name Builder
- **Hook**: `GetDisplayNameEvent.ProcessFor`
- **Contract**: `Builder(Mark, Adj, Base, Clause, Tag)`
- **Current state**: Patched (GetDisplayNameProcessPatch), but sink-first — translates the composed result
- **Target state**: Intercept at builder stage, decompose into slots, translate slots, recompose in JP order
- **Existing evidence**: `docs/ilspy-analysis.md` §6 (DisplayName Assembly Order), `DescriptionBuilder` constants
- **Prerequisite**: L2G test confirming `DescriptionBuilder` slot extraction against real DLL before migration

#### 2. Long Description / Look
- **Hook**: `Description.GetLongDescription`, `Look.GenerateTooltipContent`
- **Contracts** (split into two):
  - `Description.FlavorBody` → **Leaf** (stable prose text from XML assets)
  - `Description.RulesLines` → **Template** (rules lines with numeric slots, equipment stats)
- **Current state**: Patched (DescriptionLongDescriptionPatch, LookTooltipContentPatch)
- **Target state**: Separate rules-line templates from flavor leaves at the producer boundary
- **Split approach**: Intercept contributing producers, NOT split at output level.
  See Investigation Results OQ-1 for details.
- **Why split**: Treating the entire description as one contract recreates the current tooltip problem where flavor and rules are conflated

#### 3. Screen-specific templates (already producer-first)
- **Patches**: CharacterStatusScreen, SkillsAndPowers, Factions, Statistic.GetHelpText
- **Contracts**: Formalize existing patches with field-level contract types
- **Example**: `SkillsAndPowersStatusScreenDetailsPatch` has:
  - `skillNameText` → **Leaf**
  - `requirementsText` → **Template**
  - `requiredSkillsText` → **LeafList**

### Phase 2: New domains

#### 4. Conversation Runtime
- **Hooks**: `IConversationElement.Prepare()` Postfix (node body),
  `IConversationElement.GetDisplayText()` Postfix (choice labels)
- **Contract**: `Template` (variable-replaced conversation text)
- **Current state**: Hook #11 is `observable` but conversation translation is partial
- **Target state**: Translate after variable replacement but before UI rendering
- **Note**: Slot values (e.g., NPC names from `=subject.name=`) are already resolved
  by `GameText.VariableReplace` during `Prepare()`. See OQ-2 for details.

#### 5. Long Description rules-line refinement
- Extend `Description.RulesLines` contract with full slot definitions based on Phase 1 learnings

#### 6. Message Frame (highest impact, highest risk)
- **Hook**: `XDidY` / `XDidYToZ` (IComponent wrappers)
- **Contract**: `MessageFrame(Actor, Verb, Object, Prep, Extra, Mood)`
- **Current state**: Hooks #4/#5 are `unobserved / unpatched`
- **Target state**: Intercept SVO frame, rewrite to SOV, emit Japanese sentence
- **Why last**: Currently unpatched, highest risk. Branch should be stable before adding this.
- **Impact**: Eliminates the entire class of string-concatenation fragment entries in message dictionaries

## Claim Registry (Runtime Layer)

### Purpose

Bridges the producer→sink gap. When a producer patch translates a string, it registers
the translated string instance in a `ConditionalWeakTable<string, ClaimInfo>`. When
`UITextSkin.SetText` receives a string, it checks the table — if claimed, pass through.

### Why not TranslationScope (thread-static stack)

The original design used a thread-static stack pushed in Prefix and popped in Finalizer.
This fails because producer methods return BEFORE `UITextSkin.SetText` is called:

```
1. Producer patch fires → translates → sets __result or StringBuilder → Finalizer pops scope
2. Game code stores the result
3. Later, UI rendering calls UITextSkin.SetText with stored value
4. Scope is already gone — string appears unclaimed
```

ClaimRegistry solves this because claims are attached to string instances, not call stacks.
The claim survives as long as the string instance is alive (GC-linked via WeakTable).

### Implementation

```csharp
internal static class ClaimRegistry
{
    private static readonly ConditionalWeakTable<string, ClaimInfo> _claims = new();

    internal static void Claim(string translated, ContractKey contractKey)
    {
        _claims.AddOrUpdate(translated, new ClaimInfo(contractKey));
    }

    internal static bool IsClaimed(string text)
    {
        return _claims.TryGetValue(text, out _);
    }

    internal static bool TryGetClaim(string text, out ClaimInfo? claim)
    {
        return _claims.TryGetValue(text, out claim);
    }
}

internal sealed class ClaimInfo
{
    internal ContractKey ContractKey { get; }
    internal ClaimInfo(ContractKey key) => ContractKey = key;
}

internal readonly record struct ContractKey(string RouteId, string SchemaId, string? FieldId);
```

### Three claim categories

Not all patches interact with ClaimRegistry the same way. There are three categories:

**Category 1: Field-write patches** (screen patches that write to UITextSkin fields)
- These write a translated string directly to a UI field (e.g., `___spText.SetText(jp)`)
- When UI framework updates, SetText receives the SAME string instance
- **Claim works**: `ClaimRegistry.Claim()` after field write, SetText sees same instance

**Category 2: `__result` rewrite patches** (GetDisplayName, Grammar, Conversation, Tooltip)
- These return a translated `__result` directly to the game engine
- The result is consumed by game code, NOT by SetText directly
- **Scope Exemption**: no ClaimRegistry needed — string never reaches audit sink

**Category 3: Leaf entries** (stable UI labels with no dedicated upstream producer)
- Game code sets UI text directly (e.g., `inventoryLabel.SetText("Inventory")`)
- No QudJP producer patch intercepts this — the game is the producer
- **Sink-executed Leaf contract**: audit sink checks ContractRegistry for registered Leaf
  entries and translates them there. This is NOT fallback — it is a registered contract
  being executed at the only available interception point.

#### Why StringBuilder paths are not a problem

`DescriptionLongDescriptionPatch` writes to a StringBuilder, which later materializes
as a new string via `ToString()`. This seems like a claim-breaking materialization boundary.
However, the SB content flows to `Look.GenerateTooltipContent` which returns via `__result`
→ intercepted by `LookTooltipContentPatch` (Category 2, Scope Exemption). The SB path
does NOT reach UITextSkin.SetText directly, so ClaimRegistry is not involved.

### Runtime flow

```
Category 1 — field-write patch fires:
  → look up RouteContract from ContractRegistry
  → call renderer for this contract type
  → write translated string to UI field
  → ClaimRegistry.Claim(translatedString, contractKey)
  → later, UITextSkin.SetText fires → IsClaimed → pass through

Category 2 — __result patch fires:
  → look up RouteContract from ContractRegistry
  → call renderer, set __result to translated string
  → return (no Claim needed — string never reaches SetText)

Category 3 — no producer patch fires, game sets UI text directly:
  → UITextSkin.SetText fires
  → ClaimRegistry.IsClaimed(text)? → NO
  → ContractRegistry has a Leaf/MarkupLeaf contract for this key? → YES
  → translate via LeafRenderer, pass through translated text
  → (this is a registered contract execution, not fallback)

Unclaimed path:
  → UITextSkin.SetText fires
  → ClaimRegistry.IsClaimed(text)? → NO
  → ContractRegistry Leaf lookup? → NO
  → blind-spot/noise suppression? → suppress silently if matched
  → otherwise: log as unclaimed (no translation attempt)
```

## Audit Route (UITextSkin.SetText)

### Current behavior
- Primary translation sink: attempts exact-match dictionary lookup on every string
- Falls back to `ResolveObservabilityContext` (stack trace analysis) for route reclassification
- Logs `missing key` for untranslated strings

### Target behavior (contract-directed, no generic fallback)

The sink has three paths, checked in order:

1. **Claimed** (ClaimRegistry hit) → pass through (already translated by upstream producer)
2. **Registered Leaf** (ContractRegistry Leaf/MarkupLeaf match) → translate via LeafRenderer
   - This is NOT generic fallback — it only fires for keys explicitly registered in ContractRegistry
   - The sink is the execution point for Leaf contracts because the game (not QudJP) is the producer
3. **Unclaimed** → no translation. Log as: `[QudJP] AuditSink: unclaimed '<text>' (owner: unknown)`

**No generic dictionary fallback.** The sink does NOT call `Translator.Translate()` on arbitrary
strings. It only translates strings that are either claimed by a producer or explicitly registered
as Leaf contracts. Everything else is logged as unclaimed.

Stack trace reclassification (`ResolveObservabilityContext`) is removed entirely.

The difference from the current sink:
- Current: `Translator.Translate(anything)` — translates everything it can match
- New: `ClaimRegistry` check → `ContractRegistry` Leaf check → audit log — only translates
  what is explicitly registered

### Blind spot and noise policy

Known sources of audit noise that should NOT trigger "unclaimed" investigation:

- `HistoricStringExpander` output: procedurally generated names/lore. Intentionally
  disabled (`docs/procedural-text-status.md`). The corresponding `HistoricStringExpanderPatch`
  is dead code — `TargetMethods()` yields nothing. Tag as `(blind-spot: procedural)`.
- Empty strings, whitespace-only strings, single-character strings: noise. Suppress.
- Strings that are purely numeric (e.g., `"12"`, `"3/5"`): UI data values. Suppress.
- Version strings (e.g., `"1.0.4"`): not game text. Suppress.

### Patch priority

`UITextSkinTranslationPatch` (audit-only) must run AFTER all upstream patches.
Use `[HarmonyPriority(Priority.Last)]` to ensure it is the final Postfix on `SetText`.

## Contract Registry (Design-Time Layer)

The registry is the source of truth for "what text exists and how it should be translated."
It serves BOTH runtime (renderer dispatch) AND workflow (agent pre-work gate) purposes.

### Initialization order

ContractRegistry must be populated BEFORE `UITextSkin.SetText` audit mode activates.
Each `[HarmonyPatch]` class registers its contracts in its static constructor.
This guarantees registration happens at patch application time, before any game method is called.

### Hot-reload safety

`Register()` uses `Overwrite` semantics for duplicate keys (`RouteId + SchemaId + FieldId`).
This prevents double-registration errors when the mod is disabled and re-enabled without
restarting the game (Unity AppDomain persists).

### Leaf batch registration

~600-700 Leaf entries from `ui-default.ja.json` and other high-confidence dictionaries.
These are NOT registered via 600+ individual `Register()` calls. Instead, `ContractRegistry`
provides a `RegisterLeafDictionary(dictionaryId, routeId)` method that reads a `*.ja.json`
file at mod init and registers all entries as Leaf contracts under the specified route.
This leverages the existing `Translator` dictionary loading path.

### Schema (per field, not per route)

```
RouteId              → which Harmony route adapter owns this
SchemaId             → which contract schema applies (e.g., "DisplayName.AdjBaseClauseTag")
FieldId              → specific field within the route (e.g., "skillNameText", "requirementsText")
ContractType         → Leaf | MarkupLeaf | LeafList | Template | Builder | MessageFrame
Slots                → ordered list of semantic slot names (empty for Leaf)
Renderer             → IContractRenderer instance for this contract
ObservabilityFamily  → family label for DynamicTextProbe logging
```

### Contract inventory export

At **build time**, ContractRegistry exports its contents to `docs/contract-inventory.json`.
This file is the agent-facing artifact that enables the workflow gate.

Export is triggered by an L1 test that instantiates ContractRegistry, registers all contracts,
and writes the JSON. This runs in CI — NOT at game runtime. The exported JSON is committed
to the repository so agents can read it without building the mod.

## Screen-Specific Patches: Keep as Producer-First

These stay because they ARE producer-first — they intercept at a specific upstream method.

### Category 1: Field-write patches (need ClaimRegistry)

- `CharacterStatusScreenTranslationPatch` → `UpdateViewFromData` (Template: attributePoints, mutationPoints)
- `SkillsAndPowersStatusScreenTranslationPatch` → `UpdateViewFromData` (Template: spText)
- `SkillsAndPowersStatusScreenDetailsPatch` → `UpdateDetailsFromNode` (6 fields: detailsText=LeafList, skillNameText=MarkupLeaf, learnedText=MarkupLeaf, requirementsText=Template, requiredSkillsText=LeafList, requiredSkillsHeader=MarkupLeaf)
- `FactionsStatusScreenTranslationPatch` → `UpdateViewFromData` (Template: reputation lines)
- `CharacterStatusScreenMutationDetailsPatch` → mutation detail (Template: description + rank text)
- `CharacterStatusScreenAttributeHighlightPatch` → `HandleHighlightAttribute` (MarkupLeaf: 3 fields — primaryAttributesDetails, secondaryAttributesDetails, resistanceAttributesDetails)
- `CharacterStatusScreenHighlightEffectPatch` → `HandleHighlightEffect` (LeafList: mutationsDetails field, via ActiveEffectTextTranslator)
- `AbilityBarUpdateAbilitiesTextPatch` → `AbilityBar.UpdateAbilitiesText` (Template: AbilityCommandText pagination, Leaf: CycleCommandText)

### Category 2: `__result` rewrite patches (Scope Exempt)

- `GetDisplayNameProcessPatch`, `GrammarAPatch`, `GrammarPluralizePatch`
- `StatisticGetHelpTextPatch` → `Statistic.GetHelpText` (Leaf: pure exact-match dictionary lookup, no markup)
- `DescriptionShortDescriptionPatch` → `Description.GetShortDescription` (MarkupLeaf: via WorldModsTextTranslator + Masterwork template)
- `EffectDescriptionPatch` → `Effect.GetDescription` (Leaf/LeafList: via ActiveEffectTextTranslator)
- `EffectDetailsPatch` → `Effect.GetDetails` (Leaf/LeafList: via ActiveEffectTextTranslator)
- `GameObjectShowActiveEffectsPatch` → `GameObject.ShowActiveEffects` (Transpiler: Template for title prefix, Leaf for empty text)

### Shared helpers (not patches)

- `UITextSkinReflectionAccessor` — `GetCurrentText`/`SetCurrentText` via reflection on `UITextSkin`. Used by Category 1 field-write patches. **Must be brought into producer-first before Step 2.**
- `ActiveEffectTextTranslator` — effect text translation (exact match → line-by-line fallback). Uses `ColorAwareTranslationComposer.TranslatePreservingColors`. Consumed by EffectDescription/EffectDetails/HighlightEffect patches.
- `WorldModsTextTranslator` — scoped dictionary lookup + Masterwork template. Uses `ColorAwareTranslationComposer.Strip`/`Restore` directly. Consumed by DescriptionShortDescriptionPatch and StatusLineTranslationHelpers.

### ClaimRegistry rule

If the patch's translated string flows to `UITextSkin.SetText` (via field
assignment, `StringBuilder` write, or any path that ends at the UI rendering layer), it
needs `ClaimRegistry.Claim()`. If it returns a translated `__result` directly to the game
engine without passing through a UI sink, it does not.

## What Gets Removed/Replaced

| Current | Replacement |
|---------|-------------|
| `UITextSkinTranslationPatch` as primary translator | Audit-only mode |
| `ResolveObservabilityContext` (stack trace reclassification) | `ClaimRegistry` instance-based claims |
| Per-string `missing key` → exact-key dictionary growth | Per-contract `unclaimed` → upstream investigation |
| `Translator.Translate()` as the universal entry point | Contract-specific renderers; `Translator` limited to Leaf/MarkupLeaf via `LeafRenderer` |
| Fragment dictionary entries (`"You stagger "`, `" with your shield block!"`) | MessageFrame contract (Phase 2) |
| `TranslatePreservingColors()` in sink patch | Relocated to `ColorAwareTranslationComposer` (shared utility) |

## Test Strategy

### L1: Contract logic tests
- Builder slot decomposition and JP recomposition
- Template slot extraction and rendering
- MarkupLeaf markup preservation across translation
- LeafList iteration and per-item rendering
- ClaimRegistry claim/check/GC behavior
- ContractRegistry registration, lookup, overwrite semantics, and inventory export

### L2: Harmony integration tests
- Upstream patch renders and claims via ClaimRegistry
- Contract renderer produces correct Japanese
- UITextSkin audit mode detects unclaimed strings (no translation attempt)
- UITextSkin passes through claimed strings without re-translation
- UITextSkin translates registered Leaf entries (Category 3 sink-executed Leaf)
- Blind-spot suppression: procedural names, numeric values, empty strings are not logged

### L2 test migration for existing UITextSkinTranslationPatchTests

Existing tests in `UITextSkinTranslationPatchTests.cs` that assume sink-level translation
(e.g., `Prefix_TranslatesKnownKey`) will have their **expected behavior reversed** after
audit-only migration. Per CLAUDE.md policy, existing tests must NOT be deleted.

Migration plan:
- `Prefix_TranslatesKnownKey` → rename to `Prefix_RegisteredLeaf_TranslatesViaContractRegistry`
  and update assertion to verify Leaf contract execution path (not generic Translator.Translate)
- `Prefix_PassesThroughUnknownText` → keep as-is (behavior unchanged — unknown text passes through)
- `TranslatePreservingColors_*` tests → move to `ColorAwareTranslationComposerTests.cs`
  (tests follow the relocated method)
- Add NEW tests: `Prefix_ClaimedString_PassesThrough`, `Prefix_UnclaimedString_LogsAudit`,
  `Prefix_BlindSpot_Suppressed`

### L2G: DLL-assisted tests
- Route target methods resolve against real Assembly-CSharp.dll
- Contract slot structures match actual game method signatures
- DescriptionBuilder slot extraction validated before DisplayName Builder migration
- No regression on existing translations

## Implementation Order

### Phase 1: Workflow gate + infrastructure (workflow improvement comes FIRST)

1. **ContractRegistry + ContractType enum + IContractRenderer + concrete renderers**
   - Simultaneously generate `docs/contract-inventory.json` from registered contracts
   - Update AGENTS.md: "check contract-inventory.json before adding translations"
   - **Workflow improvement is immediately available after this step**

2. **Register existing proven patches in ContractRegistry**
   - CharacterStatusScreen, SkillsAndPowers, Factions, Statistic.GetHelpText
   - Regenerate contract-inventory.json
   - Agents can now consult the inventory for these routes

3. **ClaimRegistry (ConditionalWeakTable)**
   - Category 1 (field-write) patches add `ClaimRegistry.Claim()` after rendering
   - Category 2 (`__result` rewrite) patches are Scope Exempt — no claim needed
     (see Three Claim Categories section for details)
   - No changes to UITextSkin yet — this is additive only

4. **UITextSkin audit mode + Leaf batch registration + TranslatePreservingColors migration** (ATOMIC cutover)
   - Audit sink: ClaimRegistry check → ContractRegistry Leaf check → unclaimed log
   - `RegisterLeafDictionary()` for ~600-700 high-confidence entries (MUST be simultaneous
     with audit cutover to prevent unclaimed flood)
   - Remove `ResolveObservabilityContext` entirely
   - **TranslatePreservingColors migration**: There are TWO distinct TPC implementations
     that must be migrated separately, plus a shared generic utility:

     **Implementation A** — `UITextSkinTranslationPatch.TranslatePreservingColors` (8 callers):
     Complex sink-level logic (~10 steps: stack trace resolution, dedicated route checks,
     multiple fallback paths, `Translator.Translate()` generic fallback). This is the one
     being REMOVED (replaced by audit-only sink). Callers that currently call this method:
     - `DescriptionLongDescriptionPatch.cs`
     - `LookTooltipContentPatch.cs`
     - `ConversationDisplayTextPatch.cs`
     - `PopupTranslationPatch.cs`
     - `QudMenuBottomContextTranslationPatch.cs`
     - `CharGenLocalizationPatch.cs`
     - `HistoricStringExpanderPatch.cs` ← **DEAD CODE**: `TargetMethods()` yields nothing (`yield break`), patch never fires at runtime
     - `InventoryAndEquipmentStatusScreenTranslationPatch.cs`

     **Implementation B** — `GetDisplayNameRouteTranslator.TranslatePreservingColors` (4 callers):
     DisplayName-specific suffix decomposition logic (no generic `Translator.Translate()` fallback).
     This one is KEPT and evolved into the Builder renderer. Callers:
     - `GetDisplayNamePatch.cs`
     - `GetDisplayNameProcessPatch.cs`
     - `InventoryLocalizationPatch.cs`
     - `DeathWrapperFamilyTranslator.cs`

     **Shared infrastructure** — `ColorAwareTranslationComposer.TranslatePreservingColors(string?, Func<string,string>)`:
     Generic Strip→callback→Restore utility. Used by patches such as AbilityBarUpdateAbilitiesTextPatch
     and ActiveEffectTextTranslator (via the convenience wrapper), and by WorldModsTextTranslator
     (via direct `Strip`/`Restore` calls). All Implementation A callers should be migrated to use
     this shared utility with contract-specific callbacks.
   - Verify audit log false positive rate after cutover (run game, check Player.log)

5. **Display Name Builder contract migration**
   - L2G prerequisite: DescriptionBuilder slot validation

6. **Long Description split**: intercept contributing producers
   - `DescriptionShortDescriptionPatch` (FlavorBody) already exists on main — bring into producer-first
   - Remaining: RulesLines producers (effects block, equipment rendering)

### Phase 2: New domains

7. Conversation runtime Template migration
8. Message Frame contract (XDidY/XDidYToZ — new patch, highest risk)
9. Cleanup: remove fragment dictionary entries made obsolete by contracts

### Post-implementation documentation tasks

10. Update `Mods/QudJP/Assemblies/AGENTS.md` with ContractRegistry registration pattern
11. Update `AGENTS.md` (root) to reference `producer-first-design.md` instead of deleted docs
12. Update `README.md` to remove references to deleted `translation-process.md`

## Implementation Checklist (from cross-review)

- [ ] **Before Step 5**: Confirm `GetShortDescription()` signature via L2G test; add to `ilspy-analysis.md` hook table
- [ ] **During Step 4 (atomic cutover)**: Update all 8 Implementation A callers simultaneously; 4 Implementation B callers evolve separately with Builder renderer (full lists in Step 4 above)
- [ ] **During Step 4 (atomic cutover)**: Leaf batch registration MUST be simultaneous with audit mode activation to prevent unclaimed flood
- [ ] **During Step 2 (helper refactor)**: After changing `TranslateStringField` signature, update `MainMenuLocalizationPatchTests.cs` and `OptionsLocalizationPatchTests.cs` (existing tests must not be deleted)
- [ ] **During Step 4**: Migrate `UITextSkinTranslationPatchTests.cs` per L2 test migration plan (rename/repurpose, don't delete)
- [ ] **After Step 4**: Verify audit false positive rate is bounded (run game, check Player.log)
- [ ] **Scope Exemption + Claim Categories documented**: three categories (field-write/ClaimRegistry, __result/exempt, Leaf/sink-executed) in design doc
- [ ] **OQ-3 branch sync**: Cherry-pick or merge `PickGameObjectScreenTranslationPatch` from `main` (commit 67a3931) into `producer-first` before Step 4 cutover
- [ ] **Dead code**: `HistoricStringExpanderPatch` has `yield break` in `TargetMethods()` — patch never fires. Remove from TPC caller list during Step 4 migration; consider deleting entirely or documenting rationale for retention
- [ ] **Branch sync — local patches**: 12 uncommitted patches + 4 modified tracked files on main must be brought into producer-first before Step 4. None depend on Implementation A/B TPC. Breakdown:
  - **Category 1 (need ClaimRegistry)**: AbilityBarUpdateAbilitiesTextPatch, CharacterStatusScreenAttributeHighlightPatch, CharacterStatusScreenHighlightEffectPatch, SkillsAndPowersStatusScreenDetailsPatch
  - **Category 2 (Scope Exempt)**: DescriptionShortDescriptionPatch, StatisticGetHelpTextPatch, EffectDescriptionPatch, EffectDetailsPatch, GameObjectShowActiveEffectsPatch
  - **Helpers (not patches)**: UITextSkinReflectionAccessor (critical infra — required by multiple Cat 1 patches), ActiveEffectTextTranslator, WorldModsTextTranslator
  - **Modified tracked files (all 4 need merge)**: CharacterStatusScreenMutationDetailsPatch (UITextSkinReflectionAccessor dep), SkillsAndPowersStatusScreenTranslationPatch (6 new internal TPC methods), StatusLineTranslationHelpers (WorldModsTextTranslator insertion), UITextSkinTemplateTranslator (UITextSkinReflectionAccessor dep)
- [ ] **Contract type correction**: StatisticGetHelpTextPatch is **Leaf** (plain exact-match, no markup), not MarkupLeaf as previously documented

## Investigation Results (formerly Open Questions)

### OQ-1: Long Description split boundary — RESOLVED

**Finding: Output-level split is not possible.**

`GetLongDescription(StringBuilder)` mixes flavor and rules in a single StringBuilder.
The `\n\n` delimiter appears both between sections AND within flavor text itself
(e.g., Mark/Weight appended with `\n\n` inside `GetShortDescription`).

Build sequence:
1. `SB.Append(Short)` — flavor body (from `GetShortDescription()`, may contain `\n\n`)
2. `"\n\nGender: "` + value (if applicable)
3. `"\n\nPhysical features: "` + comma-separated list
4. `"\nEquipped: "` + comma-separated list
5. `"\n\n"` + effects block (from `GetEffectsBlock` event)

**Decision**: Do NOT split at the output. Instead, split at the **producer boundary**:
- FlavorBody: `DescriptionShortDescriptionPatch` already exists (untracked on main). It patches
  `Description.GetShortDescription(bool, bool, string)` as a `ref __result` rewrite (Category 2,
  MarkupLeaf via WorldModsTextTranslator). This patch must be brought into producer-first.
- RulesLines: Patch the Body/Effects append calls individually via Transpiler or
  by intercepting the contributing events (`GetEffectsBlock`, equipment rendering)

### OQ-2: Conversation slot ownership — RESOLVED

**Finding: `GameText.VariableReplace` handles all `=token=` expansion during `Prepare()` stage.**

Pipeline: `Prepare()` → `PrepareTextEvent.Send` → `GameText.VariableReplace` → `PrepareTextLateEvent.Send`

After `Prepare()`, `IConversationElement.Text` contains fully assembled text with no remaining tokens.

**Optimal hook points:**
- Node body: `IConversationElement.Prepare()` Postfix — text is complete, before UI render
- Choice labels: `IConversationElement.GetDisplayText()` Postfix — after DisplayTextEvent

### OQ-3: TranslateStringField helper migration — RESOLVED

**Finding: Call sites exist in MainMenuLocalizationPatch, OptionsLocalizationPatch,
and PickGameObjectScreenTranslationPatch.**

`PickGameObjectScreenTranslationPatch` exists on `main` (commit 67a3931) and
`feat/issue-45-triage-pipeline`, but NOT on the `producer-first` branch because
this branch was created before that commit. The file must be cherry-picked or
merged into `producer-first` before Step 4 cutover.

Verified call sites (on main):
- `PickGameObjectScreenTranslationPatch`: 2× `TranslateStringFieldsInCollection` + 2× `TranslateStringField`
- `MainMenuLocalizationPatch`: `TranslateStringField` calls
- `OptionsLocalizationPatch`: `TranslateStringField` calls

The migration approach (refactor helpers to accept `RouteContract`, not inline) remains valid.

### OQ-4: Leaf entry inventory — RESOLVED

**Finding: ~600-700 Leaf entries for initial cutover from high-confidence dictionaries.**

**Registration approach**: `RegisterLeafDictionary(dictionaryId, routeId)` batch method.
Leaf entries are owned by the upstream route that causes them to appear at the sink.
For entries currently falling through to the generic `Translator.Translate()` path,
ownership is assigned to a dedicated `LeafRoute` that represents "stable UI labels
with no upstream producer other than the game's own static UI code."
