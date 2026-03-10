"""Tests for the extract_base module."""

from pathlib import Path

import pytest

from scripts.extract_base import extract_xml_files


class TestExtractXmlFiles:
    """Tests for extract_xml_files."""

    def test_copies_xml_files(self, tmp_path: Path) -> None:
        """XML files are copied to the destination."""
        source = tmp_path / "source"
        source.mkdir()
        (source / "test.xml").write_text("<root/>\n", encoding="utf-8")

        dest = tmp_path / "dest"
        copied = extract_xml_files(source, dest)

        assert len(copied) == 1
        assert (dest / "test.xml").exists()
        assert (dest / "test.xml").read_text(encoding="utf-8") == "<root/>\n"

    def test_preserves_directory_structure(self, tmp_path: Path) -> None:
        """Subdirectory structure is preserved in the destination."""
        source = tmp_path / "source"
        sub = source / "sub"
        sub.mkdir(parents=True)
        (sub / "nested.xml").write_text("<nested/>\n", encoding="utf-8")

        dest = tmp_path / "dest"
        extract_xml_files(source, dest)

        assert (dest / "sub" / "nested.xml").exists()

    def test_skips_non_xml_files(self, tmp_path: Path) -> None:
        """Non-XML files are not copied."""
        source = tmp_path / "source"
        source.mkdir()
        (source / "data.json").write_text("{}\n", encoding="utf-8")

        dest = tmp_path / "dest"
        copied = extract_xml_files(source, dest)

        assert copied == []

    def test_skip_existing_without_force(self, tmp_path: Path) -> None:
        """Existing files are not overwritten when force is False."""
        source = tmp_path / "source"
        source.mkdir()
        (source / "test.xml").write_text("<new/>\n", encoding="utf-8")

        dest = tmp_path / "dest"
        dest.mkdir()
        (dest / "test.xml").write_text("<old/>\n", encoding="utf-8")

        extract_xml_files(source, dest, force=False)
        assert (dest / "test.xml").read_text(encoding="utf-8") == "<old/>\n"

    def test_overwrite_with_force(self, tmp_path: Path) -> None:
        """Existing files are overwritten when force is True."""
        source = tmp_path / "source"
        source.mkdir()
        (source / "test.xml").write_text("<new/>\n", encoding="utf-8")

        dest = tmp_path / "dest"
        dest.mkdir()
        (dest / "test.xml").write_text("<old/>\n", encoding="utf-8")

        extract_xml_files(source, dest, force=True)
        assert (dest / "test.xml").read_text(encoding="utf-8") == "<new/>\n"

    def test_nonexistent_source_raises(self) -> None:
        """Nonexistent source directory raises FileNotFoundError."""
        with pytest.raises(FileNotFoundError, match="Source directory not found"):
            extract_xml_files(Path("/nonexistent/abc123"), Path("/dest"))

    def test_empty_source_returns_empty(self, tmp_path: Path) -> None:
        """Empty source directory results in no files copied."""
        source = tmp_path / "source"
        source.mkdir()

        dest = tmp_path / "dest"
        copied = extract_xml_files(source, dest)

        assert copied == []

    def test_multiple_xml_files(self, tmp_path: Path) -> None:
        """Multiple XML files are all copied."""
        source = tmp_path / "source"
        source.mkdir()
        (source / "a.xml").write_text("<a/>\n", encoding="utf-8")
        (source / "b.xml").write_text("<b/>\n", encoding="utf-8")

        dest = tmp_path / "dest"
        copied = extract_xml_files(source, dest)

        assert len(copied) == 2
        assert (dest / "a.xml").exists()
        assert (dest / "b.xml").exists()
