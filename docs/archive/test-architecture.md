# テストアーキテクチャ

QudJP のテストは **3 層構造** を維持しつつ、L2 を `game-DLL-assisted TDD` に拡張して運用します。
目的は、`Assembly-CSharp.dll` の実シグネチャと実メソッド解決を自動検証に取り込みながら、Unity ランタイム依存の表示確認だけを L3 に残すことです。

---

## 層の概要

| 層 | 名称 | HarmonyLib | Assembly-CSharp.dll | UnityEngine 実ランタイム | 実行環境 | タグ |
|----|------|-----------|---------------------|--------------------------|---------|------|
| L1 | 純粋ロジック | 禁止 | 禁止 | 不要 | CI / ローカル | `[Category("L1")]` |
| L2 | Harmony 統合 / DummyTarget | NuGet 2.4.2 | 使用可 | 不要 | CI / ローカル | `[Category("L2")]` |
| L2G | game-DLL-assisted hook inventory | NuGet 2.4.2 | 使用可 | 不要 | ローカル中心 / 条件付き CI | `[Category("L2G")]` |
| L3 | ゲームスモーク | ゲーム同梱版 | 使用可 | 必要 | 手動のみ | なし |

---

## L1 — 純粋ロジック

**目的**: ゲームや Harmony に依存しない純粋な C# ロジックを検証する。

**制約**:
- `using HarmonyLib` を含めない
- `using UnityEngine` を含めない
- `Assembly-CSharp.dll` の型を参照しない

**対象コード**:
- `Translator`
- `ColorCodePreserver`
- 純粋な文字列変換ロジック

**実行コマンド**:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
```

---

## L2 / L2G — Harmony 統合

**目的**: Harmony パッチの target 解決、シグネチャ整合、翻訳ロジック適用を自動検証する。

L2 層は 2 つのカテゴリに分かれます。

### L2G — game-DLL-assisted (`[Category("L2G")]`)

`Assembly-CSharp.dll` を参照し、実ゲームの型名・メソッド名・シグネチャ・static メソッド・副作用の軽い処理を直接検証します。

**このモードで優先して検証するもの**:
- `HarmonyTargetMethod` / `HarmonyTargetMethods` が実 DLL 上で正しいメソッドを解決できるか
- 実ゲーム DLL 由来の static メソッドや、Unity ランタイムなしで安全に呼べる処理
- ILSpy 解析で得たフック候補が現行ゲーム 2.0.4 の実 DLL と一致しているか

**制約**:
- `Assembly-CSharp.dll` の型を参照してよい
- `using UnityEngine` を含めない
- `Assembly-CSharp.dll` の型をテスト内で直接 instantiate しない
- 画面表示や TMP/UGUI の実描画結果は L3 に残す

**推奨例**:
- `AccessTools.Method("XRL.UI.Look:GenerateTooltipContent")` の解決確認
- `ConsoleLib.Console.Markup` の static API 解決確認
- private `TargetMethod()` の反射呼び出し検証

### L2 — DummyTarget (`[Category("L2")]`)

実 DLL の安全な直接実行が難しい場合は、従来どおり同一シグネチャの DummyTarget にパッチを当てて、Prefix/Postfix のロジックを検証します。

**このモードで検証するもの**:
- パッチ本文の文字列変換結果
- 色コード保持
- 引数 rewrite / `__result` rewrite の挙動

**DummyTarget パターン**:

```csharp
internal sealed class DummyGrammar
{
    public string Pluralize(string noun) => noun + "s";
}
```

**禁止例**:

```csharp
// var grammar = new XRL.Language.Grammar();
```

L2 の基本方針は次の順序です。

1. まず実 DLL で target 解決とシグネチャを検証する
2. 次に DummyTarget でパッチ本文の挙動を固定する
3. 最後に L3 で実際の表示を確認する

**実行コマンド**:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G
```

---

## L3 — ゲームスモーク

**目的**: 実際のゲーム起動環境で、レンダリング・フォント・UI の見え方を確認する。

**L3 に残すもの**:
- 日本語文字が透明にならないか
- `TMP_Settings.defaultFontAsset` / fallback / OnEnable 適用が実際に効いているか
- メニュー、tooltip、mutation description、sidebar が実画面で読めるか
- `Player.log` に `Missing glyph`, `MODWARN`, `[QudJP]` エラーがないか

**制約**:
- 手動実行のみ
- Caves of Qud v2.0.4 の実行環境が必要

**手順**:

1. `dotnet build Mods/QudJP/Assemblies/QudJP.csproj`
2. `python scripts/sync_mod.py`
3. Apple Silicon では `scripts/launch_rosetta.sh` または `Launch CavesOfQud (Rosetta).command` で Rosetta 起動する
4. ゲームを起動して QudJP を有効化
5. 主要 UI と tooltip を確認
6. `Player.log` を確認

**Apple Silicon の注意**:
- native ARM64 の Harmony 実行結果は観測証跡として扱わない
- Apple Silicon 上の L3 受け入れ証跡は Rosetta 起動ログのみを有効とする

---

## 層境界ルール

- L1 では `Assembly-CSharp.dll` を使わない
- L2 では `UnityEngine` 実ランタイムに依存しない
- L2 では `Assembly-CSharp.dll` の型を直接 instantiate しない
- L3 だけが実レンダリングの最終保証を担う

---

## 推奨 TDD サイクル

1. 実 DLL で target 解決テストを書く
2. DummyTarget または副作用の軽い実メソッドで挙動テストを書く
3. パッチを実装する
4. L1/L2 を通す
5. L3 で表示確認する

この順序により、`フック位置が間違っている`, `翻訳ロジックが壊れている`, `表示だけが壊れている` を分離しやすくなります。

---

## テストプロジェクト構成

```text
QudJP.Tests/
|- QudJP.Tests.csproj
|- DummyTargets/
|- L1/
|- L2/
`- L2G/
```

`QudJP.Tests.csproj` は `Assembly-CSharp.dll` が存在する環境で条件付き参照を張ります。存在しない環境では game-DLL-assisted な検証は実行できないため、そうしたテストは反射ベースの存在確認や skip 戦略で扱います。
