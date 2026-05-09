"""Static contract tests for the pull-request CI workflow."""

from __future__ import annotations

from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parents[2]


def _workflow_text() -> str:
    return (_REPO_ROOT / ".github" / "workflows" / "ci.yml").read_text(encoding="utf-8")


def _job_block(workflow: str, job_name: str, next_job_name: str | None) -> str:
    start = workflow.index(f"\n  {job_name}:\n")
    end = len(workflow) if next_job_name is None else workflow.index(f"\n  {next_job_name}:\n")
    return workflow[start:end]


def test_ci_keeps_required_build_check_as_aggregator() -> None:
    """The branch-protection-facing build job stays stable while work runs in parallel jobs."""
    workflow = _workflow_text()
    build_job = _job_block(workflow, "build", None)

    assert "Check required jobs" in build_job
    for job_name in ("qudjp-dotnet-build", "qudjp-dotnet-test", "python"):
        assert f"      - {job_name}" in build_job
        assert f'check_result {job_name} "${{{{ needs.{job_name}.result }}}}"' in build_job


def test_ci_splits_qudjp_test_categories_into_matrix() -> None:
    """C# L1/L2/L2G categories should run as separate matrix legs after a shared build."""
    workflow = _workflow_text()

    assert "category: [L1, L2, L2G]" in workflow
    assert "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/bin/Release/net10.0/QudJP.Tests.dll" in workflow
    assert '--filter "TestCategory=${{ matrix.category }}"' in workflow
    assert "actions/upload-artifact" in workflow
    assert "actions/download-artifact" in workflow


def test_ci_checks_out_repo_for_qudjp_test_matrix() -> None:
    """C# tests need repository assets and source files in addition to the built DLL."""
    workflow = _workflow_text()
    job = _job_block(workflow, "qudjp-dotnet-test", "roslyn-tools")

    assert "uses: actions/checkout@v5" in job
    assert job.index("uses: actions/checkout@v5") < job.index("uses: actions/download-artifact@v4")


def test_ci_runs_python_and_dotnet_lanes_as_independent_jobs() -> None:
    """Python tests should not wait for the QudJP C# test matrix."""
    workflow = _workflow_text()
    python_job = _job_block(workflow, "python", "localization")

    assert "\n  detect-changes:\n" in workflow
    assert "pytest scripts/tests/" in python_job
    assert "qudjp-dotnet-test" not in python_job


def test_ci_installs_just_without_apt_update() -> None:
    """The justfile-only lane should avoid apt package-index overhead."""
    workflow = _workflow_text()

    assert "uses: extractions/setup-just@v3" in workflow
    assert "sudo apt-get update" not in workflow
