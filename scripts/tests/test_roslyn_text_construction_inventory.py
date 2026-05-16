"""Smoke tests for the Roslyn text construction inventory tool."""
# ruff: noqa: S603 -- tests invoke dotnet to drive the repo-local tool

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from typing import Any

import pytest

from scripts.dotnet_tool_runner import build_tool_project

_REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_PATH = _REPO_ROOT / "scripts" / "tools" / "TextConstructionInventory" / "TextConstructionInventory.csproj"


@pytest.fixture(scope="session")
def inventory_tool_dll() -> Path:
    """Build the inventory tool once and reuse its DLL for smoke tests."""
    if not shutil.which("dotnet"):
        pytest.skip("dotnet SDK not available")
    return build_tool_project(PROJECT_PATH)


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_roslyn_inventory_emits_raw_free_deterministic_family_summary(  # noqa: PLR0915
    tmp_path: Path,
    inventory_tool_dll: Path,
) -> None:
    """The Roslyn tool records construction shapes and surfaces without source text."""
    source_root = tmp_path / "source"
    source_root.mkdir()
    source_file = source_root / "Demo.cs"
    source_file.write_text(
        """
using System;
using System.Text;

namespace Demo;

public sealed class TextRoutes
{
    public string Build(string name)
    {
        var builder = new StringBuilder();
        builder.Append("Part");
        Popup.Show("Hello " + name);
        MessageQueue.AddPlayerMessage($"Warning {name}");
        DisplayName = string.Format("Name {0}", name);
        return "Done";
    }

    public string DisplayName { get; set; } = "";

    public string GetDisplayName() => "Shown";

    public string Overload(string value) => "Plain";

    public string Overload<T>(string value) => "Generic";

    public string Overload(ref string value) => "ByRef";

    public string Overload(out string value) => "Out";

    public string Overload(in string value) => "In";
}

public static class Popup
{
    public static void Show(string message) {}
}

public static class MessageQueue
{
    public static void AddPlayerMessage(string message) {}
}
""",
        encoding="utf-8",
    )
    (source_root / "Broken.cs").write_text(
        "public sealed class Broken {",
        encoding="utf-8",
    )

    output_a = tmp_path / "inventory-a.json"
    output_b = tmp_path / "inventory-b.json"
    summary_output = tmp_path / "inventory-summary.md"

    _run_tool(inventory_tool_dll, source_root, output_a, summary_output=summary_output)
    _run_tool(inventory_tool_dll, source_root, output_b)

    assert output_a.read_text(encoding="utf-8") == output_b.read_text(encoding="utf-8")
    payload = json.loads(output_a.read_text(encoding="utf-8"))

    assert payload["generation"]["parser"] == "Microsoft.CodeAnalysis.CSharp"
    assert payload["generation"]["includes_raw_source_text"] is False
    assert payload["generation"]["includes_raw_english_text"] is False
    assert payload["generation"]["parse_error_file_count"] == 1
    assert payload["generation"]["parse_error_files"] == ["Broken.cs"]
    assert "source_root" not in payload

    serialized = json.dumps(payload, ensure_ascii=False)
    assert "Hello" not in serialized
    assert "Warning" not in serialized
    assert "Done" not in serialized
    assert "Part" not in serialized
    assert "Name {0}" not in serialized
    assert "Plain" not in serialized
    assert "Generic" not in serialized
    assert "ByRef" not in serialized
    assert "Out" not in serialized
    assert '"In"' not in serialized

    totals = payload["totals"]
    assert totals["surface_counts"]["Popup"] == 1
    assert totals["surface_counts"]["AddPlayerMessage"] == 1
    assert totals["surface_counts"]["StringBuilderAppend"] == 1
    assert totals["surface_counts"]["StringFormat"] == 1
    assert totals["surface_counts"]["DisplayNameAssignment"] == 1
    assert totals["surface_counts"]["DisplayNameReturn"] == 1
    assert totals["surface_counts"]["Return"] == 6
    assert totals["shape_counts"]["concatenation"] == 1
    assert totals["shape_counts"]["interpolation"] == 1
    assert totals["shape_counts"]["static_literal"] == 10

    family = _family(payload, "Demo.cs::TextRoutes.Build(string)")
    assert family["surface_counts"] == {
        "AddPlayerMessage": 1,
        "Popup": 1,
        "Return": 1,
        "DisplayNameAssignment": 1,
        "StringBuilderAppend": 1,
        "StringFormat": 1,
    }
    assert family["text_construction_count"] == 5
    assert family["member_signature"] == "Build(string)"

    display_family = _family(payload, "Demo.cs::TextRoutes.GetDisplayName()")
    assert display_family["context_counts"] == {"return_expression": 1}
    assert display_family["surface_counts"] == {"DisplayNameReturn": 1}

    assert _family(payload, "Demo.cs::TextRoutes.Overload(string)")["member_signature"] == "Overload(string)"
    assert _family(payload, "Demo.cs::TextRoutes.Overload<T>(string)")["member_signature"] == "Overload<T>(string)"
    assert _family(payload, "Demo.cs::TextRoutes.Overload(ref string)")["member_signature"] == "Overload(ref string)"
    assert _family(payload, "Demo.cs::TextRoutes.Overload(out string)")["member_signature"] == "Overload(out string)"
    assert _family(payload, "Demo.cs::TextRoutes.Overload(in string)")["member_signature"] == "Overload(in string)"

    summary = summary_output.read_text(encoding="utf-8")
    assert "# Roslyn Text Construction Inventory Summary" in summary
    assert "| Producer/member families | 8 |" in summary
    assert "| Text constructions | 12 |" in summary
    assert "- `Broken.cs`" in summary
    assert "`DisplayNameAssignment`" in summary
    assert "full generated family inventory is intentionally not committed" in summary
    assert "Hello" not in summary
    assert "Warning" not in summary
    assert "Done" not in summary


@pytest.mark.skipif(not shutil.which("dotnet"), reason="dotnet SDK not available")
def test_roslyn_inventory_csproj_builds_in_release(inventory_tool_dll: Path) -> None:
    """The shared Release-build fixture must produce the Roslyn inventory DLL."""
    assert inventory_tool_dll.is_file()


def _run_tool(tool_dll: Path, source_root: Path, output: Path, *, summary_output: Path | None = None) -> None:
    command = [
        "dotnet",
        str(tool_dll),
        "--source-root",
        str(source_root),
        "--output",
        str(output),
    ]
    if summary_output is not None:
        command.extend(["--summary-output", str(summary_output)])

    result = subprocess.run(
        command,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode == 0, (
        f"inventory tool failed (exit {result.returncode}).\nstdout:\n{result.stdout}\nstderr:\n{result.stderr}"
    )


def _family(payload: dict[str, Any], family_id: str) -> dict[str, Any]:
    matches = [family for family in payload["families"] if family["family_id"] == family_id]
    assert len(matches) == 1
    return matches[0]
