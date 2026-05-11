# Caves of Qud Japanese Localization (QudJP)

> **Status**: Active development (主開発リポジトリ)
>
> **対象ゲームバージョン**: Caves of Qud 1.0.4
>
> **Steam Workshop**: [`3718988020`](https://steamcommunity.com/sharedfiles/filedetails/?id=3718988020)

## Why

Caves of Qud は完全な英語ローカライゼーション API が整備される前から運用されてきたタイトルで、UI / quest / conversation / 自動生成テキストを日本語化するには Harmony patch を主軸としたアプローチが必要。本リポジトリは Caves of Qud 1.0.4 ユーザーが実際にプレイできる翻訳状態を維持することを最優先する。

## What This Repo Is For

- Caves of Qud 1.0.4 ユーザー向け日本語化 Mod の開発と出荷
- 会話 / UI / quest / 自動生成テキスト / 装備名 / 能力名 / 書籍 等の翻訳資産の保守
- Harmony patch 群、CJK フォント、翻訳辞書、検証・release tooling の維持
- フォント資産とライセンス情報の同梱管理

## What It Is Not For

- experimental branch (`lang-experimental` 系) ターゲットの開発 (→ v2)
- 新ローカライゼーション API (`Strings/_T/_S` / `[LanguageProvider]`) への先行移植
- 英語以外の他言語サポート
- Caves of Qud 1.0.4 の挙動を破壊する実験的リファクタ

## macOS Apple Silicon

M1/M2/M3/M4 などの Apple Silicon Mac では、Caves of Qud をネイティブ起動すると、ゲーム本体側の `0Harmony.dll` が原因で Harmony patch が `mprotect returned EACCES` により失敗し、QudJP の UI 翻訳が効かない場合があります。

QudJP では Rosetta 2 経由での起動を推奨します。QudJP は `0Harmony.dll` を自動では同梱・上書きしません。

Steam Workshop 版と GitHub Release ZIP には `QudJP/Launch CavesOfQud (Rosetta).command` を同梱しています。ダブルクリックで Caves of Qud を Rosetta 経由で起動できます。macOS の Steam 既定ライブラリでは、Workshop 版の wrapper は次の場所にあります。

```text
~/Library/Application Support/Steam/steamapps/workshop/content/333640/3718988020/Launch CavesOfQud (Rosetta).command
```

Steam ライブラリを別の場所に置いている場合は、そのライブラリ配下の `steamapps/workshop/content/333640/3718988020/` を探してください。GitHub Release ZIP を手動展開した場合は、展開した `QudJP/` フォルダ直下にあります。

Rosetta 2 が未インストールの場合、wrapper が macOS の確認ダイアログを表示します。ゲーム本体は、まず既定の Steam ライブラリと wrapper 自身が置かれている Workshop フォルダから同じ Steam ライブラリ内を探します。それでも見つからない場合だけ `CoQ.app` の選択画面を表示します。表示に従って進めれば、手動でターミナル操作をする必要はありません。

直接起動する場合のコマンドは次の通りです。このコマンドはワンショットで、Steam から普通に起動した場合の設定を永続変更しません。

```bash
arch -x86_64 $HOME/Library/Application\ Support/Steam/steamapps/common/Caves\ of\ Qud/CoQ.app/Contents/MacOS/CoQ
```

上級者向けの別手段として、ゲーム本体側の `0Harmony.dll` を Harmony 2.4.2 に差し替えると、Apple Silicon ネイティブ起動でも動作することを確認しています。通常は上記の Rosetta 起動を使ってください。

Steam Workshop 版と GitHub Release ZIP には、明示的に実行した場合だけこの差し替えを行う `Install Native Apple Silicon Harmony.command` と、元に戻す `Restore Game Harmony.command` を同梱しています。これらはまず配置場所から Caves of Qud を探し、見つからない場合は `0Harmony.dll` の選択画面を表示します。

手動差し替えを行う場合は、CoQ を終了し、[Harmony 2.4.2](https://github.com/pardeike/Harmony/releases/tag/v2.4.2.0) の `Harmony-Fat.2.4.2.0.zip` から `net48/0Harmony.dll` を取り出して、次のファイルをバックアップ後に置き換えます。

```text
~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll
```

これはゲーム本体ファイルの手動変更です。Steam の整合性確認・再インストール・ゲーム更新で元に戻る可能性があります。

## Current Release Sources

| 項目 | Source of truth |
|---|---|
| Mod metadata / version | [`Mods/QudJP/manifest.json`](Mods/QudJP/manifest.json) |
| Release history | [`CHANGELOG.md`](CHANGELOG.md) |
| Steam Workshop release procedure | [`docs/release.md`](docs/release.md) |
| Local deployment / smoke check | [`docs/deployment.md`](docs/deployment.md) |
| Public Workshop metadata | [`steam/workshop_metadata.json`](steam/workshop_metadata.json) |
| Public Workshop description | [`steam/workshop_description.ja.txt`](steam/workshop_description.ja.txt) |

## Docs

| ドキュメント | 役割 |
|---|---|
| [`docs/RULES.md`](docs/RULES.md) | ワークフロー / route ownership 規約 / evidence order |
| [`docs/test-architecture.md`](docs/test-architecture.md) | 3 層 + L3 テストアーキテクチャ定義 |
| [`docs/contributing.md`](docs/contributing.md) | 貢献ガイド |
| [`docs/deployment.md`](docs/deployment.md) | デプロイ手順 |
| [`docs/release.md`](docs/release.md) | GitHub Release / Steam Workshop 出荷手順 |
| [`docs/workflows/pr-review.md`](docs/workflows/pr-review.md) | PR レビュー手順 |
| [`docs/workflows/runtime-evidence.md`](docs/workflows/runtime-evidence.md) | runtime evidence 収集手順 |
| [`docs/glossary.csv`](docs/glossary.csv) | 翻訳用語集 |
| [`docs/static-producer-inventory.json`](docs/static-producer-inventory.json) | issue #493 static producer inventory |
| [`docs/reports/2026-05-05-issue-493-static-producer-inventory.md`](docs/reports/2026-05-05-issue-493-static-producer-inventory.md) | static producer inventory report |
| [`CHANGELOG.md`](CHANGELOG.md) | リリース履歴 |

## Development / Verification

- 開発コマンドは `justfile` に集約しています。初回は `just --list` で利用可能な recipe を確認してください。
- 通常の `just build` / `just deploy-mod` は shipping 相当の DLL を作成し、冗長な probe ログを出しません。probe が必要なローカル調査では `just build-dev` / `just deploy-dev` を使います。
- C# テストは NUnit、L1 / L2 / L2G の 3 層構成 + 手動 L3 ([`docs/test-architecture.md`](docs/test-architecture.md))
- Python ツールは pytest + Ruff + ast-grep
- 静的解析: Roslyn analyzer suite (`QudJP.Analyzers`) に独自規約 QJ001 / QJ002 / QJ003 を加えて強制
- CI: GitHub Actions で変更パスに応じて .NET build/test、Roslyn tool build、Python lint/test、localization checks、justfile parse check を実行
- Release: tag-triggered GitHub Actions が release ZIP と draft GitHub Release を作成し、Steam Workshop upload は `docs/release.md` の手動 gate に従う
- コードレビュー: CodeRabbit

## Related Repo

- [`ToaruPen/CoQ-Japanese_v2`](https://github.com/ToaruPen/CoQ-Japanese_v2) — Caves of Qud experimental branch (`lang-experimental` 系) と新ローカライゼーション API の追跡用

## License

QudJP は [MIT License](LICENSE) の下で配布されています。
Copyright (c) 2026 ToaruPen

同梱の Noto Sans CJK JP サブセットは SIL Open Font License 1.1 を継承します。
その他の同梱物・依存関係・ゲーム本編との関係については [`NOTICE.md`](NOTICE.md) を参照してください。

Caves of Qud 本編の権利は Freehold Games に帰属します。QudJP は Freehold Games とは無関係の独立したコミュニティ Mod です。
