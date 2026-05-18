---
name: qudjp-localization-triage
description: Use when working in QudJP on untranslated or incorrectly translated Caves of Qud runtime text, player.log triage, owner-route vs sink-route decisions, generated/composed names, HistorySpice text, dictionary leaf additions, or localization patches that need decompiled producer tracing and focused verification.
---

# QudJP Localization Triage

## Overview

Use this skill to turn runtime untranslated text into the smallest correct QudJP localization change. Route text at the producer or owner when possible, use dictionary leaves only for fixed literals, and leave sink fallback as observability rather than the primary solution.

## Workflow

1. Establish current evidence.
   - Read repo instructions first, including scoped `AGENTS.md` files for C# patches, localization assets, or scripts.
   - Use fresh `~/Library/Logs/Freehold Games/CavesOfQud/Player.log` when the user asks about live gameplay output.
   - Treat `Player.log` as route evidence, not just an untranslated-string list. Use `scripts/triage_untranslated.py` or equivalent parsing to preserve sink, route, category, and producer context before assigning work.
   - Classify each finding as untranslated fixed literal, generated template, generated name, composed route, already-Japanese runtime noise, or intentional pass-through.
   - Group broad runtime surfaces by owner route or route family before deciding backlog scope. Do not collapse sink-adjacent families such as `UITextSkinTranslationPatch`, `GetDisplayName*`, `Popup*`, and generic message sinks into one undifferentiated "untranslated" bucket.
   - When an observed English source looks like a stable label but contains a domain noun, Title Case phrase, number, object name, mutation/effect name, date, festival, dish, faction, or village term, treat it as generated until the producer proves it is a fixed literal.
   - Treat semantically odd English collocations, noun piles, and fantasy/domain word salad as generated until proven otherwise, even when they are lowercase and look like ordinary noun phrases. Be especially suspicious of creature/material/object, body-part/material/object, place/faction/title, dish/ingredient, or sacred/profane noun piles such as `tortoise keratin tent`, `dragonfly chitin tent`, `algal convalessence, sealed`, or `the Desiccated Spectre`. Before adding dictionary coverage, trace the producer or data source and decide whether the fix is component reconstruction, scoped component leaves, an owner translator, or a proven fixed literal.
   - When the user's surface name is broad, such as chargen, inspect adjacent runtime probes that share the text family or UI route, then state which screens are primary and which are follow-up risk.
   - For broad C# localization coverage, static-analysis completion, or "where to start" mapping, pair with `roslyn-static-analysis` and start from `docs/localization-coverage-map.json`. Blueprint XML merge data is not a C# static-analysis surface.
   - Quote static queue counts, top files, or representative families only from current command output or a current generated artifact. The checked map plus queues can guide backlog scope, but map green alone does not close a runtime issue without owner-route evidence, tests, or fresh runtime proof.
   - When the user provides an issue sample, review comment, or remembered phrase without a matching fresh log row, treat the sample as a hypothesis for route tracing, not as proof of current runtime failure. Report the missing runtime evidence separately, then use decompiled producers, data sources, adjacent logs, tests, or existing route observability to decide the next verification step.
   - If the issue sample is already covered by current code or tests, do not propose a duplicate implementation. Report the existing owner-route coverage, identify the focused checks or runtime evidence still needed, and classify the remaining work as verification, regression-test gap, follow-up risk, or closeout.
   - If the requested text is absent from the current log, say it is absent in that log. Use adjacent logs/routes only as risk evidence, not proof that the requested text is still failing.
   - For TMP/UI rendering regressions, separate the owner/root refresh path from replacement or fallback recovery paths before declaring the issue fixed. Visible text alone is not enough: confirm the fresh `Player.log` has no QudJP exceptions or route-specific probe failures, and state whether any observed `fallback` log is an unrelated engine/font fallback or the UI fallback path under investigation.
   - Treat dictionary duplicate-key observations that do not change the selected translation as verbose diagnostics, not normal startup warnings. If they must remain observable, route them through `RuntimeDiagnostics.LogVerboseProbe(...)` so release `Player.log` stays focused on load counts and actionable failures.
   - When PR review comments are part of the request, pair this skill with `github:gh-address-comments` or `post-pr-convergence`; CodeRabbit/check pass alone is not enough if unresolved review threads may remain.
   - Treat an actionable review comment as a representative symptom for the whole route family, not only the exact line, branch, or literal named in the comment. Before editing, enumerate sibling owner tokens, parser branches, punctuation/casing variants, scoped dictionary homes, and owner-vs-sink boundaries that share the same contract, then use that family audit to choose focused edge tests.
   - When a generated pattern captures object, actor, target, faction, mutation, or item names that may already be localized, audit the grammar boundary after each capture for Japanese particles such as `の`, `を`, `に`, `で`, `は`, `が`, `と`, `から`, `へ`, and `より`. Do not treat an English possessive like `'s`/`s'` as the only localized-name boundary. Scope this audit to the same producer, message-frame family, route helper, or adjacent regex group that repeats the same capture contract, and check both source-regex boundaries and output templates that append particles after captured text.
   - When reading CodeRabbit state, compare `headRefOid`, check/status context, review decision, and the commit range named in the latest review body. `latestReviews` may retain comments from older force-pushed commits, while the current CodeRabbit status context can already be passing; unresolved threads alone can also miss actionable current-head review-body notes.

2. Trace the source before choosing a fix.
   - Use `rg` for literal strings, symbol names, files, and quick dictionary searches.
   - Use `just sg-cs '<pattern>'` for decompiled C# producers when call shape, argument structure, wrappers, assignments, overloads, or attributes matter.
   - Use `just sg-cs '<pattern>' Mods/QudJP/Assemblies/src` to compare existing patch patterns in repo-owned C#.
   - When a decision depends on C# producer/sink ownership, do not stop at literal `rg`. Run at least one `just sg-cs` query that matches the relevant call shape, such as `AddMyActivatedAbility($$$ARGS)`, `ExpandString($$$ARGS)`, `AddEntityListItem($$$ARGS)`, `SetText($$$ARGS)`, or `MessagePatternTranslator.Translate($$$ARGS)`. If `rg` is sufficient because the source is a plain data file or fixed asset literal, state that explicitly in the evidence summary.
   - For broad static C# backlog work, use `just static-producer-owner-queue` and `just text-construction-surface-queue` before ad hoc literal searches. If the user says "other scanner surfaces" without naming a scanner, start with the `ui_text_construction` lane; add scanner work only when the existing map/queue cannot express the needed classification.
   - For generated ability names, effect names, mutation labels, and similar UI labels, trace the method or object data that composes the visible name before adding any dictionary entry.
   - For generated or composed text, record the route shape before choosing a fix: fixed frame with generated noun, generated name only, generated sentence from reusable grammar templates, or final sink-only display with no known owner route yet.
   - When a generated name appears inside a fixed message, popup, combat line, or other sentence frame, trace both owners separately: the outer frame producer/pattern and the captured name's producer. Translate the frame at its owner route, and translate the captured generated name through its owning display-name or component reconstruction route rather than by adding an exact phrase leaf.
   - When a route-local regex or parser decomposes a generated sentence into captures, audit every owner/source/kind/item/mutation/effect slot before concatenating the Japanese output. Reuse the route's existing exact or visible phrase translator for captured slots when possible, and keep the current fallback only for unknown slot values. A phrase already translated as a standalone damage source or UI term can still leak in a composite route if the capture is appended raw.
   - For scoped dictionary ownership, inspect every production lookup list that can serve the route, including context-specific scoped files, non-contextual scoped phrase lists, and broad/global fallback. Report which list actually serves the failing shape.
   - For Annals / HistorySpice / village history text, inspect both the decompiled event builder under `~/dev/coq-decompiled_stable/XRL.Annals/` and the live `Base/HistorySpice.json` source when available. Title Case dish, festival, gospel, or sacred/profane phrases are usually composed outputs, not fixed leaves.
   - For HistorySpice token templates, distinguish symbolic expansion templates from visible runtime output before moving dictionary rows. A raw template in `world-gospels.ja.json` or `HistorySpice.json` is not itself an owner route; first trace the visible producer such as `CookingRecipe.GenerateRecipeName`, `Campfire.RollIngredients`, or `Campfire.DescribeMeal`, then decide whether the fix needs an owner translator, scoped component reconstruction, or a follow-up issue.
   - Treat `~/dev/coq-decompiled_stable/` as read-only evidence and never commit it.

3. Choose the ownership surface.
   - Prefer owner/source patches for generated UI text, status/inspect text, chargen framework data, journal/game-summary text, conversations, and other composed routes.
   - Use scoped dictionaries for route-specific fixed leaves or domain-specific leaf text.
   - For scoped phrase owners, keep the route contract explicit. A phrase such as a display-name adjective/suffix should live in the owning dictionary selected by the route decision rules and production scoped-dictionary list, not in a broader dictionary merely because a generic fallback would also translate it. If rules, shipped dictionary context, and the current production lookup path disagree about the owner, do not invent a priority order or bless whichever file makes the test pass. Report the mismatch as the finding, then either align the route contract or split tests by emitted shape before adding an ownership assertion.
   - Use broad dictionary entries only when the literal is stable and not context-sensitive.
   - Do not promote an observed generated name to an exact leaf merely because it appears unchanged in one `Player.log`. Prefer a generator helper that derives the translation from the owning data source, such as mutation DisplayName, effect metadata, object display names, or other shipped XML.
   - For generated HistorySpice names, prefer component leaves plus a reconstruction translator over whole observed phrases. Add whole exact leaves only after proving the phrase is authored as a stable fixed literal.
   - Do not add or keep sink translation as the intended fix when a producer route can own the text.
   - Treat generic popup and message sinks as observation or last-resort fallback, not as the primary fix for owner-specific generated sentences. If a popup or message reaches a shared helper with route-specific captures, add a narrow owner helper before delegating to the shared path.
   - Treat zero unresolved rows for a sink or composed route as a closeout candidate only after the owner route has been classified. A sink count reaching zero does not prove the upstream family is owned.
   - If a route cannot be owned safely, document why and preserve observability.

4. Fix with tests first when behavior changes.
   - Add or extend L1 translator tests for pure translation helpers and dictionary-driven templates.
   - Add L2 Harmony/dummy-target tests for owner patches and route observability.
   - Add L2G resolution tests when upstream game signatures or member contracts matter.
   - New translation families must cover exact match, fallback to English, empty input, color tags, and `\x01` marker behavior.
   - For producer or queue-gated patches that translate before a generic sink, also prove owner-absent or queue-only traffic stays unchanged, pass-through traffic is not recorded as an owner hit, and transformed owner traffic is recorded on the intended route family.
   - When articles, generated names, quantities, or placeholder-like fragments can appear, include those emitted shapes in tests instead of replacing them with dictionary leaves.
   - For capture-preserving color or markup routes, assert that separators and punctuation captures such as `:`, brackets, commas, and action markers keep their source wrappers when the source wrapped them separately from translated words.
   - When `ColorAwareTranslationComposer.Strip(...)` feeds a capture translator such as route-local `GetTranslatedCapture`, separate content spans from source-wrapper spans. Remove true whole-source boundary wrapper spans from the capture span set, for example with `WithoutTrueWholeSourceBoundarySpans(spans, stripped.Length)`, before restoring or moving captures; keep the original spans for final whole-source wrapper restoration. Add a regression that combines a whole-source wrapper with a colored capture whose position or grammar changes in Japanese.
   - For composite generated patterns, add at least one test where a captured slot is a known translatable term and one where it remains an unknown fallback value. This proves both translator reuse and pass-through behavior for the same route.
   - For generated/composed text, use this test matrix where applicable: fixed frame plus generated noun, generated noun alone, known observed sample, at least one non-observed variant, English fallback for unknown component, empty input, color tags, and `\x01` marker preservation.
   - For generated regex families with localized captures, include mixed-language boundary cases where the captured name is already Japanese and is followed by a Japanese particle. Cover every sibling branch that repeats the same capture contract, not just the observed `の` or English possessive branch.
   - For generated-name fixes, test at least one non-observed variant or a derivation path from the upstream data source so the test does not simply bless the single runtime sample.
   - For scoped dictionary ownership tests, seed the translation only in the verified owning file and assert the route still uses that owner. If the same English phrase is intentionally present under multiple scoped contexts, write separate tests for each emitted shape and context instead of collapsing them into one generic key. Do not let a global fallback, merged dictionary fallback, or broad non-contextual scoped lookup satisfy the test unless that fallback behavior is the contract under test. Prefer a concrete guard such as a poison fallback value, an owner-absent negative case, or an existing missing-hit/observability counter that proves fallback did not satisfy the lookup accidentally.
   - After adding an owner/generator fix, search the fresh log for the same raw source across UI owner routes, message patterns, journal patterns, description patterns, and final-output probes. Route the shared translation helper through every surface that can expose the same source.
   - For PR review work, scope edge-test additions to the families or files named by unresolved actionable review threads unless the user explicitly asks for a broader audit, but include sibling token or parser branch cases discovered in the family audit. A fix for one token, such as `Fire`, should also check adjacent owner tokens such as `Reload` before closing the thread.
   - Tests should assert the Japanese output, not only that output differs from English.

5. Verify using `just` recipes where available.
   - Build: `just build`
   - C# behavior tests: `just test-l1`, `just test-l2`, `just test-l2g`
   - Analyzer-inclusive local gates: `just build`, `just check`, or `just pr-check`
   - Localization checks: `just localization-check`, `just translation-token-check`
   - Static coverage map check: `just localization-coverage-map-check`
   - Broad local gate: `just check`
   - Sync only when the user wants local game deployment: `just sync-mod`
   - Before committing PR review fixes, confirm the checkout branch matches the PR head branch. If unrelated dirty work is present, use a separate worktree or stage only explicit paths.
   - For PR review convergence, confirm thread state with the GitHub review-thread workflow before calling actionable review threads handled.
   - Report `reviewDecision` separately from thread state: if unresolved actionable threads are zero and checks pass but `reviewDecision` still says `CHANGES_REQUESTED`, say the code-side comments appear handled and GitHub approval/decision state is still lagging or blocked.
   - If a CodeRabbit fix changes `ColorAwareTranslationComposer` or `Strip` call sites, run the color-route catalog and strip/restore allowlist tests before the broad gate; those deterministic inventories are part of the color-preservation contract, not incidental snapshots.
   - When a deterministic inventory or allowlist L1 test fails with `Missing` and `Extra` entries, treat the test failure output as the canonical update source. Do not infer line-number keyed allowlist entries from `rg`, `nl`, or manual file inspection when the test has already reported the current keys.
   - When a UI/color issue depends on a specific Qud color letter, verify the active mapping in decompiled `ConsoleLib.Console.ColorUtility` instead of inferring from the letter name; in the current game, `W` renders yellow and `Y` renders white.

## Output

When reporting back, include:

- what runtime text or route was investigated
- source-of-truth evidence used, including player.log and decompiled producer paths when relevant
- owner-route grouping used for broad runtime buckets, especially when closing or deferring an issue
- whether the fix is owner/source, scoped dictionary, broad leaf, or intentional pass-through
- for generated/composed text, the producer evidence and whether the fix uses upstream data derivation, component-leaf reconstruction, or a justified exact leaf
- tests and checks run
- remaining runtime risk or surfaces the user still needs to verify in-game

For PR review convergence requests, report PR/thread evidence instead of runtime route evidence:

- check state
- unresolved actionable review-thread count
- `reviewDecision` or approval state, reported separately
- edge-test families/files named by unresolved actionable threads
- local `just` checks run

Do not report sink success as final coverage if the owner route is still missing.
