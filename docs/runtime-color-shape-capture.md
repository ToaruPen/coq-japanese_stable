# Runtime color-shape capture

## 目的

色タグを含む runtime text route では、手書きの source 例だけでは実 producer が出す
組み合わせの drift を検出しきれない。`ColorShapeProbe/v1` は、実行時 route から
producer-derived text shape を捕捉し、後続の invariant test に昇格するための証跡である。

初期 lane は inventory item display name に置く。`InventoryLineTranslationPatch` は
`InventoryLineData.displayName` または `go.DisplayName` を受け取り、翻訳後に
`OwnerTextSetter` へ渡す owner route なので、source text、visible text、color span、
final output を同じ地点で観測できる。

## Artifact format

QudTest の inventory color-shape routes は、`results.json` の該当 case に
`colorShape` object を出力する。`inventory-display-name` は固定 source string を
translator route に流す L1/smoke 用 route、`inventory-display-name-game-object` は
`GameObject.DisplayName` 経由で producer-derived source を生成する route である。
dev build の verbose probe では同じ内容を
`ColorShapeProbe/v1` として `Player.log` にも出力できる。1 record には少なくとも次を含める。

- `route`: owner route。例: `InventoryLineTranslationPatch`
- `producer`: text の由来。例: `InventoryLine.GameObjectDisplayName`
- `source`: producer 由来の raw source text
- `sourceVisible`: `ColorAwareTranslationComposer.Strip` 後の visible text
- `final`: route translation 後の output
- `finalVisible`: final output の visible text
- `sourceColorSpans` / `finalColorSpans`: stripped visible index と color token の signature
- `sourceVisibleSha256` / `finalVisibleSha256`: long text 比較用 hash
- `markupSemanticStatus` / `markupSemanticFlags`: final output の markup semantic diagnostics

この artifact は exact English string を固定するためではなく、捕捉した family を構造
invariant に変換する入口として使う。たとえば inventory display-name family では、
base name、bracket state、loaded cell、weapon stat、angle code、outer color wrapper の
保持や、二重 wrapper・壊れた bracket/tag 断片の不在を検証対象にする。

## Onboarding order

次の color-sensitive route は、`ColorRouteCatalogTests` の route catalog と owner-route
境界に沿って段階的に onboard する。

1. Message log / message queue
   - `MessageQueue.AddPlayerMessage` と message pattern producer helpers を優先する。
   - Combat, pass-by, generated queue text の source/final と direct-translation marker 状態を捕捉する。

2. Popup and menu routes
   - `Popup.Show` 系の fixed popup text と menu item text を分ける。
   - Generic popup producer route は allowlist 理由を保ち、dynamic option producer は owner helper 側で捕捉する。

3. UI text skin sink surfaces
   - `UITextSkin` は owner ではなく sink observation として扱う。
   - sink-only artifact は missing owner triage 用に限定し、修正は producer/binding route へ戻す。

4. Equipment, trade, and inventory-adjacent rows
   - `EquipmentLineTranslationPatch`、`TradeLineTranslationPatch`、trade totals/labels を route ごとに分ける。
   - display-name fragment と weight/value/stat suffix を別 invariant として扱う。

5. Ability, status, and active-effect text
   - ability name/status line/effect detail の owner route ごとに producer context を付ける。
   - cooldown、hotkey、TMP rich-text、status bracket fragment を color span invariant に含める。

## Verification

最初の自動検証は L1/L2 で行う。

- L1: `ColorShapeCaptureObservability` と QudTest `inventory-display-name` route が source/final visible text と color span を出すこと。
- L2: `InventoryLineTranslationPatch` が producer-derived display name を捕捉し、その captured shape 由来の structural invariant を満たすこと。
- Headless QudTest: `just qudtest-headless qudtest:runtime .artifacts/qudtest` の `results.json` に `inventory-display-name.game-object-colored-state.colorShape` が含まれること。
- Producer-derived headless QudTest: `just qudtest-headless qudtest:inventory-shapes .artifacts/qudtest-inventory-shapes` の `results.json` に `inventory-display-name-game-object.copper-nugget.colorShape` と `inventory-display-name-game-object.grit-gate-recoiler.colorShape` が含まれ、`input` は blueprint name、`colorShape.source` は `GameObject.DisplayName` から得た colored display name になること。`grit-gate-recoiler` は inline source wrapper の visible length が翻訳で変わっても wrapper span が clean に保たれることを検証する。
- L3: 実ゲーム起動後の `Player.log` で `ColorShapeProbe/v1` の inventory record を確認すること。

L3 は runtime smoke evidence であり、CI の代替ではない。実ゲーム型生成や Unity/TMP 表示に入る検証は
`docs/test-architecture.md` の L3 境界に従う。
Headless .NET では Unity ECall を伴う `GameObject.CreateSample` が実行できない場合があるため、
`inventory-display-name-game-object` はその場合だけ blueprint XML の `Render.DisplayName` から
最小 `GameObject` を組み立て、同じ `GameObject.DisplayName` property を通して source を生成する。
この fallback は repo の `Mods/QudJP/Localization/ObjectBlueprints/*.jp.xml` を base game XML より
優先して読む。`Chem Cell` や `Grit Gate Recoiler` のような item display name は display-name
辞書 lookup ではなく ObjectBlueprint overlay で既に日本語化されるため、producer-derived capture
では `source` と `final` が同じ日本語 display name になる。
