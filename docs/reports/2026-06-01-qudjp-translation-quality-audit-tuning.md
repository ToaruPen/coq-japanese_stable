# 2026-06-01 QudJP Translation Quality Audit Skill Tuning

## Target

Skill under test:

```text
.codex/skills/qudjp-translation-quality-audit/
```

Purpose: validate whether a fresh agent can use the skill to audit QudJP translation quality across single-candidate, diff-review, and inventory-sweep scenarios.

## Iteration 0

Static consistency check:

- Frontmatter description matched the body scope: translation quality, mistranslation review, natural Japanese polish, terminology consistency, and handoff to route triage when ownership matters.
- `references/agent-prompts.md` was discoverable from `SKILL.md`.

## Iteration 1

### Scenarios

| Scenario | Success | Accuracy | Notes |
| --- | --- | ---: | --- |
| Cudgel context split | yes | 100% | Correctly classified as `single-candidate`; found local evidence; avoided route/dictionary overreach. |
| Mutation Points / Chrome Pyramid diff | yes | 100% | Correctly classified as `diff-review`; checked wrapper preservation and glossary evidence. |
| Grenades inventory sweep | yes | 100% | Correctly rejected global standardization; found `UnknownGrenade` and flashbang boundary cases. |

### Ambiguities Found

- Context split acceptance criteria were implicit.
- Hypothetical diff handling did not explicitly distinguish submitted artifacts from current repository evidence.
- Inventory sweeps did not explicitly define primary corpus vs prior docs.
- Search variant expansion was implied but not stated.

### Changes Applied

- Added explicit submitted-snippet handling.
- Added default inventory corpus and search-variant guidance.
- Added context-split acceptance criteria.
- Replaced a fixed repository path in agent prompts with `<current repository root>`.

## Iteration 2

### Scenarios

| Scenario | Success | Accuracy | Notes |
| --- | --- | ---: | --- |
| Cudgel context split | yes | 100% | Agent applied context-split criteria directly. |
| Mutation Points / Chrome Pyramid diff | yes | 100% | Agent separated hypothetical diff from current repository evidence. |
| Grenades inventory sweep | yes | 100% | Agent treated shipped assets as primary and prior spec as intent evidence. |

### Ambiguities Found

- Inventory depth was still partly implicit.
- `Corpus/` evidence handling was still underspecified.
- Boundary cases needed clearer separation from actionable edits.

### Changes Applied

- Added default exact/near-exact inventory depth.
- Added when to expand into semantic synonyms or subtype sweeps.
- Added `Corpus/` classification guidance.
- Added found-issue vs actionable-edit guidance for broad sweeps.

## Iteration 3

Held-out inventory follow-up:

| Scenario | Success | Accuracy | Notes |
| --- | --- | ---: | --- |
| Grenades inventory sweep after second tuning | yes | 100% | Agent stated inventory depth, avoided global standardization, separated found issues from edits, and handled corpus/unknown-item boundaries. |

### Remaining Ambiguity

- User-provided sweep breadth can still be broader than the default. The skill now defaults to exact/near-exact coverage and requires explicit expansion criteria, which is acceptable for initial use.

## Validation Commands

Fresh commands run after tuning:

```bash
python3 /Users/sankenbisha/.codex/skills/.system/skill-creator/scripts/quick_validate.py .codex/skills/qudjp-translation-quality-audit
npx secretlint .codex/skills/qudjp-translation-quality-audit docs/superpowers/specs/2026-06-01-qudjp-translation-quality-audit-design.md docs/superpowers/plans/2026-06-01-qudjp-translation-quality-audit.md
just markdown-report-check
uv run pytest scripts/tests/test_agent_cycle.py -q
just tool-check
```

All passed.

Known unrelated checks:

- `bash scripts/check-dotfiles.sh` is not present in this repository checkout.
- Full `npx secretlint .` currently fails on pre-existing `docs/reports/2026-05-31-workshop-v0.4.10.md`.
