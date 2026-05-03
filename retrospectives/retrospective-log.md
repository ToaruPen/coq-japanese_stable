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
- Status: <replace with exactly one backquoted value: `open`, `codified`, or `intentionally-deferred`>

For final entries, write exactly one status value on the `Status` line. Do not
leave a combined placeholder; `retrospective-open` detects entries by the exact
status value.

## Example

### 2026-05-01: Skill copies drifted across agent surfaces

- Context: The same skill existed under multiple tool-specific roots.
- What failed or repeated: Manual copies could diverge without review.
- Evidence: Duplicate skill package paths with identical names.
- Codify as: `validator` and `script`
- Target path: `skill-index.json`, `scripts/sync-skills.py`
- Follow-up gate: `just skill-check`
- Status: `codified`
