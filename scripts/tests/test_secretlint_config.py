"""Contract tests for repository-managed Secretlint patterns."""

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path

import pytest

_REPO_ROOT = Path(__file__).resolve().parents[2]
_NODE = shutil.which("node")


def _secretlint_pattern(name: str) -> str:
    config = json.loads((_REPO_ROOT / ".secretlintrc.json").read_text(encoding="utf-8"))
    for rule in config["rules"]:
        if rule["id"] != "@secretlint/secretlint-rule-pattern":
            continue
        for pattern in rule["options"]["patterns"]:
            if pattern["name"] == name:
                return pattern["patterns"][0]
    msg = f"Secretlint pattern not found: {name}"
    raise AssertionError(msg)


def _js_regex_match(pattern_literal: str, text: str) -> dict[str, str] | None:
    assert _NODE is not None
    script = """
const [literal, text] = process.argv.slice(1);
const lastSlash = literal.lastIndexOf("/");
const regex = new RegExp(literal.slice(1, lastSlash), literal.slice(lastSlash + 1));
const match = regex.exec(text);
process.stdout.write(JSON.stringify(match ? (match.groups ?? {}) : null));
"""
    result = subprocess.run(  # noqa: S603 -- test invokes Node with a fixed script and explicit args.
        [_NODE, "-e", script, pattern_literal, text],
        check=True,
        capture_output=True,
        encoding="utf-8",
    )
    return json.loads(result.stdout)


def _steamcmd_command(*login_tail: str) -> str:
    return " ".join(
        (
            "steamcmd",
            "+login",
            *login_tail,
            "+workshop_build_item",
            "<repo_root>/dist/workshop/workshop_item.vdf",
            "+quit",
        )
    )


@pytest.mark.skipif(_NODE is None, reason="node CLI not available")
def test_steamcmd_login_user_literal_matches_passwordless_upload_command() -> None:
    """Concrete Steam users are detected in cached-auth upload commands."""
    pattern = _secretlint_pattern("steamcmd login user literal")

    groups = _js_regex_match(
        pattern,
        _steamcmd_command("real_user"),
    )

    assert groups == {"steam_user": "real_user"}


@pytest.mark.skipif(_NODE is None, reason="node CLI not available")
def test_steamcmd_login_user_literal_matches_upload_command_with_password() -> None:
    """Concrete Steam users are detected when a password argument follows."""
    pattern = _secretlint_pattern("steamcmd login user literal")

    groups = _js_regex_match(
        pattern,
        _steamcmd_command("real_user", "correct-horse-battery"),
    )

    assert groups == {"steam_user": "real_user"}


@pytest.mark.skipif(_NODE is None, reason="node CLI not available")
def test_steamcmd_login_user_literal_allows_nonliteral_users() -> None:
    """Placeholders, environment variables, and anonymous login remain allowed."""
    pattern = _secretlint_pattern("steamcmd login user literal")

    for user in ("<steam_user>", "$STEAM_USER", '"$STEAM_USER"', "anonymous"):
        groups = _js_regex_match(
            pattern,
            _steamcmd_command(user),
        )
        assert groups is None
