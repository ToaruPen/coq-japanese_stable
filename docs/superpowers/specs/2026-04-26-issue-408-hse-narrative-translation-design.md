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

**Entity property** (`SetEntityProperty` で書かれる、村 entity の `properties` dict に格納):
- `proverb`
- `defaultSacredThing`
- `defaultProfaneThing`

**Entity list property** (`AddEntityListItem` で書かれる、村 entity の `listProperties` dict に格納):
- `Gospels` (各要素は `prose|eventId` 形式 → 第 1 要素のみ翻訳、`|eventId` は保持)
- `sacredThings`
- `profaneThings`
- `immigrant_dialogWhy_Q` (`PopulationInflux.cs:107` で `AddEntityListItem`)
- `immigrant_dialogWhy_A` (`PopulationInflux.cs:108` で `AddEntityListItem`)
- `pet_dialogWhy_Q` (`PopulationInflux.cs:156` で `AddEntityListItem`)

それ以外の `SetEventProperty` / `SetEntityProperty` / `AddEntityListItem` 値 (`region`, `location`, `revealsRegion`, `revealsItem*`, `tombInscriptionCategory`, `gyreplagues`, `JoppaShrine`, `rebekah`, `rebekahWasHealer`, `name`, `cultName`, `cognomen`, `signatureDishName`, `signatureHistoricObjectName`, `palette`, `colors`, `elements`, `parameters`, `locations`, `signatureLiquids`, `signatureDishIngredients`, `sharedMutations`, `sharedDiseases`, `sharedTransformations`, `worships_creature_id`, `despises_creature_id`, faction lists, etc.) は **絶対に mutate しない**。

### Harmony hook (2 つだけ)

#### Hook 1: `XRL.Annals.QudHistoryFactory.GenerateVillageEraHistory(History)` Postfix

対象: `history.events[*].eventProperties` の `gospel` / `tombInscription` のみ

理由: 初期 sultan gospel/tombInscription は `GenerateVillageEraHistory` 内で `ConvertGospelToSultanateCalendarEra` (年号変換) と `Grammar.ConvertAtoAn` を経て最終整形される。Postfix 時点で final-form。

#### Hook 2: `Qud.API.JournalAPI.AddVillageGospels(HistoricEntity)` Prefix

対象: village entity の上記 entity property + entity list property

**重要 — snapshot は read-only view、書き戻しは entity mutation API 経由**:

`HistoricEntity.GetCurrentSnapshot()` は呼ばれるたびに `events` を replay して fresh snapshot を構築する ([HistoricEntity.cs:303-323](https://github.com/freeholdgames/cavesofqud))。snapshot を直接 mutate しても entity の永続状態には反映されない。

正しい書き戻し API:
- Entity property: `entity.SetEntityPropertyAtCurrentYear(name, translatedValue)` — 内部で `ApplyEvent(new SetEntityProperty(name, value))` を呼び、entity の event list に新規 event を追加 ([HistoricEntity.cs:264-267](https://github.com/freeholdgames/cavesofqud))
- Entity list property: `entity.MutateListPropertyAtCurrentYear(name, mutation: Func<string, string>)` — 内部で `ApplyEvent(new MutateListProperty(name, mutation, snapshot))` を呼ぶ。`mutation` には translator の翻訳関数を直接渡せる ([HistoricEntity.cs:269-272](https://github.com/freeholdgames/cavesofqud))

walker は `__0` パラメータ (`HistoricEntity village`) に対し: 現在の snapshot を一度取得 → 各 allowlist プロパティについて translator で翻訳 → 上記 mutation API で書き戻し → 次に `AddVillageGospels` 内で呼ばれる `Village.GetCurrentSnapshot()` は新規 event を含めて replay するため翻訳済み値が返る。

**Prefix 採用理由**:
- `AddVillageGospels(HistoricEntity)` → `AddVillageGospels(HistoricEntitySnapshot)` の overload chain。後者が `Snapshot.GetList("Gospels")` を即消費するため、Postfix では遅すぎる
- HistoricEntity overload を hook することで、entity 単位の mutation API が使える

**初期世界 + Coda の両方を網羅**:
- 初期世界: `JoppaWorldBuilder.AddVillages()` → `Worships/Despises.PostProcessEvent` (`*Worships.LegendaryCreature.DisplayName*` 等の placeholder substitution) → `JournalAPI.InitializeVillageEntries()` → 各村に対し `AddVillageGospels(HistoricEntity)` ← **ここで Prefix 発火**
- Coda 終盤: `VillageCoda.GenerateVillageEntity()` (post-process 済み entity を返す) → `EndGame.ApplyVillage()` → `AddVillageGospels(HistoricEntity)` ← **同じ Prefix で発火**

### Markup invariants (翻訳器が壊さず保持)

`docs/RULES.md:130` の markup preservation rules + `Mods/QudJP/Assemblies/src/Translation/ColorCodePreserver.cs` の現行カバレッジに従う。

- Color/style: `&X` / `^x` (`&&` / `^^` リテラル escape を含む)
- Qud color span (任意 wrapper letter): `{{X|...}}`, `{{W|...}}`, `{{NAME|...}}` 等 — `{{Y|...}}` だけでなく一般形すべて
- TMP color span: `<color=#xxxxxx>...</color>` (TextMeshPro マークアップ)
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
  - 単一バックエンド: `JournalPatternTranslator.Translate(source, route)` (lock + ConcurrentDictionary、安全) を呼ぶだけ
  - 未マッチ入力は原文 passthrough (これは `JournalPatternTranslator` の既存挙動)
  - exact lookup 統合は本 PR スコープ外 — 将来必要なら別 helper として後付け (現 `StringHelpers.TryGetTranslationExactOrLowerAscii` の API は marker 制御不可、本 PR では使わない)
  - **`JournalTextTranslator.TryTranslate*ForStorage()` は使わない** (`` direct marker を付けるが `Gospels` は `VillageStoryReveal` 等 journal 以外でも直接表示される)
  - Markup invariant のうち `*...*` のような未知 token は pattern translator が literal として扱う既存挙動に依存 (新規パーサ不要)
- L1 unit test 対象

#### `Mods/QudJP/Assemblies/src/Translation/HistoricNarrativeDictionaryWalker.cs` (新規)

allowlist に従い、event 用 (直接 dict mutation) と entity 用 (mutation API 経由) の 2 経路を分離する。

API:

```csharp
public sealed class HistoricNarrativeDictionaryWalker
{
    private static readonly HashSet<string> EventPropertyAllowlist = new() { "gospel", "tombInscription" };
    private static readonly HashSet<string> EntityPropertyAllowlist = new() { "proverb", "defaultSacredThing", "defaultProfaneThing" };
    private static readonly HashSet<string> EntityListPropertyAllowlist = new() {
        "Gospels", "sacredThings", "profaneThings",
        "immigrant_dialogWhy_Q", "immigrant_dialogWhy_A", "pet_dialogWhy_Q",
    };

    // Event 経路: history.events[i].eventProperties は直接 mutation 可。
    // SetEventProperty は dict.Set を呼ぶだけ ([HistoricEvent.cs])。
    public void TranslateEventProperties(HistoricEvent ev, HistoricNarrativeTextTranslator translator, string route);

    // Entity 経路: GetCurrentSnapshot() で現在値読み取り → mutation API で書き戻し。
    public void TranslateEntity(HistoricEntity entity, HistoricNarrativeTextTranslator translator, string route);

    // Gospels split helper (testable in isolation)
    public string TranslateGospelEntry(string raw, HistoricNarrativeTextTranslator translator, string route);
}
```

`TranslateEntity` の擬似コード:

```csharp
var snapshot = entity.GetCurrentSnapshot();
foreach (var key in EntityPropertyAllowlist) {
    var current = snapshot.GetProperty(key);
    if (string.IsNullOrEmpty(current)) continue;
    var translated = translator.Translate(current, route);
    if (translated != current) entity.SetEntityPropertyAtCurrentYear(key, translated);
}
foreach (var key in EntityListPropertyAllowlist) {
    if (!snapshot.HasListProperty(key)) continue;
    var current = snapshot.GetList(key);
    Func<string, string> mutate = key == "Gospels"
        ? (raw => TranslateGospelEntry(raw, translator, route))
        : (raw => translator.Translate(raw, route));
    // 全要素を先行翻訳 → 1 つでも変化があった場合のみ mutation event を作る
    // (MutateListPropertyAtCurrentYear は内部で常に新規 event を adds、空 mutation でも履歴ノイズになる)
    var translated = current.Select(mutate).ToList();
    if (!translated.SequenceEqual(current)) {
        entity.MutateListPropertyAtCurrentYear(key, mutate);
    }
}
```

設計上の注意:
- Idempotency for entity properties: `if (translated != current)` ガードで余計な `SetEntityProperty` event を作らない
- Idempotency for entity list properties: `MutateListPropertyAtCurrentYear` は内部で常に `MutateListProperty` event を追加し、`Generate()` は無条件に `ChangeListProperty(...)` を emit する ([MutateListProperty.cs:26])。passthrough を保証するには **walker 側で先行翻訳して sequence equality を比較し、変化があった場合のみ API を呼ぶ** 必要がある。これで二重実行や全要素 passthrough のとき event 履歴を汚さない
- 本 PR の翻訳 1 回呼出しで `MutateListProperty` event が「変化があった list の数」だけ追加される (allowlist 全部不変なら 0 個)
- L1 unit test 対象 (`HistoricEvent` / `HistoricEntity` は dummy で代用、game DLL 非依存)
- L1 では `TranslateGospelEntry` の split/translate/rejoin は単独でテスト可

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
  - pattern translation: `JournalPatternTranslator` 経由の regex/template hit
  - markup invariant preservation:
    - Color/style: `&X`, `^x`, `&&`, `^^`
    - Qud color span (任意 wrapper letter): `{{X|...}}`, `{{W|...}}`, `{{NAME|...}}`
    - TMP color span: `<color=#xxxxxx>...</color>`
    - Layout: `\n`
    - Grammar markers: `=name=`, `=year=`, `=pluralize=`, `=article=`, `=Article=`, `=capitalize=`
    - Residual templates: `<spice.X.!random>`, `<entity.name>`
    - Diagnostic residuals: `<undefined entity property ...>`, `<empty entity list ...>`, `<unknown entity>`, `<unknown format ...>`
    - PostProcessEvent placeholder: `*Worships.LegendaryCreature.DisplayName*`
  - direct marker (``) を付けないこと
- `HistoricNarrativeDictionaryWalkerTests.cs`:
  - `TranslateGospelEntry`: `prose|eventId` split + 翻訳 + 再結合 (eventId は不変)
  - `TranslateGospelEntry`: `eventId` 部分が空 (例: `prose|`) でも壊れない
  - `TranslateGospelEntry`: `|` を含まない raw でも壊れない (eventId なし扱い)
  - `TranslateEventProperties`: dummy event の `eventProperties` dict を直接 mutation、allowlist 外キーは不変
  - `TranslateEntity`: dummy entity の現在値を読み、`SetEntityPropertyAtCurrentYear` / `MutateListPropertyAtCurrentYear` 呼出を verify (call recorder で確認)
  - `TranslateEntity`: list translation で各要素が個別に translator を通ること、index 順序が保たれること
  - 同一 entity に対する二重実行で event list に余計な重複 event を作らない: entity property は `if (translated != current)` ガード、list property は先行翻訳 + `SequenceEqual` 比較で変化があった list だけ `MutateListPropertyAtCurrentYear` を呼ぶ
  - 全要素 passthrough の list (例: 翻訳辞書未整備) に対して `MutateListProperty` event が追加されないことを assert
  - null / 空 entity の no-op
  - allowlist にないが似た名前のキー (`gospelText`, `tombInscriptionCategory`, `worships_creature_id` 等) は触らない

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
  - `class DummyHistoricEntity` — `events` の list を持ち、`GetCurrentSnapshot()` を呼ぶたびに events を replay して新規 snapshot を返す (本物の振る舞いを再現)。`SetEntityPropertyAtCurrentYear(name, value)` と `MutateListPropertyAtCurrentYear(name, mutation)` は events に新 event を追加 + 必要なら `lastSnapshot` を invalidate。これにより walker の write-back が原 entity に反映されることを赤にできるテストを構成可能
  - `class DummyHistory { List<DummyHistoricEvent> events; }`
  - 既存 `DummyJournalApiTargets.cs` の style precedent に倣う (game DLL 非依存)

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
- **Pet origin-story answer (`<spice.villages.pet.originStory.!random>` 経由の回答文)** — `pet_dialogWhy_A` は entity property/list に保存されず、zone build 時に `Village.cs:1665,1687` (Coda 側 `VillageCoda.cs:1934`) で `HistoricStringExpander.ExpandString` 直接呼出により都度生成。本 PR の 2 hook では捕捉不可。Village zone-builder hook が必要なため別 issue で追跡。`pet_dialogWhy_Q` (質問側) は本 PR スコープ内、`pet_dialogWhy_A` の回答文だけが英語のまま残る
- **Coda 以外の runtime mutation 経路** — 現時点で確認された経路は initial-world + Coda の 2 つのみ。それ以外が後で発見されれば別 hook を追加 (新 issue)

## References

- 親 issue #400 (audit overall)
- Codex 諮問 1 (HSE postfix vs mid-pipeline 設計): allowlist 方式採用
- Codex 諮問 2 (Gospels write site + JournalPatternTranslator coverage): 12+ write sites 確認、entity prose 拡張識別、PostProcessEvent + Coda 別経路発見
- Codex 諮問 3 (B-full アーキテクチャ): Approach C-modified (Postfix on `GenerateVillageEraHistory` + Prefix on `AddVillageGospels(HistoricEntity)`) 確定、3 クラス分離、L1/L2/L2G test 構成
- Codex 諮問 4 (spec review): snapshot mutation バグ発見 (entity 用 mutation API 経由に修正)、dialog allowlist 分類修正 (entity property → entity list property)、pet origin-story answer 別 issue 化、API 不整合修正 (`StringHelpers.TryGetTranslationExactOrLowerAscii` の marker 制御不可 → 単純化)、markup invariant 拡張 (任意 wrapper letter `{{X|...}}` + TMP `<color=...></color>`)
- Codex 諮問 5 (spec re-review): list mutation API の always-emit 挙動と idempotency 主張の矛盾発見 (`MutateListPropertyAtCurrentYear` は無条件 event を追加するため、walker 側で先行翻訳 + sequence equality 比較してから呼ぶ設計に修正)
