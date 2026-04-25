# Issue #402 — `templates.ja.json` `=variable=` token information loss plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore information-bearing `=variable=` tokens in 6 of 30 entries in `templates.ja.json` and lock the new Japanese strings behind the existing L1 parametrized TestCase.

**Architecture:** Pure data fix in one JSON file plus parametrized TestCase additions in one C# test file. No production C# changes. No new test files.

**Tech Stack:** NUnit3 (`[TestCase]` attributes), .NET, JSON.

**Spec:** `docs/superpowers/specs/2026-04-25-issue-402-templates-variable-replacement-design.md`

---

## File map

| Action | Path | Responsibility |
| --- | --- | --- |
| Modify | `Mods/QudJP/Assemblies/QudJP.Tests/L1/BlueprintTemplateTranslationPatchTests.cs:82-91` | Update entry-7 expected value; add 5 new TestCase lines for entries 5, 9, 12, 22, 26 |
| Modify | `Mods/QudJP/Localization/BlueprintTemplates/templates.ja.json` | Edit 6 `text` fields |

No new files. No production code changes.

---

### Task 1: Failing TestCase additions

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/BlueprintTemplateTranslationPatchTests.cs`

- [ ] **Step 1: Update the parametrized test**

Find the block at lines 82-91:

```csharp
    [TestCase("Nothing happens.", "何も起こらなかった。")]
    [TestCase("You hear inaudible mumbling.", "聞き取れないつぶやきが聞こえた。")]
    [TestCase("=subject.The==subject.name= =verb:start= up with a hum.",
        "=subject.name=がうなり声を上げて起動した。")]
    [TestCase("=subject.The==subject.name= =verb:recognize= your =object.name=.",
        "=subject.name=があなたの=object.name=を認識した。")]
    [TestCase("You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.",
        "あなたは=subject.name=に触れ、パスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。")]
    [TestCase("A loud buzz is emitted. The unauthorized glyph flashes on the display.",
        "大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。")]
```

Replace the entry-7 line (the `You touch =subject.t=...` TestCase, lines 88-89) so the expected Japanese carries `=subject.name=のパスコード` instead of bare `パスコード`. Also append 5 new TestCase attributes immediately after the existing block (still on the same `LoadTranslations_ContainsExpectedMapping` method). The full updated block looks like this:

```csharp
    [TestCase("Nothing happens.", "何も起こらなかった。")]
    [TestCase("You hear inaudible mumbling.", "聞き取れないつぶやきが聞こえた。")]
    [TestCase("=subject.The==subject.name= =verb:start= up with a hum.",
        "=subject.name=がうなり声を上げて起動した。")]
    [TestCase("=subject.The==subject.name= =verb:recognize= your =object.name=.",
        "=subject.name=があなたの=object.name=を認識した。")]
    [TestCase("You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.",
        "あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。")]
    [TestCase("A loud buzz is emitted. The unauthorized glyph flashes on the display.",
        "大きなブザー音が鳴った。認証されていないグリフがディスプレイに点滅した。")]
    [TestCase("{{g|You touch =subject.the==subject.name= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.}}",
        "{{g|あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。}}")]
    [TestCase("{{R|=subject.T= =verb:consume= =object.an==object.directionIfAny=!}}",
        "{{R|=subject.name=が=object.name==object.directionIfAny=を消費した！}}")]
    [TestCase("=object.T= =object.verb:react= strangely with =subject.t= and =object.verb:convert= =pronouns.objective= to =newLiquid=.",
        "=object.name=が=subject.name=と奇妙な反応を起こし、=subject.name=を=newLiquid=に変換した。")]
    [TestCase("=object.Does:are= much too old and rusted to enter.",
        "=object.name=は古すぎて錆びついており、中に入ることはできない。")]
    [TestCase("=subject.T= =verb:extrude= through the mirror of =pronouns.possessive= crystalline rind!",
        "=subject.name=が=subject.name=の結晶の外殻の鏡面を通り抜けた！")]
```

Use a multi-replacement Edit: target the original 10-line block (lines 82-91) and replace it with the 22-line block above. Do not modify the method body or the surrounding tests.

- [ ] **Step 2: Run the L1 BlueprintTemplate tests; expect failure**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~BlueprintTemplate" --logger "console;verbosity=normal"
```

Expected: 6 cases of `LoadTranslations_ContainsExpectedMapping` FAIL — one for the modified entry-7 case (existing JSON does not match new expected) and 5 for the newly-added entries 5, 9, 12, 22, 26. The other tests (`LoadTranslations_LoadsDictionaryWithExpectedEntryCount`, `LoadTranslations_AllKeysAreNonEmptyAndDistinct`, `LoadTranslations_TranslatedTemplatesPreserveVariableReplaceSlots`, etc.) PASS.

If a different test fails or the failure count is not exactly 6, stop and report.

---

### Task 2: Apply the 6 JSON `text` edits

**Files:**
- Modify: `Mods/QudJP/Localization/BlueprintTemplates/templates.ja.json` — six text-field edits

- [ ] **Step 1: Entry 5 (the `{{g|...}}` variant)**

Replace:

```json
      "text": "{{g|あなたは=subject.name=に触れ、パスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。}}"
```

with:

```json
      "text": "{{g|あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。}}"
```

- [ ] **Step 2: Entry 7 (the bare variant)**

Replace:

```json
      "text": "あなたは=subject.name=に触れ、パスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。"
```

with:

```json
      "text": "あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。"
```

- [ ] **Step 3: Entry 9 (consume + direction)**

Replace:

```json
      "text": "{{R|=subject.name=が=object.name=を消費した！}}"
```

with:

```json
      "text": "{{R|=subject.name=が=object.name==object.directionIfAny=を消費した！}}"
```

- [ ] **Step 4: Entry 12 (alchemy convert)**

Replace:

```json
      "text": "=object.name=が=subject.name=と奇妙な反応を起こし、=newLiquid=に変換された。"
```

with:

```json
      "text": "=object.name=が=subject.name=と奇妙な反応を起こし、=subject.name=を=newLiquid=に変換した。"
```

- [ ] **Step 5: Entry 22 (Does:are subject)**

Replace:

```json
      "text": "古すぎて錆びついており、中に入ることはできない。"
```

with:

```json
      "text": "=object.name=は古すぎて錆びついており、中に入ることはできない。"
```

- [ ] **Step 6: Entry 26 (extrude through rind)**

Replace:

```json
      "text": "=subject.name=が結晶の外殻の鏡面を通り抜けた！"
```

with:

```json
      "text": "=subject.name=が=subject.name=の結晶の外殻の鏡面を通り抜けた！"
```

- [ ] **Step 7: Re-run the L1 BlueprintTemplate tests**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~BlueprintTemplate"`

Expected: ALL pass.

---

### Task 3: Full verification

**Files:**
- (none — verification only)

- [ ] **Step 1: Run all repo checks**

Run each in order; all must succeed:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
uv run pytest scripts/tests/ -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

Expected exit codes / outputs:
- L1 dotnet: all green (1182 plus the 5 new TestCase invocations).
- L2 dotnet: all green.
- pytest: 405 passed.
- validate_xml: exit 0, no new warnings.
- check_encoding: 190 OK, 0 issues.
- ruff: clean.
- dotnet build: 0 warnings, 0 errors.

If any fails, stop, report, do not widen scope.

- [ ] **Step 2: Stop**

Do not commit. The user reviews the diff and runs the `/codex` review → `/simplify` → PR flow.

---

## Verification summary

After Task 3:

| Check | Command | Expectation |
| --- | --- | --- |
| L1 BlueprintTemplate tests | `dotnet test ... --filter "FullyQualifiedName~BlueprintTemplate"` | all green, includes 11 ContainsExpectedMapping cases |
| L1 / L2 dotnet | `dotnet test ... --filter TestCategory=L1`, `--filter TestCategory=L2` | all green |
| Full pytest | `uv run pytest scripts/tests/ -q` | 405 pass |
| Strict XML validation | `python3.12 scripts/validate_xml.py ... --strict ...` | exit 0 |
| Encoding | `python3.12 scripts/check_encoding.py ...` | clean |
| Ruff | `ruff check scripts/` | clean |
| DLL build | `dotnet build ...` | 0 warnings, 0 errors |

## Out-of-scope reminders

- Do NOT modify the 24 entries that were classified GOOD in the spec.
- Do NOT add a generic placeholder-parity validator. That is #409.
- Do NOT touch `BlueprintTemplateTranslationPatch.cs` itself — only the test and the JSON.
- Do NOT modify `LoadTranslations_TranslatedTemplatesPreserveVariableReplaceSlots`. The new TestCase additions are sufficient for #402.
