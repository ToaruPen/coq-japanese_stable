## [2026-03-12] Task 1: テスト基盤整備
- QudJPMod.cs → FontManager.Initialize() 呼出のため、FontManager.cs もテストプロジェクトに追加が必要だった
- FontManager.ResolveFontPath は #if HAS_TMP 内のみで使用 → テストビルドで S1144 (dead code) false positive → <NoWarn>S1144</NoWarn> で抑制
- 5メソッドの private → internal 変更は機械的置換で完了。ロジック変更なし
- ビルド0警告、全101テストPASS確認済み

## [2026-03-12] Task 2: RED テスト
- Test A が RED を確認: PatchAll() no-args via reflection → GetCallingAssembly() が間違ったアセンブリを返す → 0 patches → アサーション失敗
- .NET 10 + HarmonyLib 2.4.2 でもバグが再現 → 仮説が正しいことを確認
- DummyTarget は `internal static` にすることで analyzer 警告を回避 (S1118/CA1852/S2325)
- PatchAllTestPatch は `private static` (テスト内ネストクラス) で PatchAll 発見用
- 既存L2テスト36件は影響なし
