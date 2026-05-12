# Assemblies

## Why

This directory owns the shipped QudJP mod DLL and the tests that define C#
patch behavior for Caves of Qud `1.0.4`.

Most work here changes one of three contracts:

- a Harmony target or upstream game signature
- a translation, rendering, diagnostic, or cache behavior
- the test harness that proves those behaviors without a live Unity runtime

## What

- `QudJP.csproj`: `net48` mod DLL that ships with the mod.
- `QudJP.Tests/`: `net10.0` test project and the source of truth for patch behavior.
- `src/Patches/`: Harmony patch classes. Keep one patch class per file.
- `src/`: translators, renderers, diagnostics, analyzers, and shared helpers.

Canonical references:

- `docs/test-architecture.md`: L1/L2/L2G/L3 boundaries and allowed test shapes.
- `docs/RULES.md`: translation ownership, route decisions, fallback policy, diagnostics, markup, and route-contract test obligations.
- `docs/workflows/runtime-evidence.md`: runtime logs, local mod sync, deployment checks, and decompiled-source tracing.

## How

Start by identifying the contract being changed, then choose the narrowest proof
layer from `docs/test-architecture.md`. Tests in `QudJP.Tests/` outrank stale
notes or old investigations.

Routine commands:

```bash
just build
just check
just test-l1
just test-l2
just test-l2g
```

Run `just test-l1`, `just test-l2`, and `just test-l2g` sequentially for local
verification unless the task explicitly establishes isolated artifacts for a
parallel run.

Use deterministic tooling before prompt-only reasoning for route and call-shape
work:

```bash
just --list
just ast-search-cs '<pattern>' Mods/QudJP/Assemblies/src
just ast-search-cs '<pattern>'
just lsp-check
```

`just ast-search-cs '<pattern>'` without a path searches the configured
decompiled-source target. Use it with `~/dev/coq-decompiled_stable/` to trace
upstream producers, verify signatures, and investigate unclaimed routes. Promote
type-, receiver-, overload-, alias-, or inheritance-sensitive C# questions to
the repo's Roslyn/static-analysis workflow described in the root `AGENTS.md`.

When a patch reflects into upstream game members, verify the real signature in
the decompiled source before choosing `AccessTools.Method` parameter types.
C# optional arguments still appear as real reflection parameters. Add L2G
signature or contract coverage when that reflection path controls runtime UI
behavior.

When a Harmony owner scope stores more than a boolean active flag, such as a
thread-static declaring type, member name, object identity, or route key, save
and restore the previous values with Harmony `__state` or an explicit stack.
An `activeDepth` counter alone is only safe for boolean scope gates; nested
owner calls can otherwise overwrite the outer route context. Add an L2 nested
owner-scope test when a patch introduces or changes that state.

Route decisions follow `docs/RULES.md`:

- prefer producer-owned or stable mid-pipeline fixes
- treat most sink and near-sink routes as observation-only
- do not hide dynamic or owner-routed bugs with broad dictionary entries
- preserve Qud markup, TMP/rich-text tags, `\x01`, `=variable.name=`, and
  numeric placeholders as indivisible contract tokens
- prove no-op fallback behavior and at least one route boundary for tooltip,
  TMP, RTF, producer, or queue-gated translation fixes

Test boundaries follow `docs/test-architecture.md`:

- L1 has no Harmony, Unity, or `Assembly-CSharp.dll` dependency.
- L2 uses Harmony with dummy targets that match upstream signatures.
- L2G may use the game DLL for target/signature proof.
- L2G real game type instantiation is a narrow exception for upstream member
  contracts that cannot be proven by target resolution plus dummy-target tests.
- L3 runtime smoke evidence is manual and Unity-backed.

Runtime diagnostics route through `RuntimeDiagnostics`. Verbose probes are
dev-only by default; release artifacts reject direct probe markers documented in
`docs/RULES.md`. Probes touching Unity, TMP, or reflection should fail closed so
observability cannot change visible translation behavior.

Process-lifetime caches cache only successful loads. File-missing, IO, XML/JSON
parse, or other transient failures should leave the cache unset so a later
runtime pass can retry after deployment or file repair.

For `UnityEngine.Object`-derived coroutine hosts, components, transforms, or UI
objects, `== null` and `!= null` preserve Unity fake-null lifetime semantics.
Pattern checks such as `is null` do not.

QJ004 is a narrow bypass guard for statically visible verbose probe markers on
direct logging calls. Stronger future guarantees should tighten
`RuntimeDiagnostics`, marker conventions, release verification, or focused
analyzer tests before broadening QJ004 into a general logging analyzer.
