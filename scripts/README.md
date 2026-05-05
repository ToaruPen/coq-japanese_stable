# scripts/ — Python ツールガイド

QudJP の翻訳ワークフローを支援する Python スクリプト群です。

**動作要件**: Python 3.12 以上

---

## 現役スクリプト一覧

| スクリプト | 用途 |
|-----------|------|
| `check_encoding.py` | エンコーディング検証 |
| `validate_xml.py` | XML 構造検証 |
| `diff_localization.py` | 翻訳カバレッジ比較 |
| `extract_base.py` | ゲーム XML の抽出 |
| `sync_mod.py` | Mod ファイルの配備 |
| `build_workshop_upload.py` | Steam Workshop 用 staging / steamcmd VDF 生成 |
| `translation_checker.py` | Rosetta 起動で既知セーブを開き、翻訳確認用スクリーンショットを取得 |

---

## legacy / bridge-only scripts

| スクリプト | 用途 |
|-----------|------|
| `scripts.triage.cli` | bridge/triage workflow の directory-based triage classification 入口 |
| `legacies/scan_text_producers.py` | Archived Roslyn static SoT pilot, bridge/view-only candidate inventory, fixed-leaf validation |
| `legacies/reconcile_inventory_status.py` | legacy candidate inventory を現在の翻訳資産に照らして再評価する bridge CLI |

`scripts/legacies/` は、現役の翻訳ワークフローから切り離した legacy bridge/view-only tooling を置く場所です。issue-357 の scanner 群はここに隔離し、通常の active CLI 群とは分けて扱います。

## scripts.triage.cli

`scripts.triage.cli` は bridge/triage workflow 向けの package entrypoint です。`Player.log` などの証跡ディレクトリを再帰的に走査して分類し、JSON レポートを書き出します。これは active translation tooling ではなく、bridge 側の directory-based triage classification に使います。

**使い方**:

```bash
python -m scripts.triage.cli classify --input-dir /path/to/evidence --output triage-report.json
```

`--input-dir` が存在しない場合は空の report を返すので、証跡なしの場面でも形式確認に使えます。

---

## legacies/scan_text_producers.py

Roslyn static SoT pilot の入口です。decompiled source から候補を集める legacy bridge/view-only CLI です。過去の reconciled bridge snapshot は `docs/archive/candidate-inventory-2026-04-14-reconciled-bridge.json` に archived されており、source of truth ではありません。旧 fixed-leaf 手順は `docs/archive/fixed-leaf-workflow-legacy.md` に archived されています。

この CLI は `~/dev/coq-decompiled_stable/` を既定の source root として読みます。ここにある decompiled inputs は外部の read-only 入力で、commit 対象ではありません。`--validate-fixed-leaf` は同じ CLI の中にある検証フラグで、別の top-level validator はありません。

**使い方**:

```bash
python scripts/legacies/scan_text_producers.py \
  --source-root ~/dev/coq-decompiled_stable \
  --cache-dir .scanner-cache \
  --output docs/archive/candidate-inventory-2026-04-14-reconciled-bridge.json \
  --phase all \
  --validate-fixed-leaf
```

**issue-357 first PR verification**:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Analyzers.Tests/QudJP.Analyzers.Tests.csproj --filter StaticSotPilot
uv run pytest scripts/tests/test_scan_text_producers.py
uv run pytest scripts/tests/test_scanner_inventory.py
uv run pytest scripts/tests/test_scanner_ast_grep_runner.py
uv run pytest scripts/tests/test_scanner_rule_classifier.py
uv run pytest scripts/tests/test_scanner_cross_reference.py
uv run pytest scripts/tests/test_reconcile_inventory_status.py
```

**引数**:

- `--source-root`: decompiled C# source root。既定は `~/dev/coq-decompiled_stable`
- `--cache-dir`: Phase 1a / 1b の中間出力先。既定は `.scanner-cache`
- `--output`: legacy bridge/view-only candidate inventory JSON の出力先。既定は historical path の `docs/candidate-inventory.json` だが、現行作業では archive path を明示する
- `--phase`: `1a`, `1b`, `1c`, `1d`, `all` のいずれか
- `--diff`: 将来の freshness diff 用。現在は no-op
- `--validate-fixed-leaf`: Phase 1d output の fixed-leaf candidate を検証する

**出力**:

- Phase 1a / 1b / 1d の要約を stdout に表示
- `--validate-fixed-leaf` を付けた場合は fixed-leaf validation report を stdout に表示

**補足**:

- `--phase 1d` は、すでに cache があるときの再レビュー向けで、legacy bridge/view output を更新します
- `--phase all` は、スキャンから検証までを通して回す happy path です
- 受理した fixed-leaf candidate は段階的に昇格させるが、これによって現在の `Translator` のランタイム意味論は変わらない。
- 実証済み workflow は prune-first です。最初の safe batch は 27-row の pending queue が `""`, `" "`, `BodyText`, `SelectedModLabel` だけだったため、promotion 0 件で正解でした。
- fixed-leaf validation の current addition set には `translated` / `excluded` rows を含めません。既に narrow home がある duplicate family は new import ではなく existing coverage として扱います。
- `AddPlayerMessage` は sink-observed umbrella のままで、fixed-leaf owner や fallback destination にはしません。

## check_encoding.py

ローカライゼーションファイルの UTF-8 エンコーディングを検証します。

**検出する問題**:
- UTF-8 BOM（`\xef\xbb\xbf`）
- Windows 改行コード（CRLF）
- モジバケパターン（`繧` `縺` `蜒` `驕ｿ`）
- 無効な UTF-8 バイト列

**使い方**:

```bash
# Localization ディレクトリ全体を検証
python scripts/check_encoding.py Mods/QudJP/Localization/

# Localization と scripts をまとめて検証
python scripts/check_encoding.py Mods/QudJP/Localization/ scripts/
```

**出力例**:

```
Scanned 78 files: 78 OK, 0 issue(s)
```

問題があった場合:

```
Scanned 78 files: 77 OK, 1 issue(s)
  [BOM] Mods/QudJP/Localization/Creatures.jp.xml — UTF-8 BOM detected
```

**終了コード**: 0 = 問題なし、1 = 問題あり

---

## validate_xml.py

翻訳 XML ファイルの構造と内容を検証します。

**検出する問題**:
- XML パースエラー（致命的エラー）
- 無効な UTF-8（致命的エラー）
- 色コード `{{...}}` の対応関係の不整合（警告）
- 兄弟要素の ID/Name 重複（警告）
- 空の `<text>` 要素（警告）

**使い方**:

```bash
# ディレクトリ全体を検証
python scripts/validate_xml.py Mods/QudJP/Localization/

# 特定のファイルを検証
python scripts/validate_xml.py Mods/QudJP/Localization/ObjectBlueprints/Creatures.jp.xml

# 既知警告を baseline で許容し、新規警告だけエラー扱いにする（CI 向け）
python scripts/validate_xml.py Mods/QudJP/Localization/ --strict --warning-baseline scripts/validate_xml_warning_baseline.json

# 現在の警告 baseline を再生成
python scripts/validate_xml.py Mods/QudJP/Localization/ --write-warning-baseline scripts/validate_xml_warning_baseline.json
```

**出力例**:

```
Checking Mods/QudJP/Localization/Creatures.jp.xml... OK
Checking Mods/QudJP/Localization/Conversations.jp.xml... 1 warning
  WARNING: Unbalanced color code at line 42
```

**終了コード**: 0 = エラーなし（`--strict` 時は警告もなし）、1 = エラーあり

---

## diff_localization.py

ベースゲームの XML と日本語翻訳 XML を比較して、翻訳カバレッジを算出します。

**前提**: ゲームがインストールされていること（`extract_base.py` でローカルに取り出すことも可能）

**使い方**:

```bash
# カテゴリ別サマリーを表示
python scripts/diff_localization.py --summary

# 未翻訳エントリのみを表示
python scripts/diff_localization.py --missing-only

# JSON 形式で出力
python scripts/diff_localization.py --json

# ゲームパスを明示的に指定
python scripts/diff_localization.py --game-base /path/to/StreamingAssets/Base --summary
```

**出力例（`--summary`）**:

```
Category              Total  Translated  Coverage
ObjectBlueprints       5220        4020    77.01%
Conversations           200         195    97.50%
Skills                  150         150   100.00%
```

**終了コード**: 0 = 正常終了、1 = エラー

---

## extract_base.py

ゲームの `StreamingAssets/Base/` XML をリポジトリの `references/Base/` にコピーします。翻訳作業の参照用です。

**前提**: Caves of Qud が macOS の Steam デフォルトパスにインストールされていること

**使い方**:

```bash
# デフォルトパスからコピー
python scripts/extract_base.py

# ゲームパスを明示的に指定
python scripts/extract_base.py --game-base /path/to/StreamingAssets/Base
```

**出力先**: `references/Base/`（`.gitignore` で除外済み）

**終了コード**: 0 = 正常終了、1 = エラー

---

## sync_mod.py

`Mods/QudJP/` の内容をゲームの Mods ディレクトリに同期します。L3 ゲームスモークテストの前に実行します。

**対応環境**: macOS / Windows / WSL2 / Linux

**使い方**:

```bash
# デフォルトパスに同期
python scripts/sync_mod.py

# 同期先を明示的に指定
python scripts/sync_mod.py --dest /path/to/Mods/QudJP

# 非標準インストール先にデプロイ
python scripts/sync_mod.py --destination /path/to/Mods/QudJP

# ドライラン（実際にはコピーしない）
python scripts/sync_mod.py --dry-run
```

`rsync` が利用可能な環境では rsync を使い、無い環境では Python コピー実装に自動フォールバックします。

**終了コード**: 0 = 正常終了、1 = エラー

---

## build_workshop_upload.py

`scripts/build_workshop_upload.py` は `scripts/build_release.py` が作成した
`dist/QudJP-v*.zip` から Steam Workshop 用の staging directory と
`steamcmd` 用 VDF を生成します。Workshop item ID や title などの公開
metadata は `steam/workshop_metadata.json`、Workshop description は
`steam/workshop_description.ja.txt` を source of truth にします。Workshop
changenote は `scripts/release_notes.py render` で
`docs/release-notes/unreleased/*.md` から下書きを生成し、必要に応じて
`git log --oneline <previous-tag>..HEAD` と
`git rev-parse --short=12 HEAD` で補足します。

**使い方**:

```bash
# 最新の dist/QudJP-v*.zip から dist/workshop/ を生成
python3.12 scripts/build_workshop_upload.py \
  --changenote-file /tmp/qudjp-workshop-changenote.txt

# 特定の release ZIP を指定
python3.12 scripts/build_workshop_upload.py \
  --release-zip dist/QudJP-v0.2.0.zip \
  --changenote-file /tmp/qudjp-workshop-changenote.txt
```

**出力**:

- `dist/workshop/QudJP/`: Steam Workshop にアップロードする content folder
- `dist/workshop/workshop_item.vdf`: `steamcmd workshop_build_item` に渡す VDF

**アップロード例**:

```bash
steamcmd +login "$STEAM_USER" +workshop_build_item dist/workshop/workshop_item.vdf +quit
```

Steam credentials、2FA material、login script は repo に置かないでください。

**終了コード**: 0 = 正常終了、1 = エラー

---

## release_notes.py

`scripts/release_notes.py` は release-note fragment を検証し、release 時の
`CHANGELOG.md` 用 entry と Steam Workshop changenote の下書きを生成します。
`Mods/QudJP/Localization/` を変更する PR では
`docs/release-notes/unreleased/*.md` の fragment が必要です。

**fragment 例**:

```markdown
### Changed

- Improve Japanese text in the trade and conversation UI.
```

**使い方**:

```bash
# Localization 差分に release-note fragment が含まれるか確認
python3.12 scripts/release_notes.py check-fragment \
  --base-ref origin/main \
  --head-ref HEAD

# CHANGELOG / Workshop changenote の下書きを生成
python3.12 scripts/release_notes.py render \
  --version 0.1.0 \
  --git-hash "$(git rev-parse --short=12 HEAD)" \
  --date YYYY-MM-DD \
  --changelog-output /tmp/qudjp-changelog-entry.md \
  --workshop-output /tmp/qudjp-workshop-changenote.txt
```

**出力**:

- `/tmp/qudjp-changelog-entry.md`: `CHANGELOG.md` にコピーする release entry
- `/tmp/qudjp-workshop-changenote.txt`: `build_workshop_upload.py --changenote-file` に渡す changenote

**終了コード**: 0 = 正常終了、1 = エラー

---

## translation_checker.py

Rosetta でゲームを起動し、既知セーブを読み込んでインベントリを開き、
翻訳確認用スクリーンショットを取得します。`Player.log` の `[QudJP]` probe を待つため、
手動観測の再現を自動化するための L3 補助スクリプトとして使えます。

既定フローは、タイトル画面で `Continue` に 1 つ下移動してから `space` を 2 回送り、
`LOAD GAME` 画面の先頭セーブを読み込む前提です。

**前提**:
- macOS
- 実行時に Mac がロック解除済みで、通常の `screencapture` が黒画面にならないこと
- Accessibility 権限が `Terminal.app` など実行元に付与されていること
- Screen Recording 権限が実行元に付与されていること
- 検証用セーブが `Continue` -> `LOAD GAME` の先頭項目から開けること
- Hammerspoon が入っている場合、前面化補助として自動利用される（`~/.hammerspoon/init.lua` は変更しない）

**使い方**:

```bash
# 同期込みで実行（スクショは /tmp 配下に保存）
python scripts/translation_checker.py

# 既に配備済みなら同期をスキップ
python scripts/translation_checker.py --skip-sync

# スクショ保存先や待機時間を調整
python scripts/translation_checker.py \
  --screenshot-path /tmp/coq-inventory.png \
  --load-wait 20 \
  --inventory-timeout 20

# 実運用向け: スクショを残す
python scripts/translation_checker.py \
  --skip-sync \
  --screenshot-path artifacts/translation_checker/verified-inventory.png

# 最終 L3 smoke: セーブ読込、インベントリ起点の各タブ/項目、アビリティ、
# active effects、ポップアップ、NPC会話、攻撃/ログを連続確認
python scripts/translation_checker.py \
  --skip-sync \
  --flow final-smoke \
  --flow-screenshot-dir /tmp/qudjp-l3-final-smoke

# 死亡確認を明示的に調整する場合（既定は最大30回、検証セーブは自動バックアップ後に復元）
# 低HPなどの戦闘警告は既定で space を送って閉じます。
python scripts/translation_checker.py \
  --skip-sync \
  --flow final-smoke \
  --flow-screenshot-dir /tmp/qudjp-l3-final-smoke-death \
  --input-backend osascript \
  --death-attack-count 30

# Computer Use でロード操作を行う場合:
# 1. このコマンドで Rosetta 起動し、スクリプトはロード完了 probe を待つ
# 2. Computer Use で LOAD GAME を開き、テストセーブを選択する
# 3. ロード完了後、スクリプトが final-smoke のスクショ収集を再開する
# 4. macOS/Unity が Control 修飾を落とす場合に備え、入力は osascript 経由にする
python scripts/translation_checker.py \
  --skip-sync \
  --flow final-smoke \
  --manual-load \
  --input-backend osascript \
  --load-ready-timeout 300 \
  --flow-screenshot-dir /tmp/qudjp-l3-final-smoke-computer-use
```

**出力**:
- JSON 形式で `screenshot_path`、`player_log_path`、`load_ready_matches`、一致した inventory probe、ログ抜粋を標準出力に表示
- スクリーンショット自体の破棄は、この JSON を読んだ呼び出し側が行う想定

`--flow final-smoke` では `screenshot_dir`、`screenshot_paths`、`verification_report_path` を出力します。
`verification_report.json` には各スクリーンショット stage に対応する Player.log 範囲、翻訳済み runtime evidence、
missing key、message pattern gap、SinkObserve の未翻訳候補、markup/color tag 差分候補、preserved-English 分類が含まれます。
各 flow screenshot は、画面を開いた後に `--flow-screenshot-wait` 秒だけ待ってから撮影します。既定は 3 秒です。
標準ステージは次の通りです。

- セーブ読込直後
- インベントリ画面起点: 初期タブ、表示オプション、先頭アイテムの選択/アクションメニュー、複数アイテム行、Page Right で到達するタブ群
- アビリティ画面、active effects 画面
- システムメニュー、注目地点、Look 方向選択、ヘルプ、射撃武器なしポップアップ
- NPC 会話
- Classic 攻撃または攻撃確認ポップアップ
- 攻撃後のメッセージログ
- 死亡/戦闘終了画面
- 死亡/戦闘終了後のメッセージログ

インベントリ起点の走査量は `--inventory-tab-page-rights`、`--inventory-item-scan-count`、
`--inventory-item-action-row-offset`、`--inventory-item-pane-chord` で調整できます。既定では、
インベントリ一覧ペインへ移動して先頭アイテムのアクションメニューを開き、8 件分のアイテム行と
Page Right 8 回分のタブ遷移を撮影します。

アビリティ画面と active effects 画面を開くキーは `--abilities-chord` と
`--active-effects-chord` で調整できます。active effects は既定で `x,e` とし、
キャラクター画面を開いてからキャラクターシート内の「状態効果を表示」を選びます。

NPC 会話の移動先や方向はテストセーブに依存します。既定値は実測したテストセーブ向けに
`--npc-poi-key d`、`--npc-talk-direction right`、攻撃は `--attack-chord backslash,right` です。
合わない場合はこれらの引数を変えて同じ検証フローを再利用します。
戦闘は既定で初回攻撃後に最大30回まで同じ攻撃入力を繰り返し、死亡または戦闘終了画面と
その後のメッセージログを撮影します。低HPなどの戦闘警告は `--death-confirm-key space` で閉じます。
死亡確認を省略する場合は `--death-attack-count 0`、警告確認キーを送らない場合は
`--death-confirm-key ""` を使います。

`--manual-load` は Computer Use 用の導線です。スクリプトは必ず `scripts/launch_rosetta.sh`
経由で起動し、タイトル/ロード画面への入力は送らず、ロード完了 probe だけを待ちます。
ロードが確認できない場合、`final-smoke` は後続スクリーンショットを作らず終了します。

Computer Use で `Ctrl+Right` や `Ctrl+KP_6` が通常移動として扱われる場合は、
`--input-backend osascript` を指定します。この経路は macOS の Accessibility 権限で
`osascript` / 実行元アプリから `System Events` にキー送信できることが前提です。実測では
`Ctrl+Right` と `Ctrl+numpad6` は自動実行では戦闘証拠にならないことがあり、ゲーム側の
`Force attack in a direction` に対応する `backslash,right` が戦闘証拠として安定しています。

**終了コード**: 0 = 実行完了、1 = 起動/権限/ログ待機エラー

---

## テスト

```bash
# 全テストを実行
uv run pytest scripts/tests/

# 詳細出力
uv run pytest scripts/tests/ -v

# 特定のスクリプトのテストだけ実行
uv run pytest scripts/tests/test_check_encoding.py
uv run pytest scripts/tests/test_validate_xml.py
uv run pytest scripts/tests/test_diff_localization.py
uv run pytest scripts/tests/test_translation_checker.py
```

現在のテスト数: **325 件**

---

## リント

```bash
ruff check scripts/
```

Ruff は `select = ["ALL"]` で全ルールを有効にしています。インライン抑制が必要な場合は `# noqa: RULE -- 理由` の形式で書いてください。

---

## 典型的なワークフロー

翻訳ファイルを追加・編集した後の検証手順:

```bash
# 1. エンコーディング確認
python scripts/check_encoding.py Mods/QudJP/Localization/ scripts/

# 2. XML 構造確認
python scripts/validate_xml.py Mods/QudJP/Localization/ --strict --warning-baseline scripts/validate_xml_warning_baseline.json

# 3. カバレッジ確認
python scripts/diff_localization.py --summary

# 4. テスト実行
uv run pytest scripts/tests/
```
