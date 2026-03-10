# Decisions

## 2026-03-11 Task 0 PoC
- ローカルPoC検証の一次ターゲットは `net10.0`（実行可能性を優先）。
- ゲーム投入DLLは Unity/Mono 互換を優先して `net48` でビルド。
- Part B は手動ゲーム起動 + Player.log 検証をゲート条件として継続採用。

## 2026-03-11 Task 0: Target Framework Decision
- Test project: net10.0 (primary runner, dotnet SDK 10.0.100)
- Mod DLL: net48 (matches game's Mono runtime)
- Assembly-CSharp.dll reference: `<Private>false</Private>` (reference-only, not copied)
- Harmony: NuGet Lib.Harmony 2.4.2 for tests, game-bundled 0Harmony 2.2.2.0 for runtime
- 3-Layer test strategy: CONFIRMED viable after Part A success
