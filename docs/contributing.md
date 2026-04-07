# コントリビューションガイド

QudJP へのコントリビューションを歓迎します。このドキュメントでは開発環境のセットアップから PR 提出までの手順を説明します。

---

## 前提条件

- **Caves of Qud** を Steam で所有していること（ゲーム DLL が必要）
- macOS または Linux（Windows は未検証）
- .NET SDK 10.0 以上
- Python 3.12 以上

> `Assembly-CSharp.dll` はリポジトリに含まれていません。ゲームを所有していない場合、C# のビルドとテストは実行できません。
>
> Apple Silicon で実ゲーム確認を行う場合、L3 の証跡は Rosetta 起動のみを有効とします。
> `scripts/launch_rosetta.sh` または `Launch CavesOfQud (Rosetta).command` を使ってください。

---

## 開発環境セットアップ

### 1. リポジトリをクローンする

```bash
git clone git@github.com:ToaruPen/coq-japanese_stable.git
cd coq-japanese_stable
```

### 2. ゲーム DLL を参照パスに配置する

`QudJP.csproj` はゲームの DLL をローカルパスで参照しています。ゲームをインストールした後、パスが正しいことを確認してください。

macOS (Steam) のデフォルトパス:

```
~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed/
```

パスが異なる場合は `Mods/QudJP/Assemblies/QudJP.csproj` の `<HintPath>` を修正してください。

### 3. ビルドを確認する

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

### 4. テストを実行する

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj

# 必要に応じて L2G のみ
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G
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
| `style` | コードの動作に影響しない変更（フォーマット等） |
| `refactor` | バグ修正でも機能追加でもないコード変更 |
| `test` | テストの追加・修正 |
| `chore` | ビルドプロセスや補助ツールの変更 |

**scope の種類**:

| scope | 対象 |
|-------|------|
| `patch` | C# Harmony パッチ |
| `xml` | XML 翻訳ファイル |
| `scripts` | Python スクリプト |
| `ci` | GitHub Actions |
| `deps` | 依存関係の更新 |

**例**:

```
feat(patch): add grammar postfix for verb conjugation
fix(xml): correct creature name encoding in Creatures.jp.xml
test(patch): add L2 test for GrammarPatch prefix
docs(scripts): add usage examples to README
```

---

## コード品質要件

### C#

- `<Nullable>enable</Nullable>` — null 安全を強制
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — 警告ゼロを強制
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` — スタイルを CI で強制

ビルドが通れば品質要件を満たしています。

**エラーハンドリング規約**（fail-fast）:

| 処理フェーズ | 方針 |
|------------|------|
| 初期化（`LoadTranslations`, `ApplyHarmonyPatches`） | 例外を投げる |
| `TargetMethod()` 解決失敗 | `Trace.TraceError` + `null` を返す |
| ランタイム翻訳失敗 | `Trace.TraceError` + 元のテキストを返す（例外は投げない） |

詳細は `Mods/QudJP/Assemblies/AGENTS.md` を参照してください。

### Python

- Ruff `select = ["ALL"]` — 全ルールを有効化
- McCabe 複雑度 `C ≤ 10`
- 全パブリック関数に型ヒントと Google スタイルの docstring が必要

```bash
ruff check scripts/
```

---

## テスト要件

新しいコードには必ずテストを追加してください。

- **C# 純粋ロジック** → L1 テスト（`[Category("L1")]`）
- **Harmony パッチ** → L2 / L2G テスト（`[Category("L2")]` と `[Category("L2G")]`。まず game-DLL-assisted な target/sig / hook inventory 検証、その後に必要なら DummyTarget）
- **Python スクリプト** → `scripts/tests/test_<script_name>.py`

テストアーキテクチャの詳細は [docs/test-architecture.md](test-architecture.md) を参照してください。

**禁止事項**:
- `Assembly-CSharp.dll` の型をテスト内で直接インスタンス化しない
- アサーションのないテストを書かない
- カバレッジ除外ディレクティブ（`# pragma: no cover` 等）を使わない

**推奨順序**:
1. 実ゲーム DLL 上で `TargetMethod()` / シグネチャ / static メソッドの自動検証を書く
2. その上で Prefix/Postfix の文字列変換を DummyTarget または副作用の軽い実メソッドで固定する
3. 必要なら L2G で hook inventory / namespace probe を追加する
4. 最後に L3 で表示確認を行う

inventory / equipment を含む L3 表示確認は、`docs/RULES.md` の runtime evidence ルールに従って fresh log と再現メモを残してください。

---

## CI パイプライン

PR を出すと GitHub Actions が自動で以下を実行します。

1. `dotnet build` — C# ビルド
2. `dotnet test` — C# テスト（L1 + L2 + L2G を含む）
3. `ruff check scripts/` — Python リント
4. `pytest scripts/tests/` — Python テスト

**全ジョブがパスしないとマージできません。**

CI の設定は `.github/workflows/ci.yml` を参照してください。

---

## 重要な制約

**`Assembly-CSharp.dll` を絶対にコミットしない**

このファイルはゲームの著作物です。`.gitignore` で除外されていますが、意図的に追加しないよう注意してください。

**ゲームバージョン 2.0.4 に合わせる**

Blueprint ID、Conversation ID、メソッドシグネチャはすべてゲームバージョン 2.0.4 に合わせてください。バージョンが異なると Harmony パッチが当たらなくなります。

**クリーンルーム実装**

レガシープロジェクトのコードをそのままコピーしないでください。実装は独自に行ってください。

---

## PR の出し方

1. 作業ブランチで変更をコミットする
2. `git push origin <branch-name>` でプッシュする
3. GitHub で PR を作成する
4. CI が全件パスすることを確認する
5. レビューを待つ

PR の説明には「何を変えたか」と「なぜ変えたか」を書いてください。スクリーンショットがあれば添付してください（ただし L3 ゲームスモークテストが完了するまでは任意）。
