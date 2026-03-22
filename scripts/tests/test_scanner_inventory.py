"""Tests for the scanner.inventory module."""

from pathlib import Path

from scripts.scanner.inventory import (
    ExclusionReason,
    FileRecord,
    HitKind,
    RawHit,
    SourceFileInventory,
    read_raw_hits_jsonl,
    write_raw_hits_jsonl,
)


class TestRawHitsJsonl:
    """Tests for RawHit JSONL persistence."""

    def test_round_trips_raw_hits_jsonl(self, tmp_path: Path) -> None:
        """Raw hits persist without losing fields or enum values."""
        hits = [
            RawHit(
                hit_kind=HitKind.SINK,
                family="Popup",
                pattern="Popup.Show($$$)",
                file="Demo/PatternCoverage.cs",
                line=19,
                column=9,
                matched_code='Popup.Show("hello")',
            ),
            RawHit(
                hit_kind=HitKind.OVERRIDE,
                family="Effects.GetDescription",
                pattern=r"override\b.*\bGetDescription\s*\(",
                file="XRL.World.Effects/TestEffect.cs",
                line=10,
                column=28,
                matched_code="public override string GetDescription()",
            ),
        ]

        output_path = tmp_path / "raw_hits.jsonl"
        write_raw_hits_jsonl(output_path, hits)

        assert read_raw_hits_jsonl(output_path) == hits


class TestSourceFileInventory:
    """Tests for SourceFileInventory helpers."""

    def test_included_and_excluded_views_are_stable(self) -> None:
        """Inventory exposes deterministic included and excluded file records."""
        inventory = SourceFileInventory(
            files=(
                FileRecord(path="keep.cs", included=True),
                FileRecord(
                    path="drop.retry.cs",
                    included=False,
                    exclusion_reason=ExclusionReason.RETRY_ARTIFACT,
                ),
            ),
        )

        assert inventory.included_file_count == 1
        assert [record.path for record in inventory.included_files] == ["keep.cs"]
        assert [record.path for record in inventory.excluded_files] == ["drop.retry.cs"]

