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
| `verify_inventory.py` | Rosetta 起動で既知セーブを開き、インベントリのスクリーンショットを取得 |

---

## legacy / bridge-only scripts

| スクリプト | 用途 |
|-----------|------|
| `legacies/scan_text_producers.py` | Roslyn static SoT pilot, bridge/view-only candidate inventory for current static consumers, fixed-leaf validation |
| `legacies/reconcile_inventory_status.py` | legacy candidate inventory を現在の翻訳資産に照らして再評価する bridge CLI |

`scripts/legacies/` は、現役の翻訳ワークフローから切り離した legacy bridge/view-only tooling を置く場所です。issue-357 の scanner 群はここに隔離し、通常の active CLI 群とは分けて扱います。

---

## legacies/scan_text_producers.py

Roslyn static SoT pilot の入口です。decompiled source から候補を集めて `docs/candidate-inventory.json` を作ります。`docs/candidate-inventory.json` は現在の static consumer がまだ読む legacy bridge/view-only artifact であり、source of truth ではありません。`docs/fixed-leaf-workflow.md` に、レビューから昇格までの手順をまとめています。

この CLI は `~/dev/coq-decompiled_stable/` を既定の source root として読みます。ここにある decompiled inputs は外部の read-only 入力で、commit 対象ではありません。`--validate-fixed-leaf` は同じ CLI の中にある検証フラグで、別の top-level validator はありません。

**使い方**:

```bash
python scripts/legacies/scan_text_producers.py \
  --source-root ~/dev/coq-decompiled_stable \
  --cache-dir .scanner-cache \
  --output docs/candidate-inventory.json \
  --phase all \
  --validate-fixed-leaf
```

**issue-357 first PR verification**:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Analyzers.Tests/QudJP.Analyzers.Tests.csproj --filter StaticSotPilot
pytest scripts/tests/test_scan_text_producers.py
pytest scripts/tests/test_scanner_inventory.py
pytest scripts/tests/test_scanner_ast_grep_runner.py
pytest scripts/tests/test_scanner_rule_classifier.py
pytest scripts/tests/test_scanner_cross_reference.py
pytest scripts/tests/test_reconcile_inventory_status.py
```

**引数**:

- `--source-root`: decompiled C# source root。既定は `~/dev/coq-decompiled_stable`
- `--cache-dir`: Phase 1a / 1b の中間出力先。既定は `.scanner-cache`
- `--output`: 現在の static consumer 向け bridge/view-only candidate inventory JSON の出力先。既定は `docs/candidate-inventory.json`
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

## check_encoding.py

ローカライゼーションファイルの UTF-8 エンコーディングを検証します。

**検出する問題**:
- UTF-8 BOM（`\xef\xbb\xbf`）
- Windows 改行コード（CRLF）
- モジバケ文字（`繧` `縺` `驕` `蜒`）
- 無効な UTF-8 バイト列

**使い方**:

```bash
# Localization ディレクトリ全体を検証
python scripts/check_encoding.py Mods/QudJP/Localization/

# scripts/ ディレクトリを検証
python scripts/check_encoding.py scripts/
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

# 警告もエラー扱いにする（CI 向け）
python scripts/validate_xml.py Mods/QudJP/Localization/ --strict
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

## verify_inventory.py

Rosetta でゲームを起動し、既知セーブを読み込んでインベントリを開き、
スクリーンショットを取得します。`Player.log` の `[QudJP]` probe を待つため、
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
python scripts/verify_inventory.py

# 既に配備済みなら同期をスキップ
python scripts/verify_inventory.py --skip-sync

# スクショ保存先や待機時間を調整
python scripts/verify_inventory.py \
  --screenshot-path /tmp/coq-inventory.png \
  --load-wait 20 \
  --inventory-timeout 20

# 実運用向け: スクショを残す
python scripts/verify_inventory.py \
  --skip-sync \
  --screenshot-path artifacts/verify_inventory/verified-inventory.png
```

**出力**:
- JSON 形式で `screenshot_path`、`player_log_path`、`load_ready_matches`、一致した inventory probe、ログ抜粋を標準出力に表示
- スクリーンショット自体の破棄は、この JSON を読んだ呼び出し側が行う想定

**終了コード**: 0 = 実行完了、1 = 起動/権限/ログ待機エラー

---

## テスト

```bash
# 全テストを実行
pytest scripts/tests/

# 詳細出力
pytest scripts/tests/ -v

# 特定のスクリプトのテストだけ実行
pytest scripts/tests/test_check_encoding.py
pytest scripts/tests/test_validate_xml.py
pytest scripts/tests/test_diff_localization.py
pytest scripts/tests/test_verify_inventory.py
```

現在のテスト数: **53 件**

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
python scripts/check_encoding.py Mods/QudJP/Localization/

# 2. XML 構造確認
python scripts/validate_xml.py Mods/QudJP/Localization/

# 3. カバレッジ確認
python scripts/diff_localization.py --summary

# 4. テスト実行
pytest scripts/tests/
```
