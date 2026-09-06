# QudJP 1.0.5 未訳経路クローズ設計

## 目的

Caves of Qud `1.0.5` の decompiled source、現行 localization asset、静的 producer
inventory、最新 runtime log を突き合わせ、確認できた未訳経路と 1.0.5 変更による
翻訳回帰を修正する。同時に、`1.0.4` のまま残っている静的 producer inventory と
coverage map を 1.0.5 source から再生成し、今後の未訳監査が現行ゲーム版を基準に
動く状態へ戻す。

この作業は専用 worktree
`/Users/sankenbisha/Dev/coq-japanese_stable/.worktrees/qudjp-1-0-5-untranslated-closure`
と branch `codex/qudjp-1-0-5-untranslated-closure` で行う。実装・検証結果のコミットは、
リポジトリ規約に従いユーザーから明示的に依頼された場合だけ行う。

## 調査結果

### 現在の基準状態

- 最新 `Player.log` の phase A/F triage は actionable 0、observation 0 である。
- `just localization-check` と `just translation-token-check` は成功している。
- 修正前の `just game-version-check` は成功した。
  - C# 全テスト: 10,791 件成功
  - game DLL 非依存テスト: 10,187 件成功
  - QudTest exact bindings: 19 件成功
  - QudTest all-patch bindings: 534 件成功
- 広い function-word audit は 2,514 candidates を返すが、既訳文、固有名、コード断片、
  意図的英語を多く含む。この数を未訳件数として扱わない。
- XML ID の単純差分も内部用 definition を多数含むため、その件数を未訳件数として
  扱わない。Options については表示 owner と照合して 6 ID を確定した。

### `1.0.4` 表記が残った理由

静的 producer inventory は 2026-05-05 に導入された。その後の commit
`b000c608` で誤記 `2.0.4` が実際の対象版 `1.0.4` に直され、scanner 定数、テスト、
tracked artifact が 1.0.4 snapshot として固定された。2026-07-14 の commit
`de0b1de3` は runtime project を 1.0.5 に更新したが、当時の互換対応設計で
scanner と tracked inventory の更新を別タスクへ分離したため、この領域だけが
1.0.4 のまま残った。

したがって現状は「現在も 1.0.4 を対象にしている」のではなく、1.0.5 移行時に
再生成されなかった古い snapshot である。旧 JSON の version header だけを 1.0.5
へ書き換えることはせず、現行 decompiled source から全 artifact を再生成する。

### 確定した修正対象

1. `Qud.UI.GameSummaryScreen` の tombstone 保存エラー
   - 1.0.5 では `There was an error {storageResult} saving: {path}` になった。
   - 現 patch と辞書は旧形 `There was an error saving: {path}` だけに一致する。
2. modern/classic keybinding の default 復元確認
   - 1.0.5 では `keymap` が `bindings` に変わった。
   - 辞書とテストは旧文言だけを保持している。
3. `CookingDomainSpecial_UnitSlogTransform.ApplyTo` の Bilge Sphincter 獲得 popup
   - 新しい固定文 `You gained the mutation {{w|Bilge Sphincter}}!` が owner 内から出る。
   - mutation 名自体は `HiddenMutations.jp.xml` ですでに訳されている。
4. scores / leaderboard の状態・再試行表示
   - offline、generic error、leaderboard fetch retry、score post retry が未訳である。
   - stack trace やサーバー由来の詳細文字列は技術情報として英語を維持する。
5. `CodeCompressor.loadCode` の required-mod decode error
   - `Error decoding build code - Required Mod "{mod}" not found.` が未訳である。
   - mod 名は捕捉して原文のまま保持する。
6. Options の表示対象 6 ID
   - `OptionDisableAllIdleTileAnimations`
   - `OptionDrawStepImmediately`
   - `OptionEnableSeed`
   - `OptionDisableZoneThawRestrict` と help text
   - `OptionDrawFloodExplore`
   - `OptionTextBuilderDebug` と help text
7. 1.0.5 static producer inventory / coverage map / closure overlay
   - 1.0.5 preview は 2,216 callsites、1,016 families、2,246 text arguments である。
   - tracked 1.0.4 artifact は 2,208 / 1,012 / 2,238 であり、差分レビューが必要である。

### 追跡する runtime risk

`CyberneticsMedassistModule` は producer expression が
`"Your " + ParentObject.does(...)` から `ParentObject.Does(...)` に変わった一方、
現 patch の正規表現は `^Your ...` を前提にしている。実際の生成文字列が同じなら
変更不要であるため、1.0.5 inventory 差分と既存テストで runtime shape を確認し、
不一致を再現できた場合だけ failing test を追加して修正する。

## 採用方針

最初に 1.0.5 inventory を再生成して source 差分を確定し、その証拠に基づいて
各 owner を Red → Green → Refactor で修正する。leaf sink に広い置換を追加せず、
producer または既存の scoped owner で翻訳する。固定 UI 文言は owner 専用辞書へ、
動的値を含む文言は狭い正規表現または producer helper へ置く。

辞書 lookup は完全一致を基本とし、ゲームがさらに文言を変更した場合は英語原文へ
fail-open する。color markup、direct marker、改行、動的な path/mod 名は保持する。

## 変更設計

### 1. static producer inventory を 1.0.5 へ移行する

scanner と artifact 契約に埋め込まれた対象版を 1.0.5 へ更新し、
`~/dev/coq-decompiled_stable/` の現行 source から次を再生成する。

- `docs/static-producer-inventory.json`
- `docs/localization-coverage-map.json`
- closure overlay と、リポジトリが追跡する current report
- artifact version、件数、source/test evidence を固定する対応テスト

再生成後は、1.0.4 artifact との差分を callsite/family/text argument ごとに確認する。
line number だけが動いた owner は current source evidence を更新し、新規・変更 producer
は `covered`、`needs-work`、`candidate`、`unresolved`、または明示的除外へ再分類する。
`covered` とする行には現行 source とテスト evidence の両方を要求する。

1.0.5 の件数を先に期待値へ直してテストを通すのではなく、generator 出力を source of
truth とし、テストは再生成された artifact の自己整合性と再現性を固定する。

### 2. Game Summary tombstone error

既存 `GameSummaryTombstonePopupTranslationPatch` の owner scope を維持し、1.0.5 の
`storageResult` と path をそれぞれ捕捉する。日本語 template に両方を戻し、path と
storage result の内容を変更しない。同じ 1.0.5 source でも modern UI は新形、classic
UI は `There was an error saving: {path}` の旧形を現用しているため、これは 1.0.4
後方互換ではなく 1.0.5 の二経路契約として両方を維持する。

テストは exact 変換、direct marker/color markup の保持、空 path、未知文言の no-op、
owner 外の no-op を固定する。

### 3. keybinding default 復元確認

`ui-options.ja.json` と modern/classic 双方の owner test を 1.0.5 の `bindings` 文言へ
更新する。文全体の完全一致を使い、他の `bindings` 文言を巻き込まない。1.0.4 の
`keymap` 互換は追加しない。

### 4. Bilge Sphincter 獲得 popup

既存 `CookingRuntimeTranslationPatch` の `ApplyTo` owner route に限定して処理する。
獲得 template は既存 mutation 獲得表現へ合わせ、表示名は localization asset の
Bilge Sphincter 訳を再利用する。同一英語全文を汎用 popup 辞書へ追加しない。

テストは owner target、exact text、markup、mutation 表示名、未知 mutation の no-op、
owner 外の no-op を固定する。

### 5. scores / leaderboard

`ui-scores.ja.json` に次の安定した固定ラベル・template を追加する。

- game offline
- generic error
- fetch retry
- post retry

`HighScoresScreen.FetchLeaderboard`、`XRLCore.BuildScore`、必要な legacy `Scores`
経路を、それぞれ producer/scoped owner で翻訳する。retry 文に動的 error detail が
連結される場合は固定 prefix だけを訳し、detail と stack trace は原文のまま保持する。
upstream の `leaderbaord` typo も入力契約として明示的に扱い、出力には持ち込まない。

async/iterator owner の場合は既存 state-machine target 解決を再利用し、コンパイラ生成
型名をハードコードしない。テストは各固定行、詳細付き retry、markup、未知詳細、
owner 外 no-op、辞書欠落時 fail-open を固定する。

### 6. build code required-mod error

`CodeCompressor.loadCode` を target にする狭い owner patch を追加する。required mod 名を
正規表現で捕捉し、日本語 template に戻す。mod 名が空の場合も文の構造を壊さず、
未知の decode error は変更しない。

テストは exact、任意 mod 名、空 mod 名、color/direct marker、未知文言、owner 外 no-op
を固定する。

### 7. Options 6 ID

`Options.xml` の表示文と help text を `Options.jp.xml` に追加する。ID と category 構造は
英語 asset に一致させ、既存日本語 Options の語彙・句読点・全半角方針に合わせる。

各 ID の存在だけでなく、base と jp の ID 対応、help text の取りこぼし、空訳、英語
function-word residue を検出する focused test を先に追加する。debug option は設定画面に
表示されるため対象に含めるが、内部 code symbol は翻訳しない。

### 8. release note

`Mods/QudJP/Localization/` を変更するため、
`docs/release-notes/unreleased/` に今回の 1.0.5 未訳経路修正を記録する。version bump、
Workshop 公開、manifest の一時変更は行わない。

## TDD と検証順序

1. inventory/version 契約の focused tests を 1.0.5 期待値で追加し、旧 artifact に対して
   失敗することを確認する。
2. generator/scanner 定数を更新し artifact を再生成する。
3. 1.0.5 差分を分類し、closure/source/test evidence の focused gate を通す。
4. Game Summary、keybinding、Bilge Sphincter、scores、CodeCompressor、Options の順に、
   それぞれ failing test を単独で確認してから最小実装を加える。
5. 各 Green 後に関連辞書、helper、owner scope の重複を整理する。
6. 新識別子・新リテラルを `rg` で確認し、意図した owner/helper/asset だけに追加された
   ことを確認する。
7. release note を追加し、全ゲートを実行する。

テスト失敗が既存基準にも再現する場合は今回の変更による regression と混同しない。
予期しない失敗を実装で隠さず、原因を切り分けてから進める。

## 完了条件

最低限、次を成功させる。

```bash
just build
just test-l1
just test-l2
just test-l2g
just ci-dotnet-no-game
just game-version-check
just python-check
just python-test
just localization-check
just translation-token-check
just release-note-check origin/main HEAD
```

加えて、static producer inventory/coverage map の再生成 check、closure check、変更 owner
の exact/all-patch QudTest binding を成功させる。artifact の version が 1.0.5 であり、
再生成して差分が出ないこと、`covered` owner の current source/test evidence が欠けて
いないことを確認する。

自動検証後の L3 smoke では、ユーザーがゲームを起動できる場合に次を確認する。

- tombstone 保存失敗の storage result と path が保持された日本語 popup
- modern/classic keybinding の default 復元確認
- Bilge Sphincter 獲得 popup
- offline/error/retry の score 表示
- required mod がない build code error
- 追加した Options の表示と help text
- Cybernetics Medassist の runtime shape と既存訳
- 新しい QudJP exception、patch target warning、missing glyph warning がないこと

L3 の再現が危険または困難な error path は、owner-level unit/integration test と fresh log
を証拠とし、ゲームデータやセーブを破壊する操作は行わない。

## 非対象

- debug wish `AchievementWishes.DebugUnlockAnyAchievement` の開発者向けメッセージ
- 既存 vaporized death owner で訳済みの `Physics.CheckThresholds`
- stack trace、サーバー応答、mod 名、filesystem path の内容自体の翻訳
- broad audit の全 2,514 candidates を一括置換すること
- XML ID 単純差分 4,916 件を未訳として一括追加すること
- 1.0.4 との同時対応
- 新しい汎用 localization architecture の導入
- manifest version bump、Steam Workshop 公開、リリース作業
- ユーザーの main checkout にある既存変更への編集
