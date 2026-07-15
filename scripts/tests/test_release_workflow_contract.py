"""Static contract tests for the GitHub Release workflow."""

from __future__ import annotations

from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parents[2]


def _workflow_text() -> str:
    return (_REPO_ROOT / ".github" / "workflows" / "release.yml").read_text(encoding="utf-8")


def test_release_workflow_runs_only_for_release_tags() -> None:
    """Release publishing is triggered by vX.Y.Z tags, not branch pushes or PRs."""
    workflow = _workflow_text()

    assert "pull_request:" not in workflow
    assert "workflow_dispatch:" not in workflow
    assert "schedule:" not in workflow
    assert "repository_dispatch:" not in workflow
    assert "branches:" not in workflow
    assert "tags:" in workflow
    assert '"v*.*.*"' in workflow


def test_release_workflow_requires_main_ancestor_and_identity_validation() -> None:
    """Tag releases must prove main ancestry and version identity before publishing."""
    workflow = _workflow_text()

    assert "git merge-base --is-ancestor" in workflow
    assert "origin/main" in workflow
    assert "scripts/release_identity.py" in workflow
    assert "--main-ref origin/main" in workflow


def test_release_workflow_installs_python_test_tooling() -> None:
    """Release full-suite Python tests must have their external CLI dependencies."""
    workflow = _workflow_text()
    node_bin_path_step = 'echo "$PWD/node_modules/.bin" >> "$GITHUB_PATH"'

    assert "pip install hypothesis pytest ruff uv" in workflow
    assert "npm ci" in workflow
    assert node_bin_path_step in workflow
    assert "npm install -g @ast-grep/cli" not in workflow
    assert "pytest scripts/tests/" in workflow
    assert workflow.index("npm ci") < workflow.index(node_bin_path_step)
    assert workflow.index(node_bin_path_step) < workflow.index("pytest scripts/tests/")
    assert workflow.index("npm ci") < workflow.index("pytest scripts/tests/")


def test_release_workflow_creates_draft_github_release_without_steam_upload() -> None:
    """GitHub Release artifact creation stays separate from Steam Workshop upload."""
    workflow = _workflow_text()

    assert "gh release create" in workflow
    assert "GH_REPO: ${{ github.repository }}" in workflow
    assert "--draft" in workflow
    assert "--latest" in workflow
    assert "--latest=false" not in workflow
    assert "contents: write" in workflow
    assert "workshop_build_item" not in workflow


def test_release_workflow_builds_and_publishes_harmony_patch_assets() -> None:
    """Tag releases attach the standalone Harmony ZIP and checksum everywhere."""
    workflow = _workflow_text()
    harmony_zip = "QudJP-Harmony-2.4.2-Windows.zip"
    harmony_sidecar = f"{harmony_zip}.sha256"

    harmony_build_command = "uv run python scripts/build_harmony_patch.py"
    upload_start = workflow.index("      - name: Upload release artifact")
    release_start = workflow.index("      - name: Create draft GitHub Release")
    upload_block = workflow[upload_start:release_start]
    release_block = workflow[release_start:]

    assert harmony_build_command in workflow
    assert workflow.index("python scripts/build_release.py") < workflow.index(harmony_build_command)
    for block in (upload_block, release_block):
        assert f"dist/{harmony_zip}" in block
        assert f"dist/{harmony_sidecar}" in block
    assert "## Optional Harmony 2.4.2 Windows Patch" in workflow
    assert f"ZIP: \\`{harmony_zip}\\`" in workflow
    assert "Harmony ZIP SHA256:" in workflow
