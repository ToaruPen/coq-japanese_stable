"""Validate file encoding: UTF-8 without BOM, LF line endings, no mojibake."""

import argparse
import sys
from dataclasses import dataclass
from pathlib import Path

_BOM = b"\xef\xbb\xbf"
_MOJIBAKE_PATTERNS = ("繧", "縺", "蜒", "驕ｿ")
_SKIPPED_DIR_NAMES = frozenset({"__pycache__", "bin", "obj"})
_SKIPPED_SUFFIXES = frozenset({".pyc", ".pyo"})
_MOJIBAKE_SKIP_SUFFIXES = frozenset({".md", ".py"})


@dataclass(frozen=True)
class EncodingIssue:
    """A single encoding issue found in a file.

    Attributes:
        path: Path to the file with the issue.
        kind: Type of issue (BOM, CRLF, MOJIBAKE, DECODE).
        detail: Human-readable description.
    """

    path: Path
    kind: str
    detail: str


def check_file(path: Path) -> list[EncodingIssue]:
    """Check a single file for encoding issues.

    Inspects for UTF-8 BOM, CRLF line endings, mojibake characters,
    and invalid UTF-8 encoding. Mojibake detection is skipped for
    Markdown (``.md``) and Python (``.py``) files, which may contain
    intentional mojibake examples as documentation or test fixtures.

    Args:
        path: Path to the file to check.

    Returns:
        List of encoding issues found. Empty if the file is clean.

    Raises:
        FileNotFoundError: If path does not exist.
        ValueError: If path is not a regular file.
    """
    if not path.exists():
        msg = f"File not found: {path}"
        raise FileNotFoundError(msg)
    if not path.is_file():
        msg = f"Not a regular file: {path}. Pass a file path, not a directory."
        raise ValueError(msg)
    issues: list[EncodingIssue] = []
    raw = path.read_bytes()

    if raw.startswith(_BOM):
        issues.append(EncodingIssue(path, "BOM", "UTF-8 BOM detected"))

    if b"\r\n" in raw:
        issues.append(EncodingIssue(path, "CRLF", "Windows line endings (CRLF) detected"))

    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        issues.append(EncodingIssue(path, "DECODE", "File is not valid UTF-8"))
        return issues

    if path.suffix.lower() not in _MOJIBAKE_SKIP_SUFFIXES:
        for pattern in _MOJIBAKE_PATTERNS:
            if pattern in text:
                issues.append(
                    EncodingIssue(path, "MOJIBAKE", f"Suspected mojibake pattern: {pattern}"),
                )
                break

    return issues


def _is_checkable_file(path: Path) -> bool:
    """Return whether a recursive scan should inspect ``path``."""
    if any(part in _SKIPPED_DIR_NAMES for part in path.parts):
        return False
    return path.suffix.lower() not in _SKIPPED_SUFFIXES


def _iter_checkable_files(paths: list[Path]) -> list[Path]:
    """Collect files that should be scanned from file and directory inputs."""
    files: set[Path] = set()
    for input_path in paths:
        if not input_path.exists():
            msg = f"Path not found: {input_path}"
            raise FileNotFoundError(msg)

        if input_path.is_file():
            if _is_checkable_file(input_path):
                files.add(input_path)
            continue

        if input_path.is_dir():
            files.update(path for path in input_path.rglob("*") if path.is_file() and _is_checkable_file(path))
            continue

        msg = f"Path is not a regular file or directory: {input_path}"
        raise ValueError(msg)

    return sorted(files, key=str)


def check_directory(directory: Path) -> list[EncodingIssue]:
    """Check all files under a directory recursively for encoding issues.

    Args:
        directory: Root directory to scan.

    Returns:
        List of all encoding issues found across all files.

    Raises:
        FileNotFoundError: If directory does not exist.
        NotADirectoryError: If path is not a directory.
    """
    if not directory.exists():
        msg = f"Directory not found: {directory}"
        raise FileNotFoundError(msg)
    if not directory.is_dir():
        msg = f"Not a directory: {directory}"
        raise NotADirectoryError(msg)

    return check_paths([directory])


def check_paths(paths: list[Path]) -> list[EncodingIssue]:
    """Check all scan-eligible files under the given paths."""
    all_issues: list[EncodingIssue] = []
    for file_path in _iter_checkable_files(paths):
        all_issues.extend(check_file(file_path))
    return all_issues


def _print_report(issues: list[EncodingIssue], total_files: int) -> None:
    """Print a human-readable summary of encoding scan results.

    Args:
        issues: List of issues found.
        total_files: Total number of files scanned.
    """
    issue_file_count = len({issue.path for issue in issues})
    ok_count = total_files - issue_file_count
    print(f"Scanned {total_files} files: {ok_count} OK, {len(issues)} issue(s)")  # noqa: T201
    for issue in issues:
        print(f"  [{issue.kind}] {issue.path} — {issue.detail}")  # noqa: T201


def main(argv: list[str] | None = None) -> int:
    """Run the encoding checker CLI.

    Args:
        argv: Command-line arguments. Defaults to sys.argv[1:].

    Returns:
        Exit code: 0 if all files are clean, 1 if issues found.
    """
    parser = argparse.ArgumentParser(
        description="Validate UTF-8 encoding, BOM absence, and LF line endings.",
    )
    parser.add_argument("paths", nargs="+", type=Path, help="Files or directories to scan recursively")
    args = parser.parse_args(argv)

    paths: list[Path] = args.paths
    try:
        files = _iter_checkable_files(paths)
        issues = []
        for file_path in files:
            issues.extend(check_file(file_path))
    except (FileNotFoundError, NotADirectoryError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    _print_report(issues, len(files))
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
