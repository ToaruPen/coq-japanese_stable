# Mod デプロイ手順

QudJP Mod をゲームにデプロイする手順です。

---

## 前提条件

- Caves of Qud がインストールされていること（Steam 版 macOS）
- `dotnet build` で `QudJP.dll` がビルド済みであること

---

## デプロイ方法

### 方法 1: sync_mod.py（推奨）

```bash
# ビルド → デプロイ
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
python scripts/sync_mod.py
```

`sync_mod.py` は rsync の include-first 戦略で、ゲームに必要なファイルのみをデプロイします。

**ドライラン**（実際にはコピーしない）:

```bash
python scripts/sync_mod.py --dry-run
```

**フォント除外**（フォントを変更していない場合に高速化）:

```bash
python scripts/sync_mod.py --exclude-fonts
```

### 方法 2: 手動コピー

```bash
GAME_MODS="$HOME/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods"

# 古いデプロイを削除
rm -rf "$GAME_MODS/QudJP"

# 必要なファイルのみコピー
mkdir -p "$GAME_MODS/QudJP/Assemblies"
cp Mods/QudJP/manifest.json "$GAME_MODS/QudJP/"
cp Mods/QudJP/Assemblies/QudJP.dll "$GAME_MODS/QudJP/Assemblies/"
cp -r Mods/QudJP/Localization "$GAME_MODS/QudJP/"
```

---

## デプロイされるファイル

ゲームに必要なのは以下の 3 種類のみです:

| ファイル | 役割 |
|---------|------|
| `manifest.json` | Mod メタデータ（ID、タイトル、DLL パス） |
| `Assemblies/QudJP.dll` | Harmony パッチ DLL（ビルド済みバイナリ） |
| `Localization/` | XML 翻訳ファイル + JSON 辞書 |

### デプロイしてはいけないファイル

| ファイル | 理由 |
|---------|------|
| `*.cs` | ゲームの Unity/Mono コンパイラが解釈を試みてエラーになる |
| `*.csproj`, `*.sln` | ビルド設定ファイル（ゲーム不要） |
| `*.pdb` | デバッグシンボル（ゲーム不要） |
| `bin/`, `obj/` | ビルドアーティファクト |
| `src/` | ソースコードディレクトリ |
| `QudJP.Tests/` | テストプロジェクト |
| `QudJP.Analyzers/` | Roslyn アナライザープロジェクト |
| `AGENTS.md` | 開発用ドキュメント |

> **重要**: ゲームの Mod システムは、Mod ディレクトリ内の `.cs` ファイルを自動的にコンパイルしようとします。QudJP は事前コンパイル済み DLL を使用するため、ソースファイルが存在すると C# 10+ 構文（`global using`、ファイルスコープ名前空間など）が古いコンパイラで解釈できずエラーになります。

---

## デプロイ先パス（macOS Steam）

```
~/Library/Application Support/Steam/steamapps/common/
  Caves of Qud/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP/
```

---

## デプロイ後の確認

1. ゲームを起動
2. Mod マネージャーで **「Caves of Qud 日本語化」** が表示されることを確認
3. ENABLED に設定
4. ゲームを再起動し、Options 画面が日本語で表示されることを確認

### トラブルシューティング

| 症状 | 原因 | 対処 |
|------|------|------|
| FAILED + CS8652/CS1514 エラー | `.cs` ファイルがデプロイされている | `sync_mod.py` で再デプロイ（ソース除外） |
| Mod が表示されない | `manifest.json` が配置されていない | デプロイ先に `manifest.json` があるか確認 |
| 日本語が □（豆腐）表示 | フォント未同梱 | Fonts ディレクトリの配置を確認 |
| DLL 読み込みエラー | `QudJP.dll` がビルドされていない | `dotnet build` を実行後に再デプロイ |

---

## L3 テスト（ゲーム内動作確認）

自動テスト（L1/L2）ではカバーできないゲーム内動作を手動で確認します:

- [ ] Mod マネージャーに「Caves of Qud 日本語化」が表示される
- [ ] Options 画面が日本語で表示される
- [ ] キャラクター作成画面が日本語化されている
- [ ] 日本語文字が □（豆腐）にならない
- [ ] Player.log に Missing glyph / エンコーディングエラーがない
