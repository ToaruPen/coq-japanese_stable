# Localization

## Why

This area contains the shipped XML merge files and JSON dictionaries that provide stable localization assets.

## What

- Main paths:
  - `*.jp.xml` for XML merge overlays
  - `Dictionaries/*.ja.json` for dictionary assets
  - `Text.jp.txt` for root text localization
  - `Corpus/*.jp.txt` for shipped text corpus excerpts
  - `Dictionaries/README.md` for local asset notes
  - markdown files, including `AGENTS.md` and `README.md`, are development-only documentation and must not be treated as shipped localization assets
- Source of truth:
  - tests and fresh runtime evidence decide whether a route should use localization assets at all
  - layer boundaries live in `docs/test-architecture.md`
  - translation-route, ownership, and markup rules live in `docs/RULES.md`

## How

- Validate assets with:

```bash
xmllint --noout <file>
file <file>
hexdump -C <file> | head -1
```

- Use dictionary or XML assets only for true stable leaf strings, fixed labels, and atomic names.
- Only accept a candidate when it satisfies the proven fixed-leaf policy in `docs/RULES.md`, and record source route, ownership class, confidence, destination dictionary, and rejection reason.
- Prune pseudo-leaf placeholders and widget/channel identifiers such as `""`, `" "`, `BodyText`, and `SelectedModLabel` before any promotion review.
- If a route is dynamic, procedural, observation-only at the sink, or `needs_runtime`, do not add a compensating asset entry there.
- `AddPlayerMessage` remains sink-observed; do not treat it as a fixed-leaf owner or sink-side fallback.
- Validation must fail on duplicate or broad additions rather than tolerate them at runtime.
- Preserve markup and placeholders exactly, including `{{...}}`, `&X`, `^x`, `&&`, `^^`, and `=variable.name=`.
- Verify target object and conversation IDs against game version `2.0.4`.
