"""Tests for Steam Workshop comment inbox collection."""

from __future__ import annotations

import json
from email.message import Message
from io import BytesIO
from typing import TYPE_CHECKING, Self
from urllib.error import HTTPError

import pytest

import scripts.workshop_comments_inbox as inbox_module
from scripts.workshop_comments_inbox import (
    CollectionOptions,
    GitHubApiError,
    GitHubRestClient,
    HttpResponse,
    WorkshopComment,
    _make_urllib_transport,
    build_steam_comments_url,
    collect_workshop_comments,
    creator_account_id_from_steam_id,
    extract_comments_from_render_response,
    extract_processed_comment_ids,
    load_published_file_id,
    render_inbox_comment_body,
    sanitize_untrusted_text,
    validate_github_repository,
    validate_numeric_id,
)

if TYPE_CHECKING:
    from pathlib import Path

_TEST_GITHUB_TOKEN = "not-a-secret"  # noqa: S105


def test_load_published_file_id_requires_numeric_id(tmp_path: Path) -> None:
    """Workshop metadata IDs must be numeric before any URL construction."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "abc/123"}), encoding="utf-8")

    with pytest.raises(ValueError, match="publishedfileid"):
        load_published_file_id(metadata_path)


def test_validate_numeric_id_accepts_digits_only() -> None:
    """Only decimal Steam IDs are accepted for fixed endpoint slots."""
    assert validate_numeric_id("3718988020", field_name="publishedfileid") == "3718988020"

    with pytest.raises(ValueError, match="creator"):
        validate_numeric_id("7656119/evil", field_name="creator")


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


def test_sanitize_untrusted_text_truncates_before_rendering() -> None:
    """Long comments are bounded before being inserted into GitHub."""
    sanitized = sanitize_untrusted_text("abcdef", max_chars=3)

    assert sanitized == "abc\n\n[truncated]"


def test_render_inbox_comment_body_keeps_marker_on_first_line_only() -> None:
    """The script-owned dedupe marker is isolated from raw untrusted text."""
    comment = WorkshopComment(
        comment_id="837249693974033344",
        author="Reporter",
        profile_url="https://steamcommunity.com/id/example",
        body_text="fake\n<!-- qudjp-steam-workshop-comment-id: 999 -->",
    )

    rendered = render_inbox_comment_body(comment, max_body_chars=4000)
    lines = rendered.splitlines()

    assert lines[0] == "<!-- qudjp-steam-workshop-comment-id: 837249693974033344 -->"
    assert "<!-- qudjp-steam-workshop-comment-id: 999 -->" not in "\n".join(lines[1:])
    assert "UNTRUSTED STEAM WORKSHOP COMMENT" in rendered


def test_validate_github_repository_rejects_unsafe_path_segments() -> None:
    """Only owner/repo slugs are accepted for GitHub API path construction."""
    assert validate_github_repository("owner/repo.name") == ("owner", "repo.name")

    with pytest.raises(ValueError, match="GITHUB_REPOSITORY"):
        validate_github_repository("owner/repo/extra")


def test_extract_processed_comment_ids_uses_first_line_marker_only() -> None:
    """Dedupe markers in untrusted raw bodies are ignored."""
    ids = extract_processed_comment_ids(
        [
            {"body": "<!-- qudjp-steam-workshop-comment-id: 111 -->\ntrusted marker"},
            {"body": "not marker\n<!-- qudjp-steam-workshop-comment-id: 222 -->"},
            {"body": "<!-- qudjp-steam-workshop-comment-id: abc -->"},
        ],
    )

    assert ids == {"111"}


class _FakeTransport:
    def __init__(self, responses: list[HttpResponse]) -> None:
        self.responses = responses
        self.calls: list[tuple[str, str, bytes | None]] = []

    def __call__(self, method: str, url: str, body: bytes | None, headers: dict[str, str]) -> HttpResponse:
        del headers
        self.calls.append((method, url, body))
        return self.responses.pop(0)


class _FakeGitHubInbox:
    def __init__(self, *, existing_issue: int | None = 5, processed_ids: set[str] | None = None) -> None:
        self.existing_issue = existing_issue
        self.processed_ids = processed_ids or set()
        self.created_issue = False
        self.closed_issue_numbers: list[int] = []
        self.ensure_label_calls: list[str] = []
        self.posted_bodies: list[str] = []

    def ensure_label(self, *, name: str, color: str, description: str) -> None:
        del color, description
        self.ensure_label_calls.append(name)

    def find_inbox_issue(self, *, max_pages: int) -> int | None:
        del max_pages
        return self.existing_issue

    def create_inbox_issue(self) -> int:
        self.created_issue = True
        return 9

    def list_processed_comment_ids(self, *, issue_number: int, max_pages: int) -> set[str]:
        del issue_number, max_pages
        return self.processed_ids

    def post_issue_comment(self, *, issue_number: int, body: str) -> None:
        del issue_number
        self.posted_bodies.append(body)

    def close_issue(self, *, issue_number: int) -> None:
        self.closed_issue_numbers.append(issue_number)


class _FailingPostGitHubInbox(_FakeGitHubInbox):
    def __init__(self, *, fail_on_post_number: int, existing_issue: int | None = 5) -> None:
        super().__init__(existing_issue=existing_issue)
        self.fail_on_post_number = fail_on_post_number
        self.post_attempts = 0

    def post_issue_comment(self, *, issue_number: int, body: str) -> None:
        self.post_attempts += 1
        if self.post_attempts == self.fail_on_post_number:
            msg = "post failed"
            raise GitHubApiError(msg)
        super().post_issue_comment(issue_number=issue_number, body=body)


class _FailingListProcessedGitHubInbox(_FakeGitHubInbox):
    def list_processed_comment_ids(self, *, issue_number: int, max_pages: int) -> set[str]:
        del issue_number, max_pages
        msg = "list processed failed"
        raise GitHubApiError(msg)


def _github_client(transport: _FakeTransport) -> GitHubRestClient:
    return GitHubRestClient(repository="owner/repo", token=_TEST_GITHUB_TOKEN, transport=transport)


def test_github_client_uses_fixed_label_endpoints() -> None:
    """Missing fixed labels are created through the reviewed REST endpoints."""
    transport = _FakeTransport(
        [
            HttpResponse(status_code=404, body=b'{"message":"Not Found"}', headers={}),
            HttpResponse(status_code=201, body=b'{"name":"source:steam-workshop"}', headers={}),
        ],
    )
    client = _github_client(transport)

    client.ensure_label(name="source:steam-workshop", color="5319e7", description="Imported from Steam Workshop.")

    assert transport.calls[0] == (
        "GET",
        "https://api.github.com/repos/owner/repo/labels/source%3Asteam-workshop",
        None,
    )
    assert transport.calls[1][0] == "POST"
    assert transport.calls[1][1] == "https://api.github.com/repos/owner/repo/labels"
    assert json.loads((transport.calls[1][2] or b"").decode()) == {
        "name": "source:steam-workshop",
        "color": "5319e7",
        "description": "Imported from Steam Workshop.",
    }


def test_find_inbox_issue_fails_closed_when_multiple_open_inboxes_exist() -> None:
    """Ambiguous inbox state must not receive imported comments."""
    transport = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    [
                        {"number": 1, "title": "Steam Workshop comment inbox"},
                        {"number": 2, "title": "Steam Workshop comment inbox"},
                    ],
                ).encode(),
                headers={},
            ),
        ],
    )
    client = _github_client(transport)

    with pytest.raises(GitHubApiError, match="multiple"):
        client.find_inbox_issue(max_pages=10)


def test_find_inbox_issue_searches_all_states_to_reuse_closed_inbox() -> None:
    """Closed inbox issues are reused so raw comment storage stays out of open issues."""
    transport = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps([{"number": 498, "title": "Steam Workshop comment inbox"}]).encode(),
                headers={},
            ),
        ],
    )
    client = _github_client(transport)

    assert client.find_inbox_issue(max_pages=10) == 498
    assert "state=all" in transport.calls[0][1]


def test_list_processed_ids_reads_marker_from_second_comment_page() -> None:
    """Existing markers are read from paginated GitHub issue comments."""
    transport = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps([{"body": "<!-- qudjp-steam-workshop-comment-id: 111 -->"}]).encode(),
                headers={"Link": '<https://api.github.com/repositories/1/issues/5/comments?page=2>; rel="next"'},
            ),
            HttpResponse(
                status_code=200,
                body=json.dumps([{"body": "<!-- qudjp-steam-workshop-comment-id: 222 -->"}]).encode(),
                headers={},
            ),
        ],
    )
    client = _github_client(transport)

    assert client.list_processed_comment_ids(issue_number=5, max_pages=10) == {"111", "222"}


def test_list_processed_ids_fails_closed_when_comment_pagination_exceeds_cap() -> None:
    """Marker dedupe must not silently ignore unread GitHub comment pages."""
    transport = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=b"[]",
                headers={"Link": '<https://api.github.com/repositories/1/issues/5/comments?page=2>; rel="next"'},
            ),
        ],
    )
    client = _github_client(transport)

    with pytest.raises(GitHubApiError, match="pagination"):
        client.list_processed_comment_ids(issue_number=5, max_pages=1)


def test_urllib_transport_returns_http_error_response(monkeypatch: pytest.MonkeyPatch) -> None:
    """Expected REST 404 responses must reach GitHubRestClient instead of escaping as exceptions."""
    headers = Message()

    def raise_404(*args: object, **kwargs: object) -> None:
        del args, kwargs
        raise HTTPError(
            url="https://api.github.com/repos/owner/repo/labels/source%3Asteam-workshop",
            code=404,
            msg="Not Found",
            hdrs=headers,
            fp=BytesIO(b'{"message":"Not Found"}'),
        )

    monkeypatch.setattr(inbox_module, "urlopen", raise_404)
    transport = _make_urllib_transport(timeout_seconds=20)

    response = transport(
        "GET",
        "https://api.github.com/repos/owner/repo/labels/source%3Asteam-workshop",
        None,
        {},
    )

    assert response.status_code == 404
    assert response.body == b'{"message":"Not Found"}'


def test_urllib_transport_reads_only_one_byte_past_response_cap(monkeypatch: pytest.MonkeyPatch) -> None:
    """The transport must not load unbounded HTTP bodies before size validation."""

    class _FakeUrlopenResponse:
        status = 200
        headers = Message()

        def __init__(self) -> None:
            self.read_sizes: list[int | None] = []

        def __enter__(self) -> Self:
            return self

        def __exit__(self, *args: object) -> None:
            del args

        def read(self, size: int | None = None) -> bytes:
            self.read_sizes.append(size)
            return b"x" * 6

    response = _FakeUrlopenResponse()

    def fake_urlopen(*args: object, **kwargs: object) -> _FakeUrlopenResponse:
        del args, kwargs
        return response

    monkeypatch.setattr(inbox_module, "urlopen", fake_urlopen)
    transport = _make_urllib_transport(timeout_seconds=20, max_response_bytes=5)

    http_response = transport("GET", "https://example.test", None, {})

    assert response.read_sizes == [6]
    assert http_response.body == b"x" * 6


def test_collect_workshop_comments_dry_run_performs_no_github_writes(tmp_path: Path) -> None:
    """Dry-run fetches and sanitizes but never writes labels, issues, or comments."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {"response": {"publishedfiledetails": [{"result": 1, "creator": "76561198205102067"}]}},
                ).encode(),
                headers={},
            ),
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {
                        "success": True,
                        "total_count": 1,
                        "comments_html": '<div class="commentthread_comment" id="comment_111">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/a">A</a>'
                        '<div class="commentthread_comment_text">Bug report</div></div>',
                    },
                ).encode(),
                headers={},
            ),
        ],
    )
    github = _FakeGitHubInbox()

    summary = collect_workshop_comments(
        metadata_path=metadata_path,
        steam_transport=steam,
        github_client=github,
        options=CollectionOptions(dry_run=True),
    )

    assert summary.fetched == 1
    assert summary.planned_posts == 1
    assert github.ensure_label_calls == []
    assert github.posted_bodies == []
    assert github.created_issue is False


def test_collect_workshop_comments_oversized_metadata_response_performs_no_github_writes(tmp_path: Path) -> None:
    """Oversized Steam metadata responses fail closed before any GitHub write."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport([HttpResponse(status_code=200, body=b"x" * 6, headers={})])
    github = _FakeGitHubInbox()

    with pytest.raises(ValueError, match="max response bytes"):
        collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=steam,
            github_client=github,
            options=CollectionOptions(dry_run=False, max_response_bytes=5),
        )

    assert github.ensure_label_calls == []
    assert github.posted_bodies == []
    assert github.created_issue is False


def test_collect_workshop_comments_invalid_body_limit_performs_no_github_writes(tmp_path: Path) -> None:
    """Invalid sanitization limits are rejected before label, issue, or comment writes."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport([])
    github = _FakeGitHubInbox(existing_issue=None)

    with pytest.raises(ValueError, match="max_body_chars"):
        collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=steam,
            github_client=github,
            options=CollectionOptions(dry_run=False, max_body_chars=0),
        )

    assert github.ensure_label_calls == []
    assert github.created_issue is False
    assert github.posted_bodies == []


def test_collect_workshop_comments_fetch_failure_performs_no_github_writes(tmp_path: Path) -> None:
    """Steam/API failures fail closed before any GitHub write."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport([HttpResponse(status_code=500, body=b"{}", headers={})])
    github = _FakeGitHubInbox()

    with pytest.raises(ValueError, match="Steam metadata"):
        collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=steam,
            github_client=github,
            options=CollectionOptions(dry_run=False),
        )

    assert github.ensure_label_calls == []
    assert github.posted_bodies == []
    assert github.created_issue is False


def test_collect_workshop_comments_skips_processed_ids_after_successful_fetch(tmp_path: Path) -> None:
    """Already imported comment IDs are skipped after full Steam parsing succeeds."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {"response": {"publishedfiledetails": [{"result": 1, "creator": "76561198205102067"}]}},
                ).encode(),
                headers={},
            ),
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {
                        "success": True,
                        "total_count": 2,
                        "comments_html": '<div class="commentthread_comment" id="comment_111">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/a">A</a>'
                        '<div class="commentthread_comment_text">Old</div></div>'
                        '<div class="commentthread_comment" id="comment_222">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/b">B</a>'
                        '<div class="commentthread_comment_text">New</div></div>',
                    },
                ).encode(),
                headers={},
            ),
        ],
    )
    github = _FakeGitHubInbox(processed_ids={"111"})

    summary = collect_workshop_comments(
        metadata_path=metadata_path,
        steam_transport=steam,
        github_client=github,
        options=CollectionOptions(dry_run=False),
    )

    assert summary.fetched == 2
    assert summary.posted == 1
    assert len(github.posted_bodies) == 1
    assert github.posted_bodies[0].startswith("<!-- qudjp-steam-workshop-comment-id: 222 -->")


def test_collect_workshop_comments_skips_creator_comments_by_default(tmp_path: Path) -> None:
    """Author comments from the Workshop creator are not imported into the inbox."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {"response": {"publishedfiledetails": [{"result": 1, "creator": "76561198205102067"}]}},
                ).encode(),
                headers={},
            ),
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {
                        "success": True,
                        "total_count": 2,
                        "comments_html": '<div class="commentthread_comment" id="comment_111">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/creator" '
                        'data-miniprofile="244836339">Creator</a>'
                        '<div class="commentthread_comment_text">Thanks</div></div>'
                        '<div class="commentthread_comment" id="comment_222">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/reporter" '
                        'data-miniprofile="123">Reporter</a>'
                        '<div class="commentthread_comment_text">Bug</div></div>',
                    },
                ).encode(),
                headers={},
            ),
        ],
    )
    github = _FakeGitHubInbox()

    summary = collect_workshop_comments(
        metadata_path=metadata_path,
        steam_transport=steam,
        github_client=github,
        options=CollectionOptions(dry_run=False),
    )

    assert summary.fetched == 2
    assert summary.planned_posts == 1
    assert summary.posted == 1
    assert github.posted_bodies[0].startswith("<!-- qudjp-steam-workshop-comment-id: 222 -->")


def test_collect_workshop_comments_closes_new_inbox_issue_after_import(tmp_path: Path) -> None:
    """A newly created inbox issue is closed after comments are imported to keep GitHub tidy."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {"response": {"publishedfiledetails": [{"result": 1, "creator": "76561198205102067"}]}},
                ).encode(),
                headers={},
            ),
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {
                        "success": True,
                        "total_count": 1,
                        "comments_html": '<div class="commentthread_comment" id="comment_222">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/reporter" '
                        'data-miniprofile="123">Reporter</a>'
                        '<div class="commentthread_comment_text">Bug</div></div>',
                    },
                ).encode(),
                headers={},
            ),
        ],
    )
    github = _FakeGitHubInbox(existing_issue=None)

    collect_workshop_comments(
        metadata_path=metadata_path,
        steam_transport=steam,
        github_client=github,
        options=CollectionOptions(dry_run=False),
    )

    assert github.created_issue is True
    assert github.closed_issue_numbers == [9]


def test_collect_workshop_comments_closes_new_inbox_issue_when_posting_fails(tmp_path: Path) -> None:
    """Newly created inbox issues are closed even when comment posting fails."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {"response": {"publishedfiledetails": [{"result": 1, "creator": "76561198205102067"}]}},
                ).encode(),
                headers={},
            ),
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {
                        "success": True,
                        "total_count": 2,
                        "comments_html": '<div class="commentthread_comment" id="comment_111">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/a" '
                        'data-miniprofile="123">A</a>'
                        '<div class="commentthread_comment_text">First</div></div>'
                        '<div class="commentthread_comment" id="comment_222">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/b" '
                        'data-miniprofile="456">B</a>'
                        '<div class="commentthread_comment_text">Second</div></div>',
                    },
                ).encode(),
                headers={},
            ),
        ],
    )
    github = _FailingPostGitHubInbox(existing_issue=None, fail_on_post_number=2)

    with pytest.raises(GitHubApiError, match="post failed"):
        collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=steam,
            github_client=github,
            options=CollectionOptions(dry_run=False),
        )

    assert github.created_issue is True
    assert github.post_attempts == 2
    assert github.closed_issue_numbers == [9]


def test_collect_workshop_comments_closes_new_inbox_issue_when_dedupe_read_fails(tmp_path: Path) -> None:
    """Newly created inbox issues are closed if dedupe marker reading fails."""
    metadata_path = tmp_path / "workshop_metadata.json"
    metadata_path.write_text(json.dumps({"publishedfileid": "3718988020"}), encoding="utf-8")
    steam = _FakeTransport(
        [
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {"response": {"publishedfiledetails": [{"result": 1, "creator": "76561198205102067"}]}},
                ).encode(),
                headers={},
            ),
            HttpResponse(
                status_code=200,
                body=json.dumps(
                    {
                        "success": True,
                        "total_count": 1,
                        "comments_html": '<div class="commentthread_comment" id="comment_111">'
                        '<a class="hoverunderline commentthread_author_link" href="https://steam/a" '
                        'data-miniprofile="123">A</a>'
                        '<div class="commentthread_comment_text">First</div></div>',
                    },
                ).encode(),
                headers={},
            ),
        ],
    )
    github = _FailingListProcessedGitHubInbox(existing_issue=None)

    with pytest.raises(GitHubApiError, match="list processed failed"):
        collect_workshop_comments(
            metadata_path=metadata_path,
            steam_transport=steam,
            github_client=github,
            options=CollectionOptions(dry_run=False),
        )

    assert github.created_issue is True
    assert github.closed_issue_numbers == [9]
