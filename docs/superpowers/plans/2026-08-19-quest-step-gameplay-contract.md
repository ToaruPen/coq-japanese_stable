# Quest Step Gameplay Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve every game-owned quest-step gameplay attribute when `Quests.jp.xml` replaces the Caves of Qud 1.0.5 step dictionary, fixing lost XP and optional-step semantics.

**Architecture:** Add a deterministic Python validator that compares quest and step runtime identities plus the semantic values parsed by `QuestLoader` while ignoring translated display fields. Keep a compact SHA-256 snapshot of the target 1.0.5 gameplay contract for game-free CI, and run the readable live XML comparison during `game-version-check`.

**Tech Stack:** Python 3.12+, `xml.etree.ElementTree`, pytest, Just, Qud localization XML.

---

### Task 1: Quest-step gameplay contract validator

**Files:**
- Create: `scripts/validate_quest_step_contract.py`
- Create: `scripts/tests/test_validate_quest_step_contract.py`

- [ ] **Step 1: Write failing validator tests**

  Cover localized display-name changes with stable runtime IDs, normalized defaults (`XP=0`, `Optional=false`, `Collapse=true`, other boolean flags false, ordinal from document order), XP/Optional mismatches, missing/extra/reordered steps, and deterministic diagnostics.

- [ ] **Step 2: Verify RED**

  Run `uv run pytest -q scripts/tests/test_validate_quest_step_contract.py` and confirm collection fails because `scripts.validate_quest_step_contract` does not exist.

- [ ] **Step 3: Implement the minimal validator**

  Provide `_runtime_id`, semantic step normalization, `build_step_gameplay_contract`, `compare_quest_step_contracts`, and a CLI taking `<base-quests.xml> <localized-quests.xml>`. Compare only quests owned by the localized XML; require each such quest to have the same ordered runtime step IDs and the same non-display `QuestLoader` semantics. Print one actionable mismatch per line and return nonzero on drift.

- [ ] **Step 4: Verify GREEN**

  Run `uv run pytest -q scripts/tests/test_validate_quest_step_contract.py` and `uv run ruff check scripts/validate_quest_step_contract.py scripts/tests/test_validate_quest_step_contract.py`.

### Task 2: Synchronize the shipped 1.0.5 quest-step contract

**Files:**
- Modify: `scripts/tests/test_quest_identity_contract.py`
- Modify: `Mods/QudJP/Localization/Quests.jp.xml`
- Create: `docs/release-notes/unreleased/quest-step-gameplay-contract.md`

- [ ] **Step 1: Add the failing production-contract test**

  Hash the normalized gameplay contract from `Quests.jp.xml` and compare it with the digest derived from the supported Caves of Qud 1.0.5 Base quest definitions. Include a focused assertion that `O Glorious Shekhinah! / Make a Pilgrimage to the Six Day Stilt` retains `1500 XP`.

- [ ] **Step 2: Verify RED**

  Run `uv run pytest -q scripts/tests/test_quest_identity_contract.py` and confirm failure reports the stale gameplay contract and the observed `0` versus expected `1500` XP.

- [ ] **Step 3: Apply the minimal data correction**

  Update all 48 stale numeric XP values and restore the five `Optional="true"` attributes from the stable 1.0.5 Base `Quests.xml`. Do not change Japanese names, text nodes, quest-level prose, or unrelated quest attributes.

- [ ] **Step 4: Add the release-note fragment**

  Record that quest completion now preserves XP rewards and optional-step behavior, including the Six Day Stilt pilgrimage report.

- [ ] **Step 5: Verify GREEN**

  Run the focused identity test, the validator against the stable 1.0.5 Base XML, `xmllint --noout Mods/QudJP/Localization/Quests.jp.xml`, and `just localization-check`.

### Task 3: Make live parity part of the game-version gate

**Files:**
- Modify: `justfile`
- Modify: `scripts/tests/test_qudjp_dotnet_test_contracts.py`

- [ ] **Step 1: Add a failing task-runner contract test**

  Require a `quest-step-contract-check` recipe using `COQ_BASE_QUESTS` or the stable-reference default, and require `game-version-check` to run it immediately after `target-game-version-check`.

- [ ] **Step 2: Verify RED**

  Run `uv run pytest -q scripts/tests/test_qudjp_dotnet_test_contracts.py::test_game_version_gate_covers_current_and_game_free_contracts` and confirm the missing recipe/gate step causes the expected failure.

- [ ] **Step 3: Implement the Just integration**

  Add the quoted default Base quest path, invoke the validator recipe, and insert the recipe into `game-version-check` before build or C# verification.

- [ ] **Step 4: Verify GREEN and broad gates**

  Run the focused task-runner contract, `just quest-step-contract-check`, `just python-check`, `just python-test`, `just localization-check`, `just translation-token-check`, and review `git diff --check` plus the final scoped diff.
