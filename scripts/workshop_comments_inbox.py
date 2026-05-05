"""Collect Steam Workshop comments into a local SQLite inbox."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import os
import re
import sqlite3
import sys
from collections.abc import Callable
from dataclasses import dataclass
from datetime import UTC, datetime
from html.parser import HTMLParser
from pathlib import Path
from typing import Self
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

_NUMERIC_ID_PATTERN = re.compile(r"^[0-9]+$")
_STEAM_COMMENTS_URL = "https://steamcommunity.com/comment/PublishedFile_Public/render/{creator}/{published}/"
_STEAM_DETAILS_URL = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/"
_TRUNCATION_NOTE = "[truncated]"
_HTTP_OK = 200
_STEAMID64_ACCOUNT_ID_BASE = 76_561_197_960_265_728
_DEFAULT_STATE_DIR = Path(".coq-japanese_workshop/state")
_DEFAULT_DB_NAME = "workshop-inbox.sqlite3"
_COLLECTOR_VERSION = "local-sqlite-v1"

type Transport = Callable[[str, str, bytes | None, dict[str, str]], "HttpResponse"]


@dataclass(frozen=True)
class WorkshopComment:
    """A normalized public Steam Workshop comment."""

    comment_id: str
    author: str
    profile_url: str
    body_text: str
    author_account_id: str = ""


@dataclass(frozen=True)
class CollectionOptions:
    """Runtime limits for one Workshop inbox collection run."""

    dry_run: bool = False
    max_comments_per_run: int = 20
    max_pages: int = 5
    page_size: int = 20
    max_body_chars: int = 4000
    timeout_seconds: int = 20
    max_response_bytes: int = 2_097_152
    skip_creator_comments: bool = True


@dataclass(frozen=True)
class CollectionSummary:
    """Summary of one Workshop inbox collection run."""

    fetched: int
    new_comments: int
    new_snapshots: int


@dataclass(frozen=True)
class HttpResponse:
    """HTTP response returned by an injectable transport."""

    status_code: int
    body: bytes
    headers: dict[str, str]


class WorkshopInboxStore:
    """SQLite-backed local inbox for Steam Workshop comments."""

    def __init__(self, connection: sqlite3.Connection) -> None:
        """Initialize the store with an open migrated SQLite connection."""
        self.connection = connection

    def __enter__(self) -> Self:
        """Return the open store for context-manager use."""
        return self

    def __exit__(self, *args: object) -> None:
        """Close the underlying SQLite connection."""
        del args
        self.connection.close()

    def start_collection_run(self) -> int:
        """Record a running collection attempt."""
        now = utc_now()
        with self.connection:
            cursor = self.connection.execute(
                """
                INSERT INTO collection_runs (started_at, status, collector_version)
                VALUES (?, 'running', ?)
                """,
                (now, _COLLECTOR_VERSION),
            )
        return _last_row_id(cursor)

    def finish_collection_run(
        self,
        *,
        run_id: int,
        status: str,
        fetched_count: int,
        new_comment_count: int,
        new_snapshot_count: int,
        error_message: str = "",
    ) -> None:
        """Mark a collection attempt as complete."""
        with self.connection:
            self.connection.execute(
                """
                UPDATE collection_runs
                SET finished_at = ?,
                    status = ?,
                    fetched_count = ?,
                    new_comment_count = ?,
                    new_snapshot_count = ?,
                    error_message = ?
                WHERE id = ?
                """,
                (utc_now(), status, fetched_count, new_comment_count, new_snapshot_count, error_message, run_id),
            )

    def count_comments(self) -> int:
        """Return the number of locally tracked Workshop comments."""
        return int(self.connection.execute("SELECT COUNT(*) FROM workshop_comments").fetchone()[0])

    def count_snapshots(self) -> int:
        """Return the number of locally tracked Workshop comment snapshots."""
        return int(self.connection.execute("SELECT COUNT(*) FROM workshop_comment_snapshots").fetchone()[0])

    def upsert_comment_snapshot(
        self,
        *,
        published_file_id: str,
        comment: WorkshopComment,
        is_creator: bool,
        collection_run_id: int | None,
    ) -> tuple[int, int]:
        """Upsert a comment row and insert a new body snapshot when the body changed."""
        now = utc_now()
        with self.connection:
            self.connection.execute(
                """
                INSERT INTO workshop_comments (
                    source,
                    published_file_id,
                    steam_comment_id,
                    author_name,
                    author_account_id,
                    profile_url,
                    is_creator,
                    first_seen_at,
                    last_seen_at,
                    status,
                    last_collection_run_id
                )
                VALUES ('steam_workshop', ?, ?, ?, ?, ?, ?, ?, ?, 'active', ?)
                ON CONFLICT(published_file_id, steam_comment_id) DO UPDATE SET
                    author_name = excluded.author_name,
                    author_account_id = excluded.author_account_id,
                    profile_url = excluded.profile_url,
                    is_creator = excluded.is_creator,
                    last_seen_at = excluded.last_seen_at,
                    deleted_seen_at = NULL,
                    last_collection_run_id = excluded.last_collection_run_id
                """,
                (
                    published_file_id,
                    comment.comment_id,
                    comment.author,
                    comment.author_account_id,
                    comment.profile_url,
                    int(is_creator),
                    now,
                    now,
                    collection_run_id,
                ),
            )
            comment_id = int(
                self.connection.execute(
                    """
                    SELECT id
                    FROM workshop_comments
                    WHERE published_file_id = ? AND steam_comment_id = ?
                    """,
                    (published_file_id, comment.comment_id),
                ).fetchone()[0],
            )
            body_hash = sha256_text(comment.body_text)
            self.connection.execute(
                """
                INSERT OR IGNORE INTO workshop_comment_snapshots (
                    comment_id,
                    observed_at,
                    body_text,
                    body_sha256,
                    normalized_body_sha256,
                    body_length,
                    collection_run_id
                )
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    comment_id,
                    now,
                    comment.body_text,
                    body_hash,
                    sha256_text(_normalize_body(comment.body_text)),
                    len(comment.body_text),
                    collection_run_id,
                ),
            )
            snapshot_id = int(
                self.connection.execute(
                    """
                    SELECT id
                    FROM workshop_comment_snapshots
                    WHERE comment_id = ? AND body_sha256 = ?
                    """,
                    (comment_id, body_hash),
                ).fetchone()[0],
            )
        return comment_id, snapshot_id

    def record_triage_result(  # noqa: PLR0913
        self,
        *,
        comment_id: int,
        snapshot_id: int,
        agent_name: str,
        model: str,
        reasoning_effort: str,
        triage_version: str,
        category: str,
        confidence: float,
        summary_ja: str,
        evidence_quote: str,
        suggested_labels: list[str],
        promotion_recommended: bool,
        duplicate_candidates: list[object] | None = None,
        investigation_notes: str = "",
    ) -> int:
        """Append one LLM triage audit result."""
        with self.connection:
            cursor = self.connection.execute(
                """
                INSERT INTO triage_results (
                    comment_id,
                    snapshot_id,
                    triaged_at,
                    agent_name,
                    model,
                    reasoning_effort,
                    triage_version,
                    category,
                    confidence,
                    summary_ja,
                    evidence_quote,
                    suggested_labels_json,
                    promotion_recommended,
                    duplicate_candidates_json,
                    investigation_notes
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    comment_id,
                    snapshot_id,
                    utc_now(),
                    agent_name,
                    model,
                    reasoning_effort,
                    triage_version,
                    category,
                    confidence,
                    summary_ja,
                    evidence_quote,
                    json.dumps(suggested_labels, ensure_ascii=False),
                    int(promotion_recommended),
                    json.dumps(duplicate_candidates or [], ensure_ascii=False),
                    investigation_notes,
                ),
            )
        return _last_row_id(cursor)

    def record_promotion_decision(  # noqa: PLR0913
        self,
        *,
        comment_id: int,
        snapshot_id: int,
        triage_result_id: int | None,
        promoter_version: str,
        decision: str,
        reason: str,
        duplicate_search_query: str = "",
        duplicate_evidence: list[object] | None = None,
        target_repo: str | None = None,
        issue_number: int | None = None,
        issue_url: str | None = None,
        issue_title: str | None = None,
        issue_body_sha256: str | None = None,
        labels: list[str] | None = None,
    ) -> int:
        """Append one deterministic promotion decision."""
        with self.connection:
            cursor = self.connection.execute(
                """
                INSERT INTO promotion_decisions (
                    comment_id,
                    snapshot_id,
                    triage_result_id,
                    decided_at,
                    promoter_version,
                    decision,
                    reason,
                    duplicate_search_query,
                    duplicate_evidence_json,
                    target_repo,
                    issue_number,
                    issue_url,
                    issue_title,
                    issue_body_sha256,
                    labels_json
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    comment_id,
                    snapshot_id,
                    triage_result_id,
                    utc_now(),
                    promoter_version,
                    decision,
                    reason,
                    duplicate_search_query,
                    json.dumps(duplicate_evidence or [], ensure_ascii=False),
                    target_repo,
                    issue_number,
                    issue_url,
                    issue_title,
                    issue_body_sha256,
                    json.dumps(labels or [], ensure_ascii=False),
                ),
            )
        return _last_row_id(cursor)


def default_workshop_state_dir() -> Path:
    """Return the default local state directory for Workshop automation."""
    return _DEFAULT_STATE_DIR


def default_workshop_db_path() -> Path:
    """Return the default local SQLite path for Workshop automation."""
    return Path(os.environ.get("QUDJP_WORKSHOP_INBOX_DB", default_workshop_state_dir() / _DEFAULT_DB_NAME))


def open_workshop_inbox(db_path: Path) -> WorkshopInboxStore:
    """Open a migrated SQLite inbox store."""
    db_path.parent.mkdir(parents=True, exist_ok=True)
    connection = sqlite3.connect(db_path)
    connection.execute("PRAGMA foreign_keys = ON")
    connection.execute("PRAGMA journal_mode = WAL")
    connection.execute("PRAGMA busy_timeout = 5000")
    _apply_migrations(connection)
    return WorkshopInboxStore(connection)


def main(argv: list[str] | None = None) -> int:
    """Run the local Workshop comments inbox CLI."""
    args = _parse_args(sys.argv[1:] if argv is None else argv)
    if args.command != "collect":
        return 2

    options = CollectionOptions(
        dry_run=args.dry_run,
        max_comments_per_run=args.max_comments_per_run,
        max_pages=args.max_pages,
        page_size=args.page_size,
        max_body_chars=args.max_body_chars,
        timeout_seconds=args.timeout_seconds,
        max_response_bytes=args.max_response_bytes,
        skip_creator_comments=not args.include_creator_comments,
    )
    with open_workshop_inbox(args.db_path) as store:
        summary = collect_workshop_comments(
            metadata_path=args.metadata_path,
            steam_transport=_make_urllib_transport(
                timeout_seconds=options.timeout_seconds,
                max_response_bytes=options.max_response_bytes,
            ),
            store=store,
            options=options,
        )
    print(  # noqa: T201
        "Steam Workshop comments: "
        f"fetched={summary.fetched} new_comments={summary.new_comments} new_snapshots={summary.new_snapshots}",
    )
    return 0


def collect_workshop_comments(
    *,
    metadata_path: Path,
    steam_transport: Transport,
    store: WorkshopInboxStore,
    options: CollectionOptions,
) -> CollectionSummary:
    """Collect new Steam comments into the local SQLite inbox."""
    _validate_collection_options(options)
    published_file_id = load_published_file_id(metadata_path)
    creator_id = _fetch_creator_id(
        published_file_id=published_file_id,
        steam_transport=steam_transport,
        max_response_bytes=options.max_response_bytes,
    )
    comments = _fetch_all_comments(
        creator_id=creator_id,
        published_file_id=published_file_id,
        steam_transport=steam_transport,
        options=options,
    )
    creator_account_id = creator_account_id_from_steam_id(creator_id)
    importable_comments = [
        comment
        for comment in comments
        if not options.skip_creator_comments or comment.author_account_id != creator_account_id
    ][: options.max_comments_per_run]

    if options.dry_run:
        return CollectionSummary(fetched=len(comments), new_comments=len(importable_comments), new_snapshots=0)

    before_comments = store.count_comments()
    before_snapshots = store.count_snapshots()
    run_id = store.start_collection_run()
    try:
        for comment in importable_comments:
            store.upsert_comment_snapshot(
                published_file_id=published_file_id,
                comment=comment,
                is_creator=comment.author_account_id == creator_account_id,
                collection_run_id=run_id,
            )
        new_comments = store.count_comments() - before_comments
        new_snapshots = store.count_snapshots() - before_snapshots
        store.finish_collection_run(
            run_id=run_id,
            status="success",
            fetched_count=len(comments),
            new_comment_count=new_comments,
            new_snapshot_count=new_snapshots,
        )
    except Exception as error:
        store.finish_collection_run(
            run_id=run_id,
            status="failed",
            fetched_count=len(comments),
            new_comment_count=store.count_comments() - before_comments,
            new_snapshot_count=store.count_snapshots() - before_snapshots,
            error_message=str(error),
        )
        raise
    return CollectionSummary(fetched=len(comments), new_comments=new_comments, new_snapshots=new_snapshots)


def validate_numeric_id(value: object, *, field_name: str) -> str:
    """Return a Steam numeric ID or raise when the value is unsafe for URL slots."""
    text = str(value)
    if _NUMERIC_ID_PATTERN.fullmatch(text) is None:
        msg = f"{field_name} must be numeric"
        raise ValueError(msg)
    return text


def creator_account_id_from_steam_id(steam_id: str) -> str:
    """Convert a SteamID64 value to Steam Community's data-miniprofile account ID."""
    account_id = int(validate_numeric_id(steam_id, field_name="creator")) - _STEAMID64_ACCOUNT_ID_BASE
    if account_id < 0:
        msg = "creator SteamID64 is below the account ID base"
        raise ValueError(msg)
    return str(account_id)


def load_published_file_id(metadata_path: Path) -> str:
    """Load and validate the Workshop published file ID from repo metadata."""
    with metadata_path.open(encoding="utf-8") as handle:
        data = json.load(handle)
    return validate_numeric_id(data.get("publishedfileid", ""), field_name="publishedfileid")


def build_steam_comments_url(*, creator_id: str, published_file_id: str, start: int, count: int) -> str:
    """Build the fixed public Steam comments render endpoint URL."""
    creator = validate_numeric_id(creator_id, field_name="creator")
    published = validate_numeric_id(published_file_id, field_name="publishedfileid")
    if start < 0 or count < 1:
        msg = "start must be non-negative and count must be positive"
        raise ValueError(msg)
    return f"{_STEAM_COMMENTS_URL.format(creator=creator, published=published)}?start={start}&count={count}&l=japanese"


def extract_comments_from_render_response(payload: bytes) -> list[WorkshopComment]:
    """Extract normalized comments from a Steam render JSON response."""
    data = json.loads(payload.decode("utf-8"))
    if data.get("success") is not True:
        msg = "Steam comments response was not successful"
        raise ValueError(msg)
    comments_html = data.get("comments_html")
    if not isinstance(comments_html, str):
        msg = "Steam comments response did not include comments_html"
        raise TypeError(msg)

    parser = _SteamCommentParser()
    parser.feed(comments_html)
    parser.close()
    return parser.comments


def sanitize_untrusted_text(text: str, *, max_chars: int) -> str:
    """Neutralize untrusted Steam text before embedding it in GitHub Markdown."""
    if max_chars < 1:
        msg = "max_chars must be positive"
        raise ValueError(msg)

    normalized = text.replace("\r\n", "\n").replace("\r", "\n").replace("\x00", "?")
    truncated = len(normalized) > max_chars
    bounded = normalized[:max_chars]
    escaped = html.escape(bounded, quote=False)
    escaped = escaped.replace("`", "&#96;")
    escaped = escaped.replace("@", "@\u200b")
    escaped = escaped.replace("](", "]&#40;")
    if truncated:
        return f"{escaped}\n\n{_TRUNCATION_NOTE}"
    return escaped


def sha256_text(text: str) -> str:
    """Hash text using UTF-8 SHA-256."""
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def utc_now() -> str:
    """Return an ISO-8601 UTC timestamp."""
    return datetime.now(UTC).isoformat(timespec="microseconds")


def _last_row_id(cursor: sqlite3.Cursor) -> int:
    row_id = cursor.lastrowid
    if row_id is None:
        msg = "SQLite insert did not return a row id"
        raise RuntimeError(msg)
    return row_id


class _SteamCommentParser(HTMLParser):
    """HTML parser for the Steam comment render fragment."""

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.comments: list[WorkshopComment] = []
        self._current_id = ""
        self._current_author = ""
        self._current_profile_url = ""
        self._current_body_parts: list[str] = []
        self._author_parts: list[str] = []
        self._current_author_account_id = ""
        self._comment_div_depth = 0
        self._in_author = False
        self._in_body = False

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        """Track comment, author, and body elements."""
        attr_map = _attrs_to_dict(attrs)
        if tag == "div" and self._current_id == "":
            comment_id = _comment_id_from_attrs(attr_map)
            if comment_id != "":
                self._start_comment(comment_id)
                return

        if self._current_id == "":
            return

        if tag == "div":
            self._comment_div_depth += 1
            if _class_contains(attr_map, "commentthread_comment_text"):
                self._in_body = True
        elif tag == "a" and _class_contains(attr_map, "commentthread_author_link") and self._current_author == "":
            self._in_author = True
            self._author_parts = []
            self._current_profile_url = attr_map.get("href", "")
            self._current_author_account_id = attr_map.get("data-miniprofile", "")
        elif tag == "br" and self._in_body:
            self._current_body_parts.append("\n")

    def handle_endtag(self, tag: str) -> None:
        """Close tracked elements and emit a complete comment."""
        if self._current_id == "":
            return
        if tag == "a" and self._in_author:
            self._current_author = _normalize_text("".join(self._author_parts))
            self._in_author = False
        if tag == "div":
            if self._in_body:
                self._in_body = False
            self._comment_div_depth -= 1
            if self._comment_div_depth == 0:
                self._finish_comment()

    def handle_data(self, data: str) -> None:
        """Collect author and body text."""
        if self._in_author:
            self._author_parts.append(data)
        if self._in_body:
            self._current_body_parts.append(data)

    def _start_comment(self, comment_id: str) -> None:
        self._current_id = comment_id
        self._current_author = ""
        self._current_profile_url = ""
        self._current_body_parts = []
        self._author_parts = []
        self._current_author_account_id = ""
        self._comment_div_depth = 1
        self._in_author = False
        self._in_body = False

    def _finish_comment(self) -> None:
        body_text = _normalize_body("".join(self._current_body_parts))
        self.comments.append(
            WorkshopComment(
                comment_id=self._current_id,
                author=self._current_author,
                profile_url=self._current_profile_url,
                body_text=body_text,
                author_account_id=self._current_author_account_id,
            ),
        )
        self._current_id = ""
        self._current_author = ""
        self._current_profile_url = ""
        self._current_body_parts = []
        self._author_parts = []
        self._current_author_account_id = ""
        self._in_author = False
        self._in_body = False


def _apply_migrations(connection: sqlite3.Connection) -> None:
    connection.executescript(_INITIAL_SCHEMA)
    connection.execute(
        """
        INSERT OR IGNORE INTO schema_migrations (version, applied_at, description)
        VALUES (1, ?, 'initial local workshop inbox schema')
        """,
        (utc_now(),),
    )
    connection.commit()


_INITIAL_SCHEMA = """
CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL,
    description TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS app_kv (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS collection_runs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    started_at TEXT NOT NULL,
    finished_at TEXT,
    status TEXT NOT NULL CHECK (status IN ('running', 'success', 'failed')),
    fetched_count INTEGER NOT NULL DEFAULT 0,
    new_comment_count INTEGER NOT NULL DEFAULT 0,
    new_snapshot_count INTEGER NOT NULL DEFAULT 0,
    error_message TEXT NOT NULL DEFAULT '',
    collector_version TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS workshop_comments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source TEXT NOT NULL DEFAULT 'steam_workshop',
    published_file_id TEXT NOT NULL,
    steam_comment_id TEXT NOT NULL,
    author_name TEXT NOT NULL,
    author_account_id TEXT,
    profile_url TEXT,
    is_creator INTEGER NOT NULL DEFAULT 0 CHECK (is_creator IN (0, 1)),
    first_seen_at TEXT NOT NULL,
    last_seen_at TEXT NOT NULL,
    deleted_seen_at TEXT,
    status TEXT NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'triaged', 'ignored', 'promoted', 'archived')),
    last_collection_run_id INTEGER REFERENCES collection_runs(id) ON DELETE SET NULL,
    UNIQUE (published_file_id, steam_comment_id)
);

CREATE TABLE IF NOT EXISTS workshop_comment_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    comment_id INTEGER NOT NULL REFERENCES workshop_comments(id) ON DELETE RESTRICT,
    observed_at TEXT NOT NULL,
    body_text TEXT NOT NULL,
    body_sha256 TEXT NOT NULL,
    normalized_body_sha256 TEXT NOT NULL,
    body_length INTEGER NOT NULL,
    collection_run_id INTEGER REFERENCES collection_runs(id) ON DELETE SET NULL,
    redacted_at TEXT,
    redaction_reason TEXT,
    UNIQUE (comment_id, body_sha256)
);

CREATE TABLE IF NOT EXISTS triage_results (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    comment_id INTEGER NOT NULL REFERENCES workshop_comments(id) ON DELETE RESTRICT,
    snapshot_id INTEGER NOT NULL REFERENCES workshop_comment_snapshots(id) ON DELETE RESTRICT,
    triaged_at TEXT NOT NULL,
    agent_name TEXT NOT NULL,
    model TEXT NOT NULL DEFAULT '',
    reasoning_effort TEXT NOT NULL DEFAULT '',
    triage_version TEXT NOT NULL,
    category TEXT NOT NULL
        CHECK (category IN ('bug', 'feature_request', 'question', 'feedback', 'ignore', 'spam', 'unknown')),
    confidence REAL NOT NULL CHECK (confidence >= 0.0 AND confidence <= 1.0),
    summary_ja TEXT NOT NULL,
    evidence_quote TEXT NOT NULL,
    suggested_labels_json TEXT NOT NULL,
    promotion_recommended INTEGER NOT NULL CHECK (promotion_recommended IN (0, 1)),
    duplicate_candidates_json TEXT NOT NULL DEFAULT '[]',
    investigation_notes TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS promotion_decisions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    comment_id INTEGER NOT NULL REFERENCES workshop_comments(id) ON DELETE RESTRICT,
    snapshot_id INTEGER NOT NULL REFERENCES workshop_comment_snapshots(id) ON DELETE RESTRICT,
    triage_result_id INTEGER REFERENCES triage_results(id) ON DELETE RESTRICT,
    decided_at TEXT NOT NULL,
    promoter_version TEXT NOT NULL,
    decision TEXT NOT NULL CHECK (decision IN ('promoted', 'duplicate', 'skipped', 'needs_human')),
    reason TEXT NOT NULL,
    duplicate_search_query TEXT NOT NULL DEFAULT '',
    duplicate_evidence_json TEXT NOT NULL DEFAULT '[]',
    target_repo TEXT,
    issue_number INTEGER,
    issue_url TEXT,
    issue_title TEXT,
    issue_body_sha256 TEXT,
    labels_json TEXT NOT NULL DEFAULT '[]',
    UNIQUE (target_repo, issue_number)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_promotion_decisions_one_promotion_per_comment
ON promotion_decisions(comment_id)
WHERE decision = 'promoted';

CREATE INDEX IF NOT EXISTS idx_workshop_comments_status
ON workshop_comments(status, first_seen_at);

CREATE INDEX IF NOT EXISTS idx_workshop_comment_snapshots_comment
ON workshop_comment_snapshots(comment_id, observed_at DESC);

CREATE INDEX IF NOT EXISTS idx_triage_results_comment
ON triage_results(comment_id, triaged_at DESC);

CREATE TRIGGER IF NOT EXISTS triage_results_no_update
BEFORE UPDATE ON triage_results
BEGIN
    SELECT RAISE(ABORT, 'triage_results is append-only');
END;

CREATE TRIGGER IF NOT EXISTS triage_results_no_delete
BEFORE DELETE ON triage_results
BEGIN
    SELECT RAISE(ABORT, 'triage_results is append-only');
END;

CREATE TRIGGER IF NOT EXISTS triage_results_snapshot_matches_comment
BEFORE INSERT ON triage_results
WHEN NOT EXISTS (
    SELECT 1
    FROM workshop_comment_snapshots
    WHERE id = NEW.snapshot_id
      AND comment_id = NEW.comment_id
)
BEGIN
    SELECT RAISE(ABORT, 'triage_results snapshot does not belong to comment');
END;

CREATE TRIGGER IF NOT EXISTS promotion_decisions_no_update
BEFORE UPDATE ON promotion_decisions
BEGIN
    SELECT RAISE(ABORT, 'promotion_decisions is append-only');
END;

CREATE TRIGGER IF NOT EXISTS promotion_decisions_no_delete
BEFORE DELETE ON promotion_decisions
BEGIN
    SELECT RAISE(ABORT, 'promotion_decisions is append-only');
END;

CREATE TRIGGER IF NOT EXISTS promotion_decisions_snapshot_matches_comment
BEFORE INSERT ON promotion_decisions
WHEN NOT EXISTS (
    SELECT 1
    FROM workshop_comment_snapshots
    WHERE id = NEW.snapshot_id
      AND comment_id = NEW.comment_id
)
BEGIN
    SELECT RAISE(ABORT, 'promotion_decisions snapshot does not belong to comment');
END;

CREATE TRIGGER IF NOT EXISTS promotion_decisions_triage_matches_snapshot
BEFORE INSERT ON promotion_decisions
WHEN NEW.triage_result_id IS NOT NULL
 AND NOT EXISTS (
    SELECT 1
    FROM triage_results
    WHERE id = NEW.triage_result_id
      AND comment_id = NEW.comment_id
      AND snapshot_id = NEW.snapshot_id
)
BEGIN
    SELECT RAISE(ABORT, 'promotion_decisions triage result does not match comment snapshot');
END;
"""


def _attrs_to_dict(attrs: list[tuple[str, str | None]]) -> dict[str, str]:
    return {key: value or "" for key, value in attrs}


def _class_contains(attrs: dict[str, str], class_name: str) -> bool:
    return class_name in attrs.get("class", "").split()


def _comment_id_from_attrs(attrs: dict[str, str]) -> str:
    element_id = attrs.get("id", "")
    if not element_id.startswith("comment_"):
        return ""
    comment_id = element_id.removeprefix("comment_")
    if _NUMERIC_ID_PATTERN.fullmatch(comment_id) is None:
        return ""
    return comment_id


def _normalize_text(text: str) -> str:
    return " ".join(text.split())


def _normalize_body(text: str) -> str:
    lines = [_normalize_text(line) for line in text.replace("\r\n", "\n").replace("\r", "\n").split("\n")]
    return "\n".join(line for line in lines if line)


def _fetch_creator_id(*, published_file_id: str, steam_transport: Transport, max_response_bytes: int) -> str:
    form = urlencode({"itemcount": "1", "publishedfileids[0]": published_file_id}).encode()
    response = steam_transport(
        "POST",
        _STEAM_DETAILS_URL,
        form,
        {"Content-Type": "application/x-www-form-urlencoded"},
    )
    if response.status_code != _HTTP_OK:
        msg = "Steam metadata request failed"
        raise ValueError(msg)
    _require_bounded_response(response, max_response_bytes=max_response_bytes)
    data = json.loads(response.body.decode("utf-8"))
    details = data.get("response", {}).get("publishedfiledetails", [])
    if not isinstance(details, list) or not details:
        msg = "Steam metadata response did not include published file details"
        raise ValueError(msg)
    first = details[0]
    if not isinstance(first, dict) or first.get("result") != 1:
        msg = "Steam metadata response result was not successful"
        raise ValueError(msg)
    return validate_numeric_id(first.get("creator", ""), field_name="creator")


def _fetch_all_comments(
    *,
    creator_id: str,
    published_file_id: str,
    steam_transport: Transport,
    options: CollectionOptions,
) -> list[WorkshopComment]:
    comments: list[WorkshopComment] = []
    for page_index in range(options.max_pages):
        start = page_index * options.page_size
        url = build_steam_comments_url(
            creator_id=creator_id,
            published_file_id=published_file_id,
            start=start,
            count=options.page_size,
        )
        response = steam_transport("GET", url, None, {})
        if response.status_code != _HTTP_OK:
            msg = "Steam comments request failed"
            raise ValueError(msg)
        _require_bounded_response(response, max_response_bytes=options.max_response_bytes)
        page_comments = extract_comments_from_render_response(response.body)
        if not page_comments:
            break
        comments.extend(page_comments)
        if len(page_comments) < options.page_size:
            break
    return comments


def _require_bounded_response(response: HttpResponse, *, max_response_bytes: int) -> None:
    if len(response.body) > max_response_bytes:
        msg = "HTTP response exceeded max response bytes"
        raise ValueError(msg)


def _validate_collection_options(options: CollectionOptions) -> None:
    if options.max_comments_per_run < 1:
        msg = "max_comments_per_run must be positive"
        raise ValueError(msg)
    if options.max_pages < 1:
        msg = "max_pages must be positive"
        raise ValueError(msg)
    if options.page_size < 1:
        msg = "page_size must be positive"
        raise ValueError(msg)
    if options.max_body_chars < 1:
        msg = "max_body_chars must be positive"
        raise ValueError(msg)
    if options.timeout_seconds < 1:
        msg = "timeout_seconds must be positive"
        raise ValueError(msg)
    if options.max_response_bytes < 1:
        msg = "max_response_bytes must be positive"
        raise ValueError(msg)


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Collect Steam Workshop comments into a local SQLite inbox.")
    subparsers = parser.add_subparsers(dest="command", required=True)
    collect = subparsers.add_parser("collect", help="Collect public Steam Workshop comments.")
    collect.add_argument("--metadata-path", type=Path, default=Path("steam/workshop_metadata.json"))
    collect.add_argument("--db-path", type=Path, default=default_workshop_db_path())
    collect.add_argument("--max-comments-per-run", type=int, default=20)
    collect.add_argument("--max-pages", type=int, default=5)
    collect.add_argument("--page-size", type=int, default=20)
    collect.add_argument("--max-body-chars", type=int, default=4000)
    collect.add_argument("--timeout-seconds", type=int, default=20)
    collect.add_argument("--max-response-bytes", type=int, default=2_097_152)
    collect.add_argument("--include-creator-comments", action="store_true")
    collect.add_argument("--dry-run", action="store_true")
    return parser.parse_args(argv)


def _make_urllib_transport(*, timeout_seconds: int, max_response_bytes: int = 2_097_152) -> Transport:
    if max_response_bytes < 1:
        msg = "max_response_bytes must be positive"
        raise ValueError(msg)

    def _transport(method: str, url: str, body: bytes | None, headers: dict[str, str]) -> HttpResponse:
        request = Request(url, data=body, headers=headers, method=method)  # noqa: S310
        try:
            with urlopen(request, timeout=timeout_seconds) as response:  # noqa: S310
                response_headers = dict(response.headers.items())
                status_code = int(response.status)
                response_body = response.read(max_response_bytes + 1)
        except HTTPError as error:
            response_headers = dict(error.headers.items())
            status_code = int(error.code)
            response_body = error.read(max_response_bytes + 1)
        return HttpResponse(status_code=status_code, body=response_body, headers=response_headers)

    return _transport


if __name__ == "__main__":
    raise SystemExit(main())
