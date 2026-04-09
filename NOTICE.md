# NOTICE

QudJP (Caves of Qud Japanese Localization) is licensed under the MIT License.
See [`LICENSE`](LICENSE) for the full license text.

The copyright holder "ToaruPen" in `LICENSE` is the GitHub handle of the
primary author (commits are authored as `SankenBisha`). The mod's
`Mods/QudJP/manifest.json` credits `"ToaruPen & Contributors"` to allow for
future contributions; all contributions made to this repository are accepted
under the MIT License in `LICENSE`.

This project bundles, depends on, or derives from the following third-party
components. Each retains its original license.

## Caves of Qud (Freehold Games)

QudJP is a community localization mod for
[Caves of Qud](https://www.cavesofqud.com/), developed by
[Freehold Games](https://www.freeholdgames.com/).

- QudJP is an independent community project. It is not affiliated with,
  endorsed by, or sponsored by Freehold Games.
- Targeted game version: Caves of Qud stable 1.0 (v2.0.4).
- The original English text in the game is copyright Freehold Games.
  QudJP distributes Japanese translations of that text as a derivative work
  for the sole purpose of enabling Japanese-language play, in line with the
  long-standing community practice around Caves of Qud localization mods.
- No Caves of Qud game binaries (`Assembly-CSharp.dll`, etc.) are included in
  this repository or in the shipped mod package. Contributors must obtain a
  legitimate copy of the game to build or verify the C# assemblies.

## Noto Sans CJK JP (Adobe / Google)

QudJP bundles a subset of Noto Sans CJK JP under the SIL Open Font License 1.1.

- Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name
  'Noto'.
- Subset file shipped with the mod:
  `Mods/QudJP/Fonts/NotoSansCJKjp-Regular-Subset.otf`
- Full license text: [`Mods/QudJP/Fonts/OFL.txt`](Mods/QudJP/Fonts/OFL.txt)

## Harmony (pardeike/Harmony)

QudJP uses [Harmony](https://github.com/pardeike/Harmony) at runtime for
method patching.

- License: MIT
- Runtime: loaded from the Caves of Qud installation — **not bundled** with
  QudJP.
- Local development / tests: `Lib.Harmony` NuGet is referenced in
  `Mods/QudJP/Assemblies/QudJP.csproj` as a conditional fallback
  (`PackageReference Version="2.2.2"`, only active when the game's
  `0Harmony.dll` is not available), and unconditionally in
  `Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
  (`PackageReference Version="2.4.2"`) to enable builds and tests without a
  game install.

## Build-time analyzers and test frameworks

The following packages are referenced only for build and test. None of them
ship inside the deployed mod package. Exact versions are pinned in the
relevant `.csproj` files; the list below shows license identifiers and
purpose.

- `Microsoft.CodeAnalysis.CSharp` (MIT) — used by `QudJP.Analyzers`
- `SonarAnalyzer.CSharp` (LGPL-3.0) — static analysis during build
- `NUnit` / `NUnit3TestAdapter` (MIT / BSD-3-Clause) — test framework
- `Microsoft.NET.Test.Sdk` (MIT) — test SDK

## Tooling

Contributor tooling referenced by the repository (not bundled with the mod):

- `Ruff` (MIT) — Python linter
- `pytest` (MIT) — Python test framework
- `ast-grep` (MIT) — structural pattern matching for code review
