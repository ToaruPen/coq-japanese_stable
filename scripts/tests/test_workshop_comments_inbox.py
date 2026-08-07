"""Tests for local Steam Workshop comment inbox storage."""

from __future__ import annotations

import gzip
import json
import sqlite3
from pathlib import Path
from typing import ClassVar, Self

import pytest

import scripts.workshop_comments_inbox as workshop_inbox
from scripts.workshop_comments_inbox import (
    CollectionOptions,
    HttpResponse,
    WorkshopComment,
    WorkshopInboxStore,
    build_steam_comments_url,
    build_steam_discussion_thread_url,
    collect_workshop_comments,
    creator_account_id_from_steam_id,
    default_workshop_state_dir,
    extract_comments_from_render_response,
    open_workshop_inbox,
    sanitize_untrusted_text,
    validate_numeric_id,
)


class _FakeTransport:
    def __init__(self, responses: list[HttpResponse]) -> None:
        self.responses = responses
        self.calls: list[tuple[str, str, bytes | None]] = []

    def __call__(self, method: str, url: str, body: bytes | None, headers: dict[str, str]) -> HttpResponse:
        del headers
        self.calls.append((method, url, body))
        return self.responses.pop(0)


def _metadata_response() -> HttpResponse:
    return HttpResponse(
        status_code=200,
        body=json.dumps(
            {"response": {"publishedfiledetails": [{"result": 1, "creator": "76561198205102067"}]}},
        ).encode(),
        headers={},
    )


def _comments_response(comments_html: str, *, total_count: int = 1) -> HttpResponse:
    return HttpResponse(
        status_code=200,
        body=json.dumps({"success": True, "total_count": total_count, "comments_html": comments_html}).encode(),
        headers={},
    )


def _comment_html(*, comment_id: str, account_id: str, author: str, body: str) -> str:
    return (
        f'<div class="commentthread_comment" id="comment_{comment_id}">'
        f'<a class="hoverunderline commentthread_author_link" href="https://steam/{author}" '
        f'data-miniprofile="{account_id}">{author}</a>'
        f'<div class="commentthread_comment_text">{body}</div></div>'
    )


def test_default_state_dir_uses_project_workshop_namespace() -> None:
    """Local inbox state belongs under the project-specific hidden directory."""
    assert default_workshop_state_dir() == Path(".coq-japanese_workshop/state")


def test_validate_numeric_id_accepts_digits_only() -> None:
    """Only decimal Steam IDs are accepted for fixed endpoint slots."""
    assert validate_numeric_id("3718988020", field_name="publishedfileid") == "3718988020"

    with pytest.raises(ValueError, match="creator"):
        validate_numeric_id("7656119/evil", field_name="creator")


def test_urllib_transport_uses_browser_like_user_agent(monkeypatch: pytest.MonkeyPatch) -> None:
    """Steam public endpoints receive a stable browser-like User-Agent."""
    captured: dict[str, str] = {}

    class _FakeResponse:
        def __init__(self) -> None:
            self.status = 200
            self.headers: dict[str, str] = {}

        def __enter__(self) -> Self:
            return self

        def __exit__(self, *args: object) -> None:
            del args

        def read(self, size: int) -> bytes:
            del size
            return b"ok"

    def fake_urlopen(request: object, *, timeout: int) -> _FakeResponse:
        del timeout
        captured["user_agent"] = request.headers["User-agent"]  # type: ignore[attr-defined]
        captured["accept_language"] = request.headers["Accept-language"]  # type: ignore[attr-defined]
        return _FakeResponse()

    monkeypatch.setattr(workshop_inbox, "urlopen", fake_urlopen)
    transport = workshop_inbox._make_urllib_transport(timeout_seconds=20)  # noqa: SLF001

    response = transport("GET", "https://steamcommunity.com/", None, {})

    assert response.status_code == 200
    assert captured["user_agent"].startswith("Mozilla/5.0")
    assert captured["accept_language"] == "ja,en-US;q=0.9,en;q=0.8"


def test_urllib_transport_requests_supported_encoding_and_decodes_gzip(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Steam's public HTML endpoint requires browser-like compression negotiation."""
    captured: dict[str, str] = {}

    class _FakeResponse:
        status = 200
        headers: ClassVar[dict[str, str]] = {"Content-Encoding": "gzip"}

        def __enter__(self) -> Self:
            return self

        def __exit__(self, *args: object) -> None:
            del args

        def read(self, size: int) -> bytes:
            del size
            return gzip.compress(b"decoded")

    def fake_urlopen(request: object, *, timeout: int) -> _FakeResponse:
        del timeout
        captured["accept"] = request.get_header("Accept")  # type: ignore[attr-defined]
        captured["accept_encoding"] = request.get_header("Accept-encoding")  # type: ignore[attr-defined]
        return _FakeResponse()

    monkeypatch.setattr(workshop_inbox, "urlopen", fake_urlopen)
    transport = workshop_inbox._make_urllib_transport(timeout_seconds=20)  # noqa: SLF001

    response = transport("GET", "https://steamcommunity.com/", None, {})

    assert captured == {"accept": "*/*", "accept_encoding": "gzip"}
    assert response.body == b"decoded"


def test_urllib_transport_rejects_gzip_body_exceeding_response_limit(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Compressed responses are bounded after decompression as well as before it."""

    class _FakeResponse:
        status = 200
        headers: ClassVar[dict[str, str]] = {"Content-Encoding": "gzip"}

        def __enter__(self) -> Self:
            return self

        def __exit__(self, *args: object) -> None:
            del args

        def read(self, size: int) -> bytes:
            del size
            return gzip.compress(b"too-large")

    def fake_urlopen(request: object, *, timeout: int) -> _FakeResponse:
        del request, timeout
        return _FakeResponse()

    monkeypatch.setattr(workshop_inbox, "urlopen", fake_urlopen)
    transport = workshop_inbox._make_urllib_transport(timeout_seconds=20, max_response_bytes=8)  # noqa: SLF001

    with pytest.raises(ValueError, match="HTTP response exceeded max response bytes"):
        transport("GET", "https://steamcommunity.com/", None, {})


def test_build_steam_comments_url_uses_fixed_endpoint() -> None:
    """Steam IDs are inserted only into the reviewed public render endpoint shape."""
    url = build_steam_comments_url(
        creator_id="76561198205102067",
        published_file_id="3718988020",
        start=20,
        count=10,
    )

    assert (
        url
        == "https://steamcommunity.com/comment/PublishedFile_Public/render/"
        "76561198205102067/3718988020/?start=20&count=10&l=japanese"
    )


def test_build_steam_discussion_thread_url_uses_fixed_endpoint() -> None:
    """Discussion URLs are parsed into a fixed public page endpoint with numeric path slots only."""
    url = build_steam_discussion_thread_url(
        "https://steamcommunity.com/workshop/filedetails/discussion/3718988020/837250028234869281/?tscn=1778250394",
    )

    assert (
        url
        == "https://steamcommunity.com/workshop/filedetails/discussion/"
        "3718988020/837250028234869281/?l=japanese"
    )


def test_build_steam_discussion_thread_url_preserves_numeric_page() -> None:
    """Discussion page URLs keep Steam's ctp page while dropping unrelated query fields."""
    url = build_steam_discussion_thread_url(
        "https://steamcommunity.com/workshop/filedetails/discussion/"
        "3718988020/837250028234869281/?ctp=2&tscn=1778250394",
    )

    assert (
        url
        == "https://steamcommunity.com/workshop/filedetails/discussion/"
        "3718988020/837250028234869281/?ctp=2&l=japanese"
    )


def test_build_steam_discussion_thread_url_rejects_non_workshop_hosts_and_ids() -> None:
    """User-provided discussion URLs cannot become arbitrary HTTP endpoints."""
    with pytest.raises(ValueError, match="Steam discussion URL"):
        build_steam_discussion_thread_url("https://example.com/workshop/filedetails/discussion/3718988020/1/")

    with pytest.raises(ValueError, match="discussion_topic"):
        build_steam_discussion_thread_url(
            "https://steamcommunity.com/workshop/filedetails/discussion/3718988020/not-numeric/",
        )

    with pytest.raises(ValueError, match="ctp"):
        build_steam_discussion_thread_url(
            "https://steamcommunity.com/workshop/filedetails/discussion/3718988020/837250028234869281/?ctp=two",
        )


def test_creator_account_id_from_steam_id_converts_steamid64() -> None:
    """Steam comment miniprofile account IDs can be compared with creator SteamID64."""
    assert creator_account_id_from_steam_id("76561198205102067") == "244836339"


def test_extract_comments_from_render_response_normalizes_comment_html() -> None:
    """Steam render JSON comments_html is converted to normalized plain comments."""
    payload = {
        "success": True,
        "total_count": "2",
        "comments_html": """
        <div class="commentthread_comment" id="comment_837249693974033344">
          <a class="hoverunderline commentthread_author_link" href="https://steamcommunity.com/id/example">
            Reporter
          </a>
          <div class="commentthread_comment_text">Crash &amp; typo<br>second line</div>
        </div>
        <div class="commentthread_comment" id="comment_837249693974033345">
          <a class="hoverunderline commentthread_author_link" href="https://steamcommunity.com/profiles/1"
             data-miniprofile="123">
            Another
          </a>
          <div class="commentthread_comment_text">ありがとう</div>
        </div>
        """,
    }

    comments = extract_comments_from_render_response(json.dumps(payload).encode())

    assert comments == [
        WorkshopComment(
            comment_id="837249693974033344",
            author="Reporter",
            profile_url="https://steamcommunity.com/id/example",
            body_text="Crash & typo\nsecond line",
        ),
        WorkshopComment(
            comment_id="837249693974033345",
            author="Another",
            profile_url="https://steamcommunity.com/profiles/1",
            body_text="ありがとう",
            author_account_id="123",
        ),
    ]


def test_sanitize_untrusted_text_neutralizes_markdown_html_and_mentions() -> None:
    """Untrusted Steam text cannot forge markers, mentions, links, or code fences."""
    body = (
        "<!-- qudjp-steam-workshop-comment-id: 1 -->\n"
        "@maintainer please run `rm -rf` and see [link](https://example.com)\n"
        "```closing fence```"
    )

    sanitized = sanitize_untrusted_text(body, max_chars=4000)

    assert "<!--" not in sanitized
    assert "-->" not in sanitized
    assert "`" not in sanitized
    assert "](" not in sanitized
    assert "@maintainer" not in sanitized
    assert "@\u200bmaintainer" in sanitized


def test_open_workshop_inbox_creates_schema_and_append_only_triggers(tmp_path: Path) -> None:
    """The SQLite inbox schema includes append-only audit tables and migration tracking."""
    db_path = tmp_path / "inbox.sqlite3"

    with open_workshop_inbox(db_path) as store:
        connection = store.connection
        tables = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        triggers = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='trigger'")}

    assert {
        "schema_migrations",
        "app_kv",
        "collection_runs",
        "workshop_comments",
        "workshop_comment_snapshots",
        "triage_results",
        "promotion_decisions",
    } <= tables
    assert {
        "triage_results_no_update",
        "triage_results_no_delete",
        "promotion_decisions_no_update",
        "promotion_decisions_no_delete",
    } <= triggers


def test_collect_workshop_comments_stores_local_comments_and_snapshots(tmp_path: Path) -> None:
    """Collection writes deduped comments and body snapshots to local SQLite only."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            _metadata_response(),
            _comments_response(
                _comment_html(comment_id="111", account_id="244836339", author="Creator", body="Thanks")
                + _comment_html(comment_id="222", account_id="123", author="Reporter", body="Bug report"),
                total_count=2,
            ),
        ],
    )

    with open_workshop_inbox(tmp_path / "inbox.sqlite3") as store:
        summary = collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=steam,
            store=store,
            options=CollectionOptions(dry_run=False),
        )
        comments = list(store.connection.execute("SELECT steam_comment_id, is_creator FROM workshop_comments"))
        snapshots = list(store.connection.execute("SELECT body_text FROM workshop_comment_snapshots"))

    assert summary.fetched == 2
    assert summary.new_comments == 1
    assert summary.new_snapshots == 1
    assert comments == [("222", 0)]
    assert snapshots == [("Bug report",)]


def test_collect_workshop_comments_includes_configured_discussion_threads(tmp_path: Path) -> None:
    """Configured Workshop discussion threads are collected into the same local inbox."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            _metadata_response(),
            _comments_response(_comment_html(comment_id="222", account_id="123", author="Reporter", body="Bug")),
            HttpResponse(
                status_code=200,
                body=(
                    _comment_html(comment_id="333", account_id="244836339", author="Creator", body="Template")
                    + _comment_html(comment_id="444", account_id="456", author="ThreadReporter", body="Thread bug")
                ).encode(),
                headers={},
            ),
        ],
    )

    with open_workshop_inbox(tmp_path / "inbox.sqlite3") as store:
        summary = collect_workshop_comments(
            metadata_path=metadata_path,
            discussion_thread_urls=[
                "https://steamcommunity.com/workshop/filedetails/discussion/3718988020/837250028234869281/",
            ],
            steam_transport=steam,
            store=store,
            options=CollectionOptions(dry_run=False),
        )
        comments = list(
            store.connection.execute("SELECT steam_comment_id, is_creator FROM workshop_comments ORDER BY id"),
        )
        snapshots = list(store.connection.execute("SELECT body_text FROM workshop_comment_snapshots ORDER BY id"))

    assert summary.fetched == 3
    assert summary.new_comments == 2
    assert summary.new_snapshots == 2
    assert comments == [("222", 0), ("444", 0)]
    assert snapshots == [("Bug",), ("Thread bug",)]


def test_collect_workshop_comments_does_not_starve_discussion_threads(tmp_path: Path) -> None:
    """Per-run caps keep configured discussion threads represented alongside Workshop comments."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            _metadata_response(),
            _comments_response(
                _comment_html(comment_id="222", account_id="123", author="Reporter", body="Bug A")
                + _comment_html(comment_id="223", account_id="124", author="Reporter2", body="Bug B"),
                total_count=2,
            ),
            HttpResponse(
                status_code=200,
                body=_comment_html(
                    comment_id="444",
                    account_id="456",
                    author="ThreadReporter",
                    body="Thread bug",
                ).encode(),
                headers={},
            ),
        ],
    )

    with open_workshop_inbox(tmp_path / "inbox.sqlite3") as store:
        summary = collect_workshop_comments(
            metadata_path=metadata_path,
            discussion_thread_urls=[
                "https://steamcommunity.com/workshop/filedetails/discussion/3718988020/837250028234869281/",
            ],
            steam_transport=steam,
            store=store,
            options=CollectionOptions(dry_run=False, max_comments_per_run=2),
        )
        comments = list(
            store.connection.execute("SELECT steam_comment_id, is_creator FROM workshop_comments ORDER BY id"),
        )
        snapshots = list(store.connection.execute("SELECT body_text FROM workshop_comment_snapshots ORDER BY id"))

    assert summary.fetched == 3
    assert summary.new_comments == 2
    assert summary.new_snapshots == 2
    assert comments == [("222", 0), ("444", 0)]
    assert snapshots == [("Bug A",), ("Thread bug",)]


def test_collect_workshop_comments_adds_snapshot_when_body_changes(tmp_path: Path) -> None:
    """A Steam comment edit preserves history by adding a new snapshot row."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    db_path = tmp_path / "inbox.sqlite3"

    with open_workshop_inbox(db_path) as store:
        first = _FakeTransport(
            [
                _metadata_response(),
                _comments_response(_comment_html(comment_id="222", account_id="123", author="Reporter", body="Bug")),
            ],
        )
        collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=first,
            store=store,
            options=CollectionOptions(dry_run=False),
        )
        second = _FakeTransport(
            [
                _metadata_response(),
                _comments_response(
                    _comment_html(comment_id="222", account_id="123", author="Reporter", body="Bug updated"),
                ),
            ],
        )
        summary = collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=second,
            store=store,
            options=CollectionOptions(dry_run=False),
        )
        snapshots = list(
            store.connection.execute("SELECT body_text FROM workshop_comment_snapshots ORDER BY observed_at, id"),
        )

    assert summary.new_comments == 0
    assert summary.new_snapshots == 1
    assert snapshots == [("Bug",), ("Bug updated",)]


def test_audit_tables_are_append_only_and_restrict_comment_delete(tmp_path: Path) -> None:
    """Triage and promotion audit rows cannot be mutated or orphaned by deletes."""
    with open_workshop_inbox(tmp_path / "inbox.sqlite3") as store:
        comment_id, snapshot_id = _insert_comment_snapshot(store)
        triage_id = store.record_triage_result(
            comment_id=comment_id,
            snapshot_id=snapshot_id,
            agent_name="codex",
            model="gpt-5.4",
            reasoning_effort="medium",
            triage_version="test",
            category="bug",
            confidence=0.9,
            summary_ja="クラッシュ報告",
            evidence_quote="Bug",
            suggested_labels=["type:bug"],
            promotion_recommended=True,
        )
        store.record_promotion_decision(
            comment_id=comment_id,
            snapshot_id=snapshot_id,
            triage_result_id=triage_id,
            promoter_version="test",
            decision="needs_human",
            reason="manual check",
        )

        with pytest.raises(sqlite3.IntegrityError, match="append-only"):
            store.connection.execute("UPDATE triage_results SET confidence = 0.1 WHERE id = ?", (triage_id,))
        with pytest.raises(sqlite3.IntegrityError, match="append-only"):
            store.connection.execute("DELETE FROM promotion_decisions")
        with pytest.raises(sqlite3.IntegrityError):
            store.connection.execute("DELETE FROM workshop_comments WHERE id = ?", (comment_id,))


def test_collection_and_audit_rows_persist_after_reopen(tmp_path: Path) -> None:
    """Collection accounting and audit writes survive connection close/reopen."""
    db_path = tmp_path / "inbox.sqlite3"
    with open_workshop_inbox(db_path) as store:
        run_id = store.start_collection_run()
        comment_id, snapshot_id = store.upsert_comment_snapshot(
            published_file_id="3718988020",
            comment=WorkshopComment(
                comment_id="222",
                author="Reporter",
                profile_url="https://steam/reporter",
                body_text="Bug",
                author_account_id="123",
            ),
            is_creator=False,
            collection_run_id=run_id,
        )
        store.finish_collection_run(
            run_id=run_id,
            status="success",
            fetched_count=1,
            new_comment_count=1,
            new_snapshot_count=1,
        )
        triage_id = store.record_triage_result(
            comment_id=comment_id,
            snapshot_id=snapshot_id,
            agent_name="codex",
            model="gpt-5.4",
            reasoning_effort="medium",
            triage_version="test",
            category="bug",
            confidence=0.9,
            summary_ja="クラッシュ報告",
            evidence_quote="Bug",
            suggested_labels=["type:bug"],
            promotion_recommended=True,
        )
        store.record_promotion_decision(
            comment_id=comment_id,
            snapshot_id=snapshot_id,
            triage_result_id=triage_id,
            promoter_version="test",
            decision="needs_human",
            reason="manual check",
        )

    with open_workshop_inbox(db_path) as store:
        collection_run = store.connection.execute(
            "SELECT status, fetched_count, new_comment_count, new_snapshot_count FROM collection_runs",
        ).fetchone()
        triage_count = store.connection.execute("SELECT COUNT(*) FROM triage_results").fetchone()[0]
        decision_count = store.connection.execute("SELECT COUNT(*) FROM promotion_decisions").fetchone()[0]

    assert collection_run == ("success", 1, 1, 1)
    assert triage_count == 1
    assert decision_count == 1


def test_audit_rows_reject_mismatched_comment_and_snapshot(tmp_path: Path) -> None:
    """Audit rows must not pair one comment with another comment's snapshot."""
    with open_workshop_inbox(tmp_path / "inbox.sqlite3") as store:
        comment_a, _snapshot_a = _insert_comment_snapshot(store)
        comment_b, snapshot_b = store.upsert_comment_snapshot(
            published_file_id="3718988020",
            comment=WorkshopComment(
                comment_id="333",
                author="Other",
                profile_url="https://steam/other",
                body_text="Other bug",
                author_account_id="456",
            ),
            is_creator=False,
            collection_run_id=None,
        )

        with pytest.raises(sqlite3.IntegrityError, match="snapshot"):
            store.record_triage_result(
                comment_id=comment_a,
                snapshot_id=snapshot_b,
                agent_name="codex",
                model="gpt-5.4",
                reasoning_effort="medium",
                triage_version="test",
                category="bug",
                confidence=0.9,
                summary_ja="クラッシュ報告",
                evidence_quote="Bug",
                suggested_labels=["type:bug"],
                promotion_recommended=True,
            )
        with pytest.raises(sqlite3.IntegrityError, match="snapshot"):
            store.record_promotion_decision(
                comment_id=comment_a,
                snapshot_id=snapshot_b,
                triage_result_id=None,
                promoter_version="test",
                decision="needs_human",
                reason="manual check",
            )

    assert comment_b != comment_a


def test_promotion_decision_rejects_mismatched_triage_result(tmp_path: Path) -> None:
    """Promotion decisions must not point to another snapshot's triage result."""
    with open_workshop_inbox(tmp_path / "inbox.sqlite3") as store:
        comment_a, snapshot_a = _insert_comment_snapshot(store)
        comment_b, snapshot_b = store.upsert_comment_snapshot(
            published_file_id="3718988020",
            comment=WorkshopComment(
                comment_id="333",
                author="Other",
                profile_url="https://steam/other",
                body_text="Other bug",
                author_account_id="456",
            ),
            is_creator=False,
            collection_run_id=None,
        )
        triage_b = store.record_triage_result(
            comment_id=comment_b,
            snapshot_id=snapshot_b,
            agent_name="codex",
            model="gpt-5.4",
            reasoning_effort="medium",
            triage_version="test",
            category="bug",
            confidence=0.9,
            summary_ja="別コメント",
            evidence_quote="Other bug",
            suggested_labels=["type:bug"],
            promotion_recommended=True,
        )

        with pytest.raises(sqlite3.IntegrityError, match="triage result"):
            store.record_promotion_decision(
                comment_id=comment_a,
                snapshot_id=snapshot_a,
                triage_result_id=triage_b,
                promoter_version="test",
                decision="needs_human",
                reason="manual check",
            )


def _insert_comment_snapshot(store: WorkshopInboxStore) -> tuple[int, int]:
    """Insert one local comment and snapshot fixture."""
    return store.upsert_comment_snapshot(
        published_file_id="3718988020",
        comment=WorkshopComment(
            comment_id="222",
            author="Reporter",
            profile_url="https://steam/reporter",
            body_text="Bug",
            author_account_id="123",
        ),
        is_creator=False,
        collection_run_id=None,
    )
