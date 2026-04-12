# 2026-04-12 owner seam audit for issue #354

## Scope

Audit the remaining owner seams and dictionary buckets with post-#362 / post-#363 evidence only as support. The goal is to separate stale inventory noise from actual untranslated work, while keeping route-first ownership reasoning explicit under `docs/RULES.md`.

## Verdict key

- **existing-seam verification** — the route seam already exists and the remaining work is confirmation / reclassification.
- **existing-seam asset-gap** — the route seam already exists, but the remaining work is a narrow asset/helper gap on that seam.
- **true route gap** — no adequate owner seam exists yet.

## Audit matrix

| Bucket | Representative family | Verdict | Why |
| --- | --- | --- | --- |
| DidX / MessageFrame | `Prone` | existing-seam asset-gap | `DidX` already has the `XDidYTranslationPatch` / `MessageFrameTranslator` seam. Batch 01 settled `Prone` as the existing seam plus three missing tier 3 asset rows (`down on {0}`, `from {0}`, `up from {0}`), so this is a narrow asset gap, not a new route. |
| DidX / MessageFrame | `HolographicBleeding` | existing-seam verification | The batch notes already downgraded it to likely stale inventory / verification on the existing message-frame seam because the verbs already exist in `verbs.ja.json`. |
| Does / VerbComposition | `message-frame-normalizable` quick wins (`stunned`, `open`, `already full`, `falls/returns to the ground`) | existing-seam verification | The repo already has a dedicated Does seam (`DoesFragmentMarkingPatch` + `DoesVerbRouteTranslator`). These rows are the normalizable quick wins that stay on that seam and mainly need verification/reclassification. |
| Does / VerbComposition | `does-composition-specific` rows (`is empty`, `has no room for more {x}`, `encoded with ...`) | existing-seam asset-gap | The seam exists, but these families need producer-side helper/template handling rather than sink-side compensation. That is still seam-owned work, not a route discovery problem. |
| EmitMessage / AddPlayerMessage | `AddPlayerMessage` umbrella | existing-seam verification | `AddPlayerMessage` remains sink-observed. The existing message-log patch is observation-only, and the explicit producer-owned subfamilies have already been split off. |
| EmitMessage / AddPlayerMessage | broad `EmitMessage` combat/environment families | existing-seam asset-gap | `EmitMessage` is producer-owned, but the coverage audit shows only a partial set of families is proven. The gap is family coverage on an existing route, not a missing owner seam. |
| Description families | `Parts.GetShortDescription` | existing-seam verification | The route is already owned by `DescriptionShortDescriptionPatch` / `DescriptionTextTranslator`, and the fresh runtime batch confirms it as owner-routed work rather than fixed-leaf import work. |
| Description families | `Effects.GetDescription` / `Effects.GetDetails` | existing-seam verification | The active-effect patches already own these routes through `ActiveEffectTextTranslator`; the remaining work is route-family decomposition, not new ownership. |

## Reading

The audit found **no true route gap** among the representative families above. The buckets already have owner seams; the real distinctions are:

1. **stale inventory noise** — rows that are already on a seam and only need verification or reclassification;
2. **existing-seam asset gaps** — rows that need a narrow helper/template/asset addition on an already-owned route;
3. **non-existent route gaps** — none of the sampled representatives landed here.

That means the #354 follow-up should stay route-first and bucket-cleanup-focused. It should not reinterpret sink visibility as ownership, and it should not broaden into new route implementation work.

## Evidence used

- `docs/reports/2026-04-11-didx-messageframe-batch-01.md:159-237`
- `docs/reports/2026-04-11-does-verbcomposition-batch-01.md:9-126`
- `docs/reports/2026-04-11-emit-addplayermessage-batch-01.md:12-123`
- `docs/reports/2026-04-11-description-families-batch-01.md:23-118`
- `docs/reports/2026-04-12-static-untranslated-quality-review.md:14-74`
- `docs/reports/2026-04-12-issue-363-runtime-triage-batch-01.md:27-63`
- `docs/reports/2026-04-12-issue-363-runtime-route-fix-batch-01.md:19-59`
- `docs/RULES.md:43-90`
