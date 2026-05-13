---
name: qudjp-static-producer-closure
description: Use in QudJP when closing issue-493 style static producer owner queue entries from docs/static-producer-inventory.json and scripts/static_producer_closure.py, including auditing existing patches, proving all callsite shapes, adding focused tests, and registering covered owner families.
---

# QudJP Static Producer Closure

Use this skill to close one coherent semantic batch from the
`static_producer_messages_popups` lane in `docs/localization-coverage-map.json`.

This skill is not a general translation triage guide. Pair it with
`qudjp-localization-triage` for route ownership decisions and
`roslyn-static-analysis` for scanner or coverage-map changes.

## Scope

Target only the static producer inventory lane:

- inventory: `docs/static-producer-inventory.json`
- closure overlay: `scripts/static_producer_closure.py`
- queue command: `just static-producer-owner-queue`
- validation gate: `just static-producer-check`
- target surfaces: `EmitMessage`, `Popup.Show*`, `AddPlayerMessage`

Do not use legacy scanners or archived audits as source of truth. Do not
regenerate tracked inventory unless the task explicitly owns that artifact.

## Core Rule

Never close a family because a patch class exists.

Close a family only after every inventoried callsite shape is accounted for by
owner-route behavior, tests, and dictionary/translator coverage, or explicitly
split/deferred with a reason.

## Batch Workflow

1. **Choose a batch from the queue.**
   - Run `just static-producer-owner-queue` or
     `uv run python scripts/static_producer_closure.py --format json --limit 0`.
   - Use a small calibration batch only when the route, owner seam, or test
     contract is still uncertain. After one representative family proves the
     pattern, widen the next PR to a semantic batch: families may span source
     files when they share the same owner surface, handoff pipeline, target
     resolution shape, dictionary/translator contract, and reusable test
     harness.
   - Do not create one-family PRs for repeated applications of the same proven
     route/test pattern. The review unit should be the semantic contract being
     proven, not the inventory row count.
   - Keep batches narrow when they mix different owner surfaces, runtime-proof
     needs, generated-text parsers, or closure-status policies. Split those by
     the differing contract before implementation.
   - Avoid broad `needs_family_review` families until you can split the fixed,
     generated, runtime-required, and sink-observed shapes.

2. **List every inventory shape before judging coverage.**
   - Query `docs/static-producer-inventory.json` for the exact
     `producer_family_id`.
   - Record all lines, target surfaces, closure statuses, and expressions.
   - Before editing, check the current `origin/main` diff and any open or
     recently merged issue-576 PRs for the same `producer_family_id`s. If the
     family has already landed through another integration PR, treat the work
     as verification or closeout instead of reimplementing it on the current
     branch.
   - Inspect the matching decompiled source under `~/dev/coq-decompiled_stable/`.
   - Useful extraction commands:

     ```bash
     jq -r '.callsites[]
       | select(.producer_family_id=="FAMILY_ID")
       | [.line,.target_surface,.closure_status,.expression]
       | @tsv' docs/static-producer-inventory.json

     uv run python scripts/static_producer_closure.py --format json --limit 0 \
       | jq '.source_files[].families[]
         | select(.producer_family_id=="FAMILY_ID")'
     ```

3. **Audit existing coverage.**
   - Search repo patches, tests, and localization assets.
   - Verify the owner patch guard can see every relevant emitted shape.
   - Verify the real repo dictionary or translator handles every emitted shape.
     A temporary test dictionary is useful for unit isolation, but it is not
     enough for closure evidence when production coverage depends on shipped
     assets.
   - For families that mix `AddPlayerMessage` and `Popup.Show*`, audit each
     surface separately. A queue patch can close queued messages only; it does
     not prove popup callsites in the same owner method.
   - For `AddPlayerMessage`, prove the generic queue alone does not translate
     owner-specific traffic.
   - For `Popup.Show*`, use a narrow owner helper before generic popup fallback
     for generated or owner-specific sentences.

4. **Classify the result.**
   - `covered_by_owner_patch`: all inventoried shapes are covered by existing
     or newly added owner-route tests and production dictionary/translator
     coverage.
   - `needs_family_review`: mixed fixed, generated, runtime, or policy shapes
     still need splitting before implementation.
   - `runtime_required`: static evidence cannot prove the emitted shape.
   - `owner_patch_required`: owner-route behavior is still missing.

   For a `needs_family_review` family, report a split table before proposing
   implementation:
   - `messages_candidate`: fixed literals or popup/menu text to review outside
     owner-patch closure.
   - `owner_patch_required`: generated owner-route shapes that can become a
     focused batch.
   - `runtime_required`: shapes that need runtime evidence or deeper producer
     tracing.
   - deferred or policy-only shapes, with the reason they are not counted.
   Do not add the full mixed family id to `COVERED_OWNER_FAMILIES`; close only
   a smaller coherent route after tests prove that route.

5. **Use Red-Green-Refactor for changes.**
   - If behavior or closure status changes, add a failing test first.
   - For existing patches, add missing evidence tests before adding the family
     to `COVERED_OWNER_FAMILIES`.
   - For new owner patches, add focused L2 dummy-target tests and L2G target
     resolution when the patch targets game C#.
   - Use L1 tests for pure translator or regex helpers.

6. **Minimum evidence before closure overlay registration.**
   Every `CoveredOwnerFamily` entry should point to evidence for:
   - owner patch source or owner helper,
   - queue/popup handoff linkage when applicable,
   - L2 behavior tests for representative successful shapes,
   - negative coverage for queue-only or owner-absent traffic when applicable,
   - direct marker and empty input handling,
   - color/capture preservation when the family can carry markup,
   - L2G target/signature resolution for Harmony patches. Owner-patch closure
     evidence in `scripts/static_producer_closure.py` must include
     family-specific target tokens: prefer a full
     `DeclaringType|Method|Return|...` signature, or include both the exact
     upstream declaring type and member name.

7. **Verify and report.**
   - Run the focused L1/L2/L2G tests touched by the batch.
   - For focused C# test filters, use direct `dotnet test`; the `just test-l2`
     and `just test-l2g` recipes do not forward arbitrary filter arguments.

     ```bash
     dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
       --filter 'TestCategory=L2&FullyQualifiedName~NameFragment' --no-restore
     ```

   - Run `just static-producer-check`.
   - If `Mods/QudJP/Localization/` changed, run `just translation-token-check`
     in addition to `just localization-check`. If duplicate source-key baseline
     entries changed only because dictionary entry order or conflict state
     changed, regenerate with `just translation-token-baseline`, inspect the
     baseline diff, and rerun `just translation-token-check`.
   - Re-run the owner queue and report the family/count delta.
   - Mention any shapes that remain deferred rather than counting them as
     closed.

## Common Pitfalls

- Do not register coverage from substring evidence alone; evidence files must
  contain tests that exercise the inventoried shapes.
- Do not use only `typeof(Patch)` plus parameter-only signatures or shared test
  names in `EvidenceFile.required_substrings`; those can match unrelated
  families in the same patch.
- Do not let a temporary pattern dictionary prove production coverage.
- Do not treat a sink or generic popup/message fallback as owner coverage.
- Do not close a mixed `needs_family_review` family without first naming the
  deferred shapes.
- Do not quote queue counts from memory; use current command output.
- Do not rely on `just localization-check` as a substitute for
  `just translation-token-check`; CI runs the token gate for localization
  assets and can fail on duplicate-conflict baseline drift.

## Output

When reporting a batch, include:

- selected family or source file,
- inventory callsite shapes reviewed,
- owner-route decision and deferred shapes,
- implementation or closure overlay changes,
- tests/checks run,
- queue delta,
- remaining risk.
