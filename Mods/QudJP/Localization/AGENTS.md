# Localization

## Why

This area contains the shipped XML merge files and JSON dictionaries that provide stable localization assets.

## What

- Main paths:
  - `*.jp.xml` for XML merge overlays
  - `Dictionaries/*.ja.json` for dictionary assets
  - `Dictionaries/README.md` for local asset notes
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
- If a route is dynamic, procedural, or observation-only at the sink, do not add a compensating asset entry there.
- Preserve markup and placeholders exactly, including `{{...}}`, `&X`, `^x`, `&&`, `^^`, and `=variable.name=`.
- Verify target object and conversation IDs against game version `2.0.4`.
