"""Tests for provenance cross-reference logic."""

from __future__ import annotations

from scripts.provenance.cross_reference import cross_reference
from scripts.provenance.models import DictEntry, GeneratorSignature, StringClassification


def _sidebar_sig() -> GeneratorSignature:
    """Return a structured-dynamic signature that should not drive generator overlap."""
    return GeneratorSignature(
        source_file="XRL_UI_Sidebar.cs",
        class_name="XRL.UI.Sidebar",
        method_name="FormatHP",
        classification=StringClassification.STRUCTURED_DYNAMIC,
        pattern_kind="string.Format",
        evidence_line=42,
    )


def _description_builder_sig() -> GeneratorSignature:
    """Return a generator-family signature rooted in XRL.World."""
    return GeneratorSignature(
        source_file="XRL_World_DescriptionBuilder.cs",
        class_name="XRL.World.DescriptionBuilder",
        method_name="ToString",
        classification=StringClassification.GENERATOR_FAMILY,
        pattern_kind="DescriptionBuilder.Add* composition",
        evidence_line=331,
    )


def test_flag_entry_matching_known_context() -> None:
    """Namespace overlap at shared depth two or greater is flagged."""
    entries = [
        DictEntry(
            dictionary_id="world-parts",
            key="Electrified: When powered, this weapon deals",
            text="通電：起動中、この武器は",  # noqa: RUF001
            context="XRL.World.Parts.Combat",
        )
    ]
    findings = cross_reference(entries, [_description_builder_sig()])
    assert len(findings) == 1
    assert "generator-family code" in findings[0].message


def test_exact_leaf_not_flagged() -> None:
    """Entries without context overlap are ignored."""
    entries = [
        DictEntry(
            dictionary_id="ui-default",
            key="Inventory",
            text="インベントリ",
            context=None,
        )
    ]
    findings = cross_reference(entries, [_sidebar_sig()])
    assert findings == []


def test_fragment_entry_near_dynamic_source_flagged() -> None:
    """Fragment keys plus generator overlap create a high-confidence finding."""
    entries = [
        DictEntry(
            dictionary_id="msg",
            key="You stagger ",
            text="よろめかせた：",  # noqa: RUF001
            context="XRL.World.Parts.Combat",
        )
    ]
    findings = cross_reference(entries, [_sidebar_sig(), _description_builder_sig()])
    assert len(findings) == 1
    assert "prefer a template or patch route" in findings[0].message
