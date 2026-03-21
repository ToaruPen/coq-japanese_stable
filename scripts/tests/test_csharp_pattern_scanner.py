"""Tests for the C# provenance pattern scanner."""

from __future__ import annotations

from pathlib import Path

import pytest

from scripts.provenance.csharp_pattern_scanner import scan_directory, scan_source_file
from scripts.provenance.models import StringClassification

_EXACT_LEAF_SOURCE = """\
using System;

namespace Qud.UI
{
    public static class Labels
    {
        public static string Title => \"Inventory\";
        public static string Subtitle => \"Equipment\";
    }
}
"""

_STRUCTURED_DYNAMIC_SOURCE = """\
using System;
using System.Text;

namespace Qud.UI
{
    public class Sidebar
    {
        public string FormatHP(int current, int max)
        {
            return string.Format(\"HP: {0}/{1}\", current, max);
        }

        public string BuildLine(string label, int value)
        {
            var sb = new StringBuilder();
            sb.Append(label);
            sb.Append(": ");
            sb.Append(value);
            return sb.ToString();
        }

        public string JoinPieces(string left, string right)
        {
            return string.Concat(left, right);
        }
    }
}
"""

_GENERATOR_FAMILY_SOURCE = """\
using System;

namespace XRL.World
{
    public class MyPart
    {
        public void Describe(DescriptionBuilder DB)
        {
            DB.AddAdjective(\"rusty\");
            DB.AddBase(\"sword\");
            DB.AddClause(\"of power\");
        }

        public string GetName(GetDisplayNameEvent E)
        {
            E.DB.AddTag(\"[equipped]\");
            return E.DB.ToString();
        }

        public string BuildArticle(string noun)
        {
            return Grammar.A(noun);
        }

        public string ExpandHistoric(string template)
        {
            return HistoricStringExpander.ExpandString(template);
        }

        public string ReplaceText(string text)
        {
            return GameText.VariableReplace(GameText.Process(text));
        }
    }
}
"""

_ILSPY_RAW_DIR = Path(__file__).resolve().parents[2] / "docs" / "ilspy-raw"


def _write_cs(path: Path, content: str) -> None:
    """Write a synthetic C# source file for scanner tests."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def test_scan_exact_leaf(tmp_path: Path) -> None:
    """Literal-only source produces no dynamic signatures."""
    path = tmp_path / "Qud_UI_Labels.cs"
    _write_cs(path, _EXACT_LEAF_SOURCE)
    assert scan_source_file(path) == []


def test_scan_structured_dynamic(tmp_path: Path) -> None:
    """StringBuilder, string.Format, and string.Concat are detected."""
    path = tmp_path / "Qud_UI_Sidebar.cs"
    _write_cs(path, _STRUCTURED_DYNAMIC_SOURCE)
    signatures = scan_source_file(path)
    dynamic_signatures = [
        signature for signature in signatures if signature.classification == StringClassification.STRUCTURED_DYNAMIC
    ]
    assert len(dynamic_signatures) == 3
    assert {signature.method_name for signature in dynamic_signatures} == {"FormatHP", "BuildLine", "JoinPieces"}


def test_scan_generator_family(tmp_path: Path) -> None:
    """Known generator-family APIs are classified at highest priority."""
    path = tmp_path / "XRL_World_MyPart.cs"
    _write_cs(path, _GENERATOR_FAMILY_SOURCE)
    signatures = scan_source_file(path)
    generator_signatures = [
        signature for signature in signatures if signature.classification == StringClassification.GENERATOR_FAMILY
    ]
    assert len(generator_signatures) == 5
    assert any("DescriptionBuilder" in signature.pattern_kind for signature in generator_signatures)
    assert {signature.method_name for signature in generator_signatures} == {
        "Describe",
        "GetName",
        "BuildArticle",
        "ExpandHistoric",
        "ReplaceText",
    }


def test_scan_unrelated_add_method_not_generator(tmp_path: Path) -> None:
    """Arbitrary Add calls do not get mislabeled as DescriptionBuilder generators."""
    path = tmp_path / "Qud_UI_CustomList.cs"
    _write_cs(
        path,
        """\
using System.Collections.Generic;

namespace Qud.UI
{
    public class CustomList
    {
        public void AddName(List<string> items, string name)
        {
            items.Add(name);
        }
    }
}
""",
    )
    signatures = scan_source_file(path)
    assert not any(signature.classification == StringClassification.GENERATOR_FAMILY for signature in signatures)


def test_scan_ignores_braces_inside_strings_when_splitting_methods(tmp_path: Path) -> None:
    """Method splitting ignores literal braces so later methods still scan correctly."""
    path = tmp_path / "Qud_UI_MarkupBuilder.cs"
    _write_cs(
        path,
        """\
namespace Qud.UI
{
    public class MarkupBuilder
    {
        public string PrefixOnly()
        {
            return "{{";
        }

        public string FormatHP(int current, int max)
        {
            return string.Format("HP: {0}/{1}", current, max);
        }
    }
}
""",
    )
    signatures = scan_source_file(path)
    dynamic_methods = [
        signature.method_name
        for signature in signatures
        if signature.classification == StringClassification.STRUCTURED_DYNAMIC
    ]
    assert "FormatHP" in dynamic_methods


@pytest.mark.skipif(not _ILSPY_RAW_DIR.exists(), reason="ilspy-raw not available")
def test_scan_description_builder_real() -> None:
    """DescriptionBuilder decompile yields at least one generator-family signature."""
    path = _ILSPY_RAW_DIR / "XRL_World_DescriptionBuilder.cs"
    if not path.exists():
        pytest.skip("DescriptionBuilder decompile not available")
    signatures = scan_source_file(path)
    assert any(signature.classification == StringClassification.GENERATOR_FAMILY for signature in signatures)


@pytest.mark.skipif(not _ILSPY_RAW_DIR.exists(), reason="ilspy-raw not available")
def test_scan_sidebar_real() -> None:
    """Sidebar decompile yields at least one structured-dynamic signature."""
    path = _ILSPY_RAW_DIR / "XRL_UI_Sidebar.cs"
    if not path.exists():
        pytest.skip("Sidebar decompile not available")
    signatures = scan_source_file(path)
    assert any(signature.classification == StringClassification.STRUCTURED_DYNAMIC for signature in signatures)


@pytest.mark.skipif(not _ILSPY_RAW_DIR.exists(), reason="ilspy-raw not available")
def test_scan_full_directory() -> None:
    """Full-directory scan returns a non-trivial signature catalog."""
    signatures = scan_directory(_ILSPY_RAW_DIR)
    assert len(signatures) >= 10
    classifications = {signature.classification for signature in signatures}
    assert StringClassification.GENERATOR_FAMILY in classifications
    assert StringClassification.STRUCTURED_DYNAMIC in classifications
