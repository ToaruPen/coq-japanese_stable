"""Contracts for current target-game version surfaces and DLL identity."""

from __future__ import annotations

import json
import re
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]

TARGET_VERSION_PATTERN = re.compile(
    r"対象ゲームバージョン\*\*: Caves of Qud (?P<version>\d+\.\d+\.\d+)"
)
CURRENT_VERSION_SURFACES = {
    "AGENTS.md": re.compile(
        r"Japanese localization mod for Caves of Qud `(?P<version>\d+\.\d+\.\d+)`"
    ),
    "CODERABBIT.md": re.compile(
        r"inspection of Caves of Qud (?P<version>\d+\.\d+\.\d+) runtime behavior"
    ),
    "Mods/QudJP/Assemblies/AGENTS.md": re.compile(
        r"patch behavior for Caves of Qud `(?P<version>\d+\.\d+\.\d+)`"
    ),
    "Mods/QudJP/Localization/AGENTS.md": re.compile(
        r"IDs against game version `(?P<version>\d+\.\d+\.\d+)`"
    ),
    "NOTICE.md": re.compile(
        r"Targeted game version: Caves of Qud (?P<version>\d+\.\d+\.\d+)"
    ),
    "steam/workshop_description.ja.txt": re.compile(
        r"Caves of Qud (?P<version>\d+\.\d+\.\d+) 対応"
    ),
    "steam/workshop_description.en.txt": re.compile(
        r"Compatible with Caves of Qud (?P<version>\d+\.\d+\.\d+)"
    ),
    "docs/RULES.md": re.compile(
        r"IDs must match game version `(?P<version>\d+\.\d+\.\d+)`"
    ),
    "docs/contributing.md": re.compile(
        r"ゲームバージョン (?P<version>\d+\.\d+\.\d+) に合わせる"
    ),
    "docs/test-architecture.md": re.compile(
        r"現行ゲーム (?P<version>\d+\.\d+\.\d+) の実 DLL"
    ),
    "Mods/QudJP/Assemblies/src/QudJPMod.cs": re.compile(
        r'BuildMarker = "qud-(?P<version>\d+\.\d+\.\d+)-compat-v\d+"'
    ),
}
WORKSHOP_HARMONY_THREAD_URL = (
    "https://steamcommunity.com/workshop/filedetails/discussion/3718988020/572669660098532087/"
)
WORKSHOP_DESCRIPTION_LANGUAGE_CONTRACTS = {
    "steam/workshop_description.en.txt": (
        "[h1]Overview[/h1]",
        "Compatible with Caves of Qud 1.0.5",
    ),
    "steam/workshop_description.ja.txt": (
        "[h1]概要[/h1]",
        "Caves of Qud 1.0.5 対応",
    ),
}
STEAM_BBCODE_ALLOWED_TAGS = frozenset(
    {"*", "b", "code", "h1", "h2", "hr", "list", "olist", "url"}
)
STEAM_BBCODE_TOKEN_PATTERN = re.compile(
    r"\[(?P<closing>/)?(?P<tag>\*|[a-z][a-z0-9]*)(?:=(?P<argument>[^\[\]\r\n]+))?\]",
    flags=re.IGNORECASE,
)
STEAM_BRACKET_TOKEN_PATTERN = re.compile(r"\[[^\r\n]*?\]")
MARKDOWN_STAR_BULLET_PATTERN = re.compile(r"^[ \t]*\*(?:[ \t]+|$)", flags=re.MULTILINE)


def _read(path: str) -> str:
    return (REPO_ROOT / path).read_text(encoding="utf-8")


def _check_list_item_token(
    *,
    token: str,
    closing: bool,
    argument: str | None,
    stack: list[tuple[str, str]],
) -> str | None:
    if closing or argument is not None:
        return f"invalid list item token: {token}"
    if not stack or stack[-1][0] not in {"list", "olist"}:
        return "[*] must be directly nested in [list] or [olist]"
    return None


def _check_closing_bbcode_tag(
    *,
    token: str,
    tag: str,
    argument: str | None,
    stack: list[tuple[str, str]],
) -> str | None:
    if argument is not None:
        return f"closing BBCode tag cannot have an argument: {token}"
    if not stack:
        return f"closing BBCode tag has no opener: {token}"
    if stack[-1][0] != tag:
        return f"mismatched BBCode tag: expected [/{stack[-1][0]}], found {token}"
    stack.pop()
    return None


def _scan_canonical_bbcode_tokens(
    description: str,
    findings: list[str],
) -> list[re.Match[str]]:
    matches: list[re.Match[str]] = []
    cursor = 0
    for bracket_match in STEAM_BRACKET_TOKEN_PATTERN.finditer(description):
        if re.search(r"[\[\]]", description[cursor : bracket_match.start()]):
            findings.append("stray unmatched '[' or ']' outside a BBCode token")

        token = bracket_match.group(0)
        canonical_match = STEAM_BBCODE_TOKEN_PATTERN.fullmatch(token)
        if canonical_match is None:
            findings.append(f"malformed BBCode token: {token}")
        else:
            matches.append(canonical_match)
        cursor = bracket_match.end()

    if re.search(r"[\[\]]", description[cursor:]):
        findings.append("stray unmatched '[' or ']' outside a BBCode token")
    return matches


def _assert_balanced_steam_bbcode(description: str, *, source: str) -> None:
    findings: list[str] = []
    stack: list[tuple[str, str]] = []

    if "`" in description:
        findings.append("Markdown backticks are not allowed")
    if MARKDOWN_STAR_BULLET_PATTERN.search(description):
        findings.append("Markdown '*' bullets are not allowed; use [*] list items")

    for match in _scan_canonical_bbcode_tokens(description, findings):
        token = match.group(0)
        tag = match.group("tag").casefold()
        closing = match.group("closing") is not None
        argument = match.group("argument")

        if tag not in STEAM_BBCODE_ALLOWED_TAGS:
            findings.append(f"unsupported BBCode tag: {token}")
            continue
        if tag == "*":
            finding = _check_list_item_token(
                token=token,
                closing=closing,
                argument=argument,
                stack=stack,
            )
            if finding is not None:
                findings.append(finding)
            continue
        if closing:
            finding = _check_closing_bbcode_tag(
                token=token,
                tag=tag,
                argument=argument,
                stack=stack,
            )
            if finding is not None:
                findings.append(finding)
            continue
        if argument is not None and tag != "url":
            findings.append(f"only [url] may have an argument: {token}")
        stack.append((tag, token))

    findings.extend(f"unclosed BBCode tag: {token}" for _tag, token in reversed(stack))
    assert not findings, f"{source} contains invalid Steam BBCode:\n" + "\n".join(findings)


@pytest.mark.parametrize(
    "description",
    [
        "[bad tag]",
        "[bogus!]text[/bogus!]",
        "[url=]",
        "[[b]]text[[/b]]",
        "stray [",
        "stray ]",
    ],
)
def test_steam_bbcode_contract_rejects_malformed_bracket_syntax(description: str) -> None:
    """Bracket-like syntax must be canonical BBCode rather than ignored text."""
    with pytest.raises(AssertionError, match="invalid Steam BBCode"):
        _assert_balanced_steam_bbcode(description, source="test description")


def test_current_version_surfaces_match_readme_target() -> None:
    """Only current-version surfaces must agree; historical provenance is out of scope."""
    target_match = TARGET_VERSION_PATTERN.search(_read("README.md"))
    assert target_match is not None, "README target game version not found"
    target_version = target_match.group("version")

    mismatches: list[str] = []
    for path, pattern in CURRENT_VERSION_SURFACES.items():
        match = pattern.search(_read(path))
        if match is None:
            mismatches.append(f"{path}: target-version surface not found")
        elif match.group("version") != target_version:
            mismatches.append(
                f"{path}: expected {target_version}, found {match.group('version')}"
            )

    assert not mismatches, "Current target-game version drift:\n" + "\n".join(mismatches)


def test_workshop_default_description_is_english() -> None:
    """The default steamcmd upload must not overwrite English with Japanese text."""
    metadata = json.loads(_read("steam/workshop_metadata.json"))

    assert metadata["description_file"] == "workshop_description.en.txt"
    description = _read("steam/workshop_description.en.txt")
    assert "Overview" in description
    assert "概要" not in description


def test_workshop_descriptions_use_balanced_steam_bbcode() -> None:
    """Both localized descriptions must use Steam-supported BBCode only."""
    for path, (language_heading, version_text) in WORKSHOP_DESCRIPTION_LANGUAGE_CONTRACTS.items():
        description = _read(path)

        _assert_balanced_steam_bbcode(description, source=path)
        assert "[h1]Caves of Qud Japanese Mod[/h1]" in description, (
            f"{path}: missing Workshop title heading"
        )
        assert language_heading in description, f"{path}: missing language heading {language_heading!r}"
        assert version_text in description, f"{path}: missing target version {version_text!r}"
        for required_tag in ("[list]", "[olist]", "[code]"):
            assert required_tag in description, f"{path}: missing required tag {required_tag}"

        harmony_link = f"[url={WORKSHOP_HARMONY_THREAD_URL}]"
        assert description.count(harmony_link) == 1, (
            f"{path}: expected exactly one linked Harmony thread URL"
        )
        assert description.count(WORKSHOP_HARMONY_THREAD_URL) == 1, (
            f"{path}: Harmony thread URL must not also appear naked"
        )


def test_reference_stub_version_matches_l2g_game_assembly_contract() -> None:
    """The no-game stub identity must follow the version asserted against the real DLL."""
    assembly_info = _read(
        "Mods/QudJP/Assemblies/ReferenceStubs/Assembly-CSharp/AssemblyInfo.cs"
    )
    l2g_contract = _read(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs"
    )

    stub_match = re.search(r'AssemblyVersion\("(?P<version>\d+(?:\.\d+){3})"\)', assembly_info)
    l2g_match = re.search(
        r"Is\.EqualTo\(new Version\((?P<version>\d+(?:,\s*\d+){3})\)\)",
        l2g_contract,
    )
    assert stub_match is not None, "Assembly-CSharp stub version not found"
    assert l2g_match is not None, "L2G Assembly-CSharp version assertion not found"

    l2g_version = ".".join(part.strip() for part in l2g_match.group("version").split(","))
    assert stub_match.group("version") == l2g_version
