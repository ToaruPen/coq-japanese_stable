# Caves of Qud 日本語化Mod ロードマップ (v2 — テスト駆動改訂版)

## TL;DR

> **Quick Summary**: Caves of Qud の日本語化ModをILSpy解析に基づくテスト駆動開発で構築する。レガシー翻訳XML（66,000行超）を検証・移行し、3層テストアーキテクチャ（Pure Logic / Harmony Integration / Game Smoke）でパッチの正確性を保証する。
> 
> **Deliverables**:
> - ゲーム内で日本語テキストが表示される動作するMod
> - 翻訳ワークフローを支えるPythonツール群
> - NUnit自動テストスイート（~85%のパッチロジックをCI化）
> - 再現可能なビルド・デプロイパイプライン
> 
> **Estimated Effort**: XL（複数セッションにまたがる長期プロジェクト）
> **Parallel Execution**: YES — フェーズ内で最大5-7並列
> **Critical Path**: Task 0 (PoC) → Task 1 → Task 5 → Task 7 → Task 10 → Task 14 → Task 17 → Task 20

---

## Context

### Original Request
Caves of Qudの日本語化Modの作成作業に向けた準備。全体のロードマップを策定する。

### Interview Summary
**Key Discussions**:
- レガシープロジェクト（`/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy`）のコードはAI-slopで参照不可。翻訳済みXMLデータ（35ファイル, 66,301行）のみ流用
- 配布はGitHub配布のみ（Steam Workshop不要）
- 翻訳範囲は「通常プレイが成立するレベル」。プロシージャル部分は難易度が高い
- 開発環境はmacOS（レガシーはWindows前提）
- ILSpy解析は実施済み（前セッションで~20クラスを網羅的に解析完了）
- ユーザー指示:「テスト駆動で翻訳modの確からしさを確保する」
- ユーザー指示:「実ゲームのソースコードをもとにしてどこにパッチのhookを差し込むのかを決めつつ考えないといけない」

**ILSpy Decompilation Results** (前セッションで確認済み):
- **テキストパイプライン全体像**: XML → GameText.VariableReplace → Grammar → Messaging → Markup → ScreenBuffer/RTF
- **Pure関数率**: ~85%（Grammar 50+メソッド、Markup.Transform、HistoricStringExpander正常パス等）
- **会話テキスト**: ConversationLoader が `Load="Merge"` でID衝突時に自動上書き → 静的会話はXMLのみで対応可能
- **SVO→SOV問題**: `Messaging.XDidY()` / `XDidYToZ()` が英語語順をハードコード → 完全置換パッチ必須
- **3つの色コード形式**: `{{W|text}}`(pre-markup), `&G`(前景), `^r`(背景)。`&&` `^^` がエスケープ
- **DisplayName組立順序**: `[Mark(-800)] [SizeAdj] [Adj(-500)] [Base(10)] [Clause(600)] [Tag(1100)]`
- **Blueprint XMLフィールド**: `Parts["Render"].Parameters["DisplayName"]` / `Parameters["Short"]`

**Research Findings**:
- ゲームは英語のみ対応（Steam API確認済み）
- Harmony Prefix/Postfixは静的メソッド → NUnitで直接呼び出しテスト可能
- DummyTargetクラスで実ゲームメソッドシグネチャをシミュレーション可能
- TypeInitializationExceptionが主要リスク（Unity型のstatic constructor連鎖）
- NUnit + HarmonyLib がHarmony modテストの標準パターン

### Metis Review (v2改訂用)
**Identified Gaps** (addressed):
- **Task 0 PoC必須**: NUnit + net48 + HarmonyLib が macOS で動作するか検証するゲートタスクが必要 → 追加
- **Assembly-CSharp.dll テスト環境読み込みリスク**: DummyTarget + インターフェース分離で回避 → テストハーネス設計に反映
- **.NET Target Framework整合性**: net48 vs .NET 8+ → テストプロジェクトで netstandard2.0 検証
- **Grammar パッチ scope creep**: 50+メソッド全対応は過大 → Phase 1は8メソッドに絞る
- **XDidY SVO→SOV は Phase 2+**: 最も複雑なパッチ。Phase 1はメッセージ辞書置換に留める
- **ConversationLoader XML Merge楽観的想定**: 実機で部分マッチ・ネスト挙動を確認必要
- **テスト層分離の厳守**: L1がHarmonyLib依存 or L2がUnity型依存 → 設計ミス
- **セーブ互換性**: Mod有効/無効切り替え時のデータ破損リスク → ドキュメントに注記

---

## Work Objectives

### Core Objective
Caves of Qud の日本語化Modを、ILSpy解析に基づくテスト駆動開発で新規構築し、通常プレイが日本語で成立する状態を実現する。テスト可能な翻訳ロジック（~85%）はNUnit自動テストで品質保証する。

### Concrete Deliverables
- `Mods/QudJP/` — 動作するMod本体（manifest.json, Harmony DLL, Localization XML）
- `Mods/QudJP/Assemblies/QudJP.Tests/` — NUnit自動テストスイート
- `scripts/` — Python ツール群（抽出・差分・同期・検証）
- `docs/` — 翻訳プロセス・用語集・技術ドキュメント・ILSpy解析結果
- ゲーム内で Options / UI / NPC会話 / アイテム名 / スキル等が日本語表示

### Definition of Done
- [ ] `dotnet test` で全L1/L2テストがパス（最低50テスト以上）
- [ ] `Mods/QudJP` をゲームの Mods ディレクトリにコピーし、ゲーム起動後にオプション画面が日本語で表示される
- [ ] キャラクター作成 → Joppa開始 → NPC会話 → 基本的な冒険が日本語でプレイ可能
- [ ] `python scripts/diff_localization.py` で翻訳カバレッジが把握できる
- [ ] `dotnet build` が macOS 上でエラーなく完了し QudJP.dll が生成される

### Must Have
- UTF-8 (BOM無し) / LF 固定のエンコーディング規約
- XML翻訳ファイルの構造的検証（パース可能、ContextID保持）
- CJKフォント対応（TextMeshPro）
- 色制御文字・マークアップの保全（3形式全て: `{{W|text}}`, `&G`, `^r`）
- ゲームアップデート時の差分検出メカニズム
- 3層テストアーキテクチャ（L1 Pure, L2 Harmony, L3 Smoke）
- 各Harmonyパッチに対応するL1/L2テスト
- 対象ゲームバージョンの明記とバージョン固定

### Must NOT Have (Guardrails)
- レガシーのC#/Pythonコードのコピー＆ペースト（参考程度の閲覧は可、コード流用は不可）
- lintなし・型チェックなしのコード（C#はNullable enable、PythonはRuff + mypy相当）
- 過剰なコメントや不要な抽象化（AI-slop防止）
- プロシージャルテキストの完全翻訳（Phase 1スコープ外）
- Steam Workshop対応
- 翻訳品質のレビュー（翻訳内容そのものは既存を信頼、構造・エンコーディングのみ検証）
- テスト内でAssembly-CSharp.dllの型を直接インスタンス化（DummyTarget経由のみ）
- L1テストからHarmonyLibへの依存
- L2テストからUnityEngine型への依存
- Grammar系パッチの全50+メソッド対応（Phase 1は8メソッドに限定）
- Messaging.XDidY/XDidYToZ の完全SOV変換（Phase 2スコープ）

---

## Verification Strategy (MANDATORY)

> **3層テスト駆動検証**: ILSpy解析で判明したPure関数率(~85%)を活かし、
> 翻訳パッチの大部分をNUnit自動テストで検証する。
> ゲーム内確認（L3）は最終動作確認のみに限定。

### 基本方針: TDD (テスト駆動開発)

> **本プロジェクトはTDD（テスト駆動開発）を基本方針とする。**
>
> 各Harmonyパッチの実装は以下のサイクルに従う:
> 1. **RED**: ILSpy解析に基づきDummyTargetを作成し、期待する翻訳結果を検証するテストを先に書く（この時点で失敗する）
> 2. **GREEN**: テストを通す最小限のパッチ実装を書く
> 3. **REFACTOR**: コードを整理し、色制御文字保全・エッジケースのテストを追加
>
> **TDDの適用範囲**:
> - L1 (Pure Logic) テスト: Grammar変換、色コード保全、辞書検索 — **必ずテストファースト**
> - L2 (Harmony Integration) テスト: DummyTarget + パッチ適用 — **必ずテストファースト**
> - L3 (Game Smoke Test): ゲーム内動作確認 — テストファーストは不可（手動QA）
>
> **TDDの利点（本プロジェクト固有）**:
> - ILSpy解析で得たメソッドシグネチャをDummyTargetとして先にテストに落とし込むことで、パッチのhookポイントが実コードと一致していることを保証
> - Pure関数（~85%）のテストを先に書くことで、Unity環境なしで翻訳ロジックの正確性を担保
> - 「テストが通る = 翻訳パッチが正しく動く」という確信を持って実装を進められる

### 3-Layer Test Architecture

| 層 | テスト対象 | ツール | Unity依存 | 実行方法 |
|----|-----------|--------|-----------|---------|
| **L1: Pure Logic** | Grammar変換, Markup処理, 辞書検索, テンプレート展開, 色コード保全 | NUnit | ❌ なし | `dotnet test --filter TestCategory=L1` |
| **L2: Harmony Integration** | パッチ適用, DummyTarget経由のフック検証, Prefix/Postfix動作 | NUnit + HarmonyLib | ❌ なし | `dotnet test --filter TestCategory=L2` |
| **L3: Game Smoke Test** | フォント表示, UIレイアウト, 全体動作, セーブ互換性 | Player.log + screencapture | ✅ あり | 手動ゲーム操作 + ログ自動分析 |

### Test Decision
- **Infrastructure exists**: NO（Task 0 PoCで検証後、Task 6で本構築）
- **Automated tests**: YES (**TDD** — テストを先に書き、実装で満たす RED→GREEN→REFACTOR サイクル)
- **Framework**: NUnit + HarmonyLib (C#), pytest (Python)
- **Gate condition**: Task 0 PoCが成功しない限り3層テスト戦略にコミットしない。失敗時はフォールバック（Player.log主体 + 可能な範囲のテスト）

### QA Policy
- **L1/L2テスト**: 全Harmonyパッチに対応するNUnitテスト必須。`dotnet test` で自動実行
- **L3ゲーム確認**: Modデプロイ → ゲーム起動・操作は手動 → Player.log自動分析 + screencapture
- **XML整合性**: パーサーによる構造検証 + エンコーディングチェック（完全自動）
- **Python ツール**: `pytest` + 実データでの動作確認（完全自動）
- Evidence: `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`

### L3ランタイムQA 共通前提条件 (MANDATORY)

> **全てのゲーム内QA（L3）シナリオは以下の前提条件を満たした上で実行すること:**
>
> 1. **他modの無効化**: QudJP以外のmodを `StreamingAssets/Mods/` から一時退避（`mv` で別ディレクトリへ）。ゲーム本体のHarmonyエラー（`mprotect returned EACCES` 等）が他modに起因する場合、これで排除される
> 2. **Player.logクリア**: QA開始前に `> ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log` でログを空にする。これにより過去のセッションのエラーが混入しない
> 3. **ログ取得タイミング**: ゲーム操作完了直後にログを取得し、QudJPスコープの grep (`QudJP.*error|QudJP.*exception`) でフィルタリング
> 4. **QA完了後のmod復元**: 退避したmodを元に戻す
>
> この手順はTask 0 Part B, Task 9, Task 10, Task 12, Task 13, Task 14, Task 16, Task 18, F3 に適用される。

### DummyTarget 設計原則
- 実ゲームの型を直接参照しない（TypeInitializationException回避）
- メソッドシグネチャのみを再現するDummyクラスを作成
- テストではDummyTargetにHarmonyパッチを適用し、変換結果を検証
- DummyTargetのシグネチャはILSpy解析結果と照合して正確に再現

---

## Execution Strategy

### Phase Structure

```
Phase 0: PoC & 基盤構築 (Wave 0-2)
├── Task 0:  ツールチェーンPoC (NUnit + HarmonyLib macOS検証) ★ GATE
├── Task 1:  プロジェクトスキャフォールディング
├── Task 2:  ゲームデータ構造分析
├── Task 3:  レガシーXML監査 & 移行計画
├── Task 4:  ILSpy解析結果の永続化 & 補完
├── Task 5:  レガシーXML移行の実行
└── Task 6:  テストハーネス構築 (NUnit + HarmonyLib)

Phase 1: 最小動作Mod (Wave 3-4)
├── Task 7:  C# 翻訳インフラストラクチャの構築
├── Task 8:  Python ツール基盤の構築
├── Task 9:  基本 Harmony パッチ (Options, Menu, Popup) + L2テスト
├── Task 10: 最小動作Modの統合テスト

Phase 2: 翻訳カバレッジ拡大 (Wave 5-7)
├── Task 11: Grammar系パッチ (冠詞除去, 複数形中和等) + L1テスト
├── Task 12: 会話・書籍・クエスト統合 (XML Merge中心)
├── Task 13: ObjectBlueprints 全カテゴリ統合
├── Task 14: UI パッチ拡張 (CharGen, Inventory, Status等) + L2テスト
├── Task 15: 翻訳差分ツールの完成
├── Task 16: メッセージログ翻訳 (辞書置換方式) + L1/L2テスト

Phase 3: ポリッシュ & リリース (Wave 8-9)
├── Task 17: プロシージャルテキスト初期対応 (HistoricStringExpander)
├── Task 18: フルゲームプレイテスト
├── Task 19: ドキュメント整備
└── Task 20: GitHub リリース準備

Critical Path: Task 0 → Task 1 → Task 6 → Task 7 → Task 9 → Task 10 → Task 14 → Task 16 → Task 18 → Task 20
```

### Parallel Execution Waves

```
Wave 0 (GATE — must pass before anything else):
└── Task 0:  ツールチェーンPoC [quick]

Wave 1 (After Wave 0 — foundation, 4 parallel):
├── Task 1:  プロジェクトスキャフォールディング [quick]
├── Task 2:  ゲームデータ構造分析 [deep]
├── Task 3:  レガシーXML監査 & 移行計画 [deep]
└── Task 4:  ILSpy解析結果永続化 [quick]

Wave 2 (After Wave 1 — migration + test infra, 2 parallel):
├── Task 5:  XML移行実行 (depends: 1, 3) [unspecified-high]
└── Task 6:  テストハーネス構築 (depends: 0, 1, 4) [deep]

Wave 3 (After Wave 2 — core implementation, 3 parallel):
├── Task 7:  C# 翻訳インフラ (depends: 1, 4, 5, 6) [deep]
├── Task 8:  Python ツール基盤 (depends: 1) [unspecified-high]
└── Task 9:  基本Harmonyパッチ (depends: 4, 6, 7) [deep]

Wave 4 (After Wave 3 — integration):
└── Task 10: 最小Mod統合テスト (depends: 5, 7, 8, 9) [unspecified-high]

Wave 5 (After Wave 4 — coverage expansion + tooling, 3 parallel):
├── Task 11: Grammar系パッチ (depends: 6, 7) [deep]
├── Task 12: 会話・書籍・クエスト (depends: 2, 5, 10) [unspecified-high]
└── Task 15: 翻訳差分ツール (depends: 5, 8) [unspecified-high]

Wave 6 (After Wave 5 — advanced patches + integration, 3 parallel):
├── Task 13: ObjectBlueprints統合 (depends: 5, 10, 15) [unspecified-high]
├── Task 14: UIパッチ拡張 (depends: 9, 10, 11) [deep]
└── Task 16: メッセージログ翻訳 (depends: 4, 11) [deep]

Wave 7 (After Wave 6 — procedural text):
└── Task 17: プロシージャルテキスト (depends: 4, 11, 16) [ultrabrain]

Wave 8 (After Wave 7 — polish, 2 parallel):
├── Task 18: フルゲームプレイテスト (depends: 12, 13, 14, 16, 17) [unspecified-high]
└── Task 19: ドキュメント整備 (depends: 8, 15) [writing]

Wave 9 (After Wave 8 — release):
└── Task 20: GitHubリリース準備 (depends: 18, 19) [quick]

Wave FINAL (After ALL tasks — verification, 4 parallel):
├── Task F1: Plan Compliance Audit [oracle]
├── Task F2: Code Quality Review [unspecified-high]
├── Task F3: Real Manual QA [unspecified-high]
└── Task F4: Scope Fidelity Check [deep]
```

### Dependency Matrix

| Task | Depends On | Blocks | Wave |
|------|-----------|--------|------|
| 0 | — | 1,2,3,4,5,6,7,8,9,10... | 0 |
| 1 | 0 | 5,6,7,8 | 1 |
| 2 | 0 | 12 | 1 |
| 3 | 0 | 5 | 1 |
| 4 | 0 | 6,7,9,14,16,17 | 1 |
| 5 | 1,3 | 7,10,12,13 | 2 |
| 6 | 0,1,4 | 7,9,11,14 | 2 |
| 7 | 1,4,5,6 | 9,10,11,14 | 3 |
| 8 | 1 | 10,15 | 3 |
| 9 | 4,6,7 | 10,14 | 3 |
| 10 | 5,7,8,9 | 12,13,14 | 4 |
| 11 | 6,7 | 14,16,17 | 5 |
| 12 | 2,5,10 | 18 | 5 |
| 13 | 5,10,15 | 18 | 6 |
| 14 | 9,10,11 | 18 | 6 |
| 15 | 5,8 | 19 | 5 |
| 16 | 4,11 | 17,18 | 6 |
| 17 | 4,11,16 | 18 | 7 |
| 18 | 12,13,14,16,17 | 20 | 8 |
| 19 | 8,15 | 20 | 8 |
| 20 | 18,19 | — | 9 |

### Agent Dispatch Summary

| Wave | Tasks | Categories |
|------|-------|-----------|
| 0 | 1 | T0 → quick |
| 1 | 4 | T1 → quick, T2 → deep, T3 → deep, T4 → quick |
| 2 | 2 | T5 → unspecified-high, T6 → deep |
| 3 | 3 | T7 → deep, T8 → unspecified-high, T9 → deep |
| 4 | 1 | T10 → unspecified-high |
| 5 | 3 | T11 → deep, T12 → unspecified-high, T15 → unspecified-high |
| 6 | 3 | T13 → unspecified-high, T14 → deep, T16 → deep |
| 7 | 1 | T17 → ultrabrain |
| 8 | 2 | T18 → unspecified-high, T19 → writing |
| 9 | 1 | T20 → quick |
| FINAL | 4 | F1 → oracle, F2 → unspecified-high, F3 → unspecified-high, F4 → deep |

### ILSpy-Verified Hook Points (全タスク横断参照)

| Hook Target | メソッドシグネチャ | パッチ種別 | テスト層 | 使用タスク |
|------------|-----------------|-----------|---------|-----------|
| `Grammar.A(string, bool)` | Prefix + return false | L1 | Task 11 |
| `Grammar.Pluralize(string)` | Prefix + return false | L1 | Task 11 |
| `Grammar.MakePossessive(string)` | Postfix | L1 | Task 11 |
| `Grammar.MakeAndList(List<string>)` | Postfix | L1 | Task 11 |
| `Grammar.MakeOrList(List<string>)` | Postfix | L1 | Task 11 |
| `Grammar.InitCaps(string)` | Prefix + return false | L1 | Task 11 |
| `Grammar.CardinalNumber(int)` | Postfix | L1 | Task 11 |
| `Grammar.SplitOfSentenceList(string)` | Postfix | L1 | Task 11 |
| `GetDisplayNameEvent.GetFor(GameObject)` | Postfix | L2 | Task 14 |
| `MessageQueue.AddPlayerMessage(string,string,bool)` | Prefix | L2 | Task 16 |
| `IConversationElement.GetDisplayText(bool)` | Postfix | L2 | Task 12 |
| `PrepareTextLateEvent.Send(ref string)` | Postfix | L2 | Task 12 |
| `HistoricStringExpander.ExpandString(string,...)` | Postfix | L1+L2 | Task 17 |
| `Markup.Transform(string)` | — (テスト参照のみ) | L1 | Task 7 |
| `Sidebar.Render()` | Transpiler | L3のみ | Task 14 |
| FontManager (TextMeshPro) | — | L3のみ | Task 7 |

---

## TODOs

> Implementation + Test = ONE Task. Never separate.
> EVERY task MUST have: Recommended Agent Profile + Parallelization info + QA Scenarios.
> テスト対象がPureの場合はL1テスト、Harmony連携の場合はL2テスト、Unity依存の場合はL3テストを明記。

### Phase 0: PoC & 基盤構築

- [ ] 0. ツールチェーンPoC: NUnit + HarmonyLib macOS 検証 ★ GATE TASK

  **What to do**:
  - **Part A: NUnit + HarmonyLib テスト環境検証**
    - 最小限のNUnitテストプロジェクトを作成し、macOS上で `dotnet test` が動作することを確認
    - HarmonyLib をNuGetで追加し、DummyTargetクラスへのPrefix/Postfixパッチが動作することを確認
    - Assembly-CSharp.dll を参照（Reference）として追加し、Pure関数の型情報が利用可能か確認
    - Assembly-CSharp.dll の型を直接インスタンス化した場合の TypeInitializationException 挙動を確認・記録
    - .NET Target Framework の整合性確認（net48 / netstandard2.0 / net8.0 のどれが動作するか）
  - **Part B: ゲーム内 Harmony ランタイム検証** ★ CRITICAL
    - 最小限のMod（manifest.json + 空のHarmony PatchAll()を呼ぶだけのDLL）を作成
    - Caves of Qud にデプロイして実際にゲームを起動
    - Player.log で `Harmony.PatchAll()` が成功しているか確認
    - **既知リスク**: macOS環境で `mprotect returned EACCES` が発生する可能性（メモリ保護の問題）
    - 失敗時の原因特定と回避策の調査（SIP設定、entitlements、Mono互換等）
  - 検証結果を `docs/poc-results.md` に記録（Part A + Part B の両方）
  - **Part A + Part B 両方成功時**: 3層テスト戦略にフルコミット。Task 6 でテストハーネス本構築。全タスクが計画通り進行
  - **Part A 成功 / Part B 失敗時**: 以下の具体的リカバリーパスを実行:
    1. `docs/poc-results.md` に失敗原因（`mprotect EACCES`、SIP、entitlements等）を詳細記録
    2. 回避策を調査・実施（SIP一時無効化、Mono互換設定、ゲーム起動オプション等）
    3. 回避不可能な場合: **XML-onlyフォールバックブランチ**に切り替え:
       - **スキップするタスク**: Task 9 (基本Harmonyパッチ), Task 11 (Grammarパッチ), Task 14 (UIパッチ拡張), Task 16 (メッセージログ), Task 17 (プロシージャルテキスト)
       - **縮小するタスク**: Task 7 → FontManager + Translator のみ（Harmonyパッチなし）、Task 10 → XML Merge表示確認のみ
       - **維持するタスク**: Task 1-6, 8, 12, 13, 15, 18-20（XML Merge + ツーリングは影響なし）
       - **代替成功基準**: `dotnet build` 成功、全XMLパース成功、Options/会話/Blueprint がXML Merge経由で日本語表示、`dotnet test` L1テストパス（L2は対象外）
       - `docs/poc-results.md` にフォールバック決定と影響範囲を記録
    4. L1テストは引き続き有効（Pure関数テストのためゲーム実行不要）。L2テストはDummyTargetベースで引き続き実行可能だが、実ゲームでの動作は保証外
    5. L3ゲーム内QAは XML Merge による翻訳表示の確認のみに限定
  - **Part A 失敗時**: フォールバック戦略に切り替え（Player.log主体 + 可能な範囲でのテスト）

  **Must NOT do**:
  - 本番コードを書かない（検証のみ）
  - Assembly-CSharp.dll をリポジトリにコミットしない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO（全タスクのゲート）
  - **Parallel Group**: Wave 0（単独実行）
  - **Blocks**: Tasks 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
  - **Blocked By**: None

  **References**:
  - Assembly-CSharp.dll: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll`
  - 0Harmony.dll: 同ディレクトリ（ゲーム同梱 v2.2.2.0）
  - NuGet HarmonyLib: `https://www.nuget.org/packages/Lib.Harmony/`
  - NuGet NUnit: `https://www.nuget.org/packages/NUnit/`
  - Harmony テストパターン: Harmony公式リポジトリ `pardeike/Harmony` のテストディレクトリ

  **Acceptance Criteria**:
  - [ ] Part A: `dotnet test` が macOS 上で exit code 0 を返す
  - [ ] Part A: DummyTargetクラスにHarmony Prefix パッチが適用される
  - [ ] Part A: Assembly-CSharp.dll 内のPure型（例: Markup）のメソッドシグネチャが参照可能
  - [ ] Part A: TypeInitializationException の挙動が記録されている
  - [ ] Part B: 最小ModをCaves of Qudにデプロイし、ゲーム起動後にPlayer.logでHarmonyパッチ適用を確認
  - [ ] Part B: `mprotect returned EACCES` が発生する場合、回避策を調査し記録
  - [ ] `docs/poc-results.md` にPart A + Part B の結果、Target Framework、ランタイム制約が記載

  **QA Scenarios**:
  ```
  Scenario: NUnit + HarmonyLib 基本動作
    Tool: Bash
    Steps:
      1. mkdir -p /tmp/qudjp-poc && cd /tmp/qudjp-poc
      2. dotnet new nunit -n QudJP.PocTest
      3. dotnet add package Lib.Harmony
      4. DummyTarget + Prefix パッチのテストコードを追加
      5. dotnet test
    Expected Result: テスト実行成功、Harmony パッチが DummyTarget に適用される
    Evidence: .sisyphus/evidence/task-0-poc-nunit.txt

  Scenario: Assembly-CSharp.dll 参照テスト
    Tool: Bash
    Steps:
      1. Assembly-CSharp.dll をプロジェクト参照に追加
      2. Pure型（XRL.UI.Markup等）のstatic methodをテストで呼び出し
      3. Unity依存型のインスタンス化を試み、TypeInitializationException を捕捉
    Expected Result: Pure型のstaticメソッド参照可能。Unity依存型ではTypeInitializationExceptionが発生し、それが記録される
    Failure Indicators: dotnet test 自体が起動しない、全テストがTypeInitializationExceptionで失敗
    Evidence: .sisyphus/evidence/task-0-poc-assembly.txt

  Scenario: ゲーム内 Harmony ランタイム検証 (Part B) ★ CRITICAL
    Tool: Bash (ゲーム起動は手動)
    Preconditions: L3ランタイムQA共通前提条件を適用（他mod無効化 + ログクリア）
    Steps:
      1. QudJP以外のmodを一時退避: mkdir -p /tmp/qud-mods-backup && mv ~/Library/Application\ Support/Steam/steamapps/common/Caves\ of\ Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/!(QudJP_PoC) /tmp/qud-mods-backup/ 2>/dev/null || true
      2. Player.logをクリア: > ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log
      3. 最小Mod作成: manifest.json + Harmony.PatchAll() のみを呼ぶ空DLL
      4. Modをゲームディレクトリにデプロイ
      5. ゲーム起動（手動）
      6. Player.log を取得: cat ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log
      7. grep -i "harmony\|patch\|QudJP_PoC" Player.log で Harmony パッチ適用ログを確認
      8. grep "mprotect\|EACCES\|SecurityException" Player.log でメモリ保護エラーを確認
      9. PoCモッドをクリーンアップ + 退避modを復元: rm -rf .../Mods/QudJP_PoC/ && mv /tmp/qud-mods-backup/* .../Mods/ 2>/dev/null || true
    Expected Result: クリーンなログ環境で Harmony パッチ適用ログが存在し、mprotect/EACCES エラーなし
    Failure Indicators: "mprotect returned EACCES"、"SecurityException"、パッチ適用ログが全くない
    Evidence: .sisyphus/evidence/task-0-poc-runtime.txt
  ```

  **Commit**: YES
  - Message: `chore: toolchain proof-of-concept for NUnit + HarmonyLib on macOS`

- [ ] 1. プロジェクトスキャフォールディング

  **What to do**:
  - Git リポジトリ初期化（`git init`）
  - ディレクトリ構成を作成:
    ```
    Mods/QudJP/
    ├── Assemblies/       # C# Harmony パッチ
    │   ├── src/
    │   ├── QudJP.csproj
    │   └── QudJP.sln
    ├── Localization/     # 翻訳XML (カテゴリ別)
    │   └── ObjectBlueprints/
    ├── Fonts/            # CJK フォントアセット
    └── manifest.json
    scripts/              # Python ツール群
    ├── tests/
    docs/                 # ドキュメント
    references/           # ゲームから抽出したベースデータ (.gitignore対象)
    ```
  - `.editorconfig` 作成（UTF-8 BOM無し / LF / indent設定）
  - `.gitignore` 作成（references/, *.dll, obj/, bin/, __pycache__/等）
  - `manifest.json` 作成（Id: QudJP, 依存関係なし）
  - `README.md` 骨子作成
  - Python環境設定（`pyproject.toml`、Ruff lint設定）
  - C# プロジェクト作成（`QudJP.csproj` — Task 0 PoCで決定したTarget Framework, Nullable enable, GameDir設定をmacOS対応に）

  **Must NOT do**:
  - レガシーからファイルをそのままコピーしない
  - Steam Workshop関連のファイルを含めない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: [`git-master`]

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2, 3, 4)
  - **Blocks**: Tasks 5, 6, 7, 8
  - **Blocked By**: Task 0

  **References**:
  - Task 0 PoCの結果: `docs/poc-results.md` — Target Framework決定
  - `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/Mods/QudJP/manifest.json` — manifest構造の参考
  - `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/.editorconfig` — editorconfig参考
  - `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/Mods/QudJP/Assemblies/QudJP.csproj` — csproj構造参考（GameDir をmacOS対応に変更）
  - ゲーム Managed DLLs: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed/`

  **Acceptance Criteria**:
  - [ ] `git status` が正常に動作
  - [ ] `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` が成功（空プロジェクト）
  - [ ] `.editorconfig` が charset=utf-8, end_of_line=lf を指定

  **QA Scenarios**:
  ```
  Scenario: プロジェクト構造の検証
    Tool: Bash
    Steps:
      1. ls -R で全ディレクトリ構成を確認
      2. cat .editorconfig で encoding/EOL設定を確認
      3. dotnet build Mods/QudJP/Assemblies/QudJP.csproj を実行
    Expected Result: ビルド成功、ディレクトリ構成が仕様通り
    Evidence: .sisyphus/evidence/task-1-project-scaffold.txt
  ```

  **Commit**: YES
  - Message: `chore: initialize project scaffolding with build config`

- [ ] 2. ゲームデータ構造の分析

  **What to do**:
  - `StreamingAssets/Base/` 配下の全XMLファイルの一覧と構造を把握
  - 主要XMLファイル（ObjectBlueprints.xml, Conversations.xml, Options.xml等）のスキーマ分析
  - `Load="Merge"` / `Load="Replace"` / `Load="MergeIfExists"` のセマンティクスをゲーム実データで確認（ILSpy結果: `Load="Merge"` = attribute-level merge, no Load = full replacement, `Load="MergeIfExists"` = silent skip if not found）
  - 色制御文字3形式の使用箇所を特定: `{{W|text}}`(pre-markup), `&G`(前景), `^r`(背景)
  - ゲーム内テキストの分類作成:
    - **静的テキスト**: XML定義のみで完結（Options, Skills, Mutations名等）
    - **半動的テキスト**: XMLテンプレート + 変数展開（会話、クエスト文）
    - **動的テキスト**: コード生成（HistoricStringExpander, Grammar, CombatLog等）
  - 分析結果を `docs/game-data-analysis.md` に記録

  **Must NOT do**:
  - ゲームファイルを変更しない
  - 分析結果にレガシーの知見を混ぜない（独自に確認する）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3, 4)
  - **Blocks**: Task 12
  - **Blocked By**: Task 0

  **References**:
  - ゲームデータ: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Base/`
  - 公式Wiki: `https://wiki.cavesofqud.com/wiki/Modding:Objects`
  - 公式Wiki: `https://wiki.cavesofqud.com/wiki/Modding:Conversations`
  - 公式Wiki: `https://wiki.cavesofqud.com/wiki/Modding:Colors_%26_Object_Rendering`
  - ILSpy解析結果（前セッション）: ObjectBlueprint Load behavior — `Load="Merge"` = attribute-level merge

  **Acceptance Criteria**:
  - [ ] `docs/game-data-analysis.md` が存在し、テキスト分類（静的/半動的/動的）が明記
  - [ ] 色制御文字3形式の使用パターンが文書化
  - [ ] Load="Merge" / "Replace" / "MergeIfExists" の使い分けルールが文書化

  **QA Scenarios**:
  ```
  Scenario: 分析ドキュメントの完全性
    Tool: Bash
    Steps:
      1. cat docs/game-data-analysis.md で内容確認
      2. grep -c "静的" docs/game-data-analysis.md で分類セクション存在確認
      3. grep -c "Load=" docs/game-data-analysis.md でマージルール記載確認
      4. grep -c "{{" docs/game-data-analysis.md で色制御文字ドキュメント確認
    Expected Result: 3つのテキスト分類、Loadセマンティクス、3形式の色制御文字が文書化
    Evidence: .sisyphus/evidence/task-2-data-analysis.txt
  ```

  **Commit**: YES
  - Message: `docs: add game data structure analysis`

- [ ] 3. レガシーXML監査 & 移行計画

  **What to do**:
  - レガシーの全XML翻訳ファイル（35ファイル, 66,301行）を自動検証:
    - XMLパース可能か
    - UTF-8エンコーディング（BOMなし）か
    - モジバケシーケンス（`繧`, `縺`等）が含まれていないか
    - ContextID / Blueprint名が現行ゲームバージョンと一致するか
  - サイズ降順で検証（上位5ファイルで69%カバー）:
    1. `ObjectBlueprints/Creatures.jp.xml` — 14,101行
    2. `ObjectBlueprints/Items.jp.xml` — 10,996行
    3. `Conversations.jp.xml` — 13,184行
    4. `ObjectBlueprints/Furniture.jp.xml` — 5,457行
    5. `Books.jp.xml` — 2,098行
  - JSON辞書ファイル（37ファイル, 32,830行）のスキーマ検証
  - `migration-report.json` を出力（ファイルごと: 行数, エンコーディング, パース結果, 問題点）
  - 用語集（`glossary.csv`, 84エントリ）の移行可否判定
  - 移行計画を `docs/migration-plan.md` に記録

  **Must NOT do**:
  - この段階でファイルをコピーしない（計画のみ）
  - レガシーのPythonスクリプトを使わない（新規で検証ロジックを書く）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2, 4)
  - **Blocks**: Task 5
  - **Blocked By**: Task 0

  **References**:
  - レガシーXML: `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/Mods/QudJP/Localization/` — 翻訳XMLファイル群
  - レガシー辞書: `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/Mods/QudJP/Localization/Dictionaries/` — JSON辞書群
  - レガシー用語集: `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/Docs/glossary.csv`
  - 現行ゲームデータ: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Base/`

  **Acceptance Criteria**:
  - [ ] `migration-report.json` が全35ファイルのステータスを含む
  - [ ] エンコーディング異常のあるファイル数が特定されている
  - [ ] 現行ゲームバージョンとの互換性差分が把握されている

  **QA Scenarios**:
  ```
  Scenario: 移行レポートの検証
    Tool: Bash
    Steps:
      1. python -m json.tool migration-report.json でJSON妥当性確認
      2. jq '.files | length' migration-report.json でファイル数確認（35以上）
      3. jq '[.files[] | select(.issues | length > 0)] | length' migration-report.json で問題ファイル数確認
    Expected Result: 全ファイルが記録され、問題ファイルが特定されている
    Evidence: .sisyphus/evidence/task-3-migration-audit.txt
  ```

  **Commit**: YES
  - Message: `docs: complete legacy XML audit and migration plan`

- [ ] 4. ILSpy解析結果の永続化 & 補完

  **What to do**:
  - 前セッションで実施したILSpy解析結果を `docs/ilspy-analysis.md` に永続化:
    - テキストパイプライン全体像（Data Source → Text Generation → Markup → Display）
    - 全Hook Points一覧（メソッドシグネチャ、パッチ種別、テスト層、Pure/Mixed/Unity分類）
    - 3つの色コード形式の詳細
    - ConversationLoader の XML Merge 挙動
    - DisplayName 組立順序
    - Blueprint XMLフィールド構造
  - 補完調査（必要に応じてILSpyで追加確認）:
    - `GameText.VariableReplace()` の変数展開パターン一覧（`=subject.name=` 等）
    - `Description.GetLongDescription()` の組立ロジック
  - 各Hook Pointに対してDummyTarget設計のヒントを記載

  **Must NOT do**:
  - ゲームのDLLを変更しない
  - レガシーのパッチ一覧に依存しない

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2, 3)
  - **Blocks**: Tasks 6, 7, 9, 14, 16, 17
  - **Blocked By**: Task 0

  **References**:
  - Assembly-CSharp.dll: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll`
  - ILSpy コマンド: `DOTNET_ROLL_FORWARD=LatestMajor ~/.dotnet/tools/ilspycmd -t "TYPENAME" ~/Library/Application\ Support/Steam/steamapps/common/Caves\ of\ Qud/CoQ.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll`
  - 前セッション解析対象クラス: Grammar, HistoricStringExpander, TextFilters, Semantics, WordDataManager, XmlDataHelper, GameObjectFactory, MessageQueue, Messaging, ConversationLoader, ColorUtility, Markup, Popup, Look, Sidebar, ScreenBuffer, ConversationUI, ConversationText, DisplayTextEvent, PrepareTextEvent, GetDisplayNameEvent

  **Acceptance Criteria**:
  - [ ] `docs/ilspy-analysis.md` が存在し、テキストパイプライン全体像が記載
  - [ ] 全Hook Points（16+）がメソッドシグネチャ付きで一覧化
  - [ ] 各Hook PointにPure/Mixed/Unity分類とテスト層が明記
  - [ ] DummyTarget設計ヒントが記載

  **QA Scenarios**:
  ```
  Scenario: ILSpy解析ドキュメントの完全性
    Tool: Bash
    Steps:
      1. grep -c "Hook" docs/ilspy-analysis.md でHook Point数を確認（16以上）
      2. grep -c "Pure\|Mixed\|Unity" docs/ilspy-analysis.md で分類の網羅性確認
      3. grep -c "DummyTarget" docs/ilspy-analysis.md でDummyTarget設計ヒント記載確認
      4. grep "Grammar.A\|Pluralize\|XDidY\|AddPlayerMessage\|GetDisplayText" docs/ilspy-analysis.md で主要Hook記載確認
    Expected Result: 16+ Hook Points、全分類記載、DummyTargetヒント記載
    Evidence: .sisyphus/evidence/task-4-ilspy-doc.txt
  ```

  **Commit**: YES
  - Message: `docs: persist ILSpy text pipeline analysis with hook points`

### Phase 0 → Phase 1 ブリッジ (Wave 2)

- [ ] 5. レガシーXML移行の実行

  **What to do**:
  - Task 3 の `migration-report.json` に基づき、検証済みXMLファイルを新プロジェクトへコピー
  - コピー時にエンコーディング正規化（UTF-8 BOMなし / LF）
  - XML構造の軽微な修正（現行ゲームバージョンとの差分修正）
  - 用語集（`glossary.csv`）を `docs/glossary.csv` へ移行
  - 移行後の全ファイルに対して再検証を実行
  - `world-gospels.ja.json` 等のHistoricStringExpander系辞書は移行するが Phase 2 マークを付与

  **Must NOT do**:
  - 翻訳内容の品質レビューは行わない（構造・エンコーディングのみ）
  - レガシーのC#/Pythonコードをコピーしない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Task 6)
  - **Blocks**: Tasks 7, 10, 12, 13
  - **Blocked By**: Tasks 1, 3

  **References**:
  - `migration-report.json` — Task 3 の出力
  - レガシーXML: `/Users/sankenbisha/Dev/Caves-of-Qud_Japanese_legacy/Mods/QudJP/Localization/`
  - `.editorconfig` — Task 1 で作成

  **Acceptance Criteria**:
  - [ ] 全検証済みXMLが `Mods/QudJP/Localization/` 配下に存在
  - [ ] `file` コマンドで全ファイルが UTF-8 であることを確認
  - [ ] 全XMLが `python -c "import xml.etree.ElementTree as ET; ET.parse('file')"` でパース可能

  **QA Scenarios**:
  ```
  Scenario: 移行ファイルの整合性
    Tool: Bash
    Steps:
      1. find Mods/QudJP/Localization -name "*.xml" | wc -l でファイル数確認（35以上）
      2. 全XMLファイルに対して python パーサーで構造検証
      3. grep -r "繧\|縺" Mods/QudJP/Localization/ でモジバケチェック
    Expected Result: 全ファイルがパース可能、モジバケなし
    Evidence: .sisyphus/evidence/task-5-xml-migration.txt

  Scenario: エンコーディング確認
    Tool: Bash
    Steps:
      1. find Mods/QudJP/Localization -name "*.xml" -exec file {} \; で全ファイルのエンコーディング確認
      2. BOM付きファイルがないことを確認
    Expected Result: 全ファイルが "UTF-8 Unicode text" と表示
    Evidence: .sisyphus/evidence/task-5-encoding-check.txt
  ```

  **Commit**: YES
  - Message: `feat(localization): migrate validated XML translations from legacy`

- [ ] 6. テストハーネス構築 (NUnit + HarmonyLib)

  **What to do**:
  - `Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` を作成:
    - NUnit 3.x + NUnit3TestAdapter
    - HarmonyLib (NuGet)
    - Assembly-CSharp.dll 参照（Task 0 PoCで確認した方法）
    - Task 0 PoCで決定した Target Framework
  - テスト層分離のための基盤:
    - `[Category("L1")]` 属性でPure Logicテスト
    - `[Category("L2")]` 属性でHarmony Integrationテスト
    - `dotnet test --filter TestCategory=L1` / `TestCategory=L2` で分離実行
  - DummyTarget 基底クラス:
    - `DummyGrammar` — Grammar.A, Pluralize, MakePossessive等のシグネチャ再現
    - `DummyMessageQueue` — AddPlayerMessage のシグネチャ再現
    - `DummyConversationElement` — GetDisplayText のシグネチャ再現
  - サンプルテスト（基盤検証用）:
    - L1: 文字列変換のPureテスト（色制御文字の保全等）
    - L2: DummyGrammar にHarmony Postfixを適用してテスト
  - テスト実行コマンドの確認: `dotnet test`, `dotnet test --filter TestCategory=L1`

  **Must NOT do**:
  - テスト内でAssembly-CSharp.dllの型を直接インスタンス化しない（DummyTarget経由のみ）
  - L1テストからHarmonyLibを参照しない
  - L2テストからUnityEngine型を参照しない
  - 本番パッチコードをこのタスクに含めない（テスト基盤のみ）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Task 5)
  - **Blocks**: Tasks 7, 9, 11, 14
  - **Blocked By**: Tasks 0, 1, 4

  **References**:
  - Task 0 PoC結果: `docs/poc-results.md` — Target Framework、参照方法、制約事項
  - Task 4 ILSpy解析: `docs/ilspy-analysis.md` — DummyTarget設計のためのシグネチャ情報
  - Harmony テストパターン: `pardeike/Harmony` GitHub リポジトリのテストディレクトリ
  - ILSpy解析結果（Hook Points一覧）: Grammar.A(string,bool), Grammar.Pluralize(string), MessageQueue.AddPlayerMessage(string,string,bool), IConversationElement.GetDisplayText(bool)

  **Acceptance Criteria**:
  - [ ] `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/` が成功（最低5テスト）
  - [ ] `dotnet test --filter TestCategory=L1` でL1テストのみ実行できる
  - [ ] `dotnet test --filter TestCategory=L2` でL2テストのみ実行できる
  - [ ] DummyGrammar へのHarmony Postfix適用テストがパス
  - [ ] テスト層分離: L1テストコードに `using HarmonyLib` が存在しない

  **QA Scenarios**:
  ```
  Scenario: テストハーネスのビルドと実行
    Tool: Bash
    Steps:
      1. dotnet test Mods/QudJP/Assemblies/QudJP.Tests/ -v normal
      2. dotnet test --filter TestCategory=L1 -v normal でL1のみ実行
      3. dotnet test --filter TestCategory=L2 -v normal でL2のみ実行
    Expected Result: 全テストパス、層分離が機能している
    Evidence: .sisyphus/evidence/task-6-test-harness.txt

  Scenario: テスト層分離の検証
    Tool: Bash
    Steps:
      1. grep -r "using HarmonyLib" Mods/QudJP/Assemblies/QudJP.Tests/L1/ でL1にHarmony依存がないことを確認
      2. grep -r "using UnityEngine" Mods/QudJP/Assemblies/QudJP.Tests/L2/ でL2にUnity依存がないことを確認
    Expected Result: L1にHarmonyLib依存なし、L2にUnityEngine依存なし
    Failure Indicators: grepがマッチを返す（層分離違反）
    Evidence: .sisyphus/evidence/task-6-layer-isolation.txt
  ```

  **Commit**: YES
  - Message: `test: set up NUnit + HarmonyLib test harness with layer separation`

### Phase 1: 最小動作Mod (Wave 3-4)

- [ ] 7. C# 翻訳インフラストラクチャの構築

  **What to do**:
  - Mod エントリポイント `QudJPMod.cs` の実装（Harmony.PatchAll() を呼ぶブートストラップ）
  - `FontManager.cs` — CJK フォントの読み込みと適用（TextMeshPro fallback font）
  - `Translator.cs` — 基本翻訳エンジン（辞書ベースの文字列変換、キャッシュ機構）
  - `ColorCodePreserver.cs` — 3形式の色制御文字（`{{W|text}}`, `&G`, `^r`）を保全しつつ翻訳するロジック
    - ILSpy解析で確認: `Markup.Transform()` が `{{W|text}}` → `&W^ktext&y^k` 変換を行う（Pure関数）
    - `&&` `^^` がエスケープシーケンス
  - `LocalizationAssetResolver.cs` — Mod内のローカライズファイルのパス解決
  - 全クラスにNullable有効、XMLドキュメントコメント、単一責務の原則を適用
  - **L1テスト**: ColorCodePreserver の色制御文字保全テスト（3形式 × 正常/異常/エスケープ）
  - **L1テスト**: Translator の辞書検索テスト（完全一致, 未登録キー, キャッシュ動作）

  **Must NOT do**:
  - レガシーのC#コードをコピーしない
  - UI個別パッチはこのタスクに含めない（基盤のみ）
  - 過剰な抽象化（インターフェース乱立等）をしない

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 8, 9 — ただし9は7に依存)
  - **Blocks**: Tasks 9, 10, 11, 14
  - **Blocked By**: Tasks 1, 4, 5, 6

  **References**:
  - ILSpy解析結果: `docs/ilspy-analysis.md` — Markup.Transform() の色コード変換ロジック、3形式の詳細
  - Managed DLLs: ゲームの `Managed/` ディレクトリ — 参照アセンブリ
  - 公式Wiki: `https://wiki.cavesofqud.com/wiki/Modding:C_Sharp_Scripting`
  - ILSpy解析 Markup クラス: `{{W|text}}` → `&W^ktext&y^k` 変換。エスケープ: `&&` → `&`, `^^` → `^`
  - ILSpy解析 色コード: `&` + char = 前景色, `^` + char = 背景色。`&y` = デフォルト前景、`^k` = デフォルト背景

  **Acceptance Criteria**:
  - [ ] `dotnet build` が成功し `QudJP.dll` が生成される
  - [ ] Nullable warnings ゼロ
  - [ ] 各クラスが単一責務で200行以内
  - [ ] `dotnet test --filter TestCategory=L1` で ColorCodePreserver + Translator テストパス（最低8テスト）

  **QA Scenarios**:
  ```
  Scenario: C# ビルド成功とL1テスト
    Tool: Bash
    Steps:
      1. dotnet build Mods/QudJP/Assemblies/QudJP.csproj -c Release
      2. ls Mods/QudJP/Assemblies/QudJP.dll の存在確認
      3. dotnet test --filter TestCategory=L1 -v normal
    Expected Result: Build succeeded, DLL生成, L1テスト全パス
    Evidence: .sisyphus/evidence/task-7-build-and-l1.txt

  Scenario: 色制御文字保全のL1テスト詳細
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~ColorCodePreserver" -v normal
      2. テスト結果に3形式（{{W|text}}, &G, ^r）の保全テストがパスしていることを確認
    Expected Result: 3形式 × 正常/異常/エスケープ のテストケースが全パス
    Evidence: .sisyphus/evidence/task-7-colorcode-tests.txt
  ```

  **Commit**: YES
  - Message: `feat(harmony): implement core translation infrastructure with L1 tests`

- [ ] 8. Python ツール基盤の構築

  **What to do**:
  - `scripts/sync_mod.py` — Mod をゲームディレクトリへ同期（rsync ベース、macOS対応）
    - `--dry-run`, `--exclude-fonts` オプション対応
  - `scripts/check_encoding.py` — UTF-8/BOMなし/LF検証、モジバケ検出
  - `scripts/extract_base.py` — ゲームのBase XMLを `references/Base/` へ抽出
  - `pyproject.toml` に Ruff lint設定、型ヒント必須のルール
  - `scripts/tests/` にpytest基盤
  - 各スクリプトに `--help` オプション（argparse）

  **Must NOT do**:
  - レガシーのPythonスクリプトをコピーしない
  - Windows固有のパス（`%USERPROFILE%`等）をハードコードしない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with Tasks 7, 9)
  - **Blocks**: Tasks 10, 15
  - **Blocked By**: Task 1

  **References**:
  - ゲームパス: `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/`
  - Modインストール先: 同 `StreamingAssets/Mods/` 配下

  **Acceptance Criteria**:
  - [ ] `python scripts/sync_mod.py --dry-run` が正常動作
  - [ ] `python scripts/check_encoding.py Mods/QudJP/Localization/` が全ファイルOK
  - [ ] `python scripts/extract_base.py --help` がヘルプ表示
  - [ ] `ruff check scripts/` がエラーゼロ
  - [ ] `pytest scripts/tests/` が全テストパス

  **QA Scenarios**:
  ```
  Scenario: sync_mod.py の動作確認
    Tool: Bash
    Steps:
      1. python scripts/sync_mod.py --dry-run を実行
      2. 出力にコピー予定ファイル一覧が表示されること
    Expected Result: ドライラン出力にファイル一覧、実際のコピーは発生しない
    Evidence: .sisyphus/evidence/task-8-sync-dryrun.txt

  Scenario: lint チェック
    Tool: Bash
    Steps:
      1. ruff check scripts/ を実行
      2. pytest scripts/tests/ を実行
    Expected Result: lintエラーゼロ、全テストパス
    Evidence: .sisyphus/evidence/task-8-lint.txt
  ```

  **Commit**: YES
  - Message: `feat(scripts): add core Python tooling (sync, encoding check, extraction)`

- [ ] 9. 基本 Harmony パッチの実装 + L2テスト

  **What to do**:
  - `OptionsLocalizationPatch.cs` — Options画面の日本語化
    - ILSpy Hook: Options画面のテキスト表示メソッド（Postfix）
  - `MainMenuLocalizationPatch.cs` — メインメニューの日本語化
  - `PopupTranslationPatch.cs` — ポップアップメッセージの翻訳
    - ILSpy Hook: `Popup.ShowBlock()` / `Popup.ShowOptionList()` のテキスト引数
  - `UITextSkinTranslationPatch.cs` — 共通UIテキスト翻訳フレームワーク
  - 各パッチは Harmony の Prefix/Postfix を適切に使い分け
  - **L2テスト**: 各パッチをDummyTargetに適用し、翻訳結果を検証
    - DummyOptionsTarget にPostfix適用 → 日本語テキスト返却を確認
    - DummyPopupTarget にPostfix適用 → 色制御文字保全+翻訳を確認

  **Must NOT do**:
  - レガシーのパッチコードをコピーしない
  - 1パッチに複数の責務を持たせない
  - Transpilerの過剰使用（Prefix/Postfixで足りる場合はそちらを優先）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO（Task 7 完了後）
  - **Parallel Group**: Wave 3 後半
  - **Blocks**: Tasks 10, 14
  - **Blocked By**: Tasks 4, 6, 7

  **References**:
  - ILSpy解析結果: `docs/ilspy-analysis.md` — 各UIクラスのフックポイント
  - Task 6 テストハーネス — DummyTarget基盤、L2テスト実行方法
  - Task 7 翻訳インフラ — Translator, ColorCodePreserver等を使用
  - 公式Wiki: `https://wiki.cavesofqud.com/wiki/Modding:C_Sharp_Scripting`

  **Acceptance Criteria**:
  - [ ] `dotnet build` 成功
  - [ ] Harmony パッチが正しいメソッドシグネチャをターゲットしている
  - [ ] `dotnet test --filter TestCategory=L2` でパッチテスト全パス（最低6テスト）
  - [ ] Options画面が日本語表示される（L3確認）

  **QA Scenarios**:
  ```
  Scenario: L2テスト — Harmonyパッチの動作検証
    Tool: Bash
    Steps:
      1. dotnet test --filter TestCategory=L2 -v normal
      2. テスト結果に Options, Menu, Popup 各パッチのテストがパスしていることを確認
    Expected Result: 全L2テストパス（6テスト以上）
    Evidence: .sisyphus/evidence/task-9-l2-tests.txt

  Scenario: L3確認 — ゲーム内Options画面
    Tool: Bash (ゲーム操作は手動)
    Preconditions: Modデプロイ済み
    Steps:
      1. python scripts/sync_mod.py でModをデプロイ
      2. ゲーム起動後、Player.log取得: tail -200 ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log
      3. grep -i "harmony\|patch\|QudJP" Player.log でパッチ適用確認
      4. grep -i "QudJP.*error\|QudJP.*exception\|missing glyph" Player.log でQudJP関連エラー検索（他mod/ゲーム本体のエラーを除外）
      5. screencapture -x .sisyphus/evidence/task-9-options-screen.png
    Expected Result: パッチ適用ログあり、QudJP関連error 0件、Options画面に日本語表示
    Evidence: .sisyphus/evidence/task-9-options-screen.png, .sisyphus/evidence/task-9-harmony-log.txt
  ```

  **Commit**: YES
  - Message: `feat(harmony): add basic UI localization patches with L2 tests`

- [ ] 10. 最小動作Modの統合テスト

  **What to do**:
  - Tasks 5-9 の成果物を統合し、ゲーム内で動作する最小Modを構成
  - `scripts/sync_mod.py` でModをゲームへデプロイ
  - **L1/L2テスト全実行**: `dotnet test` で全自動テストがパスすることを確認
  - **L3確認**: ゲーム起動 → Options画面が日本語で表示されることを確認
  - Player.log を分析し、エラー・警告・Missing glyphがないことを確認
  - フォントが正しく適用されているか確認（日本語文字が□にならない）
  - 問題があれば修正して再テスト

  **Must NOT do**:
  - この段階で全カテゴリの翻訳統合をしない（Optionsのみで十分）
  - ゲームのセーブデータを破壊しない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 4
  - **Blocks**: Tasks 12, 13, 14
  - **Blocked By**: Tasks 5, 7, 8, 9

  **References**:
  - Player.log: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`

  **Acceptance Criteria**:
  - [ ] `dotnet test` — 全L1/L2テストパス
  - [ ] ゲームがMod有効で正常起動
  - [ ] Options画面のテキストが日本語で表示
  - [ ] Player.log にQudJP関連エラーなし
  - [ ] 日本語文字がtofu（□）にならない

  **QA Scenarios**:
  ```
  Scenario: 全自動テスト実行
    Tool: Bash
    Steps:
      1. dotnet test Mods/QudJP/Assemblies/QudJP.Tests/ -v normal
      2. pytest scripts/tests/
      3. ruff check scripts/
      4. python scripts/check_encoding.py Mods/QudJP/Localization/
    Expected Result: 全コマンド exit code 0
    Evidence: .sisyphus/evidence/task-10-all-tests.txt

  Scenario: L3統合テスト — ゲーム起動とログ検証
    Tool: Bash (ゲーム操作は手動)
    Preconditions: Tasks 5-9完了
    Steps:
      1. python scripts/sync_mod.py でModをデプロイ
      2. ゲーム起動後、Player.log取得
      3. grep -i "QudJP\|mod.*load\|harmony" Player.log でMod読み込み確認
      4. grep -c -i "QudJP.*error\|QudJP.*exception\|missing glyph" Player.log でQudJP関連エラー数カウント（他mod/ゲーム本体のエラーを除外）
      5. screencapture -x .sisyphus/evidence/task-10-options-jp.png
    Expected Result: Mod読み込みログあり、QudJP関連error 0件、Missing glyph 0件、Options画面に日本語テキスト
    Evidence: .sisyphus/evidence/task-10-integration.txt, .sisyphus/evidence/task-10-options-jp.png
  ```

  **Commit**: YES
  - Message: `milestone: minimal viable mod with Japanese options screen`

### Phase 2: 翻訳カバレッジ拡大 (Wave 5-7)

- [ ] 11. Grammar系パッチ (冠詞除去, 複数形中和等) + L1テスト

  **What to do**:
  - 日本語に不要/異なる英語文法処理の中和・変換パッチ（Phase 1スコープ: 8メソッドに限定）
  - **冠詞除去**: `Grammar.A(string, bool)` → Prefix + return false（入力文字列をそのまま返す）
  - **複数形中和**: `Grammar.Pluralize(string)` → Prefix + return false（日本語に複数形なし）
  - **所有格変換**: `Grammar.MakePossessive(string)` → Postfix（`'s` → `の` に変換）
  - **リスト結合**: `Grammar.MakeAndList(List<string>)` → Postfix（`and` → `と` / `、`）
  - **リスト結合**: `Grammar.MakeOrList(List<string>)` → Postfix（`or` → `または`）
  - **文分割**: `Grammar.SplitOfSentenceList(string)` → Postfix（日本語区切り対応）
  - **頭文字大文字**: `Grammar.InitCaps(string)` → Prefix + return false（日本語不要）
  - **基数詞**: `Grammar.CardinalNumber(int)` → Postfix（数字→日本語表記、必要なら）
  - **全メソッドがPure**: L1テストのみで完全検証可能
  - 各メソッドに最低3テストケース: 正常入力、空文字列、色制御文字混在テキスト

  **Must NOT do**:
  - 8メソッド以外のGrammarメソッドに手を出さない（Phase 2+スコープ）
  - 色制御文字を破壊するパッチを書かない
  - テストなしでパッチを追加しない

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 5 (with Tasks 12, 13, 15)
  - **Blocks**: Tasks 14, 16, 17
  - **Blocked By**: Tasks 6, 7

  **References**:
  - ILSpy解析結果: `docs/ilspy-analysis.md` — Grammar クラスの全メソッドシグネチャ
  - Task 6 テストハーネス — DummyGrammar クラス
  - ILSpy解析 Grammar.A(): `public static string A(string Name, bool Proper = false)` — 冠詞 "a"/"an" 付加
  - ILSpy解析 Grammar.Pluralize(): `public static string Pluralize(string Word)` — 英語複数形ルール（-s, -es, -ies等）
  - ILSpy解析 Grammar.MakePossessive(): `public static string MakePossessive(string Word)` — `'s` / `'` 付加
  - ILSpy解析 Grammar.MakeAndList(): `public static string MakeAndList(List<string> Items)` — "A, B, and C" 形式
  - ILSpy解析 Grammar.MakeOrList(): `public static string MakeOrList(List<string> Items)` — "A, B, or C" 形式

  **Acceptance Criteria**:
  - [ ] `dotnet test --filter TestCategory=L1` で Grammar テスト全パス（最低24テスト: 8メソッド × 3ケース）
  - [ ] `dotnet build` 成功
  - [ ] 各パッチが正しいメソッドシグネチャをターゲット
  - [ ] 色制御文字混在テキストで文字化けなし

  **QA Scenarios**:
  ```
  Scenario: Grammar L1テスト — 全8メソッド
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~Grammar" -v normal
      2. テスト結果に8メソッド分のテストが含まれることを確認
      3. テスト数が24以上であることを確認
    Expected Result: 全テストパス（24テスト以上）
    Evidence: .sisyphus/evidence/task-11-grammar-l1.txt

  Scenario: 色制御文字保全テスト
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~Grammar" --filter "FullyQualifiedName~ColorCode" -v normal
      2. {{W|sword}} → Grammar.A → {{W|sword}} (冠詞なし) のテスト結果確認
      3. &Gsword&y → Grammar.Pluralize → &Gsword&y (複数形なし) のテスト結果確認
    Expected Result: 色制御文字が保全されたまま文法処理が適用される
    Evidence: .sisyphus/evidence/task-11-grammar-colorcode.txt
  ```

  **Commit**: YES
  - Message: `feat(harmony): add Grammar neutralization patches with L1 tests`

- [ ] 12. 会話・書籍・クエストの統合検証

  **What to do**:
  - **XML Merge 方式による統合** (Harmonyパッチ不要の部分):
    - Conversations.jp.xml（13,184行）— ConversationLoader が `Load="Merge"` でID衝突時に自動上書き
    - Books.jp.xml — 書籍テキスト
    - Quests.jp.xml — クエストログテキスト
  - **XML Merge 挙動の実機確認** (Metis指摘事項):
    - 部分マッチ（一部属性のみ上書き）の挙動確認
    - ネストされた要素のマージ動作確認
    - `Load="MergeIfExists"` の使い分け確認
  - 色制御文字の保全確認（会話テキストで特に重要）
  - 会話選択肢のレイアウト崩れチェック
  - **Harmony パッチが必要な部分** (動的テキスト):
    - `PrepareTextLateEvent.Send(ref string Text)` の Postfix で変数展開後テキストを翻訳
    - `IConversationElement.GetDisplayText(bool)` の Postfix で最終表示テキストを翻訳
  - **L2テスト**: DummyConversationElement に Postfix 適用テスト

  **Must NOT do**:
  - 翻訳内容の品質レビューはしない
  - 新規翻訳の追加はしない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 5 (with Tasks 11, 13, 15)
  - **Blocks**: Task 18
  - **Blocked By**: Tasks 2, 5, 10

  **References**:
  - ILSpy解析: ConversationLoader — `Load="Merge"` でID衝突時自動上書き
  - ILSpy解析: 会話テキストチェーン — ConversationText.Text → Prepare() → GetRandomSubstring('~') → PrepareTextEvent → GameText.VariableReplace() → PrepareTextLateEvent → cache → GetDisplayText() → DisplayTextEvent
  - ILSpy解析: PrepareTextLateEvent.Send(ref string Text) — Postfix対応
  - ILSpy解析: IConversationElement.GetDisplayText(bool) — Postfix対応
  - Task 6 テストハーネス — DummyConversationElement

  **Acceptance Criteria**:
  - [ ] Conversations.jp.xml が XML Merge でゲームに正しくロードされる
  - [ ] Joppa の NPC 会話が日本語で表示
  - [ ] 書籍・クエストログが日本語表示
  - [ ] 色タグが正しく動作
  - [ ] `dotnet test --filter TestCategory=L2` で会話パッチテストパス

  **QA Scenarios**:
  ```
  Scenario: XML Merge 挙動の実機確認
    Tool: Bash
    Steps:
      1. python -c "import xml.etree.ElementTree as ET; tree=ET.parse('Mods/QudJP/Localization/Conversations.jp.xml'); print(f'Elements: {len(list(tree.iter()))}')"
      2. python scripts/sync_mod.py でデプロイ
      3. ゲーム起動後 Player.log を確認
      4. grep -i "merge\|conversation\|load" Player.log でマージ動作確認
      5. grep -c "QudJP.*error\|QudJP.*exception" Player.log でQudJP関連エラー数カウント（他mod/ゲーム本体のエラーを除外）
    Expected Result: XML パース成功、マージログあり、QudJP関連エラー 0
    Evidence: .sisyphus/evidence/task-12-xmlmerge.txt

  Scenario: L2テスト — 会話パッチ
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~Conversation" -v normal
    Expected Result: DummyConversationElement への Postfix テストパス
    Evidence: .sisyphus/evidence/task-12-conversation-l2.txt
  ```

  **Commit**: YES
  - Message: `feat(localization): integrate conversations, books, quests via XML Merge`

- [ ] 13. ObjectBlueprints 全カテゴリ統合

  **What to do**:
  - レガシーから移行済みの12+サブカテゴリのObjectBlueprint XMLをゲーム内で検証:
    - Items, Creatures, Furniture, Foods, Data, HiddenObjects
    - PhysicalPhenomena, Staging, TutorialStaging, Walls, Widgets
    - WorldTerrain, ZoneTerrain, RootObjects
  - **Blueprint XMLフィールドの確認** (ILSpy解析ベース):
    - `Parts["Render"].Parameters["DisplayName"]` — 表示名
    - `Parts["Description"].Parameters["Short"]` — 短い説明
    - `Load="Merge"` でattribute-level mergeされることを確認
  - アイテム名・クリーチャー名・地形名が正しく表示されることを確認
  - Creatures.jp.xml（14,101行）は特に慎重に検証（最大ファイル）
  - 差分ツール（Task 15 完了後に利用可能）で未訳エントリを把握

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 6 (with Tasks 14, 16)
  - **Blocks**: Task 18
  - **Blocked By**: Tasks 5, 10, 15

  **Acceptance Criteria**:
  - [ ] 全ObjectBlueprint XMLがエラーなくロード
  - [ ] アイテム名がインベントリで日本語表示
  - [ ] クリーチャー名が戦闘時に日本語表示

  **QA Scenarios**:
  ```
  Scenario: 全ObjectBlueprint XMLのパース検証
    Tool: Bash
    Steps:
      1. find Mods/QudJP/Localization/ObjectBlueprints -name "*.xml" | wc -l でファイル数確認（12以上）
      2. find Mods/QudJP/Localization/ObjectBlueprints -name "*.xml" -exec python -c "import xml.etree.ElementTree as ET; ET.parse('{}')" \; で全ファイルパース検証
      3. python scripts/validate_xml.py Mods/QudJP/Localization/ObjectBlueprints/ で構造検証（Task 15 完了後に利用可能）
    Expected Result: 全12+ ファイルがパース成功、構造エラーなし
    Evidence: .sisyphus/evidence/task-13-blueprints-validate.txt

  Scenario: ゲームロード時のBlueprintエラー検出
    Tool: Bash (ゲーム操作は手動)
    Steps:
      1. python scripts/sync_mod.py でデプロイ
      2. ゲーム起動後 Player.log を取得
      3. grep -i "blueprint\|merge\|load.*error\|xml" Player.log でエラー検索
    Expected Result: blueprint/merge関連エラー 0件
    Evidence: .sisyphus/evidence/task-13-blueprints-log.txt
  ```

  **Commit**: YES
  - Message: `feat(localization): verify ObjectBlueprints integration`

- [ ] 14. UI パッチ拡張 (CharGen, Inventory, Status等) + L2テスト

  **What to do**:
  - キャラクター作成画面（Genotypes, Subtypes, Mutations, EmbarkModules）
  - インベントリ画面（カテゴリ名、重量表示、アクション名）
  - ツールチップ（アイテム名、説明文、効果）
    - ILSpy Hook: `GetDisplayNameEvent.GetFor(GameObject)` — Postfix で表示名を翻訳
    - ILSpy解析: DisplayName 組立順序 `[Mark(-800)] [SizeAdj] [Adj(-500)] [Base(10)] [Clause(600)] [Tag(1100)]`
  - ステータス画面
  - サイドバー
    - ILSpy Hook: `Sidebar.Render()` — ハードコードされた文字列（Transpiler or Prefix必要、L3のみ）
  - 各UIパッチを個別のHarmonyパッチクラスとして実装
  - **L2テスト**: DummyTarget にパッチ適用し翻訳結果を検証（Sidebarは L3 のみ）
  - Grammar系パッチ（Task 11）との連携確認（DisplayNameにGrammar処理が適用される）

  **Must NOT do**:
  - 1つの巨大パッチクラスに詰め込まない
  - メッセージログ・戦闘テキストはこのタスクに含めない

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 6 (with Task 16)
  - **Blocks**: Task 18
  - **Blocked By**: Tasks 9, 10, 11

  **References**:
  - ILSpy解析: `docs/ilspy-analysis.md` — GetDisplayNameEvent, Sidebar.Render, DisplayName組立順序
  - ILSpy解析: GetDisplayNameEvent.GetFor() — PURE, Postfix パッチ可能
  - ILSpy解析: DisplayName組立 — DescriptionBuilder で `[Mark(-800)] [SizeAdj] [Adj(-500)] [Base(10)] [Clause(600)] [Tag(1100)]` 順序
  - ILSpy解析: Sidebar.Render() — ハードコード文字列 `&YST&y:` 等、Unity依存（L3のみ）

  **Acceptance Criteria**:
  - [ ] キャラクター作成が日本語でプレイ可能
  - [ ] インベントリのカテゴリ名・アクション名が日本語表示
  - [ ] ツールチップにアイテムの日本語説明が表示
  - [ ] `dotnet test --filter TestCategory=L2` で UIパッチテスト全パス

  **QA Scenarios**:
  ```
  Scenario: L2テスト — UIパッチ
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~UI" -v normal
      2. テスト結果に GetDisplayName, Inventory, CharGen 各パッチのテストが含まれることを確認
    Expected Result: 全UIパッチのL2テストパス
    Evidence: .sisyphus/evidence/task-14-ui-l2.txt

  Scenario: L3確認 — キャラクター作成画面
    Tool: Bash (ゲーム操作は手動)
    Steps:
      1. python scripts/sync_mod.py でデプロイ
      2. ゲーム起動→キャラクター作成画面へ遷移
      3. screencapture -x .sisyphus/evidence/task-14-chargen.png
      4. grep -i "QudJP.*error\|QudJP.*exception" Player.log でQudJP関連エラー確認（他mod/ゲーム本体のエラーを除外）
    Expected Result: キャラクター作成画面が日本語表示、QudJP関連エラー 0件
    Evidence: .sisyphus/evidence/task-14-chargen.png
  ```

  **Commit**: YES
  - Message: `feat(harmony): expand UI localization patches with L2 tests`

- [ ] 15. 翻訳差分ツールの完成

  **What to do**:
  - `scripts/diff_localization.py` — 未訳チェックツール
    - ゲームの Base XML と翻訳 XML を比較
    - 未翻訳の `<object>` や `<conversation>` を検出
    - `--missing-only`, `--summary`, `--json-path` オプション
  - `scripts/validate_xml.py` — XML構造検証ツール
    - Load="Merge" / "Replace" / "MergeIfExists" のセマンティクス検証
    - ContextID の重複検出
    - 色制御文字3形式のペア整合性チェック
  - pytestによるテスト

  **Must NOT do**:
  - レガシーのdiffスクリプトをコピーしない

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 5 (with Tasks 11, 12, 13)
  - **Blocks**: Task 19
  - **Blocked By**: Tasks 5, 8

  **Acceptance Criteria**:
  - [ ] `python scripts/diff_localization.py --summary` がカバレッジレポートを出力
  - [ ] `python scripts/validate_xml.py Mods/QudJP/Localization/` がエラーなし
  - [ ] `pytest scripts/tests/` が全テストパス

  **QA Scenarios**:
  ```
  Scenario: 差分ツールの動作確認
    Tool: Bash
    Steps:
      1. python scripts/diff_localization.py --summary を実行
      2. 出力にカテゴリ別のカバレッジ率が含まれることを確認
      3. python scripts/diff_localization.py --missing-only --json-path /tmp/missing.json
      4. python -m json.tool /tmp/missing.json でJSON妥当性確認
    Expected Result: サマリーにカバレッジ率表示、JSON出力が有効
    Evidence: .sisyphus/evidence/task-15-diff-tool.txt

  Scenario: XML検証ツールのエラー検出能力
    Tool: Bash
    Steps:
      1. python scripts/validate_xml.py Mods/QudJP/Localization/Options.jp.xml で正常ファイル検証
      2. 一時ファイルに不正XML（閉じタグなし）を作成して検証実行
      3. 不正XMLで exit code != 0 を確認
    Expected Result: 正常XMLはパス、不正XMLはエラー検出して非ゼロ終了
    Evidence: .sisyphus/evidence/task-15-validate-error.txt
  ```

  **Commit**: YES
  - Message: `feat(scripts): add diff and validation tooling`

- [ ] 16. メッセージログ翻訳 (辞書置換方式) + L1/L2テスト

  **What to do**:
  - **Phase 1 スコープ**: 辞書置換方式によるメッセージログ翻訳（XDidY完全置換はPhase 2）
  - ILSpy Hook: `MessageQueue.AddPlayerMessage(string Message, string Color, bool Urgent)` — Prefix
    - メッセージ文字列を辞書で検索し、一致すれば日本語に置換
    - 部分一致・テンプレート方式：プレースホルダ（`{0}`, `{1}` 等）付きパターンマッチ
  - 戦闘ログの基本パターン翻訳（最低30パターン）:
    - "You hit X for Y damage" → "Xに{1}ダメージを与えた"
    - "You miss X" → "Xへの攻撃をはずした"
    - "X attacks you" → "Xがあなたを攻撃した"
    - 状態異常メッセージ（confused, stunned, poisoned等）
  - **L1テスト**: 辞書置換ロジックのPureテスト（パターンマッチ、プレースホルダ展開、未登録パターン）
  - **L2テスト**: DummyMessageQueue にPrefix適用し、メッセージが翻訳されることを検証
  - メッセージパターン辞書ファイル（JSON）の設計と初期データ作成

  **Must NOT do**:
  - `Messaging.XDidY()` / `XDidYToZ()` の完全SOV変換はしない（Phase 2スコープ）
  - Grammar系のフル対応はしない（Task 11 の8メソッドのみ）
  - パターン辞書を過剰に増やさない（基本30パターンで MVP）

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 6 (with Task 14)
  - **Blocks**: Tasks 17, 18
  - **Blocked By**: Tasks 4, 11

  **References**:
  - ILSpy解析: `docs/ilspy-analysis.md` — MessageQueue.AddPlayerMessage シグネチャ
  - ILSpy解析: Messaging.XDidY/XDidYToZ — SVO構造の詳細（Phase 2参照用）
  - Task 6 テストハーネス — DummyMessageQueue
  - Task 11 Grammar パッチ — 冠詞除去等がメッセージにも影響

  **Acceptance Criteria**:
  - [ ] `dotnet test --filter TestCategory=L1` でメッセージ辞書テスト全パス（最低15テスト）
  - [ ] `dotnet test --filter TestCategory=L2` でメッセージパッチテスト全パス（最低5テスト）
  - [ ] 基本戦闘ログ30パターン以上が辞書に登録
  - [ ] 未登録パターンは英語のまま通過（クラッシュしない）

  **QA Scenarios**:
  ```
  Scenario: L1テスト — メッセージ辞書置換
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~MessageLog" --filter TestCategory=L1 -v normal
      2. テスト結果にパターンマッチ、プレースホルダ展開、未登録パターンのテストが含まれることを確認
    Expected Result: 全L1テストパス（15テスト以上）
    Evidence: .sisyphus/evidence/task-16-message-l1.txt

  Scenario: L2テスト — MessageQueue パッチ
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~MessageQueue" --filter TestCategory=L2 -v normal
    Expected Result: DummyMessageQueue へのPrefix適用テストパス
    Evidence: .sisyphus/evidence/task-16-message-l2.txt

  Scenario: L3確認 — 戦闘ログ
    Tool: Bash (ゲーム操作は手動)
    Steps:
      1. python scripts/sync_mod.py でデプロイ
      2. ゲーム起動→戦闘実行後 Player.log 取得
      3. grep -i "QudJP.*error\|QudJP.*exception" Player.log でQudJP関連エラー確認（他mod/ゲーム本体のエラーを除外）
    Expected Result: QudJP関連エラー 0件
    Evidence: .sisyphus/evidence/task-16-message-log.txt
  ```

  **Commit**: YES
  - Message: `feat(harmony): add message log localization with L1/L2 tests`

### Phase 3: ポリッシュ & リリース (Wave 7-9)

- [ ] 17. プロシージャルテキスト初期対応 (HistoricStringExpander)

  **What to do**:
  - ILSpy Hook: `HistoricStringExpander.ExpandString(string, ...)` — Postfix
    - ILSpy解析: 正常パスはPure（Debug.LogErrorのみUnity依存、エラーパスのみ）
  - Grammar系テキスト生成の日本語対応（Task 11 の8メソッドで基盤済み）
  - `world-gospels.ja.json` 等のHistoricStringExpander系辞書の有効化
  - 最低限の動的テキスト翻訳（Sultan歴史テキスト等）
  - 対応困難な箇所の文書化（`docs/procedural-text-status.md`）
  - **L1テスト**: HistoricStringExpander の展開パターンテスト（Pure部分のみ）
  - **L2テスト**: DummyHistoricStringExpander にPostfix適用テスト

  **Must NOT do**:
  - 完全対応は目指さない（段階的アプローチ）
  - 英語文法構造を壊さない（安全な範囲で翻訳）
  - `Messaging.XDidY` の完全SOV変換はしない

  **Recommended Agent Profile**:
  - **Category**: `ultrabrain`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 7
  - **Blocks**: Task 18
  - **Blocked By**: Tasks 4, 11, 16

  **References**:
  - ILSpy解析: `docs/ilspy-analysis.md` — HistoricStringExpander.ExpandString() シグネチャと内部ロジック
  - ILSpy解析: 正常パスはPure、Debug.LogErrorのみUnity依存
  - Task 11 Grammar パッチ — 8メソッドの基盤
  - レガシー辞書: world-gospels.ja.json 等（Phase 2 マーク付きで移行済み）

  **Acceptance Criteria**:
  - [ ] HistoricStringExpander のフックが正常動作
  - [ ] 一部の動的テキストが日本語で表示
  - [ ] `docs/procedural-text-status.md` に対応状況が文書化
  - [ ] `dotnet test` で HSE 関連テストパス

  **QA Scenarios**:
  ```
  Scenario: L1テスト — HistoricStringExpander
    Tool: Bash
    Steps:
      1. dotnet test --filter "FullyQualifiedName~HistoricString" -v normal
    Expected Result: Pure部分のテストパス
    Evidence: .sisyphus/evidence/task-17-hse-l1.txt

  Scenario: 文書化の完全性
    Tool: Bash
    Steps:
      1. test -f docs/procedural-text-status.md
      2. grep -c "対応済み\|未対応\|部分対応" docs/procedural-text-status.md
    Expected Result: ファイル存在、3種類以上のステータス分類
    Evidence: .sisyphus/evidence/task-17-hse-docs.txt
  ```

  **Commit**: YES
  - Message: `feat(harmony): initial procedural text localization support`

- [ ] 18. フルゲームプレイテスト

  **What to do**:
  - **全自動テスト実行**: `dotnet test` + `pytest` + `ruff check` + encoding check
  - 新規ゲーム開始 → キャラクター作成 → チュートリアル → Joppa → 基本冒険のフル通しテスト
  - 各画面・UIのスクリーンショット撮影
  - 未翻訳テキストの洗い出し
  - レイアウト崩れ・文字化け・Missing glyphの特定
  - Player.log の完全レビュー
  - 発見した問題のIssue化

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 8
  - **Blocks**: Task 20
  - **Blocked By**: Tasks 12, 13, 14, 16, 17

  **Acceptance Criteria**:
  - [ ] `dotnet test` 全パス（50テスト以上）
  - [ ] チュートリアル完走可能
  - [ ] 主要UI画面のスクリーンショット全取得
  - [ ] 重大なレイアウト崩れ・文字化けなし

  **QA Scenarios**:
  ```
  Scenario: 全自動テスト実行
    Tool: Bash
    Steps:
      1. dotnet test Mods/QudJP/Assemblies/QudJP.Tests/ -v normal
      2. pytest scripts/tests/
      3. ruff check scripts/
      4. python scripts/check_encoding.py Mods/QudJP/Localization/
    Expected Result: 全コマンド exit code 0, テスト50件以上パス
    Evidence: .sisyphus/evidence/task-18-all-tests.txt

  Scenario: Player.logの完全レビュー
    Tool: Bash (ゲーム操作は手動)
    Steps:
      1. cp ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log .sisyphus/evidence/task-18-player-full.log
      2. grep -c "QudJP.*error\|QudJP.*exception" .sisyphus/evidence/task-18-player-full.log
      3. grep -c "Missing glyph" .sisyphus/evidence/task-18-player-full.log
      4. screencapture -x .sisyphus/evidence/task-18-gameplay.png
    Expected Result: QudJP関連error 0件, Missing glyph 0件
    Evidence: .sisyphus/evidence/task-18-player-full.log, .sisyphus/evidence/task-18-gameplay.png
  ```

  **Commit**: YES
  - Message: `test: full gameplay test with evidence capture`

- [ ] 19. ドキュメント整備

  **What to do**:
  - `README.md` 完成版（インストール方法、ビルド方法、テスト実行方法、スクリーンショット）
  - `docs/translation-process.md` — 新規翻訳の追加方法
  - `docs/glossary.csv` の最終レビュー
  - `docs/test-architecture.md` — 3層テストアーキテクチャの解説（L1/L2/L3の目的・書き方）
  - `CHANGELOG.md` 初版
  - `docs/contributing.md` — コントリビューション方法
  - `scripts/README.md` — スクリプトの使い方

  **Recommended Agent Profile**:
  - **Category**: `writing`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 8 (with Task 18)
  - **Blocks**: Task 20
  - **Blocked By**: Tasks 8, 15

  **Acceptance Criteria**:
  - [ ] README.md にインストール・ビルド・テスト手順が記載
  - [ ] docs/test-architecture.md に3層テストの解説がある
  - [ ] 新規コントリビューターが翻訳を追加する手順が文書化

  **QA Scenarios**:
  ```
  Scenario: READMEの必須セクション確認
    Tool: Bash
    Steps:
      1. grep -c "## インストール\|## Install" README.md
      2. grep -c "## ビルド\|## Build" README.md
      3. grep -c "dotnet test" README.md
      4. test -f docs/test-architecture.md
      5. test -f docs/contributing.md
    Expected Result: 全セクション・ファイル存在
    Evidence: .sisyphus/evidence/task-19-docs-check.txt
  ```

  **Commit**: YES
  - Message: `docs: complete project documentation`

- [ ] 20. GitHub リリース準備

  **What to do**:
  - GitHub リポジトリ設定（description, topics, license）
  - `.github/` ディレクトリ（Issue templates, PR template）
  - リリースビルドスクリプト（Mod配布用ZIP作成）
  - GitHub Release v0.1.0 の準備（タグ、リリースノート）
  - 最終的な `dotnet build -c Release` + `dotnet test` + 全テスト通過確認
  - 対象ゲームバージョンの明記

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: [`git-master`]

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 9（最終）
  - **Blocks**: None（最終タスク）
  - **Blocked By**: Tasks 18, 19

  **Acceptance Criteria**:
  - [ ] `dotnet build -c Release` 成功
  - [ ] `dotnet test` 全パス
  - [ ] 配布用ZIPが正しいファイル構成
  - [ ] GitHubリポジトリが公開準備完了

  **QA Scenarios**:
  ```
  Scenario: リリースビルドの完全性
    Tool: Bash
    Steps:
      1. dotnet build Mods/QudJP/Assemblies/QudJP.csproj -c Release
      2. dotnet test Mods/QudJP/Assemblies/QudJP.Tests/
      3. pytest scripts/tests/
      4. ruff check scripts/
      5. python scripts/check_encoding.py Mods/QudJP/Localization/
    Expected Result: 全コマンド exit code 0
    Evidence: .sisyphus/evidence/task-20-release-build.txt

  Scenario: 配布ZIP構成の検証
    Tool: Bash
    Steps:
      1. リリーススクリプト実行でZIP生成
      2. unzip -l dist/QudJP-v0.1.0.zip でZIP内ファイル一覧確認
      3. manifest.json, QudJP.dll, Localization/ が含まれることを確認
      4. references/, .git/, __pycache__/, QudJP.Tests/ が含まれないことを確認
    Expected Result: 必須ファイルあり、除外ファイルなし
    Evidence: .sisyphus/evidence/task-20-zip-contents.txt
  ```

  **Commit**: YES
  - Message: `chore: prepare v0.1.0 release`

---

## Final Verification Wave

- [ ] F1. **Plan Compliance Audit** — `deep`

  **What to do**:
  Read the plan end-to-end. For each "Must Have": verify implementation exists. For each "Must NOT Have": search codebase for forbidden patterns. Check evidence files exist. Verify `dotnet test` passes.

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **QA Scenarios**:
  ```
  Scenario: Must Have / Must NOT Have 検証
    Tool: Bash
    Steps:
      1. dotnet test Mods/QudJP/Assemblies/QudJP.Tests/ -v normal でテスト全パス確認
      2. grep -r "using HarmonyLib" Mods/QudJP/Assemblies/QudJP.Tests/L1/ で L1 層分離違反チェック（0件期待）
      3. grep -r "using UnityEngine" Mods/QudJP/Assemblies/QudJP.Tests/L2/ で L2 層分離違反チェック（0件期待）
      4. python scripts/check_encoding.py Mods/QudJP/Localization/ でエンコーディング検証
      5. find .sisyphus/evidence -name "task-*" | wc -l でエビデンスファイル数確認
      6. cat manifest.json で Mod ID 確認
    Expected Result: テスト全パス、層分離違反 0件、エンコーディングOK、エビデンスファイル存在
    Failure Indicators: テスト失敗、層分離違反検出、エンコーディングエラー
    Evidence: .sisyphus/evidence/F1-compliance-audit.txt
  ```
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | Tests [N pass] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`

  **What to do**:
  Run `dotnet build` + `dotnet test` + linter + `pytest`. Review all changed files for: empty catches, commented-out code, unused imports. Check AI slop patterns. Verify test layer isolation.

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **QA Scenarios**:
  ```
  Scenario: ビルド・テスト・リント全実行
    Tool: Bash
    Steps:
      1. dotnet build Mods/QudJP/Assemblies/QudJP.csproj -c Release -warnaserror
      2. dotnet test Mods/QudJP/Assemblies/QudJP.Tests/ -v normal
      3. ruff check scripts/
      4. pytest scripts/tests/
      5. grep -rn "catch\s*{" Mods/QudJP/Assemblies/src/ で空catchブロック検出（0件期待）
      6. grep -rn "// TODO\|// HACK\|// FIXME" Mods/QudJP/Assemblies/src/ で未解決コメント検出
    Expected Result: ビルド成功(warning 0)、テスト全パス、lint 0エラー、空catch 0件
    Failure Indicators: ビルドwarning、テスト失敗、lintエラー、空catchブロック検出
    Evidence: .sisyphus/evidence/F2-code-quality.txt
  ```
  Output: `Build [PASS/FAIL] | Tests [N pass/N fail] | Lint [PASS/FAIL] | Layer Isolation [PASS/FAIL] | VERDICT`

- [ ] F3. **Real Manual QA** — `unspecified-high`

  **What to do**:
  エージェントがModデプロイ + Player.logクリアを実行。ゲーム操作は手動で実施。各画面でスクリーンショット取得。

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **QA Scenarios**:
  ```
  Scenario: フルゲームQA (ゲーム操作は手動)
    Tool: Bash
    Steps:
      1. python scripts/sync_mod.py でModデプロイ
      2. > ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log でログクリア
      3. ゲーム起動・操作（手動）: main menu → options → new game → character creation → Joppa → NPC会話 → inventory
      4. 各画面で screencapture -x .sisyphus/evidence/final-qa/{screen-name}.png
      5. cp ~/Library/Logs/Freehold\ Games/CavesOfQud/Player.log .sisyphus/evidence/final-qa/player.log
      6. grep -c "QudJP.*error\|QudJP.*exception" .sisyphus/evidence/final-qa/player.log
      7. grep -c "Missing glyph" .sisyphus/evidence/final-qa/player.log
    Expected Result: スクリーンショット6枚以上、QudJP関連error 0件、Missing glyph 0件
    Failure Indicators: Missing glyph > 0、QudJP例外検出、日本語テキストが□で表示
    Evidence: .sisyphus/evidence/final-qa/
  ```
  Output: `Scenarios [N/N pass] | Screenshots [N captured] | Log Errors [N] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`

  **What to do**:
  Verify: no legacy code copied, no AI-slop patterns, encoding is UTF-8/LF throughout, all XML parses cleanly, test layer isolation, DummyTarget discipline, TDDサイクル遵守（テストが実装より先に書かれている commit history 確認）.

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []

  **QA Scenarios**:
  ```
  Scenario: スコープ忠実性の検証
    Tool: Bash
    Steps:
      1. find Mods/QudJP/Localization -name "*.xml" -exec python -c "import xml.etree.ElementTree as ET; ET.parse('{}')" \; で全XMLパース
      2. find Mods/QudJP/Localization -name "*.xml" -exec file {} \; | grep -v "UTF-8" でエンコーディング違反チェック（0件期待）
      3. grep -rn "new.*GameObject\|new.*XRLCore\|new.*GameManager" Mods/QudJP/Assemblies/QudJP.Tests/ でAssembly-CSharp直接インスタンス化チェック（0件期待）
      4. TDD遵守確認: Harmonyパッチ(.cs)ごとに対応するテストファイルが存在するか確認。find Mods/QudJP/Assemblies/src/Patches -name "*Patch.cs" でパッチファイル一覧を取得し、各パッチに対応する *Test.cs or *Tests.cs がQudJP.Tests/内に存在することを確認
      5. テストカバレッジ確認: dotnet test 実行時のテスト数が各パッチタスクのAcceptance Criteriaの合計（50テスト以上）を満たすことを確認
    Expected Result: 全XMLパース成功、エンコーディング違反0件、直接インスタンス化0件、全パッチに対応テスト存在、テスト数50以上
    Failure Indicators: XMLパースエラー、UTF-8以外のファイル検出、DummyTarget規律違反、テストなしパッチの存在
    Evidence: .sisyphus/evidence/F4-scope-fidelity.txt
  ```
  Output: `Tasks [N/N compliant] | Encoding [CLEAN/N issues] | Test Isolation [CLEAN/N violations] | VERDICT`

---

## Commit Strategy

- タスク完了ごとにコミット
- Conventional Commits: `feat(scope): desc` / `fix(scope): desc` / `test(scope): desc` / `chore(scope): desc`
- コミットメッセージは英語
- テスト追加は `test(scope):` プレフィックス

---

## Success Criteria

### Verification Commands
```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj           # Expected: Build succeeded
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/             # Expected: All tests pass (50+)
dotnet test --filter TestCategory=L1                        # Expected: Pure logic tests pass
dotnet test --filter TestCategory=L2                        # Expected: Harmony integration tests pass
pytest scripts/tests/                                       # Expected: All tests pass
ruff check scripts/                                         # Expected: No issues
python scripts/check_encoding.py Mods/QudJP/Localization/  # Expected: No issues found
python scripts/diff_localization.py --summary               # Expected: Coverage report
```

### Final Checklist
- [ ] `dotnet test` — 全L1/L2テストパス（50テスト以上）
- [ ] Options画面が完全に日本語化
- [ ] キャラクター作成が日本語でプレイ可能
- [ ] Joppa開始 → 基本NPCとの会話が日本語
- [ ] アイテム名・スキル名が日本語表示
- [ ] Player.log に Missing glyph / エンコーディングエラーなし
- [ ] `dotnet build` が macOS で成功
- [ ] テスト層分離が守られている（L1にHarmonyLib依存なし、L2にUnity依存なし）
- [ ] All "Must NOT Have" absent
