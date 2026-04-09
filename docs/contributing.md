# コントリビューションガイド

QudJP へのコントリビューションを歓迎します。このドキュメントは開発環境のセットアップから PR 提出までの手順をまとめたものです。

貢献の種類は大きく 3 つです。自分が関わりたい領域のセクションを中心に読んでください。

- **ローカライゼーションの修正・追加** — JSON 辞書 / XML マージオーバーレイ / 用語集の編集 (翻訳者・プレイヤー向け)
- **C# Harmony パッチの追加・修正** — 新しい翻訳ルートや動的テキスト処理 (C# 開発者向け)
- **Python ツール・スクリプトの改善** — 検証・抽出・デプロイまわり (Python 開発者向け)

---

## 前提条件

- **Caves of Qud** を Steam で所有していること (C# ビルドおよび L2G テストで `Assembly-CSharp.dll` を参照するため)
- 対応 OS: macOS / Linux / Windows / WSL2
- .NET SDK `10.0.x` (`QudJP.Tests.csproj` が `net10.0` をターゲットするため。これを入れておけば `QudJP.csproj` の `net48` ビルドも同じ SDK で可能)
- Python `3.12` 以上
- (任意) `ast-grep` — Python スキャナーツールと CI で使用

> `Assembly-CSharp.dll` はリポジトリに含まれていません。ゲームを所有していない場合でも、リポジトリを clone してローカライゼーションファイル (JSON / XML) を編集することは可能です。ただし C# のビルドと L2G テスト実行にはゲームの所有が必要です。
>
> Apple Silicon で実ゲーム確認 (L3) を行う場合、L3 の証跡は Rosetta 起動のみを有効とします。`scripts/launch_rosetta.sh` または `Launch CavesOfQud (Rosetta).command` を使ってください。

---

## 開発環境セットアップ

### 1. リポジトリをクローンする

```bash
git clone git@github.com:ToaruPen/coq-japanese_stable.git
cd coq-japanese_stable
```

### 2. ゲーム DLL 参照パスを設定する

`Mods/QudJP/Assemblies/QudJP.csproj` はゲーム同梱 DLL (`0Harmony.dll` / Unity ランタイム) を参照します。OS ごとの Steam 既定パスを `-p:GameDir=...` で MSBuild に渡してください。csproj 内の `<HintPath>` を直接書き換える必要はありません。

`GameDir` は `Managed/` の **親ディレクトリ** を指します。ゲームが見つかれば `<Reference Include="0Harmony">` などの条件付き ItemGroup が有効化されます。DLL が見つからない場合は `Lib.Harmony` NuGet にフォールバックしますが、L2G / 実機テストはスキップされます。

**macOS (Steam)** — デフォルトのためオーバーライド不要:

```text
~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data
```

**Windows (Steam、PowerShell)**:

```powershell
dotnet build Mods/QudJP/Assemblies/QudJP.csproj `
  -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Caves of Qud\CoQ_Data"
```

**WSL2 (Windows 版ゲームを参照)**:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj \
  -p:GameDir="/mnt/c/Program Files (x86)/Steam/steamapps/common/Caves of Qud/CoQ_Data"
```

**Linux (Steam ネイティブ)**:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj \
  -p:GameDir="$HOME/.steam/steam/steamapps/common/Caves of Qud/CoQ_Data"
```

> **既知の課題**: `Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` の `<AssemblyCSharpPath>` は macOS パスをハードコードしており、MSBuild オーバーライドがまだ用意されていません。非 macOS 環境では `Exists()` チェックが false となり、L2G テストはエラーを出さずに自動的にスキップされます。非 macOS で L2G を走らせる場合、現状は csproj を一時的に書き換えるか、`~/Library/...` 配下にシンボリックリンクを張るなどの workaround が必要です。Issue として整理予定です。

### 3. ビルドを確認する

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

`Mods/QudJP/Assemblies/QudJP.dll` が生成されれば成功です。

### 4. テストを実行する

```bash
# L1 + L2 (ゲーム DLL 不要、常に実行可能)
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "TestCategory=L1|TestCategory=L2"

# L2G (ゲーム DLL 必須)
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter TestCategory=L2G

# Python スクリプトの lint とテスト
ruff check scripts/
pytest scripts/tests/
```

全テストがパスすれば環境構築完了です。

---

## Git ワークフロー

### ブランチ戦略

`main` ブランチから作業ブランチを切ります。

```bash
git checkout main
git pull
git checkout -b feat/grammar-verb-conjugation
```

### コミット規約

コミットメッセージは **英語** で書き、[Conventional Commits](https://www.conventionalcommits.org/) に従います。

```
type(scope): description
```

**type の種類**:

| type | 用途 |
|------|------|
| `feat` | 新機能 |
| `fix` | バグ修正 |
| `docs` | ドキュメントのみの変更 |
| `style` | コードの動作に影響しない変更 (フォーマット等) |
| `refactor` | バグ修正でも機能追加でもないコード変更 |
| `test` | テストの追加・修正 |
| `chore` | ビルドプロセスや補助ツールの変更 |

**scope の種類**:

| scope | 対象 |
|-------|------|
| `patch` | C# Harmony パッチ |
| `xml` | XML 翻訳ファイル |
| `json` | JSON 辞書 |
| `glossary` | `docs/glossary.csv` |
| `scripts` | Python スクリプト |
| `ci` | GitHub Actions |
| `deps` | 依存関係の更新 |

**例**:

```
feat(patch): add grammar postfix for verb conjugation
fix(xml): correct creature name encoding in Creatures.jp.xml
fix(json): restore {{B|...}} markup in ui-liquids dictionary
test(patch): add L2 test for GrammarPatch prefix
docs(scripts): add usage examples to README
```

---

## ローカライゼーション貢献ワークフロー

QudJP の翻訳資産は 3 つの場所に分かれています。どれを触るかは "対象テキストが安定リーフかプロシージャルか" で決まります (後述の「翻訳資産 vs. C# パッチの境界」参照)。

### 1. JSON 辞書 — `Mods/QudJP/Localization/Dictionaries/*.ja.json`

UI 文字列・アイテム名・能力名など、プロシージャルでない安定したテキストを key/text ペアで登録します。ファイル名は `ui-*.ja.json` / `world-*.ja.json` のようにサブシステム別に分かれています。

**ファイル構造 (例: `ui-liquids.ja.json`)**:

```json
{
  "meta": { "id": "ui-liquids", "lang": "ja", "version": 1 },
  "rules": { "protectColorTags": true, "protectHtmlEntities": true },
  "entries": [
    { "key": "{{B|water}}", "context": "liquid name", "text": "{{B|水}}" }
  ]
}
```

- `key` は原文をそのまま登録します。Qud カラーマークアップ (`{{B|...}}`)、`&G` フォアグラウンド、`^r` バックグラウンド、`=variable.name=` ランタイムプレースホルダーなどは **そのまま含めてください**。
- `text` はマークアップを保ったまま訳文を書きます。`{{B|water}}` → `{{B|水}}` のようにラッパーは維持します。
- `context` は曖昧性回避のためのヒントで、検索時にのみ使われます。
- `Dictionaries/Scoped/` サブディレクトリは限定スコープ用のオーバーライド辞書です。

### 2. XML マージオーバーレイ — `Mods/QudJP/Localization/**/*.jp.xml`

会話 (`Conversations.jp.xml`)、Object Blueprint (`ObjectBlueprints/*.jp.xml`)、書籍 (`Books.jp.xml`) などの大規模データを `Load="Merge"` でゲーム側データにマージします。

```xml
<conversations Load="Merge">
  <conversation ID="SomeConversation">
    <node ID="Start">
      <text>訳文</text>
    </node>
  </conversation>
</conversations>
```

- ID (`conversation ID`、`node ID`、`choice ID`、`object Name` など) は **ゲームバージョン 2.0.4** の値と完全一致する必要があります。
- `=variable.name=` のようなランタイム置換プレースホルダーは保持してください。
- `<text>` 要素の中身 (およびそれに類する表示テキスト要素) だけを日本語化します。

### 3. 用語集 — `docs/glossary.csv`

固有名詞や訳語の一貫性を保つための用語集です。

カラム: `English, Japanese, Short, Notes, Status`

- **新しい固有名詞の訳を入れる前に必ず grep** して、既に `confirmed` / `approved` 状態の訳があれば従ってください。
- `Short` は UI の幅制約下で使用する短縮形 (例: 「六日のスティルト」の Short が「スティルト」)。
- 新しい用語を追加する場合は `Status: draft` で開始し、レビューで `confirmed`、実装で `approved` に昇格します。
- `Notes` には出典 XML/JSON ファイル、変異表記、lore 的な裏付けを記録します。

### 翻訳資産 vs. C# パッチの境界

新しい翻訳を追加する前に「これは安定リーフ文字列か、それとも動的 / プロシージャル文字列か」を判断してください (詳細は [`RULES.md`](RULES.md) の "Choosing the fix type")。

**localization 資産で扱う**:

- 固定ラベル / アイテム名 / 能力名などの atomic な名前
- テストや実行時証跡から "非プロシージャル" と判明している文字列

**C# パッチ (translator / Harmony patch) で扱う**:

- 複数断片から組み立てられる文字列
- プレースホルダー・注入名・混在マークアップを含むもの
- 既に translator / patch テストでカバーされているルート

動的ルートに辞書エントリを足して "見た目を隠す" のは禁止です。ルートの owner を特定し、upstream に promote してください。

### ローカライゼーション変更の手動検証

ローカライゼーションファイルは CI で自動検証されません。編集後は手動で以下を実行してください:

```bash
# XML の well-formedness
xmllint --noout Mods/QudJP/Localization/Conversations.jp.xml

# BOM が無いことを確認 (先頭が EF BB BF なら要除去)
file   Mods/QudJP/Localization/Conversations.jp.xml
hexdump -C Mods/QudJP/Localization/Conversations.jp.xml | head -1

# JSON の構文チェック
python3 -m json.tool \
  Mods/QudJP/Localization/Dictionaries/ui-liquids.ja.json > /dev/null
```

ゲーム内で実際に反映されることを確認するため、Mod をゲームの Mods ディレクトリにデプロイして L3 (手動プレイ) で最終検証してください。

**macOS**: `python3.12 scripts/sync_mod.py` がそのまま使えます (デプロイ先が macOS Steam パスに固定されているため)。

**Windows / WSL2 / Linux**: `sync_mod.py` は現状 macOS 専用です。手動で rsync もしくはファイルコピーしてください。例 (WSL2):

```bash
rsync -av --delete \
  --include=manifest.json --include=Bootstrap.cs \
  --include=Assemblies/ --include=Assemblies/QudJP.dll \
  --include=Localization/ --include='Localization/**' \
  --include=Fonts/ --include='Fonts/**' \
  --exclude='*' \
  Mods/QudJP/ \
  "/mnt/c/Users/<name>/AppData/LocalLow/Freehold Games/CavesOfQud/Mods/QudJP/"
```

---

## コード品質要件

### `C#`

- `<Nullable>enable</Nullable>` — null 安全を強制
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — 警告ゼロを強制
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` — スタイルを CI で強制
- `SonarAnalyzer.CSharp` による追加の静的解析が有効

ビルドが通れば品質要件を満たしています。

**QudJP 独自アナライザー (`QudJP.Analyzers`)**:

| ID | 強制内容 |
|---|---|
| `QJ001` | `[HarmonyPatch]` クラス内の `Prefix` / `Postfix` メソッドは、本体全体が単一の `try { } catch (Exception) { }` 文でなければならない (型無し `catch` は不可)。`TargetMethod` / `TargetMethods` は除外。 |
| `QJ002` | メソッド呼び出しの戻り値に対する `??` null フォールバックの直前には `Trace.TraceWarning` / `Trace.TraceError` を置かなければならない (catch 内とテストコードは除外)。 |
| `QJ003` | `Trace.TraceError` / `Trace.TraceWarning` の第 1 引数文字列は `"QudJP:"` で始まらなければならない。 |

すべて `DiagnosticSeverity.Warning` で発行され、`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` によりビルドエラーになります。

**エラーハンドリング規約** (fail-fast):

| 処理フェーズ | 方針 |
|------------|------|
| 初期化 (`LoadTranslations`, `ApplyHarmonyPatches`) | 例外を投げる |
| `TargetMethod()` 解決失敗 | `Trace.TraceError` + `null` を返す |
| ランタイム翻訳失敗 | `Trace.TraceError` + 元のテキストを返す (例外は投げない) |

詳細は [`Mods/QudJP/Assemblies/AGENTS.md`](../Mods/QudJP/Assemblies/AGENTS.md) を参照してください。

### Python

- Python ベースライン: `3.12+`
- Ruff `select = ["ALL"]` — 全ルールを有効化 (`pyproject.toml` で `D100` / `D104` / `COM812` / `ISC001` のみ除外)
- McCabe 複雑度 `C ≤ 10`、pylint 限界: `max-branches=10` / `max-returns=6` / `max-args=6` / `max-statements=50`
- 全パブリック関数に型ヒントと Google スタイル docstring が必要
- テストファイルでは `S101` (assert) と `PLR2004` (magic value) が per-file で除外されている
- 新規スクリプトを作る前に、既存スクリプトの拡張で済まないか検討する

```bash
ruff check scripts/
ruff format scripts/
pytest scripts/tests/
```

---

## テスト要件

新しいコードには必ずテストを追加してください。

### テストレイヤー (L1 / L2 / L2G / L3)

| レイヤー | NUnit カテゴリ | 対象 | ゲーム DLL |
|---|---|---|---|
| **L1** | `[Category("L1")]` | 純粋な C# ロジック (translator、`ColorCodePreserver`、文字列変換ヘルパー) | 不要 |
| **L2** | `[Category("L2")]` | Harmony パッチの Prefix / Postfix 挙動を DummyTarget で検証 | 不要 |
| **L2G** | `[Category("L2G")]` | 実ゲーム DLL に対する `HarmonyTargetMethod` 解決・シグネチャ検証・hook inventory | **必須** |
| **L3** | (カテゴリなし・手動) | 実ゲームでの日本語表示確認、フォントフォールバック、`Player.log` 監査 | **必須** (Apple Silicon は Rosetta) |

詳細は [`test-architecture.md`](test-architecture.md) を参照してください。

### 対応付け

- **C# 純粋ロジック** → L1 テスト
- **新規 Harmony パッチ** → L2G でターゲット解決を固定 → L2 で DummyTarget 挙動を固定 → 実装 → L1/L2 をパス → L3 で表示確認
- **Python スクリプト** → `scripts/tests/test_<script_name>.py`
- **ローカライゼーション資産** → 自動テスト対象外。手動検証 + L3

### 禁止事項

- `Assembly-CSharp.dll` の型をテスト内で直接インスタンス化しない (L2 は DummyTarget を使う)
- アサーションのないテストを書かない
- カバレッジ除外ディレクティブ (`# pragma: no cover` 等) を使わない

### 推奨順序 (新規 Harmony パッチ)

1. L2G で実ゲーム DLL に対する `TargetMethod()` / シグネチャ / static メソッドの自動検証を書く
2. L2 で Prefix / Postfix の文字列変換を DummyTarget に対して固定する
3. 実装する
4. 必要なら L2G で hook inventory / namespace probe を追加する
5. 最後に L3 で表示確認する

L3 の表示確認は、[`RULES.md`](RULES.md) の runtime evidence ルールに従って fresh log と再現メモを残してください。

---

## ランタイム証跡の収集

L3 検証や "再現するか?" の判断は Caves of Qud のランタイムログを第一の証跡として使います。QudJP の診断マーカーは `Player.log` に出力されます:

- `[QudJP] Build marker`
- `DynamicTextProbe/v1`
- `SinkObserve/v1`
- `missing key`
- `MODWARN`

### ログファイルの場所

| OS | `Player.log` のパス |
|---|---|
| macOS | `~/Library/Logs/Freehold Games/CavesOfQud/Player.log` |
| Windows | `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log` |
| Linux (Steam) | `~/.config/unity3d/Freehold Games/CavesOfQud/Player.log` |
| WSL2 | Windows 側パスを `/mnt/c/Users/<name>/AppData/LocalLow/Freehold Games/CavesOfQud/Player.log` で参照 |

> macOS 以外のパスは [`RULES.md`](RULES.md) ではまだ正式化されていません。WSL2 での実働確認から上記を採用していますが、誤りがあれば PR で訂正してください。

### ログの使い方

- ログは "ある時点での現象の証跡" として扱い、挙動の定義としては扱わない
- Apple Silicon ではネイティブ ARM64 起動のログを L3 証跡として使わない (Rosetta のみ)
- 不具合レポートには Player.log の該当行と OS / ゲームバージョン / QudJP バージョンを必ず添えてください

---

## CI パイプライン

PR を出すと GitHub Actions (`.github/workflows/ci.yml`) が自動で以下を実行します:

1. `.NET SDK 8.0.x + 10.0.x` をインストール
2. `dotnet build Mods/QudJP/Assemblies/QudJP.csproj --configuration Release`
3. `dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --configuration Release`
4. `dotnet test ... --no-build` — テストカテゴリのフィルタは付けず全件実行。L2G は `QudJP.Tests.csproj` の `<Exists('$(AssemblyCSharpPath)')>` 条件付き参照により、CI 環境に `Assembly-CSharp.dll` が存在しないため自動的にスキップされます。
5. `ruff check scripts/`
6. `pytest scripts/tests/`

**全ジョブがパスしないとマージできません。**

---

## 重要な制約

**`Assembly-CSharp.dll` を絶対にコミットしない**

このファイルはゲームの著作物です。`.gitignore` で除外されていますが、意図的に追加しないよう注意してください。同様に、`Mods/QudJP/Assemblies/QudJP.dll` のビルド成果物もコミット対象ではありません。

**ゲームバージョン 2.0.4 に合わせる**

Blueprint ID、Conversation ID、メソッドシグネチャはすべてゲームバージョン 2.0.4 に合わせてください。バージョンが異なると Harmony パッチが当たらなくなります。

**クリーンルーム実装**

レガシープロジェクトのコードをそのままコピーしないでください。実装は独自に行ってください。

**フォント再配布**

`Mods/QudJP/Fonts/NotoSansCJKjp-Regular-Subset.otf` は SIL OFL 1.1 で配布されています。`Mods/QudJP/Fonts/OFL.txt` を一緒に保持してください (詳細は [`NOTICE.md`](../NOTICE.md))。

---

## PR の出し方

1. 作業ブランチで変更をコミットする
2. `git push origin <branch-name>` でプッシュする
3. GitHub で PR を作成する
4. CI が全件パスすることを確認する
5. レビューを待つ

### PR 説明に含めるもの

- **何を変えたか** — 変更の概要
- **なぜ変えたか** — 背景・再現手順・ゲーム内での確認事項
- **どうテストしたか** — L1 / L2 / L2G の実行結果、または L3 のスクリーンショット
- **影響範囲** — ルート ownership が変わった場合は明示 ([`RULES.md`](RULES.md) の route ownership 参照)

### レビュー

- CodeRabbit による自動レビューが走ります
- 人手レビューはメンテナーが確認します
- 翻訳品質については `docs/glossary.csv` との整合性を確認します

### 不具合報告

Issue を開く際は以下を含めてください:

- **OS とバージョン** (例: Windows 11 / Caves of Qud v2.0.4 / QudJP v0.1.0)
- **再現手順** — 最小ステップ
- **期待される挙動 / 実際の挙動**
- **`Player.log` の該当行** (上述の OS 別パスから抜粋)
- **スクリーンショット** (UI 表示バグの場合)
