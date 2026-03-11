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

## 2026-03-11 Task diff_localization
- `scripts/diff_localization.py` は `ObjectBlueprints` をサブディレクトリ集約、`Conversations` は `<conversation ID>`、その他はルート直下の `ID/Name` 優先で比較すると、カテゴリ別カバレッジを安定算出できる。
- `Books.xml` は `ET.parse()` 失敗時に `<book ... ID="...">` を bytes 正規表現で抽出するフォールバックを入れると、不正文字を含む実データでも継続可能。
- 空の翻訳XMLは parse error を失敗扱いにせず、空集合として処理すると `--summary`/`--missing-only` のCLI挙動を壊さずに0%を報告できる。
- `python` コマンド非搭載環境があるため、検証コマンドは `python3 scripts/diff_localization.py --help` で実行すると再現性が高い。

## 2026-03-11 Task validate_xml
- `scripts/validate_xml.py` は `Path` の複数入力（ファイル/ディレクトリ混在）を受け、ディレクトリ時は `rglob("*.xml")` で再帰収集すると `*.jp.xml` も自然に包含できる。
- 色コード検証は文字列走査で `{{`/`}}` のスタック整合を取ると、最初の不整合行を警告として返せる。
- 兄弟重複チェックは親要素ごとに `ID` と `Name` を別カウントして `count > 1` を警告化すると要件に合う。
- 空翻訳チェックは leaf 要素に限定し、`<text>` の `None` / 空白のみ文字列を警告対象にすると過検出を抑えられる。
- Ruff `D103` が `scripts/tests/` の test 関数にも適用されるため、各 test に短い docstring が必要。

## 2026-03-11 Fail-fast hardening (Assemblies/src)
- 起動時処理（`Translator.LoadTranslations` / `QudJPMod.ApplyHarmonyPatches`）は警告+継続ではなく例外送出に統一すると、Player.log で初期化失敗を即時に特定できる。
- 辞書JSONのファイル単位障害は握りつぶさず伝播させる一方、ファイル内の不正エントリは `Trace.TraceWarning` でスキップ継続する分離が有効。
- Harmony の `TargetMethod()` は null 許容運用でも、失敗時 `Trace.TraceError` を各パッチで明示すると「どのパッチが当たらなかったか」を更新追従時に即断できる。
- L1 では `SetDictionaryDirectoryForTests` と生JSON書き込みヘルパーを使うと、`DirectoryNotFoundException` / `SerializationException` / `InvalidDataException` の fail-fast 回帰を安定再現できる。

## 2026-03-11 Python fail-fast hardening
- `validate_xml.py`: `errors="ignore"` を除去し strict UTF-8 デコードに変更。ET.parse() は ISO-8859-1 宣言のXMLを正常パースするが、後続の `read_text(encoding="utf-8")` が UnicodeDecodeError を投げるため、テストでは `<?xml encoding="ISO-8859-1"?>` + `\xe9` バイトの組み合わせが有効。
- `_collect_xml_files()` の else-raise 追加で symlink等の非正規パスを拒否する際、`run_validation()` のハンドラに `ValueError` を追加する必要あり。
- `diff_localization.py`: `ET.ParseError` の fallback は意図的だが、`as exc` で束縛して `sys.stderr` に警告出力すると、サイレント降格からデバッグ可能な降格に変わる。
- `_extract_books_entries_regex()` の set comprehension を for ループに展開すると、個別 decode 例外を投げられる。
- `_extract_generic_entries()` の空集合 raise は `_is_blank_xml()` + `return set()` の上流で既にガードされているため、到達するのは非空だが ID/Name なし XML のみ。
- `check_encoding.py`: `check_file()` に `exists()` / `is_file()` ガードを追加しても、`check_directory()` 経由では `is_file()` 確認済みのパスしか渡されないため既存動線には影響なし。
- ruff S108: `/tmp/...` パスをテスト内で使う場合は `# noqa: S108` が必要。
- テスト数: 49 → 53 (新規4件追加、全パス)。

## 2026-03-11 Task 19 ドキュメント整備

- README.md: 32行スタブから142行の包括的ドキュメントに書き直し。CI バッジ、Requirements テーブル、Install/Build/Test/Lint/Project Structure/Documentation セクションを追加。
- docs/test-architecture.md: 164行。L1/L2/L3 の3層テーブル、DummyTarget パターンのコード例、層境界ルール、テストプロジェクト構成を記載。
- docs/translation-process.md: 175行。XML/JSON 翻訳ファイルの種類、追加手順、色コード保全、変数プレースホルダー、検証ワークフロー、よくある間違いを記載。
- docs/contributing.md: 197行。前提条件、開発環境セットアップ、Git ワークフロー、Conventional Commits 規約、コード品質要件（C#/Python）、テスト要件、CI パイプライン、重要な制約を記載。
- CHANGELOG.md: 65行。Keep a Changelog 形式、0.1.0-dev (Unreleased) に全実装済み機能を英語で記載。
- scripts/README.md: 219行。5スクリプト全ての用途・使い方・出力例・終了コードを記載。典型的なワークフローも追加。
- docs/glossary.csv: ヘッダー English,Japanese,Short,Notes,Status、83エントリ、UTF-8 BOM なし、問題なし（変更不要）。

## 2026-03-11 Task 10 Roslyn + SonarAnalyzer 有効化

- net48 では `EnableNETAnalyzers` はデフォルトOFF。`<EnableNETAnalyzers>true</EnableNETAnalyzers>` + `<AnalysisLevel>latest-all</AnalysisLevel>` を明示追加する必要がある。
- net10.0 は SDK analyzers が自動有効だが、`AnalysisLevel=latest-all` で追加CA規則（CA1307, CA1510, CA1515, CA1865等）が発火する。
- SonarAnalyzer.CSharp は `PrivateAssets=all` + `IncludeAssets=analyzers` で analyzer-only 参照にできる。バージョン指定は NuGet 解決後の実バージョン（10.8.0.113526）に合わせる必要がある。
- Harmony パッチ特有の suppress 対象: CA1707 (__ パラメータ), CA1859 (TargetMethod 戻り型), CA1002 (List<T> パラメータ), CA1062 (null検査).
- net48/net10.0 共有ソースでの suppress 対象: CA1510 (ThrowIfNull は .NET 6+), CA1307 (Replace+StringComparison は .NET Core 3.0+), CA1865 (EndsWith(char) は .NET Core 2.1+).
- テストプロジェクト固有の suppress: CA1515 (public→internal), CA1822 (static化), S1186 (空メソッド), CA2249 (Contains vs IndexOf).
- DataContract クラスの CA1812 は serializer 経由のリフレクション生成で偽陽性になる。
- ColorCodePreserver の S127 (ループカウンタ更新) はパーサーステートマシンの必然。
- 全 suppress は .editorconfig で管理し、#pragma warning disable ゼロを維持。
- 初回ビルドで mod project 37 errors, test project 37 errors を2ラウンドのトリアージで解消。

## 2026-03-11 Task 22 build failure fix
- QJ002の誤検出回避は `??` を残したまま抑制するより、`ResolveHarmonyType` を段階的な `Type.GetType` 試行 + `Trace.TraceWarning` に分解する方が fail-fast 方針と整合しやすい。
- `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.NUnit` 1.1.2 は NUnit 4 系と実行時互換がなく、`Assert.That(ValueTuple<,>...)` の `MissingMethodException` が出るため、Analyzer テスト側は NUnit 3.13.x が安定した。
- Analyzer テストの raw string は `using` が namespace 宣言より後ろに回ると `CS1529` を誘発するため、`using` を先頭、スタブ namespace を後置する順序が必要。
- Analyzer プロジェクトの RS2008 は release tracking markdown を維持しない運用なら `.editorconfig` の `dotnet_diagnostic.RS2008.severity = none` で無警告運用にできる。

## 2026-03-11 Task 16 メッセージログ翻訳
- `MessagePatternTranslator` は `DataContractJsonSerializer` + `ConcurrentDictionary<string, Regex>` で実装し、初回ロード時に全regexを事前コンパイルして不正パターンを fail-fast で検知できる。
- 末尾句読点を許容する正規表現で `(.+)` を使うとキャプチャに `.`/`!` が残るため、メッセージ本文抽出は `(.+?)` の非貪欲マッチが安定。
- `messages.ja.json` に `"entries": []` を併記しておくと、既存 `Translator` の `*.ja.json` 一括ロードと共存できる（`patterns` は無視される）。
- `MessageLogPatch` は `Prefix(ref string Message, string? Color, bool Capitalize)` で `Message` だけを書き換え、catch では `return true` を徹底すると英語フォールバックを維持できる。
- L2 で `MessageLogPatch.Prefix` を `DummyMessageQueue.AddPlayerMessage` に直接 `harmony.Patch` する形にすると、実ゲーム型依存なしで辞書置換・色コード保全・引数透過を検証できる。

## 2026-03-11 Task 17 HistoricStringExpander 初期対応
- `HistoricStringExpander` は `AccessTools.Method("HistoryKit.HistoricStringExpander:ExpandString")` の名前解決を第一選択にし、失敗時のみ `TypeByName + GetDeclaredMethods` で `ExpandString`/5引数/先頭`string` を拾うと、ゲーム型直参照なしで安全に解決できる。
- Phase 1 は Expand 後の `__result` に対して `UITextSkinTranslationPatch.TranslatePreservingColors()` をそのまま適用する最小実装で、既存辞書・色コード保全ロジックを再利用できる。
- L1 は Postfix を直接呼び、L2 は `DummyHistoricStringExpander.ExpandString` へ Harmony Postfix を当てる二層検証にすると、仕様（既知キー翻訳・未知文パススルー・色コード保持）を過不足なく固定化できる。

## 2026-03-11 Task 12 会話表示テキストHarmony Postfix
- `ConversationNode.GetDisplayText(bool)` は `AccessTools.Method("XRL.World.Conversations.ConversationNode:GetDisplayText")` で一次解決し、失敗時は `AccessTools.AllTypes()` から `XRL.World.Conversations.*` + `GetDisplayText(bool):string` を走査する二段階解決で追従性を確保できる。
- Postfix は `UITextSkinTranslationPatch.TranslatePreservingColors(__result)` へ委譲し、翻訳辞書適用と `{{W|...}}` 色コード保全を共通経路で再利用すると実装差分を最小化できる。
- 会話表示L2は `DummyConversationElement.GetDisplayText(bool)` へ実パッチPostfixを直接当てる形で、翻訳/未知文パススルー/色コード保全/null・empty/既存日本語パススルーを1ファイル内で網羅できる。

## 2026-03-11 Task 14 UIパッチ拡張 (GetDisplayName/CharGen/Inventory)
- `GetDisplayNameEvent.GetFor` は `AccessTools.Method("XRL.World.GetDisplayNameEvent:GetFor")` を一次解決し、失敗時に `AccessTools.AllTypes()` から `GetDisplayNameEvent` + `GetFor` + `string` 戻り値を探す二段階解決で実装できる。
- CharGen/Inventory はゲーム更新で型名が変わりやすいため、既知型名配列 + 名前空間/型名パターンのフォールバック走査で `string` 戻り値メソッドをまとめて Postfix する設計が保守しやすい。
- 共有ソースを `net48` と `net10.0` の両方で analyzer clean にするには、`StringComparison.OrdinalIgnoreCase` 付き部分一致を `#if NET48` で `IndexOf`、それ以外で `Contains` に分岐するヘルパー化が有効（CA2249 回避）。
- L2は `DummyGetDisplayNameEvent` + UI拡張テスト1ファイルで8ケース（GetDisplayName 4 / CharGen 2 / Inventory 2）を追加し、全体テストは 96 passed まで増加した。
