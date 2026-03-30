# Scripts

Python tooling for validation, extraction, sync, and deployment.

## Area Map

- `scripts/*.py` — operational utilities (sync, validation, extraction, diff)
- `scripts/*.sh` — shell tools (decompile, launch)
- `scripts/tests/` — pytest coverage
- `pyproject.toml` — Ruff and pytest config

## Commands

```bash
ruff check scripts/              # Lint
ruff format scripts/             # Format
pytest scripts/tests/            # All tests
pytest scripts/tests/ -k <pat>   # Filtered tests

# Decompile game DLL for source reference
scripts/decompile_game_dll.sh          # Text pipeline classes (37)
scripts/decompile_game_dll.sh --list   # List classes only
scripts/decompile_game_dll.sh --all    # All classes (slow)

# Conversation diagnostic cycle (L3 — requires Rosetta + Hammerspoon IPC)
scripts/diagnose_conversation.sh               # Full cycle: build → deploy → launch → talk to NPC → collect log
scripts/diagnose_conversation.sh --skip-build  # Skip build+deploy (requires an already deployed DLL)
```

### diagnose_conversation.sh

Automates the full conversation diagnostic cycle for L3 in-game verification:

1. Clean + full build and deploy via `sync_mod.py`
2. Launch game under Rosetta (`arch -x86_64`)
3. Navigate to Continue and wait for save to load
4. Send Talk key sequence (`c` + right arrow) to open a conversation with an adjacent NPC
5. Collect `ConversationDiag` log lines from `Player.log` and print a summary

**Prerequisites:**
- Hammerspoon running with IPC enabled (`require("hs.ipc")` in `init.lua`)
- Save game with player adjacent to a talkable NPC (e.g. Mehmet in Joppa)
- `ConversationDiagnosticPatch.cs` present in `src/Patches/` (temporary diagnostic patch)

**Outputs:**
- Diagnostic lines tagged `ConversationDiag` from `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- Recent Talk-related log lines (`Talk`, `HaveConversation`, `ShowConversation`)

## Editing Rules

- Prefer extending an existing script over creating a new one for the same tool.
- Keep error paths explicit and actionable — these scripts drive validation and deployment.
- Align validation checks with canonical docs, not ad hoc rules.
- Python baseline: `3.12+`. Typed and documented public interfaces.

## Constraints

- Do not add silent fallbacks that hide invalid asset state.
- Script naming: `verb_noun.py` for top-level tools, `verb_noun.sh` for shell scripts.
