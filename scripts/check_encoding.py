"""Validate file encoding: UTF-8 without BOM, LF line endings, no mojibake."""

import argparse
import sys
from dataclasses import dataclass
from pathlib import Path

_BOM = b"\xef\xbb\xbf"
_MOJIBAKE_CHARS = "繧縺驕蜒"


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
    and invalid UTF-8 encoding.

    Args:
        path: Path to the file to check.

    Returns:
        List of encoding issues found. Empty if the file is clean.
    """
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

    for char in _MOJIBAKE_CHARS:
        if char in text:
            issues.append(
                EncodingIssue(path, "MOJIBAKE", f"Suspected mojibake character: {char}"),
            )
            break

    return issues


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

    all_issues: list[EncodingIssue] = []
    for file_path in sorted(directory.rglob("*")):
        if file_path.is_file():
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
    parser.add_argument("directory", type=Path, help="Directory to scan recursively")
    args = parser.parse_args(argv)

    directory: Path = args.directory
    try:
        issues = check_directory(directory)
    except (FileNotFoundError, NotADirectoryError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    total_files = sum(1 for p in directory.rglob("*") if p.is_file())
    _print_report(issues, total_files)
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
