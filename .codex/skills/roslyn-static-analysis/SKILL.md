---
name: roslyn-static-analysis
description: Use in QudJP when designing, reviewing, or extending C# static analysis over decompiled Caves of Qud source, including coverage maps, inventories, and semantic classification.
---

# Roslyn Static Analysis

Use this skill for QudJP static analysis work where syntax shape is not enough.
The main goal is to turn exploratory source findings into a durable, low-false-positive inventory.

Use cases include localization coverage maps, scanner inventories, owner/action
queues, text-construction surface classification, owner/sink route discovery,
symbol-backed call classification, and deciding when ast-grep findings should
be promoted to Roslyn SemanticModel analysis.

## First Choose The Lane

Before choosing a C# static-analysis tool, classify the request:

- Localization asset quality, placeholder correctness, or Japanese prose review:
  use the localization guides and validation commands such as
  `just localization-check` and `just translation-token-check`. Roslyn is not a
  translation-quality checker.
- One-off decompiled C# owner or sink-route investigation: start with `rg` /
  `ast-grep`, then use `just semantic-probe ...` only when symbol ownership
  changes the answer.
- Broad C# localization coverage, "where should we start?", or "is static
  coverage complete?" questions: start with `docs/localization-coverage-map.json`
  and its queue commands before opening raw inventories. The coverage map is the
  executable index of static-analysis lanes and closure gates.
- Durable, reviewable, or tracked C# inventories: use or extend the
  purpose-built Roslyn inventory. For current text producer callsites, the
  tracked artifact is `docs/static-producer-inventory.json`, not generic
  semantic-probe output.
- Blueprint XML merge sources are not a C# static-analysis lane in this repo.
  They are handled by the localization merge flow; do not add them back to the
  C# coverage map or use them as the answer to "which class file should we
  inspect next?"
- Runtime route proof: use fresh runtime evidence. Static analysis can choose
  the next hypothesis; it is not live behavior proof.

## Tool Choice

Start light, then promote when needed:

1. Use `rg` for literal names, files, and nearby vocabulary.
2. Use `ast-grep` for call shape, argument shape, assignments, wrappers, and quick structural exploration. In this repo, prefer `just sg-cs '<pattern>'` for common C# structural searches; it defaults to `~/dev/coq-decompiled_stable`, and accepts an optional path as the second argument.
3. Use `just semantic-probe ...` for ad hoc symbol-backed owner evidence when correctness depends on type, receiver, overload, inheritance, alias, generic owner identity, or Unity/TMP property ownership.
4. Promote to a purpose-built Roslyn inventory when the result becomes a source-of-truth artifact, needs domain-specific closure classification, or must be regenerated and reviewed as tracked project evidence.

Do not replace `ast-grep` with Roslyn for one-off exploration. Do not keep an `ast-grep` or regex scanner as the authoritative source when same-name methods on unrelated types would change the answer.
Do not treat `just semantic-probe` output as runtime proof; it is static owner evidence with explicit uncertainty.

## Semantic Probe Quick Reference

Use the generic semantic probe after `rg` / `ast-grep` have identified a
candidate shape but before committing to an owner route:

```bash
just semantic-probe --method Show --owner XRL.UI.Popup --limit 20
just semantic-probe --method 'Show*' --owner XRL.UI.Popup --include-nonmatching-owners
just semantic-probe --method SetText --owner XRL.UI.UITextSkin --owner XRL.UI.UIHotkeySkin --limit 20
just semantic-probe --assignment-property text --owner TMPro.TMP_Text --owner UnityEngine.UI.Text --managed-dir "$COQ_MANAGED_DIR"
```

Interpretation rules:

- `resolved_matching_owner_hits` is static semantic owner evidence.
- `candidate_matching_owner_hits` needs review and must not be consumed as
  resolved owner proof.
- `unresolved_hits` must remain visible. Do not silently drop or promote them.
- Wrapper callsites are reported as the wrapper owner. The wrapped call inside
  the wrapper body is a separate hit; v1 does not propagate wrapper callsites to
  wrapped owners.
- If a target surface becomes recurring or artifact-grade, implement or extend
  a purpose-built Roslyn inventory instead of using the generic probe as the
  source of truth.

## Localization Coverage Map And Queues

For broad QudJP C# localization coverage, start from the executable map and
its current queue outputs:

```bash
just localization-coverage-map-check
```

`docs/localization-coverage-map.json` is the machine-readable index of static
lanes, closure gates, tests, and next commands. Update it with tests whenever a
surface is promoted, excluded, or closed.

- `static_producer_messages_popups`: `EmitMessage`, `Popup.Show*`, and
  `AddPlayerMessage`; pick work from `just static-producer-owner-queue`, not
  raw inventory counts. Validate with `just static-producer-check`.
- `ui_text_construction`: player-visible text-construction APIs and likely UI
  owner candidates; classify with `just text-construction-surface-queue`. If
  the user says "other scanner surfaces" without naming a scanner, start here.

When asked to "make surfaces detectable", use an existing map lane and queue
first; add scanner work only when no lane can express the needed
player-visible/non-target classification. Generic construction shapes are not
ownership surfaces by themselves; `SetText` and direct text assignment require
player-visible owner context; Wish/debug/editor/test-like routes are queue noise
unless separate evidence promotes them.

Do not quote queue counts, top files, or representative families from memory.
Use current command output or a current generated artifact and name that source.
Static completeness is limited to the map lanes and current queue closure; a
green map file alone does not prove runtime issue closure.

## Existing Scanner Quick Reference

Use these repo-local facts when the task touches the current static producer
inventory:

- Python entrypoint: `scripts/scan_static_producer_inventory.py`
- Roslyn project: `scripts/tools/StaticProducerInventoryScanner/StaticProducerInventoryScanner.csproj`
- Tracked generated artifact: `docs/static-producer-inventory.json`
- Current-repo closure overlay: `scripts/static_producer_closure.py`
- Target surfaces: `EmitMessage`, `Popup.Show*`, and `AddPlayerMessage`
- Existing fixture tests: `scripts/tests/test_scan_static_producer_inventory.py`
- Closure tests: `scripts/tests/test_static_producer_closure.py`
- Existing Roslyn smoke tests: `scripts/tests/test_roslyn_extractor_smoke.py`

Current semantic target owners:

- `Popup.Show*`: `XRL.UI.Popup`
- `EmitMessage`: `XRL.World.Capabilities.Messaging`,
  `XRL.World.GameObject`, and `XRL.World.IComponent<...>`
- `AddPlayerMessage`: `XRL.Messages.MessageQueue`, `XRL.IGameSystem`,
  `XRL.World.AI.GoalHandler`, and `XRL.World.IComponent<...>`

Current fixture roots include
`scripts/tests/fixtures/static_producer_inventory/Demo/StaticProducerCases.cs`
and `scripts/tests/fixtures/static_producer_inventory/XRL.UI/Popup.cs`.
Update those fixtures first for same-name false positives, overloads, named
arguments, wrappers, direct markers, collection arguments, and forwarding sinks
unless a new fixture file is clearer.

The current `docs/static-producer-inventory.json` baseline for decompiled
`1.0.4` has 2,208 callsites, 1,012 families, 2,238 text arguments, 2,206
`resolved` callsites, 2 `candidate` callsites, and 0 `unresolved` callsites.
Use this as the starting comparison for this artifact. Any delta should be
explained; do not treat these counts as a universal threshold for unrelated
scanners.

For this existing static producer inventory, default to no increase in
`candidate` rows and keep `unresolved` at 0. A PR may override that default only
by naming the downstream consumer that accepts the uncertainty and documenting
the changed rows. For new scanners, set thresholds before regeneration instead
of copying these counts.

Regenerate the tracked inventory only when the task explicitly owns that
artifact:

```bash
just static-producer-regenerate-tracked
```

For non-mutating real-source validation, write to a disposable output:

```bash
just static-producer-preview
```

For current static producer inventory changes, run:

```bash
just static-producer-check
just static-producer-preview
just static-producer-owner-queue
```

## Roslyn Scanner Contract

For repo-local scanner tools:

- Keep the Roslyn implementation in `scripts/tools/<ToolName>/`.
- Keep Python responsible for CLI compatibility, cache handling, JSON validation, and integration with existing script tests.
- Keep C# responsible for syntax traversal, `Compilation`, `SemanticModel`, symbol resolution, and source location extraction.
- Build with the SDK version already used by repo tools, currently `net10.0`.
- Add or update a smoke test that builds the `.csproj`, so CI catches tool rot.
- Keep build-time analysis separate from shipped runtime DLLs.

Use these Roslyn defaults unless the task proves otherwise:

- Parse with `LanguageVersion.Preview`.
- Allow unsafe source when scanning decompiled game code.
- Build a best-effort `CSharpCompilation` over all candidate source files.
- Reference trusted platform assemblies plus any repo-known game assemblies that are locally available.
- Include the reference set in output metadata or cache fingerprinting when it can change symbol resolution.
- Treat diagnostics as evidence, not an automatic failure. Decompiled source often has missing references or unsupported fragments.

## Semantic Classification

Prefer symbol-backed classification over name-only classification.

Before implementing a scanner, define the target surface explicitly:

- List the fully qualified owner types and method names that are in scope.
- Decide how inherited, generic, implicit-receiver, and extension-method calls should map to that surface.
- Record intentionally excluded same-name helpers or wrappers when they are known false positives.

For callsite inventories, emit enough metadata for later review:

- `roslyn_symbol_status`: `resolved`, `candidate`, `unresolved`, or a similarly explicit status.
- `method_symbol`: the resolved or selected candidate method.
- `containing_type_symbol`: the owning type of the method symbol.
- `receiver_type_symbol`: the receiver type when available.
- Source location fields that are stable under regenerated decompiled source.
- A reason or classification field for any fallback.

Classification policy:

- `resolved`: Roslyn selected one method symbol and the owning type matches the intended surface.
- `candidate`: Roslyn produced candidates but could not choose one; include the best candidate only when a deterministic policy exists.
- `unresolved`: no useful symbol evidence; keep the row visible and avoid silently treating it as true.

When filtering target surfaces, compare symbols or fully qualified containing type names. A method name match alone is not enough for source-of-truth output.

In this skill, authoritative does not automatically mean `candidate=0` and `unresolved=0`. It means every row has an explicit semantic status, fallback reason, and consumer-visible uncertainty. If a downstream consumer requires only resolved targets, set an explicit threshold for allowed `candidate` / `unresolved` counts and fail validation when the threshold is exceeded.

## ast-grep Boundary

Use `ast-grep` results as a candidate set, not a proof, when any of these are true:

- The method name is common or wrapper-like, such as `Show`, `EmitMessage`, or `AddPlayerMessage`.
- The same call shape can occur on unrelated helper types.
- The receiver may be implicit, inherited, generic, aliased, or an extension method.
- The output will drive patch ownership, translation family status, or test expectations.

Use `ast-grep` as the final tool only when the rule is purely structural and false positives are acceptable or independently reviewed.

## Cache And Regeneration

Scanner output is stale if either source or scanner code changes.

Include cache fingerprints for:

- Source root path and relevant source file metadata.
- Roslyn tool project files and source files.
- Python wrapper code that changes output shape.
- Reference-resolution inputs such as target surface configuration and assembly reference lists.
- Schema version or output contract version.

After changing classification logic, regenerate against the real decompiled source and report totals for callsites, families, text arguments, and symbol statuses. Any expected total change should be explained as a false-positive removal, newly covered surface, or classification policy change.

## Validation

Use a two-layer check:

1. Fixture tests: small C# samples that lock edge cases such as same-name false positives, overloads, named arguments, direct-marked strings, or collection arguments.
2. Real-source regeneration: run the scanner against `~/dev/coq-decompiled_stable/` and compare aggregate totals and representative changed rows.

Recommended local checks:

```bash
just localization-coverage-map-check
just roslyn-build
just roslyn-test
just roslyn-python-check
just static-producer-preview
```

For QudJP static producer inventory work, also run the real-source regeneration command and inspect `roslyn_symbol_status` counts before claiming the scanner is authoritative.

Acceptance criteria for scanner changes should state:

- The intended target surface and known exclusions.
- The expected totals or allowed delta for callsites, families, text arguments, and symbol statuses.
- Whether `candidate` / `unresolved` rows are allowed, and why.
- Which fixture edge cases protect the classification policy.

When extending an existing scanner, preserve its established schema names unless
the task is explicitly a schema migration. Add new fields or statuses only with
wrapper validation, fixture expectations, generated or human-facing docs that
describe the changed contract, and regenerated inventory updated together. For
static producer inventory schema changes, update `scripts/scan_static_producer_inventory.py`,
`scripts/tests/test_scan_static_producer_inventory.py`,
`docs/reports/2026-05-05-issue-493-static-producer-inventory.md`, and
`docs/static-producer-inventory.json` together unless the task explicitly
narrows that scope.
