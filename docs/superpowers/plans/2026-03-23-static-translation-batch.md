# Static Translation Batch Implementation Plan (#82 + #83)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add up to 464 translation entries to JSON configuration files covering Issue #82 (226 needs_patch) and Issue #83 (238 static-analyzable sites). Task 9 (SetText 60 sites) may defer sites requiring C# patches, so actual count is an upper bound.

**Architecture:** All translations are JSON-only additions to two files: `messages.ja.json` (regex patterns for MessagePatternTranslator) and `verbs.ja.json` (Tier1/2/3 verb entries for MessageFrameTranslator). No C# code changes. Family-based batching groups sites by shared pattern shape to maximize reuse.

**Tech Stack:** JSON configuration, regex patterns, NUnit L1 tests (C# .NET)

**Build/Test:**
```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
```

**Key files:**
- `Mods/QudJP/Localization/Dictionaries/messages.ja.json` — message patterns (currently 359 patterns)
- `Mods/QudJP/Localization/MessageFrames/verbs.ja.json` — verb entries (currently 143 tier1, 114 tier2, 3 tier3)
- `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs` — Does() pattern tests
- `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs` — verb frame tests
- `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs` — pattern translator tests
- `docs/candidate-inventory.json` — site inventory with classification metadata

**Decompiled source:** `~/Dev/coq-decompiled/` — read to extract full English text for each site.

---

## Task 1: #82 DidX — Tier1 verb additions (50 new verbs, covers 117 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs`

**Context:** 117 DidX sites use 50 unique verbs across 6 frame types (DidX, XDidYToZ, DidXToY, WDidXToYWithZ, XDidY, DidXToYWithZ). Many verbs already have Tier1 entries (143 existing). Need to add missing verbs and Tier2/Tier3 entries for verbs that need extra-context-dependent translations.

**Approach:**
1. Extract all 50 verbs from inventory (`status=needs_patch, sink=DidX`)
2. Cross-reference with existing `verbs.ja.json` tier1 entries
3. Add missing Tier1 verbs
4. For verbs with context-dependent `extra` strings, add Tier2 entries
5. For verbs with regex-pattern `extra` strings (e.g., variable damage amounts), add Tier3 entries

- [ ] **Step 1: Extract missing verbs and write failing tests**

Read `docs/candidate-inventory.json` to get all DidX verbs. Compare against existing `verbs.ja.json`. Write test cases for representative verbs from each frame type (DidX, XDidYToZ, DidXToY, WDidXToYWithZ, XDidY).

Test pattern (in MessageFrameTranslatorTests.cs):
```csharp
[Test]
public void TryTranslateXDidY_NewTier2Verbs_Clasp()
{
    WriteDictionary(tier2: new[] { ("clasp", "its hands together", "手を合わせた") });
    var ok = MessageFrameTranslator.TryTranslateXDidY("ゴブリン", "clasp", "its hands together", ".", out var sentence);
    Assert.Multiple(() => { Assert.That(ok, Is.True); Assert.That(sentence, Is.EqualTo("ゴブリンは手を合わせた。")); });
}
```

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1 -v n`
Expected: FAIL (missing verb entries)

- [ ] **Step 2: Add Tier1/Tier2/Tier3 verb entries to verbs.ja.json**

Read decompiled source for each verb to determine proper Japanese translation in context. Add entries to appropriate tiers in `verbs.ja.json`.

- [ ] **Step 3: Run tests to verify pass**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1 -v n`
Expected: ALL PASS

- [ ] **Step 4: Commit**

```bash
git add Mods/QudJP/Localization/MessageFrames/verbs.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageFrameTranslatorTests.cs
git commit -m "feat(translation): add DidX verb entries for 117 needs_patch sites (#82)"
```

---

## Task 2: #82 EmitMessage — Message patterns (91 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs`

**Context:** 91 EmitMessage sites classified as `pattern_matchable`. Each has `analysis_notes` with the English pattern and `intercepted_by: MessageLogPatch (via AddPlayerMessage)`. Major clusters: MissileWeapon.cs (64), Door.cs (12), Nosebleed.cs (8), Harvestable.cs (7).

**Approach:**
1. Group by file/pattern family
2. For each family, create one regex pattern that captures the variable parts
3. Add patterns to `messages.ja.json`

- [ ] **Step 1: Extract English patterns from inventory analysis_notes and decompiled source**

For each of the 91 sites, read the `analysis_notes` field from inventory and the corresponding decompiled source to get the exact English message text. Group into pattern families.

- [ ] **Step 2: Write failing tests for representative patterns**

Test pattern (in DoesVerbFamilyTests.cs — uses real messages.ja.json):
```csharp
[TestCase("Your nose begins bleeding more heavily.", "あなたの鼻がさらにひどく出血し始めた。")]
[TestCase("The door is locked.", "ドアは施錠されている。")]
public void Translate_EmitMessagePatterns(string input, string expected)
{
    var translated = MessagePatternTranslator.Translate(input);
    Assert.That(translated, Is.EqualTo(expected));
}
```

Note: These tests go in `DoesVerbFamilyTests.cs` (not `MessagePatternTranslatorTests.cs`) because DoesVerbFamilyTests loads the real `messages.ja.json` in its SetUp, while MessagePatternTranslatorTests uses isolated temp data.

Run: `dotnet test ... --filter TestCategory=L1 -v n`
Expected: FAIL

- [ ] **Step 3: Add regex patterns to messages.ja.json**

Add patterns for each family. Priority order (most specific first):
1. MissileWeapon hit/damage patterns (64 sites)
2. Door lock/open patterns (12 sites)
3. Nosebleed bleeding patterns (8 sites)
4. Harvestable gather patterns (7 sites)

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test ... --filter TestCategory=L1 -v n`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs
git commit -m "feat(translation): add EmitMessage patterns for 91 needs_patch sites (#82)"
```

---

## Task 3: #82 JournalAPI — Narrative template patterns (18 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs`

**Context:** 18 JournalAPI sites with `narrative_arguments` structured data. 11 covered by `JournalEntryDisplayTextPatch`, 7 by `JournalMapNoteDisplayTextPatch`. These flow through `JournalTextTranslator` which falls back to `MessagePatternTranslator`.

- [ ] **Step 1: Extract journal text patterns from decompiled source**

Read the `narrative_arguments` field from inventory and decompiled source for:
- `AddObservation.cs` (observations)
- `GivesRep.cs` (reputation entries)
- Other journal entry producers

- [ ] **Step 2: Write failing tests**

- [ ] **Step 3: Add patterns to messages.ja.json**

- [ ] **Step 4: Run tests, verify pass**

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs
git commit -m "feat(translation): add JournalAPI patterns for 18 needs_patch sites (#82)"
```

---

## Task 4: #83 fully_static — Does() literal patterns (28 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`

**Context:** 28 sites where the entire message after Does() is string literals. Examples:
- `Does("go") + " into sleep mode."` → "Xはスリープモードに入った。"
- `Does("fall") + " asleep."` → "Xは眠りに落ちた。"
- `Does("click", "merely") + "."` → "Xはカチッと鳴った。"

**Approach:** Each site produces a recognizable English message that flows through MessageLogPatch. Add a regex pattern for each unique message.

- [ ] **Step 1: Extract all 28 English message patterns from decompiled source**

Read decompiled source for each site, reconstruct the full English sentence (Does() output + tail literals).

- [ ] **Step 2: Write failing tests in DoesVerbFamilyTests.cs**

```csharp
[TestCase("The クマ goes into {{C|sleep mode}}.", "クマはスリープモードに入った。")]
[TestCase("The クマ falls {{C|asleep}}.", "クマは眠りに落ちた。")]
[TestCase("The 装置 clicks merely.", "装置はカチッと鳴った。")]
public void Translate_FullyStaticDoesFamilies(string input, string expected)
{
    var translated = MessagePatternTranslator.Translate(input);
    Assert.That(translated, Is.EqualTo(expected));
}
```

- [ ] **Step 3: Add patterns to messages.ja.json**

- [ ] **Step 4: Run tests, verify pass**

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs
git commit -m "feat(translation): add fully_static Does() patterns for 28 sites (#83)"
```

---

## Task 5: #83 template_static Does() — GetStatusPhrase family (10 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`

**Context:** 10 sites follow `Does("are") + " " + GetStatusPhrase() + "."` pattern. GetStatusPhrase() returns exactly 7 fixed strings:
1. `{{W|disabled by electromagnetic pulse}}`
2. `{{K|unpowered}}`
3. `{{K|unfueled}}`
4. `{{g|disabled by fuel contamination}}`
5. `{{K|switched off}}`
6. `{{b|still warming up}}`
7. `{{r|nonfunctional}}`

**Approach:** 7 regex patterns, one per status phrase.

- [ ] **Step 1: Write failing tests for all 7 status phrases**

```csharp
[TestCase("The 装置 is {{W|disabled by electromagnetic pulse}}.", "装置は電磁パルスにより無効化されている。")]
[TestCase("The 装置 is {{K|unpowered}}.", "装置は電力が供給されていない。")]
// ... all 7
```

- [ ] **Step 2: Add 7 patterns to messages.ja.json**

- [ ] **Step 3: Run tests, verify pass**

- [ ] **Step 4: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs
git commit -m "feat(translation): add GetStatusPhrase family patterns for 10 sites (#83)"
```

---

## Task 6: #83 template_static Does() — MissileWeapon hit family (13 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`

**Context:** 13 MissileWeapon Does("hit") sites classified as template_static (without OutcomeMessageFragment). Pattern: `Does("hit", adverb) + " " + target + optionalDamage + direction + "!"`.

Read `~/Dev/coq-decompiled/XRL.World.Parts/MissileWeapon.cs` lines 2093-2291 to identify exact output shapes for these 13 sites.

- [ ] **Step 1: Extract English hit message patterns from MissileWeapon.cs**

- [ ] **Step 2: Write failing tests**

- [ ] **Step 3: Add hit-family patterns to messages.ja.json**

- [ ] **Step 4: Run tests, verify pass**

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs
git commit -m "feat(translation): add MissileWeapon hit family patterns for 13 sites (#83)"
```

---

## Task 7: #83 template_static Does() — Remaining verb families (64 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Localization/MessageFrames/verbs.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`

**Context:** 64 remaining template_static Does() sites across various verbs (are:24 excluding StatusPhrase, don't:4, impale:3, kick:3, harvest:3, reflect:3, etc.). Group by verb and pattern shape.

**Approach:**
1. Read decompiled source for each site
2. Group by (verb + tail pattern shape)
3. Create regex patterns for messages.ja.json
4. Add missing verb entries to verbs.ja.json if needed

- [ ] **Step 1: Extract and group English patterns**

- [ ] **Step 2: Write failing tests**

- [ ] **Step 3: Add patterns and verb entries**

- [ ] **Step 4: Run tests, verify pass**

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Localization/MessageFrames/verbs.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs
git commit -m "feat(translation): add remaining Does() verb family patterns for 64 sites (#83)"
```

---

## Task 8: #83 template_static unresolved — EmitMessage patterns (73 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs`

**Context:** 73 unresolved EmitMessage sites classified template_static. These flow through MessageLogPatch → MessagePatternTranslator. Major clusters: Liquids (LiquidWarmStatic, LiquidProteanGunk), Mutations (SunderMind, FungalInfection), Parts (DecoyHologramEmitter, MagazineAmmoLoader, Tinkering_Mine).

- [ ] **Step 1: Extract English patterns from decompiled source**

- [ ] **Step 2: Write failing tests**

- [ ] **Step 3: Add patterns to messages.ja.json**

- [ ] **Step 4: Run tests, verify pass**

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs
git commit -m "feat(translation): add unresolved EmitMessage patterns for 73 sites (#83)"
```

---

## Task 9: #83 template_static unresolved — SetText UI patterns (60 sites)

**Files:**
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs`

**Context:** 60 unresolved SetText sites classified template_static. These are UI elements. Major clusters: TinkeringDetailsLine (12), ModMenuLine (5), SaveManagementRow (4), SkillsAndPowersLine (3), TradeScreen (3).

**Note:** SetText sites may NOT flow through MessageLogPatch. Need to verify the interception path for each cluster. Some may need new Harmony patches on the specific UI setData/setDetails methods.

- [ ] **Step 1: Verify interception path for each SetText cluster**

Read decompiled source to determine:
- Does the text flow through any existing sink?
- If not, what method should be patched?

- [ ] **Step 2: For sites flowing through existing sinks, add patterns**

- [ ] **Step 3: For sites needing new patches, document and defer to separate task**

Note: If C# patches are needed, those are out of scope for this JSON-only plan and should be tracked separately.

- [ ] **Step 4: Write tests, verify pass**

- [ ] **Step 5: Commit**

```bash
git add Mods/QudJP/Localization/Dictionaries/messages.ja.json Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs
git commit -m "feat(translation): add SetText UI patterns for static sites (#83)"
```

---

## Task 10: Update inventory status and final verification

**Files:**
- Modify: `docs/candidate-inventory.json`

- [ ] **Step 1: Update inventory status for all covered sites**

For each site that now has a matching pattern or verb entry, update status from `needs_patch`/`needs_review`/`unresolved` to `translated`.

- [ ] **Step 2: Run full L1 test suite**

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1 -v n
```

Expected: ALL PASS

- [ ] **Step 3: Run build**

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add docs/candidate-inventory.json
git commit -m "docs(inventory): update status for translated sites (#82, #83)"
```

---

## Execution Notes

- **Task order matters for Task 9**: SetText sites may need C# patches, so verify interception paths early. If patches are needed, split into a separate issue.
- **Translation quality**: Use game context from decompiled source to produce natural Japanese. Caves of Qud uses a distinctive literary style — match the existing translation tone in `messages.ja.json`.
- **Pattern ordering**: More specific patterns must come before generic ones in `messages.ja.json` (first-match-wins). Insert new patterns in family blocks. Run full L1 regression after each task to catch shadowing conflicts.
- **Color codes**: Preserve `{{...}}` markup in translated patterns. Use `{0}` for captured groups, `{t0}` for groups needing recursive dictionary lookup.
- **Dedup check**: Before adding a pattern, search existing `messages.ja.json` to avoid duplicates. Also check cross-task overlap (Task 2 EmitMessage #82 vs Task 8 EmitMessage #83).
