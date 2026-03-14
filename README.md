# Caves of Qud Japanese Localization (QudJP)

Caves of Qud の会話・UI・自動生成テキストを日本語化し、CJK フォントを同梱する Mod です。

[![CI](https://github.com/ToaruPen/Caves-of-Qud_Japanese/actions/workflows/ci.yml/badge.svg)](https://github.com/ToaruPen/Caves-of-Qud_Japanese/actions/workflows/ci.yml)

> 🚧 **開発中** — まだプレイ可能な状態ではありません。

---

## Requirements

| ツール | バージョン |
|--------|-----------|
| Caves of Qud | v2.0.4 以上 |
| .NET SDK | 10.0 以上 |
| Python | 3.12 以上 |

ゲーム本体を所有していること（Steam 版）が必須です。`Assembly-CSharp.dll` はリポジトリに含まれていません。

Apple Silicon (M1/M2/M3/M4) で runtime 観測や L3 手動確認を行う場合は、**Rosetta 起動を必須**とします。
native ARM64 の Harmony 実行結果は観測証跡として扱いません。`scripts/launch_rosetta.sh` または
`Launch CavesOfQud (Rosetta).command` を使って起動してください。

---

## Install

1. DLL をビルドする（下記 **Build** 参照）
2. `Mods/QudJP/` フォルダをゲームの Mods ディレクトリにコピーする

   ```
   # macOS (Steam)
   ~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/
   ```

3. ゲームを起動し、Mod マネージャーで **"Caves of Qud 日本語化"** を有効にする

---

## Build

### C# Mod DLL

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

ビルド成果物 `QudJP.dll` は `Mods/QudJP/Assemblies/` に出力されます。

### Python ツール（ビルド不要）

Python スクリプトはそのまま実行できます。依存パッケージは `pyproject.toml` に記載されています。

---

## Test

### C# テスト

```bash
# 全テスト実行
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj

# L1 のみ（純粋ロジック、高速）
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1

# L2 のみ（Harmony 統合）
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2

# L2G のみ（game-DLL-assisted target resolution / hook inventory）
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G
```

現在の C# テスト数: **185 件**（L1: 75、L2: 85、L2G: 25）

### Python テスト

```bash
pytest scripts/tests/
```

現在のテスト数: **83 件**

---

## Lint

### C#

`TreatWarningsAsErrors` が有効です。ビルドが通れば警告ゼロが保証されます。

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
```

### Python

```bash
ruff check scripts/
```

Ruff は `select = ["ALL"]` で全ルールを有効にしています。

---

## Project Structure

```
Caves-of-Qud_Japanese/
├── Mods/QudJP/
│   ├── Assemblies/
│   │   ├── src/                  # C# 本番コード（Translator, Patches など）
│   │   ├── QudJP.csproj          # net48 Mod DLL プロジェクト
│   │   └── QudJP.Tests/          # net10.0 テストプロジェクト
│   ├── Localization/
│   │   ├── *.jp.xml              # XML 翻訳ファイル（ゲームの Merge システム使用）
│   │   └── Dictionaries/         # UI テキスト用 JSON 辞書
│   └── manifest.json             # Mod メタデータ
├── scripts/
│   ├── check_encoding.py         # エンコーディング検証
│   ├── validate_xml.py           # XML 構造検証
│   ├── diff_localization.py      # 翻訳カバレッジ比較
│   ├── extract_base.py           # ゲーム XML から翻訳対象を抽出
│   ├── sync_mod.py               # Mod ファイルをゲームディレクトリに配備
│   └── tests/                    # pytest テストスイート
├── docs/
│   ├── test-architecture.md      # 3 層テストアーキテクチャの説明
│   ├── translation-process.md    # 翻訳追加手順
│   ├── contributing.md           # コントリビューションガイド
│   └── glossary.csv              # 用語集（84 エントリ）
├── .github/workflows/ci.yml      # GitHub Actions CI
└── pyproject.toml                # Python プロジェクト設定
```

---

## Documentation

- [テストアーキテクチャ](docs/test-architecture.md) — L1/L2/L2G/L3 の役割、game-DLL-assisted TDD、DummyTarget の使い分け
- [翻訳追加手順](docs/translation-process.md) — XML 翻訳ファイルの作り方
- [コントリビューションガイド](docs/contributing.md) — 開発環境セットアップ、コーディング規約、PR フロー
- [スクリプトガイド](scripts/README.md) — Python ツールの使い方

---

## License

TBD
