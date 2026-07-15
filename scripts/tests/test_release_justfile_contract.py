"""Static contract tests for release just recipes."""

from __future__ import annotations

import re
import shutil
import subprocess
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parents[2]


def _justfile_text() -> str:
    return (_REPO_ROOT / "justfile").read_text(encoding="utf-8")


def _download_release_zip_recipe() -> str:
    return _recipe_body("download-release-zip")


def _recipe_body(name: str) -> str:
    justfile = _justfile_text()
    marker_pattern = rf"^{re.escape(name)}(?:\s+.*)?\s*:\r?\n"
    match = re.search(marker_pattern, justfile, flags=re.MULTILINE)
    assert match, f"{name}: recipe not found in justfile"
    remainder = justfile[match.end() :]
    next_recipe = re.search(r"^[A-Za-z0-9_-]+\b.*:\r?\n", remainder, flags=re.MULTILINE)
    return remainder[: next_recipe.start()] if next_recipe is not None else remainder


def test_download_release_zip_quotes_version_argument() -> None:
    """The release download recipe must not splice raw version text into shell syntax."""
    just = shutil.which("just")
    if just is None:
        recipe = _download_release_zip_recipe()

        assert "version={{quote(version)}}" in recipe
        assert 'tag="v{{version}}"' not in recipe
        assert "QudJP-v{{version}}" not in recipe
        return

    probe = '1.2.3"; touch /tmp/qudjp-just-injection #'
    result = subprocess.run(  # noqa: S603 - intentionally probes shell quoting via just dry-run.
        [just, "--dry-run", "download-release-zip", probe],
        cwd=_REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )

    dry_run = result.stdout + result.stderr

    assert re.search(r"^version=(['\"]).*touch /tmp/qudjp-just-injection.*\1$", dry_run, re.MULTILINE)
    assert not re.search(r"^(?!version=).*;\s*touch\s+/tmp/qudjp-just-injection", dry_run, re.MULTILINE)


def test_workshop_upload_preflight_recipe_uses_dedicated_verifier() -> None:
    """The upload gate must verify staged content against the chosen release ZIP."""
    recipe = _recipe_body("workshop-upload-preflight")

    assert "scripts/verify_workshop_upload.py" in recipe
    assert "--release-zip" in recipe
    assert "--content-folder" in recipe
    assert "--vdf" in recipe
    assert "--expected-version" in recipe


def test_build_harmony_patch_recipe_uses_the_pinned_builder() -> None:
    """The task runner exposes the standalone Harmony release-asset build."""
    recipe = _recipe_body("build-harmony-patch")

    assert "{{python}} scripts/build_harmony_patch.py" in recipe


def test_agent_tool_recipes_have_readable_primary_names() -> None:
    """Agent-facing tool recipes should expose descriptive names, not only abbreviations."""
    justfile = _justfile_text()

    assert '\nast-search-cs pattern path="":' in justfile
    assert '\nast-search-py pattern path="scripts":' in justfile
    assert '\nlsp-check solution="Mods/QudJP/Assemblies/QudJP.sln":' in justfile
    assert 'sg-cs pattern path="":' in justfile
    assert "just ast-search-cs" in justfile
    assert 'lsp-diagnostics solution="Mods/QudJP/Assemblies/QudJP.sln":' in justfile


def test_qudtest_recipes_use_runtime_artifact_inspector() -> None:
    """QudTest recipes should expose the in-game artifact workflow without hand-copying paths."""
    headless_recipe = _recipe_body("qudtest-headless")
    inspect_recipe = _recipe_body("qudtest-inspect-game")
    results_recipe = _recipe_body("qudtest-results")
    history_recipe = _recipe_body("qudtest-history")

    assert "scripts/tools/QudTestHeadless/QudTestHeadless.csproj" in headless_recipe
    assert "--command {{quote(command)}}" in headless_recipe
    assert "--skip-player-log" in headless_recipe
    assert "scripts/qudtest_inspect.py" in inspect_recipe
    assert "--skip-player-log" not in inspect_recipe
    assert "scripts/qudtest_inspect.py --print-default-paths" in results_recipe
    assert "Library/Application Support/Freehold Games/CavesOfQud/Local/QudTest" not in results_recipe
    assert "scripts/qudtest_inspect.py --list-runs" in history_recipe


def test_issue737_runtime_closeout_recipe_uses_dedicated_checker() -> None:
    """Issue #737 runtime closeout should be executable without hand-copying report commands."""
    recipe = _recipe_body("issue737-runtime-closeout")

    assert "scripts/issue737_runtime_closeout.py" in recipe
    assert "--log {{quote(log)}}" in recipe
    assert "--min-mtime {{quote(min_mtime)}}" in recipe
    assert "--output {{quote(output)}}" in recipe
    assert "--require-passed" not in recipe


def test_issue737_player_log_variable_only_uses_regular_files() -> None:
    """Default Player.log discovery should not pass missing paths or directories to the checker."""
    justfile = _justfile_text()
    player_log_line = next(line for line in justfile.splitlines() if line.startswith("player_log :="))

    assert '[ -f "${HOME}/Library/Logs/Freehold Games/CavesOfQud/Player.log" ]' in player_log_line
    assert '[ -f "${path}" ]' in player_log_line
    assert "[ -e " not in player_log_line


def test_issue737_runtime_closeout_strict_recipe_requires_passed_status() -> None:
    """The strict Issue #737 closeout gate should fail until runtime evidence fully passes."""
    recipe = _recipe_body("issue737-runtime-closeout-strict")

    assert "scripts/issue737_runtime_closeout.py" in recipe
    assert "--log {{quote(log)}}" in recipe
    assert "--min-mtime {{quote(min_mtime)}}" in recipe
    assert "--output {{quote(output)}}" in recipe
    assert "--require-passed" in recipe
