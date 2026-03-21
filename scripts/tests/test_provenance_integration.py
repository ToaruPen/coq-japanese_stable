"""Integration tests for the provenance analysis pipeline."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from scripts.analyze_provenance import main
from scripts.provenance.dictionary_auditor import audit_dictionaries
from scripts.provenance.models import AuditFindingKind

_PROJECT_ROOT = Path(__file__).resolve().parents[2]
_DICT_DIR = _PROJECT_ROOT / "Mods" / "QudJP" / "Localization" / "Dictionaries"
_ILSPY_DIR = _PROJECT_ROOT / "docs" / "ilspy-raw"


def test_full_pipeline_with_synthetic_inputs(tmp_path: Path) -> None:
    """End-to-end pipeline works on isolated synthetic inputs."""
    dict_dir = tmp_path / "Dictionaries"
    ilspy_dir = tmp_path / "ilspy-raw"
    dict_dir.mkdir()
    ilspy_dir.mkdir()
    (dict_dir / "msg.ja.json").write_text(
        json.dumps(
            {
                "meta": {"id": "msg", "lang": "ja", "version": "0.1.0"},
                "rules": {"protectColorTags": True, "protectHtmlEntities": True},
                "entries": [
                    {"key": "You stagger ", "text": "よろめかせた：", "context": "XRL.World.Parts.Combat"},  # noqa: RUF001
                    {"key": "You hit", "text": "{target}に命中した。"},
                ],
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    (ilspy_dir / "XRL_World_MyPart.cs").write_text(
        """
namespace XRL.World
{
    public class MyPart
    {
        public void Describe(DescriptionBuilder DB)
        {
            DB.AddBase("sword");
        }
    }
}
""",
        encoding="utf-8",
    )

    out_path = tmp_path / "report.json"
    result = main(["--dictionaries", str(dict_dir), "--ilspy-raw", str(ilspy_dir), "--output", str(out_path)])
    data = json.loads(out_path.read_text(encoding="utf-8"))

    assert result == 0
    assert data["summary"]["total_audit_findings"] >= 2
    assert data["summary"]["total_xref_findings"] >= 1
    assert data["summary"]["total_generators"] >= 1


@pytest.mark.skipif(not _DICT_DIR.exists(), reason="Dictionaries not available")
def test_real_dictionary_audit_runs() -> None:
    """Real repository dictionaries can be audited successfully."""
    findings = audit_dictionaries(_DICT_DIR)
    assert isinstance(findings, list)
    assert all(finding.kind in AuditFindingKind for finding in findings)


@pytest.mark.skipif(
    not (_DICT_DIR.exists() and _ILSPY_DIR.exists()),
    reason="Real provenance inputs not fully available",
)
def test_full_pipeline_real_inputs(tmp_path: Path) -> None:
    """Real repository data can run through the CLI when ilspy-raw is present."""
    out_path = tmp_path / "real-report.json"
    result = main(["--dictionaries", str(_DICT_DIR), "--ilspy-raw", str(_ILSPY_DIR), "--output", str(out_path)])
    assert result == 0
    data = json.loads(out_path.read_text(encoding="utf-8"))
    assert data["summary"]["total_generators"] >= 1
