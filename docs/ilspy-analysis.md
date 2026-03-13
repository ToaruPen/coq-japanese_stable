# ILSpy Text Pipeline Analysis (Game 2.0.4)

Evidence base: historical ILSpy notes captured in this document plus current game-DLL-assisted probes in `Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs`.

Decompilation gaps observed:
- `XRL_Messages_Messaging.retry.cs`: decompilation failed - namespace probe needed.
- `XRL_World_Messaging.retry.cs`: decompilation failed - namespace probe needed.
- `XRL_World_Conversations_ConversationUI.cs`: decompilation failed - namespace probe needed.
- `XRL_World_ConversationUI.retry.cs`: decompilation failed - namespace probe needed.
- `XRL_UI_Messaging.cs`: decompilation failed - namespace probe needed.

## 1. Text Pipeline Overview

```text
XML assets
  (Conversations.xml, ObjectBlueprints.xml, mod merge files)
        |
        v
Loaders
  - ConversationLoader.ReadConversation / LoadConversations
  - GameObjectFactory.LoadBlueprints / LoadBakedXML
        |
        v
Text assembly
  - IConversationElement.Prepare / GetDisplayText
  - GetDisplayNameEvent + DescriptionBuilder ordering
  - Description.GetLongDescription
        |
        v
Variable + grammar transform
  - GameText.Process / VariableReplace
  - ReplaceBuilder.Process
  - Grammar.A / Pluralize / MakePossessive
  - HistoricStringExpander / TextFilters
        |
        v
Markup/color transform
  - Markup.Parse / Transform / Strip
        |
        v
UI/message rendering
  - MessageQueue.AddPlayerMessage
  - Popup.ShowConversation / Popup.Show
  - Look.GenerateTooltipContent
  - ConversationUI.Render
```

## 2. Hook Points Table

| # | Hook Point | Class | Patch Type | Observability Status | Test Layer |
|---|------------|-------|------------|----------------------|------------|
| 1 | `A(string,bool)` | `XRL.Language.Grammar` | Postfix | `observable` | L1 |
| 2 | `Pluralize(string)` | `XRL.Language.Grammar` | Prefix | `observable` | L1 |
| 3 | `MakePossessive(string)` | `XRL.Language.Grammar` | Prefix | `observable` | L1 |
| 4 | `XDidY(...)` | `XRL.World.IComponent<T>` wrapper | Prefix | `unobserved / unpatched` | L2 |
| 5 | `XDidYToZ(...)` | `XRL.World.IComponent<T>` wrapper | Prefix | `unobserved / unpatched` | L2 |
| 6 | `AddPlayerMessage(string,string,bool)` | `XRL.Messages.MessageQueue` | Prefix | `observable` | L3 |
| 7 | `Transform(string,bool)` | `ConsoleLib.Console.Markup` | Prefix | `unobserved / unpatched` | L2 |
| 8 | `Parse(string)` | `ConsoleLib.Console.Markup` | Transpiler | `unobserved / unpatched` | L2 |
| 9 | `Strip(string)` | `ConsoleLib.Console.Markup` | Postfix | `unobserved / unpatched` | L2 |
| 10 | `Prepare()` | `XRL.World.Conversations.IConversationElement` | Postfix | `unobserved / unpatched` | L2 |
| 11 | `GetDisplayText(bool)` | `XRL.World.Conversations.IConversationElement` | Postfix | `observable` | L2 |
| 12 | `Render()` | `XRL.UI.ConversationUI` | Prefix | `unobserved / unpatched` | L3 |
| 13 | `GenerateTooltipContent(GameObject)` | `XRL.UI.Look` | Postfix | `observable` | L2 |
| 14 | `ShowConversation(...)` | `XRL.UI.Popup` | Prefix | `unobserved / unpatched` | L3 |
| 15 | `GetFor(...)` | `XRL.World.GetDisplayNameEvent` | Postfix | `observable` | L2 |
| 16 | `ProcessFor(GameObject,bool)` | `XRL.World.GetDisplayNameEvent` | Prefix | `observable` | L2 |
| 17 | `GetLongDescription(StringBuilder)` | `XRL.World.Parts.Description` | Postfix | `observable` | L2 |
| 18 | `VariableReplace(StringBuilder, GameObject, GameObject, bool)` | `XRL.GameText` | Prefix | `unobserved / unpatched` | L2 |
| 19 | `Process(StringBuilder,...)` | `XRL.GameText` | Prefix | `unobserved / unpatched` | L2 |
| 20 | `ExpandString(string, HistoricEntitySnapshot, History, Dictionary<string,string>, Random)` | `HistoryKit.HistoricStringExpander` | Prefix | `intentionally disabled` | L2 |
| 21 | `Filter(string,string,string,bool)` | `XRL.Language.TextFilters` | Prefix | `unobserved / unpatched` | L2 |
| 22 | `ReadConversation(string,BuildContext)` | `XRL.World.Conversations.ConversationLoader` | Postfix | `unobserved / unpatched` | L2 |
| 23 | `LoadConversations()` | `XRL.World.Conversations.ConversationLoader` | Postfix | `unobserved / unpatched` | L2 |
| 24 | `LoadBlueprints()` | `XRL.World.GameObjectFactory` | Postfix | `unobserved / unpatched` | L2 |
| 25 | `LoadBakedXML(ObjectBlueprintXMLData)` | `XRL.World.GameObjectFactory` | Postfix | `unobserved / unpatched` | L2 |
| 26 | `Process()` | `XRL.World.Text.ReplaceBuilder` | Prefix | `unobserved / unpatched` | L2 |

### 2.1 Current blind spots outside the main hook table

| Patch class | Status | Reason |
|------------|--------|--------|
| `CharGenLocalizationPatch` | `observable (heuristic)` | current runtime observations route character-generation menus, callings, mutation lists, cybernetics, and stat-allocation text through this patch; some downstream sink-only UITextSkin residues still remain |
| `GrammarSplitOfSentenceListPatch` | `currently skipped` | not part of the currently claimed observable route set |
| `GrammarInitCapsPatch` | `currently skipped` | not part of the currently claimed observable route set |
| `GrammarCardinalNumberPatch` | `currently skipped` | not part of the currently claimed observable route set |

## 3. Detailed Hook Points (16+)

### 1) Grammar.A() - article removal
- Signature: `public static string A(string Word, bool Capitalize = false)`
- Classification rationale: no Unity or IO; deterministic English article logic.
- Recommended patch type: Postfix.
- JP purpose: remove/replace `a/an` behavior for Japanese noun phrases.
- DummyTarget hint: `DummyGrammar.A("apple")` and `DummyGrammar.A("sword")` expectation checks.

### 2) Grammar.Pluralize() - plural bypass
- Signature: `public static string Pluralize(string word)`
- Classification rationale: string/rule transform only.
- Recommended patch type: Prefix (short-circuit).
- JP purpose: bypass English plural morphology and preserve source token.
- DummyTarget hint: include irregular (`child`), suffix (`knife`), marker (`=pluralize=`) cases.

### 3) Grammar.MakePossessive() - possessive bypass
- Signature: `public static string MakePossessive(string word)`
- Classification rationale: local string operation.
- Recommended patch type: Prefix.
- JP purpose: avoid apostrophe possessive surface forms.
- DummyTarget hint: test `you`, `boss`, and `name}}` cases.

### 4) Messaging.XDidY() - SVO->SOV candidate
- Signature (observable wrapper):
  `public static void XDidY(GameObject Actor, string Verb, string Extra = null, string EndMark = null, string SubjectOverride = null, string Color = null, GameObject ColorAsGoodFor = null, GameObject ColorAsBadFor = null, bool UseFullNames = false, bool IndefiniteSubject = false, GameObject SubjectPossessedBy = null, GameObject Source = null, bool DescribeSubjectDirection = false, bool DescribeSubjectDirectionLate = false, bool AlwaysVisible = false, bool FromDialog = false, bool UsePopup = false, GameObject UseVisibilityOf = null)`
- Classification rationale: game object and messaging pipeline dependent.
- Recommended patch type: Prefix.
- JP purpose: reorder actor/verb/object phrase layout from English SVO to JP SOV.
- DummyTarget hint: create `DummyMessaging.XDidY(...)` recorder (args capture) and assert reordered output string.

### 5) Messaging.XDidYToZ() - SVO->SOV candidate
- Signature (observable wrapper):
  `public static void XDidYToZ(GameObject Actor, string Verb, string Preposition, GameObject Object, string Extra = null, string EndMark = null, string SubjectOverride = null, string Color = null, GameObject ColorAsGoodFor = null, GameObject ColorAsBadFor = null, bool UseFullNames = false, bool IndefiniteSubject = false, bool IndefiniteObject = false, bool IndefiniteObjectForOthers = false, bool PossessiveObject = false, GameObject SubjectPossessedBy = null, GameObject ObjectPossessedBy = null, GameObject Source = null, bool DescribeSubjectDirection = false, bool DescribeSubjectDirectionLate = false, bool AlwaysVisible = false, bool FromDialog = false, bool UsePopup = false, GameObject UseVisibilityOf = null)`
- Classification rationale: same as above, plus preposition slot.
- Recommended patch type: Prefix.
- JP purpose: rewrite preposition-based phrase into particle-based JP syntax.
- DummyTarget hint: `DummyMessageBuilder` with fields `{subject, verb, object, prep}` then assert JP order.

### 6) MessageQueue.AddPlayerMessage() - interception point
- Signature: `public static void AddPlayerMessage(string Message, string Color = null, bool Capitalize = true)`
- Classification rationale: accesses `XRLCore.Core.Game.Player.Messages` and logs exceptions via Unity debug.
- Recommended patch type: Prefix.
- JP purpose: last-mile message rewrite before queue insertion.
- DummyTarget hint: `DummyQueue.Add(string)` sink; assert color wrapper and capitalization handling survive patch.

### 7) Markup.Transform() - color code preservation
- Signature: `public static string Transform(string text, bool refreshAtNewline = false)`
- Classification rationale: parser/pool static state, no Unity runtime object required.
- Recommended patch type: Prefix or Postfix.
- JP purpose: avoid breaking `{{shader|...}}` and `&/^` codes while translating text segments.
- DummyTarget hint: feed nested markup and verify exact control code balance.

### 8) Markup.Parse() - deep parser hook
- Signature: `private static Markup Parse(string text)`
- Classification rationale: internal parser + pooled node allocation.
- Recommended patch type: Transpiler.
- JP purpose: custom parser behavior if translator adds token-aware segmentation.
- DummyTarget hint: parse tree snapshot compare (`DebugDump`) before/after patch.

### 9) Markup.Strip() - formatting stripper
- Signature: `public static string Strip(string Text)`
- Classification rationale: pooled `StringBuilder`, deterministic scan/remove loop.
- Recommended patch type: Postfix.
- JP purpose: safe plain-text extraction for dictionary lookup or MT cache keys.
- DummyTarget hint: cases with nested `{{a|{{b|x}}}}` and escaped control chars.

### 10) Conversation display text assembly
- Signature: `public virtual string GetDisplayText(bool WithColor = false)` (`IConversationElement`)
- Classification rationale: conversation events + variable replace + color wrapper.
- Recommended patch type: Postfix.
- JP purpose: per-node conversation text translation while preserving event substitutions.
- DummyTarget hint: `DummyConversationElement.Text = "=subject.name= says hi"` and assert transformed output.

### 11) Conversation prepare stage
- Signature: `public virtual void Prepare()` (`IConversationElement`)
- Classification rationale: selects random variant, runs `PrepareTextEvent` then `GameText.VariableReplace`.
- Recommended patch type: Postfix.
- JP purpose: pre-display normalization and token policy before visible render.
- DummyTarget hint: fixed random seed + known text variant to ensure reproducible assertions.

### 12) Conversation UI render handoff
- Signature: `public static void Render()` (`XRL.UI.ConversationUI`)
- Classification rationale: routes through UI popup and game interaction events.
- Recommended patch type: Prefix.
- JP purpose: inject translated title/options right before popup draw.
- DummyTarget hint: `DummyPopup.ShowConversation(...)` returns selection index; verify option text passed in JP order.

### 13) Look display pipeline
- Signature: `public static string GenerateTooltipContent(GameObject O)` (`XRL.UI.Look`)
- Classification rationale: composes display name, long description, wound text, then `Markup.Transform`.
- Recommended patch type: Postfix.
- JP purpose: object inspection localization (name + long description block).
- DummyTarget hint: `DummyDescription.GetLongDescription` + `DummyGameObject.GetDisplayName` stub outputs.

### 14) Popup dialog text
- Signature: `public static int ShowConversation(string Title, IRenderable Icon = null, string Intro = null, List<string> Options = null, bool AllowTrade = false, bool AllowEscape = true, bool AllowRenderMapBehind = false)`
- Classification rationale: full UI workflow.
- Recommended patch type: Prefix.
- JP purpose: translate popup intro/options and keep hotkey mapping stable.
- DummyTarget hint: `DummyPopupWindow` with captured `Title/Intro/Options` payload.

### 15) DisplayName assembly entry point
- Signature: `public static string GetFor(GameObject Object, string Base, int Cutoff = int.MaxValue, string Context = null, bool AsIfKnown = false, bool Single = false, bool NoConfusion = false, bool NoColor = false, bool ColorOnly = false, bool Visible = true, bool BaseOnly = false, bool UsingAdjunctNoun = false, bool WithoutTitles = false, bool ForSort = false, bool Reference = false, bool IncludeImplantPrefix = true)`
- Classification rationale: event-driven name composition with `DescriptionBuilder`.
- Recommended patch type: Postfix.
- JP purpose: final name surface conversion after builder ordering resolves.
- DummyTarget hint: `DummyDisplayNameEvent` storing added parts and final `ToString` output.

### 16) DisplayName event process stage
- Signature: `public string ProcessFor(GameObject obj, bool NoReturn = false)`
- Classification rationale: runs object event hooks (`GetDisplayName`) and postfix channels.
- Recommended patch type: Prefix.
- JP purpose: intercept before returning wrapped final display name.
- DummyTarget hint: stub event bus with `Prefix/Infix/Postfix/PostPostfix` buffers.

### 17) Long description assembly
- Signature: `public void GetLongDescription(StringBuilder SB)` (`XRL.World.Parts.Description`)
- Classification rationale: collects equipment/effects and appends textual blocks.
- Recommended patch type: Postfix.
- JP purpose: translate concatenated body sections while preserving gameplay facts.
- DummyTarget hint: `DummyBodyPart` list and `DummyEffect.GetDescription()` fixtures.

### 18) GameText variable replacement core
- Signature 1: `public static string VariableReplace(StringBuilder Message, GameObject Subject, GameObject Object = null, bool StripColors = false)`
- Signature 2: `public static void Process(StringBuilder Message, StringMap<ReplacerEntry> Replacers = null, StringMap<int> Aliases = null, IList<TextArgument> Arguments = null, int DefaultArgument = -1, bool StripColors = false)`
- Classification rationale: parser/state machine + replacer maps + post-process pipeline.
- Recommended patch type: Prefix.
- JP purpose: central token expansion point (`=...=`) and grammar-sensitive substitution.
- DummyTarget hint: fixed replacer map and deterministic argument set; assert token parse, params, and post-process ordering.

### 19) Historic procedural lore expansion
- Signature: `public static string ExpandString(string input, HistoricEntitySnapshot entity, History history, Dictionary<string, string> vars = null, System.Random Random = null)`
- Classification rationale: mostly string/query expansion but uses history state, random source, and Unity debug logging.
- Recommended patch type: Prefix.
- JP purpose: preserve procedural lore structure while localizing expanded fragments.
- DummyTarget hint: deterministic `Random` + tiny mock spice tree to validate `<...>` query expansion.

### 20) Text filter pipeline
- Signature: `public static string Filter(string Phrase, string Filter, string Extras = null, bool FormattingProtect = true)`
- Classification rationale: dispatch function over filter names (`Angry`, `Corvid`, `Leet`, `Weird`, etc.).
- Recommended patch type: Prefix.
- JP purpose: gate or replace English-specific filters before final display text.
- DummyTarget hint: route through `DummyTextFilters.Filter("hello", "Leet")` and assert formatting protection behavior.

### 21) Conversation XML merge loader
- Signature 1: `private static void ReadConversation(string Path, BuildContext Context)`
- Signature 2: `private static void LoadConversations()`
- Classification rationale: XML load + global blueprint dictionary mutation.
- Recommended patch type: Postfix.
- JP purpose: verify and control merged conversation text at data-load stage.
- DummyTarget hint: `DummyConversationBlueprintMap` with ID collision cases (`merge/replace/add/remove`).

### 22) Blueprint text loading
- Signature 1: `public void LoadBlueprints()` (`XRL.World.GameObjectFactory`)
- Signature 2: `public GameObjectBlueprint LoadBakedXML(ObjectBlueprintLoader.ObjectBlueprintXMLData node)`
- Classification rationale: object blueprint ingestion, attribute/tag/property population.
- Recommended patch type: Postfix.
- JP purpose: intercept blueprint-derived textual fields from baked XML.
- DummyTarget hint: minimal `ObjectBlueprintXMLData` fixture with `property`, `tag`, `part` nodes.

## 4. Color Code Processing (Markup)

From `ConsoleLib_Console_Markup.cs` + `ConsoleLib_Console_MarkupControlNode.cs`:

1. `Transform(text)` early-exits unless `Enabled && text != null && text.Contains("{{")`.
2. `Parse(text)` builds a node tree with `ParseText` + `ParseControl`:
   - `{{action|text}}` becomes control node with `Action=action`.
   - nested `{{...{{...}}...}}` is parsed recursively.
3. `ToStringBuilder` applies shader-driven foreground/background updates:
   - foreground codes are emitted as `&X`
   - background codes are emitted as `^Y`
4. Literal escaping:
   - `&&` means literal `&`
   - `^^` means literal `^`
5. `refreshAtNewline=true` re-emits active color code after newline.
6. `Strip(StringBuilder)` removes brace markup only (`{{...|...}}`) while preserving plain text payload.
7. `Wrap(text)` auto-wraps to `{{|...}}` when string has `&` or `^` but no brace markup.

Practical patch safety rule: translate inner text payloads, never mutate control delimiters (`{{`, `}}`, `|`, `&`, `^`).

## 5. ConversationLoader Merge Behavior

Observed behavior from `ConversationLoader` + `ConversationXMLBlueprint`:

- `ConversationLoader.ReadConversation` reads `<conversations Namespace=...>` and `<conversation ...>` nodes.
- Each conversation starts with `Inherits = "BaseConversation"` and is read via `ConversationXMLBlueprint.Read(...)`.
- Collision handling in `Conversation._Blueprints`:
  - if same ID exists and `conversationXMLBlueprint.Load == 0`, call `existing.Merge(new)`.
  - else overwrite dictionary entry (`Conversation._Blueprints[id] = new`).
- `Load` attribute decoding in `ConversationXMLBlueprint.ReadAttribute`:
  - `replace -> 1`
  - `add -> 2`
  - `remove -> 3`
  - default/other -> `0` (merge path)
- Child merge semantics in `ConversationXMLBlueprint.Merge`:
  - `Load==2`: add child
  - matching child + `Load==1`: replace
  - matching child + `Load==3`: remove
  - matching child + default: recursive merge
  - no match: append child
- Attribute merge semantics: key-by-key overwrite (`Attributes[key] = value`).

Localization impact: `Load="Merge"` behaves as attribute-level and node-level merge; ID collisions default to merge unless explicit `replace/add/remove` is set.

## 6. DisplayName Assembly Order

From `DescriptionBuilder` constants and `GetDisplayNameEvent.ProcessFor`:

- Base ordering constants:
  - `ORDER_MARK = -800`
  - `ORDER_ADJECTIVE = -500`
  - `ORDER_BASE = 10`
  - `ORDER_CLAUSE = 600`
  - `ORDER_TAG = 1100`
- Size adjective is resolved into adjective slot (`AddAdjective`) at resolve time.
- `GetDisplayNameEvent.ProcessFor` may append event channels in this order:
  - `Prefix -> AddAdjective(...)`
  - `Infix -> AddClause(...)`
  - `Postfix -> AddTag(...)`
  - `PostPostfix -> AddTag(..., +20)`
- `DescriptionBuilder.ToString` sorts by order then lexical compare.
- Color wrapping closes before items with order `> 600` (tag region), so tags can render outside prior color block.

Canonical assembly order used by localization planning:

```text
[Mark(-800)] -> [SizeAdj/Adj(-500)] -> [Base(10)] -> [Clause(600)] -> [Tag(1100+)]
```

## 7. Variable Replacement Patterns (`=variable=`)

### 7.1 Core syntax

From `GameText.Process` and `ReplaceBuilder`:

```text
=<target>.<key>[:param1[:param2...]][|post1[|post2...]]=
```

- Target aliases built-in: `player`, `subject`, `pronouns`, `objpronouns`, `object`.
- Indexed form supported: e.g. `subject[2].name`, `object[1].t`.
- `:` separates replacer parameters.
- `|` attaches post-processors (can chain multiple).
- Capitalized key variants exist when replacer attribute has `Capitalization=true`.

### 7.2 Global/base replacer keys (42)

`EitherOrWhisper, EskhindRoadDirection, FCC, FCL, GR1, GR2, GR3, IS1, MARKOVCORVIDSENTENCE, MARKOVFISHSENTENCE, MARKOVPARAGRAPH, MARKOVSENTENCE, MARKOVWATERBIRDSENTENCE, MC1, MarkOfDeath, RebekahRegion, SEEKERENEMY, V0tinkeraddendum, WEIRDMARKOVSENTENCE, booleangamestate, day, factionaddress, factionrank, generic, ifplayerplural, int64gamestate, intgamestate, month, mostHatedFaction, mostHatedFaction.t, secondMostHatedFaction, secondMostHatedFaction.t, state.bool, state.int, state.long, state.string, stringgamestate, sultan, sultanTerm, time, villageZeroName, year`

### 7.3 Object-scoped replacer keys (49)

`a, an, an's, apparentSpecies, bodypart, bodypart.shuffled, direction, directionIfAny, does, faction, faction.t, formalAddressTerm, fragment, fragment.activity, fragment.arable, fragment.ore, fragment.poetic, fragment.sacred, fragment.village, generalDirection, generalDirectionIfAny, immaturePersonTerm, indicativeDistal, indicativeProximal, isplural, landmark.nearest, longname, name, name's, nameSingle, objective, offspringTerm, parentTerm, personTerm, possessive, possessiveAdjective, reflexive, refname, refname's, siblingTerm, species, subjective, substantivePossessive, t, t's, terrain.t, the, verb, waterRitualLiquid`

### 7.4 Post-processors (8)

`article, capitalize, lower, playerpluralize, pluralize, strip, title, upper`

### 7.5 Example patterns

```text
=subject.name=
=object.longname|capitalize=
=player.verb:attack|title=
=subject[1].bodypart.shuffled|article=
=state.string:QuestKey:unknown=
```

## Appendix: Known missing-type probes

When targeting message/conversation internals directly, these types need namespace/type-system probing because decompilation failed:

- `XRL.Messages.Messaging`
- `XRL.World.Messaging`
- `XRL.World.Conversations.ConversationUI`
- `XRL.World.ConversationUI`
- `XRL.UI.Messaging`
