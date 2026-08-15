# QudJP 1.0.5 未訳経路クローズ実装計画

> 実行時は `test-driven-development` に従い、各タスクで Red を確認してから production code / asset を変更する。完了宣言前は `verification-before-completion` を適用する。

**目的:** 1.0.5 の現行 source に対して静的 producer inventory を再生成し、確認済みの Game Summary、keybinding、Bilge Sphincter、score/leaderboard、build code、Options の未訳経路を閉じる。

**設計:** producer または安定した mid-pipeline owner を使い、汎用 sink は観測専用のままにする。固定 leaf は最も狭い既存辞書へ、動的 capture を含む文は owner patch へ置く。modern/classic の両 UI が 1.0.5 で現用されている場合は両方を現行契約として維持する。

**技術:** C# / Harmony / NUnit、Python / pytest、Roslyn static inventory、XML / JSON localization assets、`just` validation recipes。

**作業場所:** `/Users/sankenbisha/Dev/coq-japanese_stable/.worktrees/qudjp-1-0-5-untranslated-closure`

**コミット方針:** ユーザーからコミット依頼はないため、タスク間の commit step は設けない。各 Green では `git diff --check` と focused gate で checkpoint を作る。

---

## Task 1: static producer と coverage map の version 契約を 1.0.5 にする

**変更ファイル:**

- `scripts/tests/test_scan_static_producer_inventory.py`
- `scripts/tests/test_localization_coverage_map.py`
- `scripts/scan_static_producer_inventory.py`
- `scripts/tools/StaticProducerInventoryScanner/Program.cs`
- `scripts/localization_coverage_map.py`
- `docs/localization-coverage-map.json`

### Step 1: failing tests を追加する

- scanner fixture output が `game_version == "1.0.5"` であることを要求する。
- tracked coverage map が `game_version == "1.0.5"` であることを明示的に要求する。
- malformed-map fixture も現行 version を使い、version mismatch が本来の validation error を隠さないようにする。

### Step 2: Red を確認する

```bash
uv run pytest scripts/tests/test_scan_static_producer_inventory.py::test_cli_writes_deterministic_inventory_without_absolute_source_root scripts/tests/test_localization_coverage_map.py -q
```

期待結果: scanner または coverage map の `1.0.4` が原因で失敗する。

### Step 3: 最小実装を加える

- Python wrapper、Roslyn scanner、coverage-map validator の現行 version 定数を `1.0.5` にする。
- `docs/localization-coverage-map.json` の header を 1.0.5 にする。
- 過去 report/spec/archive 内の履歴表記は変更しない。

### Step 4: Green を確認する

```bash
uv run pytest scripts/tests/test_scan_static_producer_inventory.py::test_cli_writes_deterministic_inventory_without_absolute_source_root scripts/tests/test_localization_coverage_map.py -q
just localization-coverage-map-check
```

## Task 2: 1.0.5 static producer artifact を再生成し差分を分類する

**変更ファイル:**

- `docs/static-producer-inventory.json`
- `scripts/static_producer_closure.py`
- `scripts/tests/test_static_producer_closure.py`
- `docs/localization-coverage-map.json`
- 新規 `docs/reports/2026-08-15-qud-1.0.5-static-producer-reconciliation.md`

### Step 1: disposable preview を生成する

```bash
just static-producer-preview ~/dev/coq-decompiled_stable /tmp/qudjp-static-producer-1.0.5.json
```

期待値: 1.0.5 source 由来の totals が 2,216 callsites、1,016 families、2,246 text arguments になる。差が出た場合は preview 自体を source of truth として report に記録する。

### Step 2: tracked artifact を再生成する

```bash
just static-producer-regenerate-tracked ~/dev/coq-decompiled_stable
cmp docs/static-producer-inventory.json /tmp/qudjp-static-producer-1.0.5.json
```

旧 JSON の header だけを変更せず、全 callsite/family/line evidence を generator 出力へ置き換える。

### Step 3: closure の Red を収集する

```bash
uv run python scripts/static_producer_closure.py --inventory /tmp/qudjp-static-producer-1.0.5.json --format json --limit 0 > /tmp/qudjp-static-producer-1.0.5-closure.json
uv run pytest scripts/tests/test_static_producer_closure.py -q
```

期待結果: 1.0.4 の line/status mapping と evidence token が 1.0.5 callsite に一致せず失敗する。新規 owner 候補は未修正のまま見えることを確認する。

### Step 4: line-only drift と分類を修正する

- `scripts/static_producer_closure.py` の tracked line mapping を現行 callsite へ移す。
- Game Summary、keybinding、Bilge、score retry、CodeCompressor は後続タスクで閉じる owner queue として残す。
- `AchievementWishes.DebugUnlockAnyAchievement` は debug-only、`Physics.CheckThresholds` は既存 vaporized owner coverage として根拠付きで除外する。
- `CyberneticsMedassistModule` は source expression 変更を report に残し、既存 emitted-shape test と target evidence が通る限り production code を変更しない。

### Step 5: reconciliation report と中間 Green を作る

report に version 移行理由、旧/新 totals、追加・削除・line-only family、残る owner queue、意図的除外、runtime risk を記録する。

```bash
uv run pytest scripts/tests/test_static_producer_closure.py -q
just static-producer-check
git diff --check
```

## Task 3: Game Summary の modern/classic 保存エラーを両方翻訳する

**変更ファイル:**

- `Mods/QudJP/Assemblies/QudJP.Tests/L2/GameSummaryTombstonePopupTranslationPatchTests.cs`
- `Mods/QudJP/Assemblies/src/Patches/GameSummaryTombstonePopupTranslationPatch.cs`
- `Mods/QudJP/Localization/Dictionaries/ui-game-summary.ja.json`
- `scripts/static_producer_closure.py`

### Step 1: 1.0.5 の二形を failing test にする

- modern: `There was an error AccessDenied saving: /tmp/Qudman.txt`
- classic: `There was an error saving: /tmp/Qudman.txt`
- modern は storage result と path の両 capture、classic は path captureを検証する。
- color markup、空/未知文、direct marker、owner absent、dictionary missing/format failure、nested owner を維持する。

### Step 2: Red を確認する

```bash
just test-l2
```

期待結果: modern 新形だけが英語のまま残る。

### Step 3: patch と辞書を更新する

- modern 用 regex と template key `There was an error {0} saving: {1}` を追加する。
- 日本語 template は storage result と path を変更せず戻す。
- classic の既存 `There was an error saving: {0}` は 1.0.5 現用経路として維持する。
- transform hit は実際に翻訳した場合だけ記録する。

### Step 4: Green と target 契約を確認する

```bash
just test-l2
just test-l2g
rg -n 'There was an error .* saving:' Mods/QudJP/Assemblies Mods/QudJP/Localization scripts/static_producer_closure.py
git diff --check
```

## Task 4: 1.0.5 keybinding default 復元確認へ更新する

**変更ファイル:**

- `Mods/QudJP/Localization/Dictionaries/ui-options.ja.json`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/KeyMappingUiTranslationPatchTests.cs`
- `scripts/static_producer_closure.py`

### Step 1: modern/classic の新しい固定 leaf を failing test にする

`Are you sure you want to override your bindings with the default?` が modern async と classic sync の双方で `キー設定を既定に上書きしますか？` になること、owner helper が固定 leaf を動的 family として誤 claim しないことを検証する。

### Step 2: Red を確認する

```bash
just test-l2
```

### Step 3: 辞書と evidence を更新する

- `ui-options.ja.json` の旧 `keymap` key を 1.0.5 の `bindings` key に置き換える。
- 1.0.4 key の互換 entry は残さない。
- closure の source/test token と line mapping を新文言へ合わせる。

### Step 4: Green を確認する

```bash
just test-l2
just localization-check
just translation-token-check
git diff --check
```

## Task 5: Bilge Sphincter mutation 獲得 popup を既存 cooking owner で閉じる

**変更ファイル:**

- `Mods/QudJP/Assemblies/QudJP.Tests/L2/CookingRuntimeTranslationPatchTests.cs`
- `Mods/QudJP/Assemblies/src/Patches/CookingRuntimeTranslationPatch.cs`
- `scripts/static_producer_closure.py`
- `scripts/tests/test_static_producer_closure.py`

### Step 1: exact runtime shape の failing test を追加する

`You gained the mutation {{w|Bilge Sphincter}}!` が `変異{{w|ビルジスフィンクター}}を得た！` になることを既存 popup owner harness で検証する。owner absent、direct marker、unknown mutation、markup preservation の既存契約も維持する。

### Step 2: Red を確認する

```bash
just test-l2
```

### Step 3: 既存 owner helper を最小拡張する

`CookingRuntimeTranslationPatch.TryTranslatePopupCore` に、`HiddenMutations.jp.xml` の mutation 表示名と一致する 1.0.5 固定文を追加する。汎用 popup 辞書には追加しない。

### Step 4: Green と closure evidence を確認する

```bash
just test-l2
uv run pytest scripts/tests/test_static_producer_closure.py -q
just test-l2g
git diff --check
```

## Task 6: score / leaderboard の固定状態と retry popup を閉じる

**変更ファイル:**

- 新規 `Mods/QudJP/Assemblies/src/Patches/SteamScoresRowTranslationPatch.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/DummyTargets/DummyScoresAndAchievementsTargets.cs`
- 新規 `Mods/QudJP/Assemblies/QudJP.Tests/L2/SteamScoresRowTranslationPatchTests.cs`
- 新規 `Mods/QudJP/Assemblies/QudJP.Tests/L2/ScoreAndLeaderboardPopupTranslationTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs`
- `Mods/QudJP/Localization/Dictionaries/ui-scores.ja.json`
- `scripts/static_producer_closure.py`
- `scripts/tests/test_static_producer_closure.py`

### Step 1: row と popup の failing tests を追加する

- `SteamScoresRow.setData` の message route で offline と generic error を翻訳する。
- `{{R|Error: \n{stacktrace}}}` は英語技術情報のまま維持する。
- Fetch retry、BuildScore post retry（upstream typo `leaderbaord` を含む）、legacy retry を exact fixed leaf として翻訳する。
- markup、unknown message、empty、dictionary missing、owner外/別 row type の no-op、transform hit を検証する。

### Step 2: Red を確認する

```bash
just test-l2
```

### Step 3: 安定した mid-pipeline owner と辞書を実装する

- `SteamScoresRow.setData(XRL.UI.Framework.FrameworkDataElement)` を target にする。
- Prefix で `HighScoresDataElement.message` の固定 leaf だけを `ui-scores.ja.json` から context-only lookup し、Finalizer で元 message を復元して model state を変更し続けない。
- nested/reentrant state は Harmony `__state` で呼び出し単位に保持し、例外を抑制しない。
- retry popup は fixed leaf policy に従って `ui-scores.ja.json` に置き、generic Popup producer の exact lookup を使う。stack trace を捕捉する broad pattern は追加しない。

### Step 4: L2G target と closure evidence を追加する

- `SteamScoresRow|setData|System.Void|XRL.UI.Framework.FrameworkDataElement` を固定する。
- HighScores `FetchLeaderboard`、`XRLCore.BuildScore`、legacy `Scores.Show` の fixed popup leaf を辞書 evidence として closure に登録する。

### Step 5: Green を確認する

```bash
just test-l2
just test-l2g
uv run pytest scripts/tests/test_static_producer_closure.py -q
just localization-check
just translation-token-check
git diff --check
```

## Task 7: CodeCompressor required-mod error を producer owner で閉じる

**変更ファイル:**

- 新規 `Mods/QudJP/Assemblies/src/Patches/CodeCompressorRequiredModPopupTranslationPatch.cs`
- `Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs`
- 新規 `Mods/QudJP/Assemblies/QudJP.Tests/DummyTargets/DummyCodeCompressorTarget.cs`
- 新規 `Mods/QudJP/Assemblies/QudJP.Tests/L2/CodeCompressorRequiredModPopupTranslationPatchTests.cs`
- `Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs`
- `Mods/QudJP/Localization/Dictionaries/ui-chargen.ja.json`
- `scripts/static_producer_closure.py`
- `scripts/tests/test_static_producer_closure.py`

### Step 1: owner route の failing tests を追加する

exact mod 名、markup を含む mod 名、空 mod 名、direct marker、未知 decode error、owner absent、dictionary missing/format failure、transform hit/no-hit を検証する。

### Step 2: Red を確認する

```bash
just test-l2
```

### Step 3: 狭い owner patch を実装する

- `XRL.CharacterBuilds.CodeCompressor.loadCode(string, List<AbstractEmbarkBuilderModule>, bool)` を exact target にする。
- owner scope 内だけ `^Error decoding build code - Required Mod "(?<mod>.*)" not found\.$` を処理する。
- mod 名を変更せず日本語 template へ戻し、翻訳済み文を direct marker で generic sink に渡す。
- `PopupShowSemanticPipeline` に owner helper を登録する。

### Step 4: L2G と closure evidence を追加する

target の完全シグネチャと、patch/pipeline/test/dictionary の証拠 token を固定する。

### Step 5: Green を確認する

```bash
just test-l2
just test-l2g
uv run pytest scripts/tests/test_static_producer_closure.py -q
just localization-check
just translation-token-check
git diff --check
```

## Task 8: Options 6 ID と help text を追加する

**変更ファイル:**

- 新規 `scripts/tests/test_options_localization_coverage.py`
- `Mods/QudJP/Localization/Options.jp.xml`

### Step 1: asset contract の failing test を追加する

1.0.5 base `Options.xml` を fixture ではなく明示した expected contract として扱い、次の 6 ID が jp overlay に一意に存在することを検証する。

- `OptionDisableAllIdleTileAnimations`
- `OptionDrawStepImmediately`
- `OptionEnableSeed`
- `OptionDisableZoneThawRestrict`（help text 必須）
- `OptionDrawFloodExplore`
- `OptionTextBuilderDebug`（help text 必須）

各 entry について、非翻訳属性（Requires、RequiresCapability、Type、Default）を 1.0.5 base と一致させ、DisplayText/Category/help text が空でなく、対象英語原文のままでないことを要求する。

### Step 2: Red を確認する

```bash
uv run pytest scripts/tests/test_options_localization_coverage.py -q
```

### Step 3: XML overlay を追加する

既存のカテゴリ位置へ 6 option を挿入し、help text の改行と技術用語を保持した日本語にする。XML declaration/UTF-8/LF と既存 attribute style を維持する。

### Step 4: Green と asset validation を確認する

```bash
uv run pytest scripts/tests/test_options_localization_coverage.py -q
xmllint --noout Mods/QudJP/Localization/Options.jp.xml
file Mods/QudJP/Localization/Options.jp.xml
just localization-check
just translation-token-check
git diff --check
```

## Task 9: 1.0.5 closure を完了し release note を追加する

**変更ファイル:**

- `scripts/static_producer_closure.py`
- `scripts/tests/test_static_producer_closure.py`
- `docs/localization-coverage-map.json`
- `docs/reports/2026-08-15-qud-1.0.5-static-producer-reconciliation.md`
- 新規 `docs/release-notes/unreleased/2026-08-15-qudjp-1.0.5-untranslated-closure.md`

### Step 1: 最終 queue の Red を確認する

```bash
uv run python scripts/static_producer_closure.py --format json --limit 0 > /tmp/qudjp-static-producer-final-closure.json
uv run pytest scripts/tests/test_static_producer_closure.py -q
```

### Step 2: 新 owner の source/test/dictionary evidence を登録する

- Game Summary、Bilge、scores、CodeCompressor の family を current line/status に対応付ける。
- keybinding fixed leaf は辞書 route として閉じる。
- coverage map の既知 limit/next action を 1.0.5 reconciliation 結果へ更新する。
- report に最終 queue と runtime-only risk を記録する。

### Step 3: release note を追加する

1.0.5 static inventory 更新、未訳 popup/score/options 修正を利用者向けに短く記載する。manifest/version/Workshop は変更しない。

### Step 4: closure Green を確認する

```bash
just static-producer-check
just static-producer-owner-queue 30
just localization-coverage-map-check
just release-note-check origin/main HEAD
git diff --check
```

## Task 10: 全体検証と completion audit

### Step 1: 新識別子・新リテラルの所有先を確認する

```bash
rg -n 'SteamScoresRowTranslationPatch|CodeCompressorRequiredModPopupTranslationPatch|Bilge Sphincter|override your bindings|Required Mod|There was an error .* saving:' Mods/QudJP scripts docs
```

意図した patch/helper/test/dictionary/evidence 以外への重複がないことを確認する。

### Step 2: generated artifact の再現性を確認する

```bash
just static-producer-preview ~/dev/coq-decompiled_stable /tmp/qudjp-static-producer-final.json
cmp docs/static-producer-inventory.json /tmp/qudjp-static-producer-final.json
```

### Step 3: 全ゲートを実行する

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
just static-producer-check
just localization-coverage-map-check
just release-note-check origin/main HEAD
git diff --check
```

### Step 4: worktree scope を監査する

```bash
git status --short
git diff --stat
git diff --name-only
```

main checkout のユーザー変更が含まれず、manifest/version bump、Workshop file、game binary が変更されていないことを確認する。自動ゲートで再現できない L3 error path は pending として明示し、完了扱いを誇張しない。
