"""Tests for the check_encoding module."""

from pathlib import Path

import pytest

from scripts.check_encoding import check_directory, check_file, main


class TestCheckFile:
    """Tests for the check_file function."""

    def test_clean_file_has_no_issues(self, tmp_path: Path) -> None:
        """A valid UTF-8 file with LF endings reports no issues."""
        f = tmp_path / "clean.txt"
        f.write_bytes(b"Hello, world!\n")
        assert check_file(f) == []

    def test_bom_detected(self, tmp_path: Path) -> None:
        """A file with UTF-8 BOM is flagged."""
        f = tmp_path / "bom.txt"
        f.write_bytes(b"\xef\xbb\xbfHello\n")
        issues = check_file(f)
        assert len(issues) == 1
        assert issues[0].kind == "BOM"

    def test_crlf_detected(self, tmp_path: Path) -> None:
        """A file with CRLF line endings is flagged."""
        f = tmp_path / "crlf.txt"
        f.write_bytes(b"Hello\r\nWorld\r\n")
        issues = check_file(f)
        assert len(issues) == 1
        assert issues[0].kind == "CRLF"

    def test_mojibake_detected(self, tmp_path: Path) -> None:
        """A file containing mojibake characters is flagged."""
        f = tmp_path / "mojibake.txt"
        f.write_text("This has 繧 mojibake\n", encoding="utf-8")
        issues = check_file(f)
        assert len(issues) == 1
        assert issues[0].kind == "MOJIBAKE"

    def test_invalid_utf8_detected(self, tmp_path: Path) -> None:
        """A file with invalid UTF-8 bytes is flagged as DECODE error."""
        f = tmp_path / "invalid.bin"
        f.write_bytes(b"\x80\x81\x82\n")
        issues = check_file(f)
        assert len(issues) == 1
        assert issues[0].kind == "DECODE"

    def test_multiple_issues_reported(self, tmp_path: Path) -> None:
        """A file with BOM and CRLF reports both issues."""
        f = tmp_path / "multi.txt"
        f.write_bytes(b"\xef\xbb\xbfHello\r\n")
        issues = check_file(f)
        kinds = {issue.kind for issue in issues}
        assert "BOM" in kinds
        assert "CRLF" in kinds

    def test_empty_file_is_clean(self, tmp_path: Path) -> None:
        """An empty file should have no issues."""
        f = tmp_path / "empty.txt"
        f.write_bytes(b"")
        assert check_file(f) == []


class TestCheckDirectory:
    """Tests for the check_directory function."""

    def test_clean_directory(self, tmp_path: Path) -> None:
        """A directory with only clean files returns no issues."""
        (tmp_path / "a.txt").write_bytes(b"clean\n")
        (tmp_path / "b.txt").write_bytes(b"also clean\n")
        assert check_directory(tmp_path) == []

    def test_issues_across_files(self, tmp_path: Path) -> None:
        """Issues from multiple files are aggregated."""
        (tmp_path / "bom.txt").write_bytes(b"\xef\xbb\xbfHello\n")
        (tmp_path / "crlf.txt").write_bytes(b"Hello\r\n")
        issues = check_directory(tmp_path)
        kinds = {issue.kind for issue in issues}
        assert "BOM" in kinds
        assert "CRLF" in kinds

    def test_recursive_scan(self, tmp_path: Path) -> None:
        """Files in subdirectories are also scanned."""
        sub = tmp_path / "sub"
        sub.mkdir()
        (sub / "nested.txt").write_bytes(b"\xef\xbb\xbfBOM\n")
        issues = check_directory(tmp_path)
        assert len(issues) == 1
        assert issues[0].kind == "BOM"

    def test_nonexistent_directory_raises(self) -> None:
        """Scanning a nonexistent directory raises FileNotFoundError."""
        with pytest.raises(FileNotFoundError):
            check_directory(Path("/nonexistent/path/abc123"))

    def test_file_instead_of_directory_raises(self, tmp_path: Path) -> None:
        """Passing a file path instead of directory raises NotADirectoryError."""
        f = tmp_path / "file.txt"
        f.write_bytes(b"hello\n")
        with pytest.raises(NotADirectoryError):
            check_directory(f)


class TestMain:
    """Tests for the main CLI entry point."""

    def test_clean_directory_returns_zero(self, tmp_path: Path) -> None:
        """Clean directory results in exit code 0."""
        (tmp_path / "file.txt").write_bytes(b"clean\n")
        assert main([str(tmp_path)]) == 0

    def test_issues_return_one(self, tmp_path: Path) -> None:
        """Directory with issues results in exit code 1."""
        (tmp_path / "bad.txt").write_bytes(b"\xef\xbb\xbfbom\n")
        assert main([str(tmp_path)]) == 1

    def test_nonexistent_directory_returns_one(self) -> None:
        """Nonexistent directory results in exit code 1."""
        assert main(["/nonexistent/path/abc123"]) == 1
