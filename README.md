# Caves of Qud Japanese Localization (QudJP)

> **Status**: Active development (主開発リポジトリ)
>
> **対象ゲームライン**: Caves of Qud stable 1.0 (v2.0.4)
>
> experimental branch (`lang-experimental`, build 212.x 系) は別リポジトリ [`ToaruPen/CoQ-Japanese_v2`](https://github.com/ToaruPen/CoQ-Japanese_v2) で observation mode 追跡中。

## Why

Caves of Qud は完全な英語ローカライゼーション API が整備される前から運用されてきたタイトルで、UI / quest / conversation / 自動生成テキストを日本語化するには Harmony patch を主軸としたアプローチが必要。本リポジトリは stable 1.0 mainline ユーザーが実際にプレイできる翻訳状態を維持することを最優先する。

experimental ブランチ (`lang-experimental`) の新ローカライゼーションフレームワーク (`Strings/_T/_S` / `[LanguageProvider]` / `ExampleLanguage` 等) は 2026-04-07 時点で early alpha 段階であることが判明しており、詳細な一次調査結果と observation mode 復帰の意思決定は v2 リポジトリの [`docs/snapshots/2026-04-07-beta-l10n-status.md`](https://github.com/ToaruPen/CoQ-Japanese_v2/blob/main/docs/snapshots/2026-04-07-beta-l10n-status.md) / [`docs/decisions/0001-v1-v2-roles.md`](https://github.com/ToaruPen/CoQ-Japanese_v2/blob/main/docs/decisions/0001-v1-v2-roles.md) を参照。

## What This Repo Is For

- Caves of Qud stable 1.0 (v2.0.4) ユーザー向け日本語化 Mod の開発と出荷
- 会話 / UI / quest / 自動生成テキスト / 装備名 / 能力名 / 書籍 等の翻訳資産の保守
- Harmony patch 群 + Markov コーパス + 翻訳パイプラインスクリプトの維持
- CJK フォント同梱

## What It Is Not For

- experimental branch (`lang-experimental`, 212.x 系) ターゲットの開発 (→ v2)
- 新ローカライゼーション API (`Strings/_T/_S` / `[LanguageProvider]`) への先行移植
- 英語以外の他言語サポート
- stable 1.0 mainline の挙動を破壊する実験的リファクタ

## Docs

| ドキュメント | 役割 |
|---|---|
| [`docs/RULES.md`](docs/RULES.md) | ワークフロー / route ownership 規約 / evidence order |
| [`docs/test-architecture.md`](docs/test-architecture.md) | 3 層 + L3 テストアーキテクチャ定義 |
| [`docs/contributing.md`](docs/contributing.md) | 貢献ガイド |
| [`docs/deployment.md`](docs/deployment.md) | デプロイ手順 |
| [`docs/glossary.csv`](docs/glossary.csv) | 翻訳用語集 |
| [`docs/static-producer-inventory.json`](docs/static-producer-inventory.json) | issue #493 static producer inventory |
| [`docs/reports/2026-05-05-issue-493-static-producer-inventory.md`](docs/reports/2026-05-05-issue-493-static-producer-inventory.md) | static producer inventory report |
| [`CHANGELOG.md`](CHANGELOG.md) | リリース履歴 |

## Development / Verification

- C# テストは NUnit、L1 / L2 / L2G の 3 層構成 + 手動 L3 ([`docs/test-architecture.md`](docs/test-architecture.md))
- Python ツールは pytest + Ruff + ast-grep
- 静的解析: Roslyn analyzer suite (`QudJP.Analyzers`) に独自規約 QJ001 / QJ002 / QJ003 を加えて強制
- CI: GitHub Actions (Ubuntu 24.04 / .NET 8 + 10 / Python 3.12) で push / PR ごとに build + test
- コードレビュー: CodeRabbit

## Related Repo

- [`ToaruPen/CoQ-Japanese_v2`](https://github.com/ToaruPen/CoQ-Japanese_v2) — **v2, observation mode (frozen 2026-04-07)**, Caves of Qud experimental branch (`lang-experimental`, build 212.x) 追跡用

## License

QudJP は [MIT License](LICENSE) の下で配布されています。
Copyright (c) 2026 ToaruPen

同梱の Noto Sans CJK JP サブセットは SIL Open Font License 1.1 を継承します。
その他の同梱物・依存関係・ゲーム本編との関係については [`NOTICE.md`](NOTICE.md) を参照してください。

Caves of Qud 本編の権利は Freehold Games に帰属します。QudJP は Freehold Games とは無関係の独立したコミュニティ Mod です。
