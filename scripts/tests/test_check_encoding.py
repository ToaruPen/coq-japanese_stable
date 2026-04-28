"""Tests for the check_encoding module."""

from pathlib import Path

import pytest

from scripts.check_encoding import check_directory, check_file, check_paths, main


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

    def test_md_file_skips_mojibake_check(self, tmp_path: Path) -> None:
        """A .md file with mojibake characters is not flagged for MOJIBAKE."""
        f = tmp_path / "docs.md"
        f.write_text("Example mojibake: 繧縺\n", encoding="utf-8")
        issues = check_file(f)
        kinds = {issue.kind for issue in issues}
        assert "MOJIBAKE" not in kinds

    def test_md_file_still_checks_bom(self, tmp_path: Path) -> None:
        """A .md file with BOM is still flagged for BOM."""
        f = tmp_path / "bom.md"
        f.write_bytes(b"\xef\xbb\xbf# Title\n")
        issues = check_file(f)
        kinds = {issue.kind for issue in issues}
        assert "BOM" in kinds
        assert "MOJIBAKE" not in kinds


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

    def test_recursive_scan_skips_python_bytecode_cache(self, tmp_path: Path) -> None:
        """Bytecode caches are ignored during recursive scans."""
        cache = tmp_path / "__pycache__"
        cache.mkdir()
        (cache / "module.cpython-312.pyc").write_bytes(b"\x80\r\n")

        assert check_directory(tmp_path) == []

    def test_recursive_scan_skips_generated_build_directories(self, tmp_path: Path) -> None:
        """Generated build outputs are ignored during recursive scans."""
        extractor = tmp_path / "scripts" / "tools" / "AnnalsPatternExtractor"
        bin_dir = extractor / "bin" / "Debug" / "net10.0"
        obj_dir = extractor / "obj" / "Debug" / "net10.0"
        bin_dir.mkdir(parents=True)
        obj_dir.mkdir(parents=True)
        (bin_dir / "AnnalsPatternExtractor.dll").write_bytes(b"\x80\r\n")
        (obj_dir / "AnnalsPatternExtractor.assets.cache").write_bytes(b"\x81\r\n")

        assert check_directory(tmp_path) == []

    def test_python_files_skip_mojibake_check(self, tmp_path: Path) -> None:
        """Python fixtures may contain mojibake sentinels without failing the scan."""
        script = tmp_path / "fixture.py"
        script.write_text('_MOJIBAKE_CHARS = "繧縺驕蜒"\n', encoding="utf-8")

        assert check_file(script) == []

    def test_multiple_paths_are_scanned(self, tmp_path: Path) -> None:
        """File and directory inputs can be checked in one invocation."""
        clean_dir = tmp_path / "clean"
        clean_dir.mkdir()
        (clean_dir / "ok.txt").write_text("ok\n", encoding="utf-8")
        bad_file = tmp_path / "bad.txt"
        bad_file.write_bytes(b"\xef\xbb\xbfbad\n")

        issues = check_paths([clean_dir, bad_file])

        assert [issue.kind for issue in issues] == ["BOM"]

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

    def test_multiple_clean_paths_return_zero(self, tmp_path: Path) -> None:
        """Multiple path arguments are accepted."""
        first = tmp_path / "first"
        second = tmp_path / "second"
        first.mkdir()
        second.mkdir()
        (first / "a.txt").write_bytes(b"clean\n")
        (second / "b.txt").write_bytes(b"also clean\n")

        assert main([str(first), str(second)]) == 0

    def test_issues_return_one(self, tmp_path: Path) -> None:
        """Directory with issues results in exit code 1."""
        (tmp_path / "bad.txt").write_bytes(b"\xef\xbb\xbfbom\n")
        assert main([str(tmp_path)]) == 1

    def test_nonexistent_directory_returns_one(self) -> None:
        """Nonexistent directory results in exit code 1."""
        assert main(["/nonexistent/path/abc123"]) == 1


class TestCheckFileValidation:
    """Tests for check_file input validation."""

    def test_nonexistent_file_raises(self) -> None:
        """check_file raises FileNotFoundError for a missing path."""
        with pytest.raises(FileNotFoundError, match="File not found"):
            check_file(Path("/tmp/nonexistent_qudjp"))  # noqa: S108 -- intentional nonexistent path for test

    def test_directory_raises_value_error(self, tmp_path: Path) -> None:
        """check_file raises ValueError when given a directory."""
        with pytest.raises(ValueError, match="Not a regular file"):
            check_file(tmp_path)
