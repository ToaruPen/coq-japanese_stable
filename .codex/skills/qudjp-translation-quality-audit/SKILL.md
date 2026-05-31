---
name: qudjp-translation-quality-audit
description: Use in QudJP when auditing Japanese translation quality for contextual correctness, obvious mistranslations, strange or overly literal phrasing, natural Japanese polish, terminology consistency, glossary alignment, style consistency, or cross-file wording variance in localization XML/JSON/corpus text. Use for translation diff reviews, single suspicious translation candidates, and broad inventory sweeps; pair with qudjp-localization-triage when route ownership or generated text implementation strategy is part of the decision.
---

# QudJP Translation Quality Audit

## Overview

Use this skill to audit the quality of Japanese translations after, before, or during QudJP localization work. Keep it focused on translation judgment: meaning, Japanese fluency, terminology consistency, tone, and markup preservation.

Do not use this skill as a shortcut for route ownership. If the question becomes "where should this dynamic/generated string be translated?" or "is this safe as a dictionary leaf?", invoke `qudjp-localization-triage` and follow `docs/RULES.md`.

## Intake Classification

Classify the request before investigating:

- `diff-review`: the user wants changed localization files reviewed. Start from `git diff -- Mods/QudJP/Localization docs/glossary.csv` or the explicit paths.
- `single-candidate`: the user gives one phrase, key, visible string, or suspected mistranslation. Trace that item deeply before broad scans.
- `inventory-sweep`: the user wants suspicious translations, term drift, or style variance found across assets. Define the corpus and sampling strategy before claiming coverage.

State the classification in the work summary. If the classification changes after evidence gathering, say why.

For submitted snippets or hypothetical diffs, distinguish the submitted artifact from the current working tree. Review the snippet as given, then use existing files only as corroborating or conflicting evidence. Do not imply that a submitted line exists in the repository unless you found it.

For inventory sweeps, use shipped localization assets and `docs/glossary.csv` as the primary corpus by default. Use prior specs, plans, reports, and archive docs only as intent evidence, not as shipped usage. Expand search terms deliberately: include singular/plural English variants, case variants, and known Japanese variants, then state which variants were searched.

Default inventory depth is an exact and near-exact term sweep over the declared corpus. Expand into semantic synonyms, every related item subtype, or full source-English reclassification only when the user asks for exhaustive coverage, the initial sweep finds a risky boundary case, or the term family is known to be generated/composed. Treat `Corpus/` files as shipped text evidence, but classify whether a corpus row is authored prose, excerpted book text, or derived/tokenized support material before recommending edits from it.

## Evidence Sources

Use local sources first:

1. Scoped repo instructions for the touched area, especially `Mods/QudJP/Localization/AGENTS.md`.
2. `docs/RULES.md` for route boundaries, dictionary safety, markup, and placeholder rules.
3. `docs/glossary-policy.md` and `docs/glossary.csv` for stable terms. Treat `approved` and `confirmed` rows as authoritative unless fresh evidence proves them wrong.
4. Existing shipped localization assets under `Mods/QudJP/Localization/`.
5. Tests under `Mods/QudJP/Assemblies/QudJP.Tests/` when behavior or route contracts matter.
6. Decompiled source under `~/dev/coq-decompiled_stable/` when game context, producer meaning, or UI role is unclear.
7. Fresh runtime evidence under `~/Library/Logs/Freehold Games/CavesOfQud/` when visible output or route behavior is the claim.

Use wiki or other external sources only when local evidence does not settle lore, item meaning, mechanics, or authored context. When using external sources, cite the source in the final evidence summary and separate sourced fact from translation judgment.

## Parallel Dispatch

Use multiple agents when at least two independent judgment axes can run without sharing mutable state, especially for large diffs or inventory sweeps.

Default roles:

- `Context Investigator`: establish source meaning, gameplay/lore context, implementation route, and source evidence.
- `Japanese Quality Reviewer`: judge naturalness, tone, readability, and whether the Japanese sounds like a player-facing Caves of Qud line.
- `Consistency Auditor`: compare glossary rows, existing asset usage, context splits, and nearby terminology.
- `Coordinator`: synthesize the reports, resolve conflicts, and choose the recommendation.

Do not dispatch when the request is a tiny single phrase that can be resolved from one local file and the glossary. Do not let multiple agents edit the same files. Dispatch agents for evidence and recommendations; keep final editing decisions in the main session.

When dispatching, use the templates in `references/agent-prompts.md`. Pass raw artifacts, paths, diffs, and questions. Do not pass your preferred translation or expected answer unless the task is explicitly to validate that preference.

If subagent dispatch is unavailable, run the same role separation sequentially in your own analysis and state that empirical parallel evidence was not available.

## Audit Criteria

Check every recommendation against these gates:

- **Context correctness**: The Japanese must preserve the source meaning in the specific game, UI, lore, or implementation context. Do not flatten authored fantasy terms into generic Japanese when the source is a proper noun, faction, mutation, item, caste, title, or generated phrase.
- **Natural Japanese**: Prefer concise, idiomatic Japanese that fits the UI surface. Remove unnecessary literal English structure, but do not over-polish into a meaning that the source did not support.
- **Consistency**: Follow `approved` and `confirmed` glossary rows. For `draft` rows, use them as evidence but verify against shipped assets. Preserve intentional context splits such as category label vs prose when documented or justified by visible usage.
- **Style and tone**: Keep the local register. UI labels should be compact; prose can be more literary; combat/log lines should stay readable and mechanically clear.
- **Markup and placeholders**: Preserve Qud wrappers, color codes, TMP tags, escaped markers, placeholders, numeric formats, and direct translation markers exactly unless a route-specific test proves a transformation is correct.
- **Route safety**: Do not recommend broad dictionary additions for dynamic, procedural, sink-observed, or generated text. Escalate to `qudjp-localization-triage` when route ownership is unresolved.

Accept a context split only when the surface roles differ, existing usage is internally consistent or documented, the split does not conflict with an `approved` or `confirmed` glossary row, and player-facing ambiguity is lower than forced uniformity. If any of those points is unresolved, recommend `defer` or `needs evidence` instead of treating the split as proven.

## Workflow

1. Read the scoped instructions and classify the intake.
2. Identify target files, keys, source English, current Japanese, and visible context.
3. Gather local evidence with `rg`, JSON/XML inspection, glossary lookup, and tests or decompiled source when needed.
4. Decide whether to dispatch parallel agents. If yes, dispatch by independent role and keep their outputs separate until synthesis.
5. Produce a recommendation per item:
   - keep current translation
   - change to a specific Japanese translation
   - defer pending route evidence
   - split by context
   - add or update glossary row
   - open follow-up for deterministic tooling
6. If editing localization assets, preserve keys and structural tokens. Add release-note fragments before PR work that changes `Mods/QudJP/Localization/`.
7. Verify with focused checks. Use `just localization-check`, `just translation-token-check`, JSON/XML parse checks, and route tests when behavior changes.

For broad sweeps, separate "found issue" from "actionable edit". Boundary cases such as unknown/identified item names, generated display names, and corpus excerpts usually need visible-surface or source-route evidence before editing.

## Output

For small reviews, report:

- target text and file/key
- recommendation
- evidence used
- glossary/consistency status
- remaining uncertainty
- checks run or needed

For multi-item reviews, use a table:

| Item | Current | Recommendation | Reason | Evidence | Action |
| --- | --- | --- | --- | --- | --- |

After a parallel review, include a short synthesis:

- context findings
- Japanese quality findings
- consistency findings
- coordinator decision
- conflicts between agents and how they were resolved

Never present "sounds better" as the only reason. Tie polish recommendations to source meaning, local style, glossary, or player-facing readability.
