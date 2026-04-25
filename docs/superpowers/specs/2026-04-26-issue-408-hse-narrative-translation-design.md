# Issue #408 — HistoricStringExpander narrative-property translation strategy

## Why

`HistoryKit.HistoricStringExpander` が動的に組み立てる sultan gospel / tomb inscription / village proverb / village gospel list / sacred-and-profane things / immigrant-and-pet dialog などの史実テキストは現在ほぼすべて未翻訳である。`Mods/QudJP/Assemblies/src/Patches/HistoricStringExpanderPatch.cs` には `ExpandString` postfix の試作実装が残っているが、`TargetMethods()` が `yield break` し「temporarily disabled to avoid corrupting HistorySpice/world generation output」のコメント付きで無効化されている。これは妥当な判断である:

- `HistoricStringExpander.ExpandString` は **可視文と symbolic JSON path key の両方** に使われる ([HistoricStringExpander.cs](https://github.com/freeholdgames/cavesofqud) ExpandQuery / ForgeItem / MeetFaction 等)
- `HistoricEvent.SetEventProperty` / `SetEntityProperty` / `AddEntityListItem` は保存前に `ExpandString(Value)` を経由する場合があり、戻り値は eventProperties / entityProperties に格納される
- そこを postfix で日本語化すると `<spice...>` の path lookup が `<spice.ガラスの.items.!random>` のような失敗ノードを生成し、save-game state まで汚染が伝播する

本 issue では **mid-pipeline narrative-property allowlist patch** 戦略により、symbolic key 用 property を一切 mutate せず、ユーザー可視 prose **だけ** を 2 つの Harmony hook で翻訳する。Codex 諮問 3 ラウンドで設計確定。

## What

### スコープ (B-full): 翻訳対象 allowlist

**Event property** (`history.events[i].eventProperties`):
- `gospel`
- `tombInscription`

**Entity property** (HistoricEntitySnapshot に保存):
- `proverb`
- `defaultSacredThing`
- `defaultProfaneThing`
- `immigrant_dialogWhy_Q`
- `immigrant_dialogWhy_A`
- `pet_dialogWhy_Q`

**Entity list property**:
- `Gospels` (各要素は `prose|eventId` 形式 → 第 1 要素のみ翻訳、`|eventId` は保持)
- `sacredThings`
- `profaneThings`

それ以外の `SetEventProperty` / `SetEntityProperty` / `AddEntityListItem` 値 (`region`, `location`, `revealsRegion`, `revealsItem*`, `tombInscriptionCategory`, `gyreplagues`, `JoppaShrine`, `rebekah`, `rebekahWasHealer`, `name`, `cultName`, `cognomen`, `signatureDishName`, `signatureHistoricObjectName`, `palette`, `colors`, `elements`, `parameters`, `locations`, `signatureLiquids`, `signatureDishIngredients`, `sharedMutations`, `sharedDiseases`, `sharedTransformations`, faction lists, etc.) は **絶対に mutate しない**。

### Harmony hook (2 つだけ)

#### Hook 1: `XRL.Annals.QudHistoryFactory.GenerateVillageEraHistory(History)` Postfix

対象: `history.events[*].eventProperties` の `gospel` / `tombInscription` のみ

理由: 初期 sultan gospel/tombInscription は `GenerateVillageEraHistory` 内で `ConvertGospelToSultanateCalendarEra` (年号変換) と `Grammar.ConvertAtoAn` を経て最終整形される。Postfix 時点で final-form。

#### Hook 2: `Qud.API.JournalAPI.AddVillageGospels(HistoricEntity)` Prefix

対象: village entity snapshot の上記 entity property + entity list property

理由 + 重要事項:

- **Prefix にする**: `AddVillageGospels(HistoricEntity)` → `AddVillageGospels(HistoricEntitySnapshot)` の overload chain。Snapshot overload 本体が `Snapshot.GetList("Gospels")` を即消費するため、Postfix では遅すぎる
- **HistoricEntity overload を hook**: HistoricEntitySnapshot overload は書き戻し先がない (snapshot は immutable view)。HistoricEntity を hook すれば snapshot mutation を経由して反映される
- **初期世界 + Coda の両方を網羅**:
  - 初期世界: `JoppaWorldBuilder.AddVillages()` → `Worships/Despises.PostProcessEvent` (`*Worships.LegendaryCreature.DisplayName*` 等の placeholder substitution) → `JournalAPI.InitializeVillageEntries()` → 各村に対し `AddVillageGospels(HistoricEntity)` ← **ここで Prefix 発火**
  - Coda 終盤: `VillageCoda.GenerateVillageEntity()` (post-process 済み entity を返す) → `EndGame.ApplyVillage()` → `AddVillageGospels(HistoricEntity)` ← **同じ Prefix で発火**

### Markup invariants (翻訳器が壊さず保持)

- Color/style: `&X` / `^x` (`&&` / `^^` リテラル escape を含む)
- Span: `{{Y|...}}`
- Grammar marker (residual when value starts with `=`): `=name=`, `=year=`, `=pluralize=`, `=article=`, `=Article=`, `=capitalize=`, 一般化 `=alphanumeric=`
- Residual template (失敗時に残る `<spice...>` / `<entity...>`):
  - `<undefined entity property ...>`
  - `<undefined entity list ...>`
  - `<empty entity list ...>`
  - `<unknown entity>`
  - `<unknown format ...>`
- PostProcessEvent placeholder: `*Worships.LegendaryCreature.DisplayName*` 等 `*alphanumeric...*`
- Layout: `\n`
- List separator (Gospels のみ): `|` (split 後の右側 `eventId` 部分は不変)

### `*...*` placeholder 戦略

`AddVillageGospels` Prefix 時点で `Worships.PostProcessEvent` / `Despises.PostProcessEvent` は完了済みのため、通常 `*Worships.LegendaryCreature.DisplayName*` は creatureName に置換済 (`GetReferenceDisplayName(..., Stripped: true)` 由来)。QudJP の `GetDisplayNamePatch` が JP 化する経路があるため固有名詞は通常 JP。MVP では:

- Translator は `*alphanumeric*` を invariant として preserve する (defensive)
- 助詞・grammar agreement は pattern/template 側で吸収。固有名詞自体への追加処理はしない
- 残存 `*...*` が混ざる稀なケースは未翻訳混在を許容

### コード分離 (3 クラス)

#### `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeTextTranslator.cs` (新規)

- 純粋 helper、game DLL / Harmony 非依存
- API: `string Translate(string? source, string route)`
- 内部:
  - exact lookup 前段: `StringHelpers.TryGetTranslationExactOrLowerAscii(source, out result, useDirectMarker: false)` (marker なし)
  - pattern 後段: `JournalPatternTranslator.Translate(source, route)` (lock + ConcurrentDictionary、安全)
  - **`JournalTextTranslator.TryTranslate*ForStorage()` は使わない** (`` direct marker を付けるが `Gospels` は `VillageStoryReveal` 等 journal 以外でも直接表示される)
  - Markup invariant のうち `*...*` のような未知 token は pattern translator が literal として扱う既存挙動に依存 (新規パーサ不要)
- L1 unit test 対象

#### `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs` (新規)

- `IDictionary<string, string> properties` と `IDictionary<string, List<string>> listProperties` を allowlist に従って走査
- API:
  - `void TranslateEventProperties(IDictionary<string, string> properties, HistoricNarrativeTextTranslator translator, string route)`
  - `void TranslateEntitySnapshot(HistoricEntitySnapshot snapshot, HistoricNarrativeTextTranslator translator, string route)` (内部で properties + listProperties を走査)
  - `string TranslateGospelEntry(string raw, HistoricNarrativeTextTranslator translator, string route)` ← `prose|eventId` を split → prose のみ翻訳 → 再結合
- 各 allowlist キーは module-level `static readonly HashSet<string>` で定数化
- Idempotency: 翻訳済み判定は不要 (`Translate` が pass-through なら no-op)
- L1 unit test 対象 (game DLL 非依存にするため `IDictionary` shape で受け取る)

#### `Mods/QudJP/Assemblies/src/Patches/HistoricNarrativeTranslationPatches.cs` (新規)

- 2 つの Harmony patch class を 1 ファイルにまとめる:
  - `[HarmonyPatch(typeof(QudHistoryFactory), nameof(QudHistoryFactory.GenerateVillageEraHistory))]` Postfix
  - `[HarmonyPatch(typeof(JournalAPI), nameof(JournalAPI.AddVillageGospels), typeof(HistoricEntity))]` Prefix (HistoricEntity overload を明示指定)
- 各 patch body は短く: walker を呼ぶだけ
- `HistoricStringExpanderPatch.cs` は **触らない** (disabled stub のまま、経緯記録維持)

### テスト (test-architecture.md 準拠)

#### L1 (純粋関数テスト): `Mods/QudJP/Assemblies/QudJP.Tests/L1/`

新規:
- `HistoricNarrativeTextTranslatorTests.cs`:
  - passthrough: 未マッチ入力は原文返す
  - exact translation: dictionary hit
  - pattern translation: regex/template hit
  - markup invariant preservation: `&X`, `^x`, `&&`, `^^`, `{{Y|...}}`, `\n`, `=name=`, `=year=`, `=pluralize=`, `=article=`, `=Article=`, `=capitalize=`, `<spice...>`, `<entity...>`, `<undefined entity property ...>`, `<empty entity list ...>`, `<unknown entity>`, `<unknown format ...>`, `*Worships.LegendaryCreature.DisplayName*`
  - direct marker (``) を付けないこと
- `HistoricNarrativeDictionaryWalkerTests.cs`:
  - allowlist 内のキーだけ翻訳、それ以外不変
  - `Gospels` の `prose|eventId` split + 再結合の往復
  - 二重実行 (idempotency)
  - null / 空 dictionary の no-op
  - allowlist にないが似た名前のキー (`gospelText` 等) は触らない

#### L2 (dummy seam test): `Mods/QudJP/Assemblies/QudJP.Tests/L2/`

新規:
- `HistoricNarrativeTranslationPatchesTests.cs`:
  - dummy `DummyHistoricNarrativeTargets.cs` で `History` / `HistoricEvent` / `HistoricEntity` / `HistoricEntitySnapshot` の最小モック (test-architecture.md:97 に従い実 HistoryKit instantiate 禁止)
  - `GenerateVillageEraHistory(History)` Postfix が走った後、event の `gospel`/`tombInscription` だけ JP 化、`region`/`location`/`tombInscriptionCategory`/`revealsRegion` 等は不変
  - `AddVillageGospels(HistoricEntity)` Prefix が走った後、entity の allowlist プロパティだけ JP 化、symbolic key は不変
  - Coda 経路 (EndGame.ApplyVillage 模擬) でも同じ Prefix が発火することを `__originalMethod` 経由で確認
  - Save / load 経由しても direct marker (``) が漏れないこと

新規 Dummy:
- `Mods/QudJP/Assemblies/QudJP.Tests/DummyTargets/DummyHistoricNarrativeTargets.cs`:
  - `class DummyHistoricEvent { Dictionary<string, string> eventProperties; }`
  - `class DummyHistoricEntitySnapshot { Dictionary<string, string> properties; Dictionary<string, List<string>> listProperties; }`
  - `class DummyHistory { List<DummyHistoricEvent> events; }`
  - 既存 `DummyJournalApiTargets.cs` の style precedent に倣う

#### L2G (signature verification): `Mods/QudJP/Assemblies/QudJP.Tests/L2G/`

新規:
- `HistoricNarrativeTranslationPatchesGuardTests.cs`:
  - **Patch target signature 検証** (Harmony 失敗を fail-fast):
    - `XRL.Annals.QudHistoryFactory.GenerateVillageEraHistory(History)` の存在 + return type + parameter list
    - `Qud.API.JournalAPI.AddVillageGospels(HistoricEntity)` の存在 + parameter (HistoricEntity overload を `typeof(HistoricEntity)` で resolve)
  - **Assumption check** (patch しないが前提):
    - `Qud.API.JournalAPI.AddVillageGospels(HistoricEntitySnapshot)` (overload chain の存在)
    - `XRL.Annals.Worships.PostProcessEvent` (placeholder substitution の存在)
    - `XRL.Annals.Despises.PostProcessEvent`
    - `XRL.World.ZoneBuilders.VillageCoda.GenerateVillageEntity` (Coda 経路)
    - `XRL.World.Conversations.Parts.EndGame.ApplyVillage` (Coda 経路)
    - `XRL.World.WorldBuilders.JoppaWorldBuilder.AddVillages` (初期経路)

### 翻訳ソース (out of scope)

`world-gospels.ja.json` は raw `<spice...>` template 形式 (1286 entries 中 1242 が `HistoryKit.HistoricStringExpander.ExpandQuery.Token` context) で、final expanded prose には直接 lookup できない。本 PR の MVP は **passthrough**: パッチ経路を開通させ、未マッチ prose は英語のまま表示。pattern/template 辞書 (`journal-patterns.ja.json` への追記、または専用 `historic-narrative-patterns.ja.json`) の充実は **別 issue** として後続。

## How

### 実装順序 (TDD: Red → Green → Refactor)

1. **L1 failing tests 追加 (Red)**:
   - `HistoricNarrativeTextTranslatorTests` 全ケースを書く (translator class はまだ存在しないので compile 失敗)
   - `HistoricNarrativeDictionaryWalkerTests` 全ケースを書く (walker class はまだ存在しないので compile 失敗)
2. **`HistoricNarrativeTextTranslator` 実装 (Green 1)**: L1 translator tests pass
3. **`HistoricNarrativeDictionaryWalker` 実装 (Green 2)**: L1 walker tests pass
4. **L2 dummy + tests 追加 (Red)**:
   - `DummyHistoricNarrativeTargets.cs` 新規
   - `HistoricNarrativeTranslationPatchesTests.cs` 全ケースを書く (patch class はまだ存在しないので runtime 失敗)
5. **`HistoricNarrativeTranslationPatches` 実装 (Green 3)**: L2 tests pass
6. **L2G signature verification 追加**: 全 target / assumption method の存在検証
7. **Full repository verification**: pytest / ruff / encoding / strict validate / dotnet build / L1 / L2 / L2G

### タスク分割 (subagent-driven 用)

- Task 1: L1 translator failing tests + class skeleton
- Task 2: `HistoricNarrativeTextTranslator` 実装
- Task 3: L1 walker failing tests + class skeleton
- Task 4: `HistoricNarrativeDictionaryWalker` 実装
- Task 5: Dummy fixtures + L2 failing tests
- Task 6: `HistoricNarrativeTranslationPatches` 実装
- Task 7: L2G signature verification
- Task 8: Full verification

## Verification

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G
uv run pytest scripts/tests/ -q
ruff check scripts/
python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json
```

すべて green でなければならない。

## Risks

### Save-game persistence (許容)
本 patch が Translator を直接 marker 付与なしで呼ぶため、save-game に格納される `gospel`/`tombInscription`/`Gospels`/`proverb` は素の JP テキスト。mod uninstall 後も読める (英語に戻らないが破壊はしない)。Acceptable for B-full scope.

### Mod 互換 (低リスク)
他の Caves of Qud mod が同じ method を Harmony 化する可能性は低い (HistoricStringExpander は内部 API)。ただし Harmony priority を `[HarmonyPriority(Priority.Low)]` で他 producer の mutation 後に走らせる。

### パフォーマンス (無視可)
village-era history は数百 events + 数十 entities 規模。translator regex / dict 負荷は無視可。測定不要。

### Null safety
- `AddVillageGospels(HistoricEntity)` Prefix で entity が null なら no-op
- `Gospels` list が null/empty なら no-op
- `HistoryAPI.GetVillages()` が null を返す場合は vanilla 側が先に throw する想定 (我々の hook で別途防御しない)

### Re-entrancy (発生しない)
`HistoricNarrativeTextTranslator` は `History` / `HistoricEntity` / `HistoricStringExpander` のいずれにもアクセスしない。`JournalPatternTranslator` も同様。`ExpandString` 経由の再帰は構造的に発生不可能。

### 翻訳品質 (MVP 上限)
本 PR は seam 開通のみ。翻訳カバレッジは passthrough。実際の prose 翻訳は別 issue (HistorySpice corpus translation) で `journal-patterns.ja.json` 追記または専用 pattern source 構築。

## Out of scope

- **HistoricStringExpanderPatch.cs の再有効化** — 一切しない (disabled stub のまま経緯記録)
- **`world-gospels.ja.json` の翻訳追加** — 別 issue
- **`journal-patterns.ja.json` への HSE prose pattern 追加** — 別 issue
- **`HistorySpice.json` template manifest の翻訳パイプライン整備** — issue 本文の Suggested Task 6、別 issue
- **C# コード以外の追加** — 不要 (本 PR は翻訳経路のみ、辞書追加は無し)
- **Coda 以外の runtime mutation 経路** — 現時点で確認された経路は initial-world + Coda の 2 つのみ。それ以外が後で発見されれば別 hook を追加 (新 issue)

## References

- 親 issue #400 (audit overall)
- Codex 諮問 1 (HSE postfix vs mid-pipeline 設計): allowlist 方式採用
- Codex 諮問 2 (Gospels write site + JournalPatternTranslator coverage): 12+ write sites 確認、entity prose 拡張識別、PostProcessEvent + Coda 別経路発見
- Codex 諮問 3 (B-full アーキテクチャ): Approach C-modified (Postfix on `GenerateVillageEraHistory` + Prefix on `AddVillageGospels(HistoricEntity)`) 確定、3 クラス分離、L1/L2/L2G test 構成
