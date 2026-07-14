"""Contracts for current target-game version surfaces and DLL identity."""

from __future__ import annotations

import re
from pathlib import Path

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


def _read(path: str) -> str:
    return (REPO_ROOT / path).read_text(encoding="utf-8")


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
