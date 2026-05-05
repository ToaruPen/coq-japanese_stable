# Steam Workshop Local Inbox Triage Plan

> **For agentic workers:** use the local SQLite inbox as the only raw Workshop comment store. Do not use public GitHub issues, GitHub Actions logs, or Actions artifacts as an inbox.

**Goal:** Collect public Steam Workshop comments into a local SQLite inbox under `.coq-japanese_workshop/`, classify them with Codex Automation, and promote only high-confidence actionable non-duplicate reports to public GitHub issues.

**Architecture:** Steam collection, model triage, and public GitHub promotion are separate phases. Collection is deterministic and writes only local SQLite. Triage may read raw untrusted comments and append local `triage_results`, but has no GitHub write authority. Promotion is a deterministic step that reads validated triage output, duplicate-search evidence, and fixed allowlists before creating public issues.

**Visibility boundary:** `.coq-japanese_workshop/state/workshop-inbox.sqlite3` can contain raw Workshop comments and local audit records. It is gitignored and must not be printed to Actions logs, uploaded as artifacts, or committed. GitHub receives only final promoted issue bodies.

## Local State

Tracked layout:

- `.coq-japanese_workshop/README.md`
- `.coq-japanese_workshop/state/.gitkeep`
- `.coq-japanese_workshop/backups/.gitkeep`
- `.coq-japanese_workshop/exports/.gitkeep`

Ignored runtime data:

- `.coq-japanese_workshop/state/*`
- `.coq-japanese_workshop/backups/*`
- `.coq-japanese_workshop/exports/*`

Default DB path:

- `.coq-japanese_workshop/state/workshop-inbox.sqlite3`
- override: `QUDJP_WORKSHOP_INBOX_DB`

SQLite must be opened with:

- `PRAGMA foreign_keys = ON`
- `PRAGMA journal_mode = WAL`
- `PRAGMA busy_timeout = 5000`

## Schema

Core tables:

- `schema_migrations`: transactional schema version records.
- `app_kv`: local operational key/value state.
- `collection_runs`: collection attempts, counts, status, and collector version.
- `workshop_comments`: stable Steam comment identity, author metadata, creator flag, lifecycle status.
- `workshop_comment_snapshots`: body history keyed by `body_sha256`; comments edited on Steam create new snapshots.
- `triage_results`: append-only LLM classification audit rows.
- `promotion_decisions`: append-only deterministic promotion audit rows.

Important constraints:

- Steam IDs and GitHub repository IDs are stored as `TEXT` numeric/string identifiers, not SQLite `INTEGER`.
- `workshop_comments` dedupes by `(published_file_id, steam_comment_id)`.
- `workshop_comment_snapshots` dedupes by `(comment_id, body_sha256)`.
- `triage_results` and `promotion_decisions` use `ON DELETE RESTRICT`.
- triggers reject `UPDATE` and `DELETE` on `triage_results` and `promotion_decisions`.

## Phase 1: Deterministic Collection

Files:

- `scripts/workshop_comments_inbox.py`
- `scripts/tests/test_workshop_comments_inbox.py`

Responsibilities:

- Load `steam/workshop_metadata.json`.
- Fetch Steam metadata and public comment render endpoint over fixed HTTPS URLs.
- Validate numeric Steam IDs before URL construction.
- Parse `comments_html` into normalized plain text.
- Capture `data-miniprofile` to skip Workshop creator comments by default.
- Store comments and snapshots in local SQLite.
- Never write raw comments to public GitHub issues.

Safety checks:

- invalid IDs fail before network use.
- response body reads are bounded.
- creator comments are skipped by default.
- edited Steam comments create additional snapshots.
- audit tables are append-only.

## Phase 2: Codex Triage

Files:

- `scripts/workshop_comments_triage.py`
- `scripts/tests/test_workshop_comments_triage.py`

Responsibilities:

- Read pending local snapshots.
- Build a bounded packet with schema `qudjp.steam_workshop_local_triage_packet.v1`.
- Include no GitHub write tools, GitHub token, OpenAI API key, or endpoint.
- Validate returned categories, labels, confidence, and promotion recommendation.
- Append results to `triage_results`.

Boundary:

- The triage phase may read raw untrusted comments.
- The triage phase must not create GitHub issues.
- Model output is audit input, not direct public output.

## Phase 3: Deterministic Promotion

Promotion may create a public GitHub issue only when all conditions hold:

- triage result is validated.
- confidence is high enough for the configured threshold.
- category is actionable.
- duplicate search evidence indicates non-duplicate.
- labels are from the fixed allowlist.
- issue body is rendered from a fixed template.

Evidence handling:

- `triage_results.evidence_quote` is never published directly.
- promoter verifies model evidence is an exact substring of `workshop_comment_snapshots.body_text`.
- if verification fails, the promoter ignores the model quote and records `needs_human`, or uses a deterministic bounded excerpt.
- published evidence is truncated, sanitized, and rendered only in a fixed untrusted quote block.

Promotion audit:

- every promoted, duplicate, skipped, or needs-human decision appends a `promotion_decisions` row.
- successful public issues record `target_repo`, `issue_number`, `issue_url`, title/body hashes, and labels.
- duplicate decisions record query and evidence JSON.

## Retention And Export

- default export omits raw body text.
- raw body export requires an explicit local-only command.
- normal automation does not delete comments, snapshots, triage results, or promotion decisions.
- redaction is a dedicated deterministic maintenance operation and must preserve auditability.
- true destructive deletion is outside automation.

## Verification

Required checks:

```bash
uv run pytest scripts/tests/test_workshop_comments_inbox.py scripts/tests/test_workshop_comments_triage.py -q
just python-check
```

Useful local smoke:

```bash
uv run python scripts/workshop_comments_inbox.py collect --dry-run
uv run python scripts/workshop_comments_triage.py
```
