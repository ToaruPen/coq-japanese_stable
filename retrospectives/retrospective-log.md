# Retrospective Log

Use this log after a session reveals a reusable lesson, repeated failure mode,
or missing guardrail.

## Entry Template

### YYYY-MM-DD: Short Title

- Context:
- What failed or repeated:
- Evidence:
- Codify as: `validator | script | ast-grep | skill | AGENTS | docs | test`
- Target path:
- Follow-up gate:
- Status: `open | codified | intentionally-deferred`

## Example

### 2026-05-01: Skill copies drifted across agent surfaces

- Context: The same skill existed under multiple tool-specific roots.
- What failed or repeated: Manual copies could diverge without review.
- Evidence: Duplicate skill package paths with identical names.
- Codify as: `validator` and `script`
- Target path: `skill-index.json`, `scripts/sync-skills.py`
- Follow-up gate: `just skill-check`
- Status: `codified`
