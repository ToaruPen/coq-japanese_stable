# Task 0 PoC Results

## Environment
- macOS (Apple Silicon, Unix platform)
- dotnet SDK: 10.0.100
- NUnit: 4.3.2, NUnit3TestAdapter: 5.0.0
- HarmonyLib (NuGet): 2.4.2
- Game-bundled 0Harmony.dll: v2.2.2.0
- Assembly-CSharp.dll: 11.5MB at `~/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed/`

## Part A Results — ALL PASSED ✅

1. **NUnit on macOS**: `dotnet test` exits code 0, 6/6 tests pass on net10.0 (`.sisyphus/evidence/task-0-poc-nunit.txt`).
2. **Harmony Prefix patching**: `DummyTarget.OriginalMethod("hello")` → `"patched: hello"` ✅ (`.sisyphus/evidence/task-0-poc-nunit.txt`).
3. **Harmony Postfix patching**: `DummyTarget.OriginalMethod("hello")` → `"hello [postfixed]"` ✅ (`.sisyphus/evidence/task-0-poc-nunit.txt`).
4. **Assembly-CSharp.dll reference**: `ConsoleLib.Console.Markup` type and method signatures are readable:
   - `Transform(String, Boolean)`
   - `Strip(String)`
   - `Parse(String)`
   - `Wrap(String)`
   - `Color(String, String)`
   - etc. (`.sisyphus/evidence/task-0-poc-assembly.txt`).
5. **Game-bundled 0Harmony.dll metadata**: Readable, identity = `"0Harmony, Version=2.2.2.0, Culture=neutral, PublicKeyToken=null"`.
6. **TypeInitializationException test**: NO `TypeInitializationException` was observed for 20 Unity-dependent types (`RightClickButton`, `CampfireSounds`, etc.); all successfully instantiated.

## Target Framework Analysis
- **net10.0**: ✅ Works for test execution (primary test runner)
- **net9.0**: ✅ Builds (directory exists)
- **net8.0**: ✅ Builds (directory exists)
- **net48**: ✅ Builds (used for Mod DLL targeting game's Mono runtime)
- **netstandard2.0**: ⚠️ Build directory empty — likely incompatible with test adapter

Decision: Tests will run on net10.0. Mod DLL targets net48 (matches game's Mono runtime).
Test project uses multi-targeting with `<TargetFrameworks>net10.0;net48</TargetFrameworks>` for cross-compatibility.

## Part B Status — AWAITING MANUAL VERIFICATION
- Minimal mod created and deployed to `StreamingAssets/Mods/QudJP_PoC/`:
  - `manifest.json` (`id: "QudJP_PoC"`)
  - `Assemblies/QudJP.PoC.Mod.dll` (4.6KB, net48)
  - Uses `[ModuleInitializer]` for bootstrap (with polyfill for net48)
  - Calls `new Harmony("qudjp.poc.runtime").PatchAll()`
- **Known risk**: `[ModuleInitializer]` relies on compiler-emitted module constructor. On Unity's Mono runtime, this may not be called automatically. If Part B fails, Task 7 should switch to the game's native mod loading mechanism.
- **Pending**: User must manually launch Caves of Qud and provide `Player.log` contents.
- Check for: `mprotect returned EACCES`, `SecurityException`, Harmony patch application logs

## Risks and Constraints
1. NUnit3TestAdapter NU1701 warning for netstandard2.0 — not critical; tests run fine on net10.0.
2. `[ModuleInitializer]` on Mono runtime is unverified — Part B will confirm.
3. TypeInitializationException was NOT observed in tests, but may occur differently at game runtime.
4. Assembly-CSharp.dll is loaded as reference-only (`<Private>false</Private>`).

## Recommendation for Subsequent Tasks
- **Target Framework for tests**: net10.0 (or net9.0/net8.0 as alternatives if runtimes are present).
- **Target Framework for Mod DLL**: net48.
- **Assembly-CSharp.dll reference**: Use `<Private>false</Private>` to avoid copying.
- **Harmony approach**: NuGet `Lib.Harmony` 2.4.2 for tests, game-bundled `0Harmony.dll` 2.2.2.0 for runtime.
- **3-Layer test strategy**: CONFIRMED viable — Part A proves L1/L2 tests work.
- **DummyTarget pattern**: CONFIRMED — Prefix (return false) and Postfix (ref __result) both work.
