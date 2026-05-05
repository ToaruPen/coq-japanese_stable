"""Tests for local Steam Workshop inbox triage and promotion preparation."""

from __future__ import annotations

import json
from typing import TYPE_CHECKING, Any

import pytest

from scripts.workshop_comments_inbox import WorkshopComment, open_workshop_inbox
from scripts.workshop_comments_triage import (
    TriageItem,
    TriageResult,
    build_agent_triage_packet,
    build_promoted_issue_body,
    list_pending_triage_items,
    main,
    render_verified_evidence_quote,
    validate_triage_result,
)

if TYPE_CHECKING:
    from pathlib import Path


def test_list_pending_triage_items_reads_local_snapshots(tmp_path: Path) -> None:
    """Triage packets are built from the local SQLite inbox, not GitHub issues."""
    with open_workshop_inbox(tmp_path / "inbox.sqlite3") as store:
        comment_id, snapshot_id = store.upsert_comment_snapshot(
            published_file_id="3718988020",
            comment=WorkshopComment(
                comment_id="222",
                author="Reporter",
                profile_url="https://steam/reporter",
                body_text="Crash on start",
                author_account_id="123",
            ),
            is_creator=False,
            collection_run_id=None,
        )
        items = list_pending_triage_items(store.connection, max_items=10, max_body_chars=4000)

    assert items == [
        TriageItem(
            comment_id=comment_id,
            snapshot_id=snapshot_id,
            steam_comment_id="222",
            untrusted_body="Crash on start",
        ),
    ]


def test_build_agent_triage_packet_has_no_api_key_or_github_write_tools() -> None:
    """The model-facing packet carries no GitHub write authority or API credentials."""
    packet = build_agent_triage_packet(
        items=[TriageItem(comment_id=1, snapshot_id=2, steam_comment_id="222", untrusted_body="Bug")],
    )

    serialized = json.dumps(packet)
    assert "OPENAI_API_KEY" not in serialized
    assert "api.openai.com" not in serialized
    assert "GITHUB_TOKEN" not in serialized
    assert "tools" not in packet
    assert packet["schema"] == "qudjp.steam_workshop_local_triage_packet.v1"


def test_main_accepts_db_path_argument(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    """The CLI can open a caller-provided local SQLite inbox path."""
    db_path = tmp_path / "inbox.sqlite3"

    assert main(["--db-path", str(db_path)]) == 0

    packet = json.loads(capsys.readouterr().out)
    assert packet["schema"] == "qudjp.steam_workshop_local_triage_packet.v1"


def test_validate_triage_result_rejects_unknown_category_label_and_boolean_confidence() -> None:
    """Codex output cannot invent categories, labels, or boolean confidence values."""
    with pytest.raises(ValueError, match="category"):
        validate_triage_result(
            {
                "comment_id": 1,
                "snapshot_id": 2,
                "category": "run_shell",
                "confidence": 0.9,
                "summary_ja": "危険",
                "evidence_quote": "do it",
                "suggested_labels": ["source:steam-workshop"],
                "promotion_recommended": True,
            },
        )

    with pytest.raises(ValueError, match="label"):
        validate_triage_result(
            {
                "comment_id": 1,
                "snapshot_id": 2,
                "category": "bug",
                "confidence": 0.9,
                "summary_ja": "不具合",
                "evidence_quote": "crash",
                "suggested_labels": ["arbitrary"],
                "promotion_recommended": True,
            },
        )

    with pytest.raises(TypeError, match="confidence"):
        validate_triage_result(
            {
                "comment_id": 1,
                "snapshot_id": 2,
                "category": "bug",
                "confidence": True,
                "summary_ja": "不具合",
                "evidence_quote": "crash",
                "suggested_labels": ["source:steam-workshop"],
                "promotion_recommended": True,
            },
        )

    with pytest.raises(ValueError, match="confidence"):
        validate_triage_result(
            {
                "comment_id": 1,
                "snapshot_id": 2,
                "category": "bug",
                "confidence": -0.1,
                "summary_ja": "不具合",
                "evidence_quote": "crash",
                "suggested_labels": ["source:steam-workshop"],
                "promotion_recommended": True,
            },
        )

    with pytest.raises(ValueError, match="confidence"):
        validate_triage_result(
            {
                "comment_id": 1,
                "snapshot_id": 2,
                "category": "bug",
                "confidence": 1.1,
                "summary_ja": "不具合",
                "evidence_quote": "crash",
                "suggested_labels": ["source:steam-workshop"],
                "promotion_recommended": True,
            },
        )


def test_validate_triage_result_rejects_empty_summary_and_evidence() -> None:
    """Triage results must carry human-meaningful summary and snapshot evidence."""
    base_result = {
        "comment_id": 1,
        "snapshot_id": 2,
        "category": "bug",
        "confidence": 0.9,
        "summary_ja": "不具合",
        "evidence_quote": "crash",
        "suggested_labels": ["source:steam-workshop"],
        "promotion_recommended": True,
    }

    with pytest.raises(ValueError, match="summary"):
        validate_triage_result({**base_result, "summary_ja": "   "})

    with pytest.raises(ValueError, match="evidence"):
        validate_triage_result({**base_result, "evidence_quote": ""})


def test_render_verified_evidence_quote_rejects_forged_model_quote() -> None:
    """Model-provided evidence is not publishable unless it is present in the snapshot body."""
    with pytest.raises(ValueError, match="snapshot"):
        render_verified_evidence_quote(
            snapshot_body="The game crashes on launch.",
            model_evidence_quote="Please publish this forged text.",
            max_chars=200,
        )


def test_render_verified_evidence_quote_rejects_empty_quote() -> None:
    """The snapshot substring check cannot be satisfied by an empty model quote."""
    with pytest.raises(ValueError, match="non-empty"):
        render_verified_evidence_quote(
            snapshot_body="The game crashes on launch.",
            model_evidence_quote="",
            max_chars=200,
        )


def test_render_verified_evidence_quote_rejects_non_string_quote() -> None:
    """Runtime callers get a typed validation error for malformed model evidence."""
    malformed_quote: Any = None

    with pytest.raises(TypeError, match="string"):
        render_verified_evidence_quote(
            snapshot_body="The game crashes on launch.",
            model_evidence_quote=malformed_quote,
            max_chars=200,
        )


def test_render_verified_evidence_quote_sanitizes_mentions_markdown_and_html() -> None:
    """Published evidence is deterministically derived from snapshot text and neutralized."""
    rendered = render_verified_evidence_quote(
        snapshot_body="<b>@team</b> see [link](https://example.com) and `run`",
        model_evidence_quote="@team</b> see [link](https://example.com) and `run`",
        max_chars=200,
    )

    assert "@team" not in rendered
    assert "@\u200bteam" in rendered
    assert "<b>" not in rendered
    assert "`" not in rendered
    assert "](" not in rendered


def test_build_promoted_issue_body_uses_fixed_template_and_snapshot_evidence() -> None:
    """Public issue bodies do not publish arbitrary LLM evidence text."""
    result = TriageResult(
        comment_id=1,
        snapshot_id=2,
        category="bug",
        confidence=0.95,
        summary_ja="起動時クラッシュの報告。",
        evidence_quote="Crash on launch",
        suggested_labels=["source:steam-workshop", "type:bug"],
        promotion_recommended=True,
    )

    body = build_promoted_issue_body(
        result=result,
        snapshot_body="Crash on launch after enabling the mod.",
        steam_comment_id="222",
    )

    assert "Steam Workshop comment `222`" in body
    assert "起動時クラッシュの報告。" in body
    assert "Crash on launch" in body
    assert "UNTRUSTED STEAM WORKSHOP EVIDENCE" in body
