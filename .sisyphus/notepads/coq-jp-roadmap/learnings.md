# Learnings

## 2026-03-10 Session Start
- GitHub repo created: ToaruPen/Caves-of-Qud_Japanese (public)
- Default branch: main
- Protocol: SSH
- Initial commit: f26f8d7

## 2026-03-11 Task 0 PoC
- macOS + dotnet SDK 10.0.100 では `net10.0` の `dotnet test` が安定して実行可能。
- `Assembly-CSharp.dll` 参照で `ConsoleLib.Console.Markup` のメソッド署名取得が可能。
- ゲーム同梱 `0Harmony.dll` は `Version=2.2.2.0` を確認。
- Unity依存型のサンプル実体化では `TypeInitializationException` を再現できず（観測値は `False`）。

## 2026-03-11 Task 3 Legacy XML監査
- レガシー `Localization/` 配下は XML 35ファイル（66,306行）・辞書JSON 35ファイル（32,836行）・再帰全76ファイルを確認。
- XML 35/35 が `xml.etree.ElementTree` でパース成功、モジバケ指標文字（繧/縺/驕/蜒）は0件。
- XMLのBOM付きは15ファイル、辞書JSONのBOM付きは2ファイル（`mutation-ranktext.ja.json`, `world-parts.ja.json`）。
- カバレッジ比較（現行Base 2.0.4）で `ObjectBlueprints` 77.01%（4020/5220）、`Conversations` 97.5%（195/200）、他カテゴリは100%一致を確認。
- `glossary.csv` は列 `English, Japanese, Short, Notes, Status` で83エントリ（ヘッダ除く）、移行利用可能。

## 2026-03-11 Task 2 Game Data Analysis
- StreamingAssets/Base の XML は 44 ファイル (総計約 103,408 行) を確認。
- 大分類: ObjectBlueprints 15 / Conversations 3 / UI-Options 4 / Skills-Mutations 4 / Other 18。
- 翻訳文字列の近似は Static 約9,427、Semi-dynamic 約874、Dynamic は実行時生成で固定件数化不可。
- 色コード頻度は `{[A-Z]|` 826、`&amp;[A-Za-z]` 2,356、`^[A-Za-z]` 419。`&amp;&amp;` は13件、`^^` はBase XMLで0件。
- 会話 Load 実装は `merge(0)/replace(1)/add(2)/remove(3)` を確認。`MergeIfExists` は Base XML と ilspy-raw で未観測。
- 出力ドキュメント: `docs/game-data-analysis.md` を新規作成。

## AGENTS.md scaffolding (2026-03-11)

- Root AGENTS.md: 133 lines (well under 200 limit)
- Assemblies AGENTS.md: 155 lines — Harmony prefix/postfix patterns, DummyTarget pattern, HarmonyLib version split
- scripts AGENTS.md: 155 lines — Ruff ALL rules, Google docstrings, verb_noun.py naming
- Localization AGENTS.md: 151 lines — color code formats ({{W|}}, &G, ^r), =variable.name= preservation, mojibake sequences to avoid
- CLAUDE.md symlink: `ln -s AGENTS.md CLAUDE.md` works fine on macOS; `test -f CLAUDE.md` resolves through symlink correctly
- `readlink CLAUDE.md` returns `AGENTS.md` (relative, not absolute) — correct behavior


## 2026-03-11 Task 4 ILSpy補完
- `XRL.Core.GameText` は存在せず、実体は `XRL.GameText`（`VariableReplace`/`Process`）。
- `XRL.Language.HistoricStringExpander` は存在せず、実体は `HistoryKit.HistoricStringExpander`。
- `XRL.World.Conversations.ConversationUI` は存在せず、実体は `XRL.UI.ConversationUI`。
- 会話テキストイベント実体は `XRL.World.Conversations.DisplayTextEvent` / `PrepareTextEvent` / `PrepareTextLateEvent`。
- `ConversationLoader` は ID 衝突時に `Load=Merge(0)` で `ConversationXMLBlueprint.Merge()`、それ以外は辞書上書き。
- `Load=MergeIfExists` は会話ではなく `ObjectBlueprintLoader.ReadObjectsNode()` 側に実装（存在時のみmerge、不存在は無言スキップ）。
- `DescriptionBuilder` の優先順は `Mark(-800) -> Adjective(-500) -> Base(10) -> Clause(600) -> Tag(1100)`。
- `Markup` のエスケープは `&&` と `^^` がリテラル扱い（色コード開始を抑止）。
- `IComponent<T>` に `XDidY` / `XDidYToZ` / `DidX` / `DidXToY` ラッパーがあり、内部で `Messaging.*` へ委譲。

## 2026-03-11 Task 6 テストハーネス構築
- `QudJP.Tests` は `net10.0` + NUnit 4.3.2 + NUnit3TestAdapter 5.0.0 + Microsoft.NET.Test.Sdk 17.14.0 の組み合わせで安定実行。
- Harmony 2.4.2 は NuGet パッケージIDが `HarmonyLib` ではなく `Lib.Harmony`。
- `net10.0` テストプロジェクトから `net48` 本体への `ProjectReference` は `ReferenceOutputAssembly=false` と `SkipGetTargetFrameworkProperties=true` を併用すると警告なしで共存できる。
- L2 の Harmony 後片付けは `Harmony.UnpatchID` ではなく `harmony.UnpatchAll(harmonyId)` が有効（この環境の API 実体に一致）。
- レイヤ分離チェックは `L1` で `using HarmonyLib` 0件、`L2` で `using UnityEngine` 0件を grep で機械確認できる。

## 2026-03-11 Task 5 エンコーディング正規化検証
- Task 3のコピー時にBOM除去・LF正規化が既に実施済みだった。
- 全78ファイルがUTF-8 (BOM無し)・LF改行であることを確認。
- XML 35/35・JSON 35/35のパース検証OK。
- mojibake文字 (繧/縺/驕/蜒) は0件。
- `docs/glossary.csv` は正しい位置にあり、UTF-8/BOM無し。
- エビデンス: `.sisyphus/evidence/task-5-encoding-check.txt`, `task-5-xml-migration.txt`
- コミット: `chore(localization): verify encoding normalization for migrated translations`
- 教訓: ファイルコピー時にPythonのopen(encoding='utf-8')で読み書きするとBOMが自動除去される。明示的なBOM除去ステップが不要になる場合がある。

## 2026-03-11 Task 7 C#翻訳インフラ
- `ColorCodePreserver` は `{{W|...}}` / `&X` / `^Y` を span 化して可視テキストと分離し、復元時にインデックス順で再挿入する方式がL1で安定した。
- `&&` と `^^` は色コードではなくエスケープとして `Strip` 時にそのまま残すと、誤検出を防ぎつつ往復一致を維持できる。
- `Translator` は `DataContractJsonSerializer` + 遅延ロード + `ConcurrentDictionary` キャッシュで net48 互換のままスレッドセーフに実装できる。
- `QudJP.Tests` 側で `../src/*.cs` を `Compile Include` でリンクすると、`net10.0` L1 から本番ロジックを直接検証できる（`net48` 参照なし）。
- LSP は `csharp-ls` が PATH 未解決だと診断不可。`/Users/sankenbisha/bin` に `~/.dotnet/tools/csharp-ls` をリンクすると `lsp_diagnostics` 実行が復旧した。

## 2026-03-11 Task 9 基本UI Harmonyパッチ + L2
- `QudJP.csproj` にゲーム同梱 `0Harmony.dll` 参照（`<Private>false</Private>`）を追加し、net48本体で `[HarmonyPatch]` 属性付きパッチをコンパイル可能にした。
- UI共通処理は `UITextSkinTranslationPatch` に集約し、`Translator.Translate()` + `ColorCodePreserver` で `{{...|...}}` 等を保全したまま翻訳できる形にした。
- Popupは `XRL.UI.Popup.ShowBlock` と `ShowOptionList` を `HarmonyTargetMethods` + `object[] __args` で一括Prefix処理し、タイトル/本文/選択肢/ボタン文言を反映できる。
- MainMenu/Options は `Show()` Postfixでメニュー要素フィールドを反射更新する方式にすると、ゲーム型を直接参照せずにL2 DummyTargetでも検証可能。
- L2は既存3 + 新規6 = 合計9テストが `--filter TestCategory=L2` で全件パス。

## 2026-03-11 Task 0: PoC Results
- dotnet SDK 10.0.100 on macOS ARM64 — NUnit + HarmonyLib fully functional
- 6/6 tests pass: NUnit harness, Harmony Prefix, Harmony Postfix, Assembly-CSharp reference, 0Harmony metadata, Unity type instantiation
- Assembly-CSharp.dll: Pure types like ConsoleLib.Console.Markup are fully accessible via reflection
- TypeInitializationException: NOT observed for 20 Unity types — better than expected
- HarmonyLib NuGet 2.4.2 works alongside game's 0Harmony 2.2.2.0
- Target frameworks: net10.0 for tests, net48 for mod DLL
- netstandard2.0 doesn't work well for test projects (adapter incompatibility)
- PoC project structure: /tmp/qudjp-poc/{QudJP.PoC.Tests, QudJP.PoC.Mod}

## 2026-03-11 Task 8 Python ツール基盤
- Ruff `select = ["ALL"]` で CLI スクリプトを書く際は `# noqa: T201` (print), `# noqa: S603, S607` (subprocess) が頻出。per-file-ignores で `scripts/tests/**` に `S101` (assert) と `PLR2004` (magic value) を抑制すると pytest テストが自然に書ける。
- `scripts/__init__.py` を作ると INP001 (implicit-namespace-package) を回避でき、テストからの `from scripts.check_encoding import ...` も動作する。
- Python 3.12+ ターゲットでは `from __future__ import annotations` 不要。`list[str] | None` 等が runtime で直接動作する。
- boolean パラメータは keyword-only (`*` 後に配置) にすると FBT001/FBT002 を回避できる。
- 例外メッセージは変数 `msg = f"..."` に格納してから `raise Error(msg)` とすると EM101/EM102 を回避。
- `shutil.copy2` はメタデータ保持。`dest_path.parent.mkdir(parents=True, exist_ok=True)` でネストしたコピー先も安全。
- 32 テスト全パス、ruff 0 エラーを初回実行で達成。

## 2026-03-11 Task 1 スキャフォールディング（再実行）
- dotnet SDK 10.0.100 では `dotnet new sln` がデフォルトで `.slnx` (XML形式) を生成。`--format sln` で従来の `.sln` を指定可能。
- `dotnet sln add` も `.slnx` に変換するため、`.sln` 形式を維持するには手動生成が安全。
- `QudJP.csproj` は `net48` + `Nullable enable` + `TreatWarningsAsErrors` でゲームDLL参照なしで正常ビルド（`src/AssemblyInfo.cs` のみ）。
- `Mods/QudJP/Localization/ObjectBlueprints/` にはTask 3で移行済みの jp.xml ファイル（14ファイル）が既存。

## 2026-03-11 Task 11 Grammar中和パッチ
- `XRL.Language.Grammar` は `[HarmonyTargetMethod]` + `AccessTools.Method("Type:Method")` でゲーム型直参照なしに8メソッドを安全に解決できる。
- net48 互換では `string.Replace(old, new, StringComparison)` と `^1` インデクサは使えないため、`Replace(old, new)` と `items[items.Count - 1]` を使う必要がある。
- L1 では Harmony適用を使わず Prefix を直接呼ぶだけで、文法中和ロジック（無冠詞化・複数形無効化・`の`付与・和文リスト化）を高速に検証できる。
- `SplitOfSentenceList` は `、` と英語の `, and` / ` and ` を `,` 正規化してから分割すると、色コードを壊さずに要素化できる。
