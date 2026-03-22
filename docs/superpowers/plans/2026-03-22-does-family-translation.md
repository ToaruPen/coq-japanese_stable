# Does() Family Translation — Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Translate the top 3 Does() VerbComposition families (Status Predicate, Negation/Lack, Combat Damage) covering ~152 of 307 sites via family-based sink translation in MessagePatternTranslator.

**Architecture:** New regex patterns are added to `messages.ja.json` (the existing JSON pattern file loaded by MessagePatternTranslator). For patterns that need structural translation logic beyond regex+template, a new shared family translator is created following the `DeathWrapperFamilyTranslator` pattern and hooked into `MessagePatternTranslator.TranslateStripped`. All patterns are TDD — failing test first, then implementation.

**Tech Stack:** C# (.NET/netstandard2.0), Harmony, NUnit, JSON pattern dictionaries

**Branch:** Create from `source-first`: `feat/issue-61-does-family-phase1`

**Key reference files:**
- `Mods/QudJP/Assemblies/src/MessagePatternTranslator.cs` — pattern engine (loads JSON, linear scan, template expansion)
- `Mods/QudJP/Assemblies/src/Patches/DeathWrapperFamilyTranslator.cs` — reference implementation for shared family translator
- `Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs` — Harmony Prefix on AddPlayerMessage (entry point)
- `Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs` — popup translation chain (falls back to MessagePatternTranslator)
- `Mods/QudJP/Localization/Dictionaries/messages.ja.json` — 92 existing regex patterns
- `Mods/QudJP/Assemblies/QudJP.Tests/L1/MessagePatternTranslatorTests.cs` — L1 test patterns
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/MessageLogPatchTests.cs` — L2 Harmony integration tests

---

## File Structure

### New files
| File | Responsibility |
|------|---------------|
| `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs` | L1 unit tests for Does() family patterns |

### Modified files
| File | Changes |
|------|---------|
| `Mods/QudJP/Localization/Dictionaries/messages.ja.json` | Add ~40-50 new regex patterns for 3 families |
| `Mods/QudJP/Assemblies/QudJP.Tests/L2/MessageLogPatchTests.cs` | Add L2 integration tests for new families |

---

## Task 1: Status Predicate Family — Simple Patterns (86 sites)

The largest family. `actor.Does("are") + " exhausted!"` → `{actor}は疲弊した！`

Most patterns follow: `^(?:The |the )?(.+?) (?:is|are) (.+?)[.!]?$`

**CRITICAL: Pattern ordering** — Existing `messages.ja.json` already has `You are stunned`, `You are confused`, `You are poisoned` etc. as exact-match patterns (lines 69-80). New third-person `is|are` patterns MUST be placed AFTER these existing `You are X` patterns to avoid shadowing them. The new patterns capture `(.+?)` which would match "You" and produce incorrect output like "Youは気絶した".

**Files:**
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`

- [ ] **Step 1: Write failing L1 tests for status predicate patterns**

Create `DoesVerbFamilyTests.cs`:
```csharp
using QudJP;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class DoesVerbFamilyTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;
    private string dictionaryPath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-does-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");
        dictionaryPath = Path.Combine(tempDirectory, "ui-test.ja.json");

        // Copy the real messages.ja.json for pattern testing
        File.Copy(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
                "Mods", "QudJP", "Localization", "Dictionaries", "messages.ja.json"),
            patternFilePath);
        File.WriteAllText(dictionaryPath, "{\"entries\":[]}\n");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        MessagePatternTranslator.ResetForTests();
        Translator.ResetForTests();
        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, recursive: true);
    }

    // --- Status Predicate Family ---

    [TestCase("The bear is exhausted!", "熊は疲弊した！")]
    [TestCase("The snapjaw is stunned!", "スナップジョーは気絶した！")]
    [TestCase("The bear is stuck.", "熊は動けなくなった。")]
    [TestCase("The glowpad is sealed.", "グロウパッドは封印された。")]
    [TestCase("You are exhausted!", "あなたは疲弊した！")]
    public void Translate_StatusPredicateFamily(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(expected));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~DoesVerbFamilyTests" -v n`
Expected: FAIL — patterns not yet added

- [ ] **Step 3: Add status predicate patterns to messages.ja.json**

Add these patterns to the `"patterns"` array in `messages.ja.json`. Place them AFTER ALL existing `You are X` patterns (lines 69-80: stunned, confused, poisoned, bleeding, burning, etc.) to avoid shadowing. The existing `You are stunned` pattern must match first for player messages; these new patterns handle third-person actors only:

```json
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:is|are) exhausted[.!]?$",
  "template": "{t0}は疲弊した"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:is|are) stunned[.!]?$",
  "template": "{t0}は気絶した"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:is|are) stuck[.!]?$",
  "template": "{t0}は動けなくなった"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:is|are) sealed[.!]?$",
  "template": "{t0}は封印された"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:is|are) jammed[.!]?$",
  "template": "{t0}は動作不良を起こした"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:is|are) empty[.!]?$",
  "template": "{t0}は空になった"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:is|are) unresponsive[.!]?$",
  "template": "{t0}は反応しなくなった"
}
```

Note: `{t0}` passes the capture through `Translator.Translate` to translate entity display names.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~DoesVerbFamilyTests" -v n`
Expected: PASS

- [ ] **Step 5: Run full test suite for regression check**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
Expected: 538+ tests PASS, 0 FAIL

- [ ] **Step 6: Commit**

```bash
git add Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs Mods/QudJP/Localization/Dictionaries/messages.ja.json
git commit -m "feat(translation): add status predicate family patterns for Does() sites"
```

---

## Task 2: Negation/Lack Family (38 sites)

`actor.Does("don't") + " have enough charge..."` → `{actor}は十分な電力がない…`

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`

- [ ] **Step 1: Write failing L1 tests for negation patterns**

Add to `DoesVerbFamilyTests.cs`:
```csharp
    // --- Negation/Lack Family ---

    [TestCase("The turret can't hear you!", "タレットにはあなたの声が聞こえない！")]
    [TestCase("The bear doesn't have a consciousness to appeal to.", "熊には訴えるべき意識がない。")]
    [TestCase("You don't penetrate the snapjaw's armor!", "スナップジョーの防具を貫通できなかった！")]
    [TestCase("You can't see!", "視界がない！")]
    [TestCase("The turret doesn't have enough charge to fire.", "タレットはfireするのに十分なchargeがない。")]
    public void Translate_NegationLackFamily(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(expected));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~NegationLackFamily" -v n`
Expected: FAIL

- [ ] **Step 3: Add negation patterns to messages.ja.json**

```json
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:can't|cannot) hear you[.!]?$",
  "template": "{t0}にはあなたの声が聞こえない"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:doesn't|don't|does not|do not) have a consciousness to appeal to[.!]?$",
  "template": "{t0}には訴えるべき意識がない"
},
{
  "pattern": "^You (?:don't|do not) penetrate (?:the |)?(.+?)(?:'s|s') armor[.!]?$",
  "template": "{t0}の防具を貫通できなかった"
},
{
  "pattern": "^You (?:can't|cannot) see[.!]?$",
  "template": "視界がない"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:doesn't|don't) seem to be working[.!]?$",
  "template": "{t0}は機能していないようだ"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:doesn't|don't) have enough (.+?) to (.+?)[.!]?$",
  "template": "{t0}は{2}するのに十分な{1}がない"
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~NegationLackFamily" -v n`
Expected: PASS

- [ ] **Step 5: Run full test suite**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs Mods/QudJP/Localization/Dictionaries/messages.ja.json
git commit -m "feat(translation): add negation/lack family patterns for Does() sites"
```

---

## Task 3: Combat Damage Family — Third-Person Variants (28 sites)

Existing patterns cover "You hit X for N damage" but NOT "The bear hits you for N damage" when the actor is a third-party NPC hitting another NPC. Also missing: weapon-specific variants.

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`

- [ ] **Step 1: Write failing L1 tests for third-person combat**

Add to `DoesVerbFamilyTests.cs`:
```csharp
    // --- Combat Damage Family (third-person actor) ---

    [TestCase("The bear hits the snapjaw for 5 damage!", "熊はスナップジョーに5ダメージを与えた！")]
    [TestCase("The bear misses the snapjaw!", "熊はスナップジョーへの攻撃を外した！")]
    [TestCase("The bear hits the snapjaw with a bronze short sword for 3 damage.", "熊は青銅の短剣でスナップジョーに3ダメージを与えた。")]
    [TestCase("The bear misses the snapjaw with a bronze short sword! [8 vs 12]", "熊は青銅の短剣でスナップジョーへの攻撃を外した！ [8 vs 12]")]
    public void Translate_CombatDamageThirdPerson(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(expected));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~CombatDamageThirdPerson" -v n`
Expected: FAIL

- [ ] **Step 3: Add third-person combat patterns to messages.ja.json**

Insert these AFTER the existing "You hit/miss" and "X hits/misses you" patterns (lines 1-50ish) to avoid shadowing them. The existing patterns handle player-centric messages; these handle NPC-vs-NPC:

**CRITICAL**: The generic `misses (.+?)` pattern would also match `"The bear misses you!"` — existing `misses you` pattern (line 37) must come FIRST. Place these new patterns AFTER all existing `you`-targeted patterns.

```json
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) hits (?:the |a |an )?(.+?) with (?:a |an |the )?(.+?) for (\\d+) damage[.!]?$",
  "template": "{t0}は{2}で{t1}に{3}ダメージを与えた"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) hits (?:the |a |an )?(.+?) for (\\d+) damage[.!]?$",
  "template": "{t0}は{t1}に{2}ダメージを与えた"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) misses (?:the |a |an )?(.+?) with (?:a |an |the )?(.+?)[.!]? \\[(.+?) vs (.+?)\\]$",
  "template": "{t0}は{2}で{t1}への攻撃を外した [{3} vs {4}]"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) misses (?:the |a |an )(.+?)[.!]?$",
  "template": "{t0}は{t1}への攻撃を外した"
}
```

Note: The last `misses` pattern requires `(?:the |a |an )` (NOT optional) to avoid matching `"X misses you"`. The `[vs]` pattern is a separate regex (not optional group) so the template can include the values.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~CombatDamageThirdPerson" -v n`
Expected: PASS

- [ ] **Step 5: Run full test suite — check existing combat patterns aren't broken**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
Expected: All PASS. Pay special attention to existing "You hit/miss" tests — new patterns must NOT conflict.

- [ ] **Step 6: Commit**

```bash
git add Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs Mods/QudJP/Localization/Dictionaries/messages.ja.json
git commit -m "feat(translation): add third-person combat damage patterns for Does() sites"
```

---

## Task 4: Possession/Lack Family (13 sites)

`actor.Does("have") + " no room for more water."` → `{actor}にはこれ以上の水を入れる余地がない。`

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`

- [ ] **Step 1: Write failing tests**

```csharp
    // --- Possession/Lack Family ---

    [TestCase("The canteen has no room for more water.", "水筒にはこれ以上の水を入れる余地がない。")]
    [TestCase("You have no more ammo!", "弾薬が尽きた！")]
    [TestCase("The bear has nothing to say.", "熊は何も言うことがない。")]
    [TestCase("You have left your party.", "あなたはパーティーを離れた。")]
    public void Translate_PossessionLackFamily(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(expected));
    }
```

- [ ] **Step 2: Run tests — verify fail**
- [ ] **Step 3: Add possession/lack patterns**

```json
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:has|have) no room for more (.+?)[.!]?$",
  "template": "{t0}にはこれ以上の{1}を入れる余地がない"
},
{
  "pattern": "^You have no more ammo[.!]?$",
  "template": "弾薬が尽きた"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:has|have) nothing to say[.!]?$",
  "template": "{t0}は何も言うことがない"
},
{
  "pattern": "^You have left your party[.!]?$",
  "template": "あなたはパーティーを離れた"
}
```

- [ ] **Step 4: Run tests — verify pass**
- [ ] **Step 5: Full regression check**
- [ ] **Step 6: Commit**

```bash
git commit -m "feat(translation): add possession/lack family patterns for Does() sites"
```

---

## Task 5: Motion/Direction Family (6 sites)

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/DoesVerbFamilyTests.cs`
- Modify: `Mods/QudJP/Localization/Dictionaries/messages.ja.json`

- [ ] **Step 1: Write failing tests**

```csharp
    // --- Motion/Direction Family ---

    [TestCase("The bear falls to the ground.", "熊は地面に倒れた。")]
    [TestCase("You fall to the ground.", "あなたは地面に倒れた。")]
    [TestCase("The snapjaw falls asleep.", "スナップジョーは眠りに落ちた。")]
    public void Translate_MotionDirectionFamily(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(expected));
    }
```

- [ ] **Step 2: Run tests — verify fail**
- [ ] **Step 3: Add motion patterns**

**NOTE**: `MessagePatternTranslator.Translate` strips color tags (`{{C|asleep}}` → `asleep`) before pattern matching, then restores them via `ColorAwareTranslationComposer`. Patterns must match the STRIPPED text. Do NOT include `\{\{C\|...\}\}` in regex patterns.

```json
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:falls?|fell) to the ground[.!]?$",
  "template": "{t0}は地面に倒れた"
},
{
  "pattern": "^(?:The |the |[Aa]n? )?(.+?) (?:falls?|fell) asleep[.!]?$",
  "template": "{t0}は眠りに落ちた"
}
```

- [ ] **Step 4: Run tests — verify pass**
- [ ] **Step 5: Full regression check**
- [ ] **Step 6: Commit**

```bash
git commit -m "feat(translation): add motion/direction family patterns for Does() sites"
```

---

## Task 6: Create PR and request review

- [ ] **Step 1: Push branch**

```bash
git push -u origin feat/issue-61-does-family-phase1
```

- [ ] **Step 2: Create PR targeting source-first**

```bash
gh pr create --base source-first --title "feat(translation): Does() family patterns — Phase 1 (status, negation, combat, possession, motion)" --body "## Summary
Phase 1 of #61: Add family-based sink translation patterns for the top 5 Does() VerbComposition families.

### Families added
| Family | Sites covered | Patterns added |
|--------|--------------|----------------|
| Status Predicate | ~86 | 7 |
| Negation/Lack | ~38 | 6 |
| Combat (3rd person) | ~28 | 4 |
| Possession/Lack | ~13 | 4 |
| Motion/Direction | ~6 | 2 |

### Approach
All patterns added to messages.ja.json (JSON pattern file loaded by MessagePatternTranslator). No C# code changes needed — the existing sink infrastructure handles everything.

### Test plan
- [ ] L1 unit tests for all new families (DoesVerbFamilyTests.cs)
- [ ] Full regression: 538+ existing tests pass
- [ ] No conflict with existing combat/status patterns

Closes phase 1 of #61

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

- [ ] **Step 3: Wait for CodeRabbit review**

CodeRabbit is configured as a required reviewer on `source-first` branch. Review must approve before merge.

---

## Notes for implementing agent

- **Pattern order matters**: MessagePatternTranslator does linear scan. More specific patterns MUST come before generic ones.
- **`{t0}` vs `{0}`**: Use `{t0}` when the capture is an entity display name (runs through `Translator.Translate`). Use `{0}` for numbers or untranslatable tokens.
- **Test file location**: The test copies the REAL `messages.ja.json` from the project. The `SetUp` method handles this via relative path from `AppDomain.CurrentDomain.BaseDirectory`.
- **Existing patterns**: There are already 92 patterns. New patterns for the same domain (combat) must be ordered carefully to avoid shadowing.
- **Japanese translation quality**: Each pattern's Japanese output must be natural. Status predicates should use past tense (～した) for completed actions. Negation should use natural negative forms (～ない, ～できなかった).
- **Color tags**: `MessagePatternTranslator.Translate` STRIPS color markup before pattern matching (`{{C|asleep}}` → `asleep`), then restores it via `ColorAwareTranslationComposer`. Do NOT put `\{\{C\|...\}\}` in regex patterns — match the stripped text only.
- **Pattern shadowing**: New third-person patterns (e.g., `(.+?) misses (.+?)`) can shadow existing player-centric patterns (`misses you`). Always place new patterns AFTER existing more-specific patterns. For `misses`, require `(?:the |a |an )` (non-optional) before the target to avoid matching `"you"`.
