# Legacy Scripts Separation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move legacy scanner/bridge scripts out of the active `scripts/` surface into `scripts/legacies/`, then repair imports, docs, tests, and release-facing references so the old workflow stays usable but clearly separated.

**Architecture:** Keep active tooling at `scripts/` root, create a dedicated `scripts/legacies/` package for legacy bridge/view-only code, and update every import and CLI path that points at the moved modules. Treat this as a repository-structure change rather than a behavior change: the same commands should still work after their paths are updated, and tests/docs should prove that no references were left behind.

**Tech Stack:** Python 3.12+, pytest, repository docs, GitHub PR workflow.

---

### Task 1: Move legacy Python entrypoints and package modules

**Files:**
- Create: `scripts/legacies/__init__.py`
- Create: `scripts/legacies/scanner/__init__.py`
- Move: `scripts/legacies/scan_text_producers.py`
- Move: `scripts/legacies/reconcile_inventory_status.py`
- Move: `scripts/legacies/scanner/ast_grep_runner.py`
- Move: `scripts/legacies/scanner/cross_reference.py`
- Move: `scripts/legacies/scanner/fixed_leaf_validation.py`
- Move: `scripts/legacies/scanner/inventory.py`
- Move: `scripts/legacies/scanner/rule_classifier.py`

- [ ] Move the two legacy CLI entrypoints into `scripts/legacies/`.
- [ ] Move the scanner package into `scripts/legacies/scanner/`.
- [ ] Update package-bootstrap logic (`__package__` / `sys.path`) so direct execution still resolves the `scripts` package from the repo root.
- [ ] Rewrite intra-package imports from `scripts.scanner...` to `scripts.legacies.scanner...` and top-level imports from `scripts.scan_text_producers` / `scripts.reconcile_inventory_status` to `scripts.legacies...`.

### Task 2: Update tests and frozen boundary strings

**Files:**
- Modify: `scripts/tests/test_scan_text_producers.py`
- Modify: `scripts/tests/test_reconcile_inventory_status.py`
- Modify: `scripts/tests/test_scanner_ast_grep_runner.py`
- Modify: `scripts/tests/test_scanner_cross_reference.py`
- Modify: `scripts/tests/test_scanner_fixed_leaf_validation.py`
- Modify: `scripts/tests/test_scanner_inventory.py`
- Modify: `scripts/tests/test_scanner_rule_classifier.py`
- Modify: moved inventory/help modules if frozen strings include old paths

- [ ] Update test imports to the new `scripts.legacies...` module paths.
- [ ] Update frozen help/boundary strings that mention old file paths so they point at `scripts/legacies/...`.
- [ ] Keep direct-script execution coverage, but target the new paths under `scripts/legacies/`.

### Task 3: Update docs and user-facing command references

**Files:**
- Modify: `scripts/README.md`
- Modify: `docs/fixed-leaf-workflow.md`
- Modify: `docs/archive/source-first-design.md`
- Modify: `docs/reports/2026-04-11-fixed-leaf-owner-triage.md`
- Modify: `docs/reports/2026-04-11-fixed-leaf-pruning-batch-01.md`

- [ ] Split `scripts/README.md` into active vs legacy sections so the visual separation is explicit.
- [ ] Update command examples from `scripts/...` to `scripts/legacies/...` where the moved CLI is referenced.
- [ ] Update file-path references in supporting docs/reports so they no longer point at removed paths.

### Task 4: Verify structure, behavior, and reference integrity

**Files:**
- Verify all files touched above

- [ ] Run targeted pytest for the moved legacy suite.
- [ ] Run targeted import/runtime checks for direct execution of moved CLIs.
- [ ] Search the repo for stale pre-move legacy-script references and clean up any remaining false references.

### Task 5: Finish branch, PR, and convergence

**Files:**
- Git branch/PR only

- [ ] Create a feature branch for the legacy separation work.
- [ ] Commit the change set in atomic commits.
- [ ] Push and open a PR with a summary focused on separating active vs legacy scripts.
- [ ] Run post-PR convergence until reviewer/CI feedback is resolved.
