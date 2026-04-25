# Issue #402 — `templates.ja.json` `=variable=` token information loss

## Why

`Mods/QudJP/Localization/BlueprintTemplates/templates.ja.json` shipped 30 entries. The issue body counted 22 of them as having a `=variable=` token-multiset mismatch and feared runtime crashes, but the deeper read of the resolver shows the picture is mixed:

- The Caves of Qud resolver (`XRL.GameText.Process` / `XRL.World.Text.Delegates.VariableReplacers`, decompiled) treats `=subject.T=`, `=subject.t=`, `=subject.The=`, `=subject.name=` etc. all as valid registered keys. Substituting `=subject.name=` for `=subject.T=` does **not** crash and does **not** leave a raw token; it returns the bare display name. The article ("the ", capitalisation) information is simply discarded — which is appropriate for Japanese, where definite articles do not exist.
- However, several tokens carry **information that Japanese still needs**: the possessive ("his/her/its passcode"), the direction marker (`=directionIfAny=`), and the subject of the sentence itself. When those are dropped, the player loses information that no Japanese surface form can recover.

So the actual #402 work is narrower than the issue body suggested: **6 entries truly lose information; the remaining 24 are acceptable Japanese collapses** of article/verb-conjugation tokens that have no Japanese equivalent.

A general placeholder-parity validator that flags every `=T=`-to-`=name=` collapse would produce noise. The right place for that automation is #409, with an allowlist that distinguishes article-class tokens (collapse OK) from information-bearing tokens (must survive). #402 stays data-scoped.

## What

Six entries in `Mods/QudJP/Localization/BlueprintTemplates/templates.ja.json` need their `text` field updated. Indices below are 1-based to match the issue body.

| # | Source key (excerpt) | Current Japanese | New Japanese | Information restored |
| ---: | --- | --- | --- | --- |
| 5 | `{{g\|You touch =subject.the==subject.name= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.}}` | `{{g\|あなたは=subject.name=に触れ、パスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。}}` | `{{g\|あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。}}` | passcode owner |
| 7 | `You touch =subject.t= and recall =pronouns.possessive= passcode. =pronouns.Subjective= =verb:beep:afterpronoun= warmly.` | `あなたは=subject.name=に触れ、パスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。` | `あなたは=subject.name=に触れ、=subject.name=のパスコードを思い出した。=subject.name=が温かくビープ音を鳴らした。` | passcode owner |
| 9 | `{{R\|=subject.T= =verb:consume= =object.an==object.directionIfAny=!}}` | `{{R\|=subject.name=が=object.name=を消費した！}}` | `{{R\|=subject.name=が=object.name==object.directionIfAny=を消費した！}}` | direction marker |
| 12 | `=object.T= =object.verb:react= strangely with =subject.t= and =object.verb:convert= =pronouns.objective= to =newLiquid=.` | `=object.name=が=subject.name=と奇妙な反応を起こし、=newLiquid=に変換された。` | `=object.name=が=subject.name=と奇妙な反応を起こし、=subject.name=を=newLiquid=に変換した。` | what is converted (subject), active voice |
| 22 | `=object.Does:are= much too old and rusted to enter.` | `古すぎて錆びついており、中に入ることはできない。` | `=object.name=は古すぎて錆びついており、中に入ることはできない。` | sentence subject |
| 26 | `=subject.T= =verb:extrude= through the mirror of =pronouns.possessive= crystalline rind!` | `=subject.name=が結晶の外殻の鏡面を通り抜けた！` | `=subject.name=が=subject.name=の結晶の外殻の鏡面を通り抜けた！` | rind owner |

The remaining 24 entries are left as shipped. They collapse pure article-class or English-verb-conjugation tokens (`=subject.T=` / `=subject.t=` / `=subject.The=` / `=verb:start=` / `=verb:stare=` / `=Name's=` etc.) into bare Japanese forms that carry the same information. The resolver renders them correctly via `name` lookup (verified against decompiled `VariableReplacers.cs`).

### Tradeoffs in the chosen Japanese forms

- **Entries 5 & 7**: The original English uses `=pronouns.possessive=` ("his/her/its"). Japanese has no equivalent neuter possessive pronoun for inanimate objects, so the natural rendering reuses the subject's name with the genitive `の`: `=subject.name=のパスコード`. The token multiset gains an extra `=subject.name=` rather than introducing `=pronouns.possessive=` (which would render in Japanese as awkward English-style pronouns).
- **Entry 9**: `=object.directionIfAny=` resolves to a direction marker like `↑` or `に北` (or empty). Inserting it after `=object.name=` matches the surface form the player expects when the action is directional.
- **Entry 12**: The current passive `に変換された` is grammatically correct but loses the subject of the conversion. The new active form `=subject.name=を=newLiquid=に変換した` mirrors the English `convert =pronouns.objective= to =newLiquid=` exactly.
- **Entry 22**: A bare `=object.name=は` opens the sentence, restoring the subject reference that `=object.Does:are=` carried in English.
- **Entry 26**: The `=subject.name=の結晶の外殻` repetition is mildly redundant, but the alternative `自分の` would lose machine-readability for future translators auditing token coverage. Preferred mechanical preservation.

### Test seam

Update `Mods/QudJP/Assemblies/QudJP.Tests/L1/BlueprintTemplateTranslationPatchTests.cs` `LoadTranslations_ContainsExpectedMapping` parametrized test:

- The existing `[TestCase("You touch =subject.t= ...", "あなたは=subject.name=に触れ、パスコードを思い出した。...")]` is the entry-7 case. Update the `expected` argument to the new Japanese.
- Add five new `[TestCase(...)]` lines covering entries 5, 9, 12, 22, 26.

Each TestCase exercises the dictionary load path end-to-end: it asserts the JSON entry exists with the expected key and renders the expected text. If the JSON edit forgets one fix, the test fails on that case.

The existing `LoadTranslations_TranslatedTemplatesPreserveVariableReplaceSlots` test (line 59-80) only checks `=subject.name=` and `=object.name=` slot survival, which is too loose to catch this class of bug. We do not extend it here; #409 owns the broader contract.

## How

1. Update `BlueprintTemplateTranslationPatchTests.cs`: change the existing entry-7 TestCase's expected value, then add 5 new TestCase lines for entries 5, 9, 12, 22, 26 with the new Japanese strings.
2. Run the L1 test. Expect exactly 6 failures (entry 7 because the existing TestCase now expects new text; entries 5/9/12/22/26 because their TestCases assert text that does not yet exist in JSON).
3. Apply the six text-field edits to `templates.ja.json`.
4. Re-run the L1 test. Expect green.
5. Run the standard repo verification suite.

## Verification

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~BlueprintTemplate"
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
uv run pytest scripts/tests/ -q
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
ruff check scripts/
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

All must pass.

## Out of scope

- Generic `=variable=` token-parity validator across all dictionaries. That belongs in #409 with an allowlist that classifies tokens as "article-collapsable" vs. "information-bearing".
- Style polish on the 24 GOOD entries (e.g., `start up with a hum` → more natural Japanese verbs). Translation tone is not a Steam blocker for these entries; treat as polish, file separately if pursued.
- `Pettable` part-translation pipeline question Codex raised about entry 16 — the entry is in the JSON but may not be reached at runtime because `Pettable` is not in the patch's `TranslatablePartFields` list. That is a separate runtime-evidence question and not the scope of #402's data fix.
- XML override files (`*.jp.xml`) that may have similar `=variable=` issues — those have their own translation paths and belong to other issues.

## Risks

- **Resolver behaviour drift**: if a future game version changes how unrecognised `=variable=` keys are handled (e.g., from "log warning + leave raw" to "throw"), our liberal collapses become liabilities. The L1 TestCase locks current expectations.
- **Translation tone**: the chosen Japanese for entries 5, 7, 26 reuses `=subject.name=` in possessive position. If a future glossary prefers `自分の` style, the test will still pass because it asserts exact text — change in one place.
- **Entry 16 (Pettable) silent inapplicability**: out of scope here, called out for tracking.
