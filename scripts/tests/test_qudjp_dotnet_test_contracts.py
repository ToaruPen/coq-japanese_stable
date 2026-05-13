"""Static contracts for QudJP NUnit test fixtures."""

from __future__ import annotations

import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TEST_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Assemblies" / "QudJP.Tests"
JUSTFILE = REPO_ROOT / "justfile"
TEST_ARCHITECTURE_DOC = REPO_ROOT / "docs" / "test-architecture.md"
RULES_DOC = REPO_ROOT / "docs" / "RULES.md"
TRANSLATOR_DICTIONARY_CALL = "Translator.SetDictionaryDirectoryForTests("

CLASS_DECLARATION = re.compile(r"\bclass\s+[A-Za-z_][A-Za-z0-9_]*\b")
METHOD_DECLARATION = re.compile(
    r"^\s*(?:public|private|protected|internal|static|async|virtual|override|sealed"
    r"|partial|\s)+[A-Za-z_][A-Za-z0-9_<>,\[\]?]*\s+"
    r"[A-Za-z_][A-Za-z0-9_]*\s*\("
)


def _has_non_parallelizable_attribute(lines: list[str], declaration_index: int) -> bool:
    """Return whether the declaration has an immediate NonParallelizable attribute."""
    j = declaration_index - 1
    attribute_lines: list[str] = []
    while j >= 0:
        stripped = lines[j].strip()
        if stripped == "":
            j -= 1
            continue
        if not stripped.startswith("["):
            break
        attribute_lines.append(stripped)
        j -= 1

    return any("NonParallelizable" in line for line in attribute_lines)


def _nearest_declaration(
    lines: list[str],
    line_index: int,
    pattern: re.Pattern[str],
    *,
    stop_after: int = -1,
) -> int | None:
    """Find the nearest preceding line that matches a C# declaration pattern."""
    for index in range(line_index, stop_after, -1):
        if pattern.search(lines[index]):
            return index
    return None


def test_translator_dictionary_fixtures_are_non_parallelizable() -> None:
    """Translator dictionary overrides mutate global state and must not run in parallel."""
    assert TEST_ROOT.is_dir(), f"QudJP test root not found: {TEST_ROOT}"

    offenders: list[str] = []
    for path in sorted(TEST_ROOT.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        if TRANSLATOR_DICTIONARY_CALL not in text:
            continue
        lines = text.splitlines()
        for call_index, line in enumerate(lines):
            if TRANSLATOR_DICTIONARY_CALL not in line:
                continue

            class_index = _nearest_declaration(lines, call_index, CLASS_DECLARATION)
            if class_index is None:
                offenders.append(f"{path.relative_to(REPO_ROOT)}:{call_index + 1}")
                continue

            method_index = _nearest_declaration(
                lines,
                call_index,
                METHOD_DECLARATION,
                stop_after=class_index,
            )
            if (
                not _has_non_parallelizable_attribute(lines, class_index)
                and (
                    method_index is None
                    or not _has_non_parallelizable_attribute(lines, method_index)
                )
            ):
                offenders.append(f"{path.relative_to(REPO_ROOT)}:{call_index + 1}")

    assert not offenders, (
        "Translator.SetDictionaryDirectoryForTests(...) mutates global translator "
        "state. Add [NonParallelizable] to each enclosing NUnit fixture class or "
        "test method that uses it:\n"
        + "\n".join(offenders)
    )


def _recipe_block(justfile: str, recipe_name: str, next_recipe_name: str | None) -> str:
    recipe_start = re.compile(rf"\n{re.escape(recipe_name)}(?:\s[^:\n]*)?:")
    start_match = recipe_start.search(justfile)
    assert start_match is not None, f"recipe not found: {recipe_name}"
    start = start_match.start()
    if next_recipe_name is None:
        end = len(justfile)
    else:
        next_recipe_start = re.compile(rf"\n{re.escape(next_recipe_name)}(?:\s[^:\n]*)?:")
        end_match = next_recipe_start.search(justfile, start_match.end())
        assert end_match is not None, f"recipe not found: {next_recipe_name}"
        end = end_match.start()
    return justfile[start:end]


def test_local_csharp_full_suite_builds_test_project_once() -> None:
    """The local all-C# test entrypoint should avoid per-category build and VSTest startup."""
    justfile = "\n" + JUSTFILE.read_text(encoding="utf-8")
    recipe = _recipe_block(justfile, "test-csharp", "python-check")
    check_recipe = _recipe_block(justfile, "check", "pr-check")

    assert "dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj" in recipe
    assert recipe.count("dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj") == 1
    assert recipe.count('dotnet test "$test_dll"') == 1
    assert "TestCategory=" not in recipe

    assert "test-csharp" in check_recipe
    assert "test-l1 test-l2 test-l2g" not in check_recipe


def test_route_family_test_guidance_limits_l2_case_growth() -> None:
    """Route-family additions should not default to one Harmony test per string."""
    test_architecture = TEST_ARCHITECTURE_DOC.read_text(encoding="utf-8")
    rules = RULES_DOC.read_text(encoding="utf-8")

    assert "1 回の Harmony patch setup にまとめて batch 実行する" in test_architecture
    assert "静的 inventory や data-contract coverage" in test_architecture
    assert "one L2 smoke case" in rules
    assert "Do not add one new Harmony patch/unpatch test case for every newly claimed string" in rules
