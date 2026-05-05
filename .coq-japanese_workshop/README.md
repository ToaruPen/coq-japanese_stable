# QudJP Workshop Automation State

This directory is the local namespace for Steam Workshop comment automation.

Tracked files in this directory describe the state layout only. Runtime data is intentionally gitignored because it can contain raw public Workshop comments, triage audit records, and local promotion history.

Default runtime paths:

- `state/workshop-inbox.sqlite3`
- `state/workshop-inbox.sqlite3-wal`
- `state/workshop-inbox.sqlite3-shm`
- `backups/`
- `exports/`

Automation code and migrations are tracked in `scripts/`. The SQLite database itself is local state and must not be committed.
