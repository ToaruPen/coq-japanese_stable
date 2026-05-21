"""Static contract tests for the pull-request CI workflow."""

from __future__ import annotations

import re
from pathlib import Path

from scripts.tests.test_validate_pattern_routes import _EXPECTED_MESSAGE_ROUTE_COUNTS

_REPO_ROOT = Path(__file__).resolve().parents[2]


def _workflow_text() -> str:
    return (_REPO_ROOT / ".github" / "workflows" / "ci.yml").read_text(encoding="utf-8")


def _job_block(workflow: str, job_name: str, next_job_name: str | None) -> str:
    start = workflow.index(f"\n  {job_name}:\n")
    end = len(workflow) if next_job_name is None else workflow.index(f"\n  {next_job_name}:\n")
    return workflow[start:end]


def _step_block(job: str, step_name: str, next_step_name: str | None) -> str:
    start = job.index(f"\n      - name: {step_name}\n")
    if next_step_name is None:
        return job[start:]
    end = job.index(f"\n      - name: {next_step_name}\n", start + 1)
    return job[start:end]


def test_ci_keeps_required_build_check_as_aggregator() -> None:
    """The branch-protection-facing build job stays stable while work runs in parallel jobs."""
    workflow = _workflow_text()
    build_job = _job_block(workflow, "build", None)

    assert "Check required jobs" in build_job
    for job_name in ("qudjp-dotnet-build", "qudjp-dotnet-test", "roslyn-tools", "python"):
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


def test_ci_keeps_analyzers_on_production_build_and_skips_them_for_test_artifact() -> None:
    """The CI test DLL should build cheaply without dropping the production analyzer gate."""
    workflow = _workflow_text()
    job = _job_block(workflow, "qudjp-dotnet-build", "qudjp-dotnet-test")
    production_build = _step_block(job, "Build QudJP", "Build QudJP.Tests")
    test_artifact_build = _step_block(job, "Build QudJP.Tests", "Upload QudJP test artifact")

    assert "dotnet build Mods/QudJP/Assemblies/QudJP.csproj --configuration Release" in production_build
    assert "-p:RunAnalyzers" not in production_build
    assert "-p:RunAnalyzersDuringBuild" not in production_build
    assert "dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj" in test_artifact_build
    assert "-p:RunAnalyzers=false" in test_artifact_build
    assert "-p:RunAnalyzersDuringBuild=false" in test_artifact_build


def test_ci_checks_out_repo_for_qudjp_test_matrix() -> None:
    """C# tests need repository assets and source files in addition to the built DLL."""
    workflow = _workflow_text()
    job = _job_block(workflow, "qudjp-dotnet-test", "roslyn-tools")

    assert "uses: actions/checkout@v5" in job
    assert job.index("uses: actions/checkout@v5") < job.index("uses: actions/download-artifact@v4")


def test_ci_runs_python_and_dotnet_lanes_as_independent_jobs() -> None:
    """Core Python tests should not wait for QudJP C# or Roslyn scan lanes."""
    workflow = _workflow_text()
    python_job = _job_block(workflow, "python", "localization")

    assert "\n  detect-changes:\n" in workflow
    assert "pytest scripts/tests/" in python_job
    assert "qudjp-dotnet-test" not in python_job
    assert "roslyn-tools" not in python_job


def test_ci_python_lane_restores_repo_local_node_tools() -> None:
    """Python/agent-tool tests should use package-lock pinned Node tools."""
    workflow = _workflow_text()
    python_job = _job_block(workflow, "python", "localization")
    node_bin_path_step = 'echo "$PWD/node_modules/.bin" >> "$GITHUB_PATH"'

    assert "npm ci" in python_job
    assert node_bin_path_step in python_job
    assert "pytest scripts/tests/" in python_job
    assert python_job.index("npm ci") < python_job.index(node_bin_path_step)
    assert python_job.index(node_bin_path_step) < python_job.index("pytest scripts/tests/")
    assert "npm install -g @ast-grep/cli" not in python_job


def test_ci_python_lane_reports_slowest_test_durations() -> None:
    """CI logs should keep enough timing detail to diagnose future pytest slowdowns."""
    workflow = _workflow_text()
    python_job = _job_block(workflow, "python", "localization")

    assert "pytest scripts/tests/" in python_job
    assert "--durations=30" in python_job
    assert "--ignore=scripts/tests/test_roslyn_extractor_smoke.py" in python_job
    assert "--ignore=scripts/tests/test_scan_static_producer_inventory.py" in python_job


def test_ci_roslyn_lane_runs_scan_backed_pytest_separately() -> None:
    """Roslyn/scan pytest coverage should run in the Roslyn lane, not the core Python lane."""
    workflow = _workflow_text()
    roslyn_job = _job_block(workflow, "roslyn-tools", "python")

    assert "uses: actions/setup-dotnet@v5" in roslyn_job
    assert "uses: actions/setup-python@v6" in roslyn_job
    assert "from scripts.dotnet_tool_runner import build_tool_project" in roslyn_job
    assert "Test Roslyn-backed Python tools" in roslyn_job
    assert "scripts/tests/test_roslyn_semantic_probe.py" in roslyn_job
    assert "scripts/tests/test_scan_static_producer_inventory.py" in roslyn_job
    assert "--durations=20" in roslyn_job


def test_ci_roslyn_gate_covers_scan_wrapper_changes() -> None:
    """Changes to Roslyn-backed Python wrappers and tests should trigger the Roslyn lane."""
    workflow = _workflow_text()

    assert "matches_roslyn_python_tests" in workflow
    assert "dotnet_tool_runner" in workflow
    assert "docs/static-producer-inventory\\.json" in workflow
    assert "roslyn_semantic_probe" in workflow
    assert "scan_static_producer_inventory" in workflow
    assert "test_roslyn_text_construction_inventory" in workflow


def test_ci_qudjp_test_matrix_uploads_category_test_results() -> None:
    """C# matrix legs should publish per-category TRX results for timing and failure review."""
    workflow = _workflow_text()
    job = _job_block(workflow, "qudjp-dotnet-test", "roslyn-tools")

    assert '--logger "trx;LogFileName=qudjp-${{ matrix.category }}.trx"' in job
    assert "--results-directory TestResults/qudjp-${{ matrix.category }}" in job
    assert "Upload QudJP ${{ matrix.category }} test results" in job
    assert "if: always()" in job
    assert "uses: actions/upload-artifact@v4" in job
    assert "name: qudjp-test-results-${{ matrix.category }}" in job
    assert "path: TestResults/qudjp-${{ matrix.category }}/*.trx" in job
    assert "if-no-files-found: ignore" in job


def test_ci_package_lock_changes_trigger_python_tool_checks() -> None:
    """Node tool dependency changes must exercise the Python/agent tooling lane."""
    workflow = _workflow_text()

    assert "package(-lock)?\\.json" in workflow


def test_ci_dotnet_tool_manifest_changes_trigger_python_tool_checks() -> None:
    """The pinned csharp-ls manifest must exercise agent tooling checks."""
    workflow = _workflow_text()

    assert "dotnet-tools\\.json" in workflow


def test_ci_codex_hook_changes_trigger_python_tool_checks() -> None:
    """Repo-local Codex hooks are shell/tooling and must exercise Python tests."""
    workflow = _workflow_text()

    assert "\\.codex/hooks/" in workflow
    assert "\\.codex/hooks\\.json" in workflow


def test_ci_installs_just_without_apt_update() -> None:
    """The justfile-only lane should avoid apt package-index overhead."""
    workflow = _workflow_text()

    assert "uses: extractions/setup-just@v3" in workflow
    assert "sudo apt-get update" not in workflow


def test_ci_message_pattern_route_counts_match_python_contract() -> None:
    """CI and repository tests must enforce the same message-pattern route inventory."""
    workflow = _workflow_text()
    localization_job = _job_block(workflow, "localization", "justfile")
    matches = re.findall(r"--expect-count ([\w-]+)=(\d+)", localization_job)
    observed: dict[str, int] = {}
    for route, count in matches:
        assert route not in observed, f"duplicate --expect-count for route: {route}"
        observed[route] = int(count)

    assert observed == _EXPECTED_MESSAGE_ROUTE_COUNTS
