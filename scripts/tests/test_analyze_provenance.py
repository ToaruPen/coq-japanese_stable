"""Tests for the provenance analysis CLI."""

from __future__ import annotations

import json
from typing import TYPE_CHECKING

import pytest

from scripts.analyze_provenance import main

if TYPE_CHECKING:
    from pathlib import Path


def _write_dict(path: Path, dict_id: str, entries: list[dict[str, object]]) -> None:
    """Write a minimal test dictionary file."""
    path.parent.mkdir(parents=True, exist_ok=True)
    data = {
        "meta": {"id": dict_id, "lang": "ja", "version": "0.1.0"},
        "rules": {"protectColorTags": True, "protectHtmlEntities": True},
        "entries": entries,
    }
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def _write_cs(path: Path, content: str) -> None:
    """Write a minimal C# file for CLI tests."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def test_cli_runs_and_produces_json(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """CLI writes a JSON report file and reports the output path on stderr."""
    dict_dir = tmp_path / "Dictionaries"
    ilspy_dir = tmp_path / "ilspy-raw"
    _write_dict(
        dict_dir / "test.ja.json",
        "test",
        [{"key": "You stagger ", "text": "よろめかせた："}],  # noqa: RUF001
    )
    _write_cs(
        ilspy_dir / "XRL_World_MyPart.cs",
        """\
namespace XRL.World
{
    public class MyPart
    {
        public string Build(string name)
        {
            return string.Concat(name, "!");
        }
    }
}
""",
    )
    out_path = tmp_path / "report.json"

    result = main(["--dictionaries", str(dict_dir), "--ilspy-raw", str(ilspy_dir), "--output", str(out_path)])
    captured = capsys.readouterr()

    assert result == 0
    assert out_path.exists()
    assert "Report written to" in captured.err
    data = json.loads(out_path.read_text(encoding="utf-8"))
    assert data["summary"]["total_audit_findings"] >= 1
    assert data["summary"]["total_generators"] >= 1


def test_cli_help_exits_zero(capsys: pytest.CaptureFixture[str]) -> None:
    """Argparse help exits successfully."""
    with pytest.raises(SystemExit) as exc_info:
        main(["--help"])
    captured = capsys.readouterr()
    assert exc_info.value.code == 0
    assert "Analyze translation dictionary entries" in captured.out
