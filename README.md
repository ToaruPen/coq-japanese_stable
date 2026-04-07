# Caves of Qud Japanese Localization (QudJP) ~~— Legacy~~

> ~~**このリポジトリはアーカイブ済みです。**~~
> ~~開発は後継リポジトリに移行しました。~~

## ~~移行先~~

### ~~[ToaruPen/CoQ-Japanese_v2](https://github.com/ToaruPen/CoQ-Japanese_v2)~~

~~v2 はベータ版ローカライゼーションパイプラインに対応したグリーンフィールド再設計です。~~

~~主な改善点:~~

- ~~ベータ版の `_S/_T`・`GameText`・`ReplaceBuilder`・`[LanguageProvider]` パイプラインに最適化~~
- ~~テスト駆動開発 (TDD): デコンパイルソースを忠実に再現する DummyTargets パターン~~
- ~~434+ L1 テスト、64+ Python テスト~~
- ~~Harmony パッチは最終手段 — ベータネイティブ拡張ポイントを優先~~

---

> **2026-04-07 更新**: まだしばらくはアルファ止まりな可能性があるため、Stable版の開発を続けます。適宜更新を確認しつつ、段階的に移行していきたいと思う。
>
> ベータブランチ調査の詳細は [`CoQ-Japanese_v2`](https://github.com/ToaruPen/CoQ-Japanese_v2) の README を参照。

---

## このリポジトリについて

QudJP は **Caves of Qud 安定版 (1.0 mainline / v2.0.4)** を対象とした日本語ローカライゼーション Mod です。会話・UI・自動生成テキストを日本語化し、CJK フォントを同梱します。

ベータブランチ (212.x experimental, `lang-experimental`) には新しい `Strings/_T/_S` / `[LanguageProvider]` / `ExampleLanguage` API が用意されていますが、2026-04-07 時点の調査では実装は **early alpha 段階** で、load-bearing な API 経路は依然として旧 `Grammar.*` + 拡張メソッド (`.t()` / `.Does()` / `.an()` 等、約 910 箇所、新 API の ~48 倍) です。そのため stable 1.0 を主軸とする本リポジトリ (v1) を継続開発し、ベータブランチ追従用の [`CoQ-Japanese_v2`](https://github.com/ToaruPen/CoQ-Japanese_v2) は観察モードで併存させます。

## テスト・開発体制 (2026-04-07 時点)

### 3 層 + L3 テストアーキテクチャ

詳細は [`docs/test-architecture.md`](docs/test-architecture.md) を参照。

| 層 | 名称 | 目的 | HarmonyLib | Assembly-CSharp.dll | UnityEngine | タグ |
|---|---|---|---|---|---|---|
| L1 | 純粋ロジック | ゲーム/Harmony 非依存の C# ロジック検証 | 禁止 | 禁止 | 不要 | `[Category("L1")]` |
| L2 | Harmony 統合 (DummyTarget) | パッチ本文の文字列変換結果検証 | 使用 | 使用可 | 不要 | `[Category("L2")]` |
| L2G | game-DLL-assisted | 実 DLL 上での target/シグネチャ解決検証 | 使用 | 使用 | 不要 | `[Category("L2G")]` |
| L3 | ゲームスモーク | Unity ランタイムでの実描画確認 | ゲーム同梱 | 使用 | 必要 | (手動) |

### C# テスト実態 (NUnit + xUnit)

| Project | 層 | テストファイル数 | `[Test]` メソッド | `[TestCase]` 件 | invocation 計 |
|---|---|---:|---:|---:|---:|
| `QudJP.Tests` | L1 | 52 | 381 | 627 | **1,008** |
| `QudJP.Tests` | L2 | 91 | 528 | 85 | **613** |
| `QudJP.Tests` | L2G | 5 | 14 | 153 | **167** |
| `QudJP.Analyzers.Tests` | (xUnit) | 3 | 12 | — | **12** |
| **合計** | | **151** | **935** | **865** | **1,800** |

### Python テスト実態 (pytest)

| テストファイル数 | テスト関数数 |
|---:|---:|
| **22** | **193** |

`scripts/tests/` 配下に、`validate_xml.py` / `diff_localization.py` / `build_release.py` / `triage_*.py` / `scanner_*.py` / `translate_corpus_batch.py` 等の Python ツール各種に対する unit + integration テスト一式。

### 全体合計

**約 1,993 test invocations** (C# NUnit + xUnit + Python pytest)。

### CI / 静的解析

- **GitHub Actions** ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)): Ubuntu 24.04 / .NET 8.0 + 10.0 / Python 3.12 で push と PR ごとに build + test を実行
- **Roslyn analyzers** (`QudJP.Analyzers/`): `EnableNETAnalyzers` + `AnalysisLevel=latest-all` + SonarAnalyzer.CSharp に加え、独自規約 **QJ001** (catch-all 抑制) / **QJ002** (TargetMethod 内 null 合体) / **QJ003** (空 catch ブロック) を強制
- **Lint**: Ruff (`select = ["ALL"]`) + ast-grep + Roslyn analyzer suite
- **コードレビュー**: CodeRabbit (`.coderabbit.yaml`)

## ランタイムコード規模

`Mods/QudJP/Assemblies/src/` 配下:

- **224 .cs ファイル / 約 43,670 行**
- うち `src/Patches/` は **187 個の Harmony patch ファイル**
- 主要ランタイムコンポーネント: `Translator.cs` / `MessagePatternTranslator.cs` (31 KB) / `MessageFrameTranslator.cs` (29 KB) / `JournalPatternTranslator.cs` (26 KB) / `TextShellReplacementRenderer.cs` (52 KB) / `TmpTextRepairer.cs` (23 KB) / `ColorAwareTranslationComposer.cs` / `ColorCodePreserver.cs` / 15+ "Observability" classes（テキスト emission の追跡パターン）

## 翻訳資産

`Mods/QudJP/Localization/` 配下に **約 1.6 MB の翻訳 XML** を出荷:

| ファイル | サイズ |
|---|---:|
| `Conversations.jp.xml` | **712 KB** |
| `Books.jp.xml` | 164 KB |
| `Worlds.jp.xml` | 153 KB |
| `Naming.jp.xml` | 150 KB |
| `HiddenConversations.jp.xml` | 111 KB |
| `ActivatedAbilities.jp.xml` | 80 KB |
| `Skills.jp.xml` | 47 KB |
| `Commands.jp.xml` | 45 KB |
| `Options.jp.xml` | 40 KB |
| `EmbarkModules.jp.xml` | 32 KB |
| `Quests.jp.xml` | 28 KB |
| `ChiliadFactions.jp.xml` | 26 KB |
| `Mutations.jp.xml` | 16 KB |
| `Subtypes.jp.xml` | 13 KB |
| `Mods.jp.xml` | 13 KB |
| (その他 8 ファイル) | ~30 KB |

加えて `scripts/` 配下に翻訳パイプライン (Markov コーパス `corpus_ja_translated.json` 1.2 MB / `reuse_manifest.json` 6.5 MB) と検証ツール群 (`validate_xml.py` / `diff_localization.py` / `triage_*.py` / `scanner_*.py` 等) を備えています。

## ドキュメント

- [`docs/RULES.md`](docs/RULES.md) — ワークフロー / route ownership 規約 / evidence order
- [`docs/test-architecture.md`](docs/test-architecture.md) — 3 層 + L3 テストアーキテクチャ定義
- [`docs/contributing.md`](docs/contributing.md) — 貢献ガイド
- [`docs/deployment.md`](docs/deployment.md) — デプロイ手順
- [`docs/glossary.csv`](docs/glossary.csv) — 翻訳用語集 (24 KB)
- [`docs/emit-message-coverage-audit.md`](docs/emit-message-coverage-audit.md) — emit-message coverage 監査結果
- [`CHANGELOG.md`](CHANGELOG.md) — リリース履歴

---

## License

TBD
