"""Static contracts for QudJP NUnit test fixtures."""

from __future__ import annotations

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TEST_ROOT = REPO_ROOT / "Mods" / "QudJP" / "Assemblies" / "QudJP.Tests"


def test_translator_dictionary_fixtures_are_non_parallelizable() -> None:
    """Translator dictionary overrides mutate global state and must not run in parallel."""
    offenders: list[str] = []
    for path in sorted(TEST_ROOT.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        if "Translator.SetDictionaryDirectoryForTests(" not in text:
            continue
        if "[NonParallelizable]" not in text:
            offenders.append(str(path.relative_to(REPO_ROOT)))

    assert offenders == []
