# QudJP Translation Quality Audit Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create and tune a repo-local Codex skill that audits QudJP translations for contextual correctness, natural Japanese, and terminology/style consistency.

**Architecture:** Add one orchestrator skill under `.codex/skills/qudjp-translation-quality-audit/`. Keep the main `SKILL.md` concise and move reusable parallel-agent prompt templates into `references/agent-prompts.md`.

**Tech Stack:** Codex skill markdown, YAML `agents/openai.yaml`, existing QudJP docs, existing skill validation scripts, `just` recipes, and `uv`-managed Python tooling.

---

## Tasks

### Task 1: Skill Structure And Design Docs

**Files:**
- Create: `docs/superpowers/specs/2026-06-01-qudjp-translation-quality-audit-design.md`
- Create: `docs/superpowers/plans/2026-06-01-qudjp-translation-quality-audit.md`
- Create: `.codex/skills/qudjp-translation-quality-audit/SKILL.md`
- Create: `.codex/skills/qudjp-translation-quality-audit/agents/openai.yaml`
- Create: `.codex/skills/qudjp-translation-quality-audit/references/agent-prompts.md`

- [ ] **Step 1: Initialize the skill skeleton**

Run:

```bash
python3 ~/.codex/skills/.system/skill-creator/scripts/init_skill.py qudjp-translation-quality-audit --path .codex/skills --resources references --interface display_name='QudJP Translation Quality Audit' --interface short_description='Audit QudJP translations for context, fluency, and consistency.' --interface default_prompt='Use $qudjp-translation-quality-audit to review this QudJP translation for context accuracy, Japanese fluency, and glossary consistency.'
```

Expected: `.codex/skills/qudjp-translation-quality-audit/` exists with `SKILL.md`, `agents/openai.yaml`, and `references/`.

- [ ] **Step 2: Write the design and plan documents**

Create the design document with the approved architecture, boundaries, workflow, validation, and risks. Create this plan with exact paths and commands. Do not commit unless the user explicitly asks, because repo instructions say commits require explicit request.

- [ ] **Step 3: Verify generated metadata**

Run:

```bash
sed -n '1,80p' .codex/skills/qudjp-translation-quality-audit/agents/openai.yaml
```

Expected: `display_name`, `short_description`, and `default_prompt` are populated, and `default_prompt` mentions `$qudjp-translation-quality-audit`.

### Task 2: Skill Body

**Files:**
- Modify: `.codex/skills/qudjp-translation-quality-audit/SKILL.md`

- [ ] **Step 1: Replace the template with the orchestrator workflow**

Write `SKILL.md` in English because LLM-facing documents must be English. Include:

- Frontmatter `name` and a trigger-rich `description`.
- Overview that distinguishes translation quality audit from route triage.
- Intake classification for `diff-review`, `single-candidate`, and `inventory-sweep`.
- Evidence order and required local sources.
- Parallel dispatch rules and when not to dispatch.
- Quality gates for contextual correctness, Japanese fluency, consistency, and markup preservation.
- Editing guardrails and verification expectations.
- Output format.

- [ ] **Step 2: Check for template residue**

Run:

```bash
rg -n 'TODO|Replace|optional|Structuring This Skill' .codex/skills/qudjp-translation-quality-audit/SKILL.md
```

Expected: no matches.

### Task 3: Parallel Agent Prompt Reference

**Files:**
- Create: `.codex/skills/qudjp-translation-quality-audit/references/agent-prompts.md`

- [ ] **Step 1: Add role-specific prompt templates**

Write reusable templates for:

- `Context Investigator`
- `Japanese Quality Reviewer`
- `Consistency Auditor`
- `Coordinator synthesis`

Each template must request raw evidence, uncertainty, and a compact recommendation. The prompts must not leak expected answers or tell agents which translation to prefer.

- [ ] **Step 2: Check prompt reference is discoverable**

Run:

```bash
rg -n 'references/agent-prompts.md|Context Investigator|Consistency Auditor' .codex/skills/qudjp-translation-quality-audit/SKILL.md .codex/skills/qudjp-translation-quality-audit/references/agent-prompts.md
```

Expected: matches in both files.

### Task 4: Validation And Tuning

**Files:**
- Modify if needed: `.codex/skills/qudjp-translation-quality-audit/SKILL.md`
- Modify if needed: `.codex/skills/qudjp-translation-quality-audit/references/agent-prompts.md`

- [ ] **Step 1: Run skill validation**

Run:

```bash
python3 ~/.codex/skills/.system/skill-creator/scripts/quick_validate.py .codex/skills/qudjp-translation-quality-audit
```

Expected: validation passes.

- [ ] **Step 2: Run repository static checks**

Run:

```bash
python3 ~/.codex/skills/.system/skill-creator/scripts/quick_validate.py .codex/skills/qudjp-translation-quality-audit
uv run pytest scripts/tests/test_agent_cycle.py -q
just python-check
just tool-check
just markdown-report-check
npx secretlint .codex/skills/qudjp-translation-quality-audit docs/superpowers/specs/2026-06-01-qudjp-translation-quality-audit-design.md docs/superpowers/plans/2026-06-01-qudjp-translation-quality-audit.md docs/reports/2026-06-01-qudjp-translation-quality-audit-tuning.md
```

Expected: all checks pass.

- [ ] **Step 3: Run empirical prompt tuning**

Evaluate three scenarios:

1. A single suspicious translation where the executor must distinguish mistranslation from acceptable context split.
2. A translation diff review where markup and glossary consistency matter.
3. An inventory-style sweep where the executor must avoid broad claims without evidence.

Record scenario results, ambiguities, discretion-filled spots, and minimal fixes. If fresh subagent dispatch is unavailable, report that empirical execution was skipped and perform only a structural review.

- [ ] **Step 4: Re-run validation after tuning edits**

Run:

```bash
python3 ~/.codex/skills/.system/skill-creator/scripts/quick_validate.py .codex/skills/qudjp-translation-quality-audit
uv run pytest scripts/tests/test_agent_cycle.py -q
just python-check
just tool-check
just markdown-report-check
npx secretlint .codex/skills/qudjp-translation-quality-audit docs/superpowers/specs/2026-06-01-qudjp-translation-quality-audit-design.md docs/superpowers/plans/2026-06-01-qudjp-translation-quality-audit.md docs/reports/2026-06-01-qudjp-translation-quality-audit-tuning.md
```

Expected: all checks pass.
