# Steam Workshop 概要欄 BBCode 化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Steam Workshop の英語・日本語概要欄を Steam BBCode へ移行し、Apple Silicon の通常利用者向け説明を Rosetta 2 の推奨手順に絞る。

**Architecture:** `steam/workshop_description.en.txt` を steamcmd が使用する既定英語原稿、`steam/workshop_description.ja.txt` を Steam 編集画面で保存する日本語原稿として維持する。決定論的な Python 契約テストで、言語・対象バージョン・BBCode の許可タグと閉じ方・Markdown 残留・Harmony スレッドへの集約・VDF の改行保持を検査する。Steam 側の保存はリポジトリ変更と分離し、登録本文の提示と明示許可の後にだけ実行する。

**Tech Stack:** Steam Community BBCode、Python 3.12+、pytest、QudJP Workshop VDF generator、Steam Workshop item editor

---

## File Map

- Create: `docs/superpowers/specs/2026-07-16-steam-workshop-description-bbcode-design.md`
  - 承認済みの情報構造、使用タグ、Apple Silicon 説明の境界、公開ゲートを記録する。
- Create: `docs/superpowers/plans/2026-07-16-steam-workshop-description-bbcode.md`
  - Red→Green、検証、公開確認の手順を記録する。
- Modify: `scripts/tests/test_target_game_version_contract.py`
  - 英日原稿の対象バージョン、言語、許可 BBCode、閉じタグ、Markdown 残留、Harmony スレッドリンクを検査する。
- Modify: `scripts/tests/test_build_workshop_upload.py`
  - 既定英語原稿を VDF に描画したとき BBCode と実改行が保持されることを検査する。
- Modify: `steam/workshop_description.en.txt`
  - 既定英語概要欄を Steam BBCode と短い Apple Silicon 手順へ移行する。
- Modify: `steam/workshop_description.ja.txt`
  - 日本語概要欄を英語版と同じ構造の Steam BBCode へ移行する。

リポジトリの `AGENTS.md` により、ユーザーからコミットを明示依頼されるまでコミットしない。

### Task 1: BBCode 契約を Red にする

**Files:**
- Modify: `scripts/tests/test_target_game_version_contract.py`
- Modify: `scripts/tests/test_build_workshop_upload.py`

- [ ] **Step 1: 英日原稿の BBCode 契約テストを追加する**

`scripts/tests/test_target_game_version_contract.py` の import 群へ次を追加する。

```python
import pytest
```

同ファイルの定数群へ次を追加する。

```python
WORKSHOP_HARMONY_THREAD_URL = (
    "https://steamcommunity.com/workshop/filedetails/discussion/3718988020/572669660098532087/"
)
WORKSHOP_DESCRIPTION_LANGUAGE_CONTRACTS = {
    "steam/workshop_description.en.txt": (
        "[h1]Overview[/h1]",
        "Compatible with Caves of Qud 1.0.5",
    ),
    "steam/workshop_description.ja.txt": (
        "[h1]概要[/h1]",
        "Caves of Qud 1.0.5 対応",
    ),
}
STEAM_BBCODE_ALLOWED_TAGS = frozenset(
    {"*", "b", "code", "h1", "h2", "hr", "list", "olist", "url"}
)
STEAM_BBCODE_TOKEN_PATTERN = re.compile(
    r"\[(?P<closing>/)?(?P<tag>\*|[a-z][a-z0-9]*)(?:=(?P<argument>[^\[\]\r\n]+))?\]",
    flags=re.IGNORECASE,
)
STEAM_BRACKET_TOKEN_PATTERN = re.compile(r"\[[^\r\n]*?\]")
MARKDOWN_STAR_BULLET_PATTERN = re.compile(r"^[ \t]*\*(?:[ \t]+|$)", flags=re.MULTILINE)
```

同ファイルへ、角括弧トークン、タグ引数、リスト項目、スタックの整合性を検査する
helper と malformed syntax テストを追加する。

```python
def _check_list_item_token(
    *,
    token: str,
    closing: bool,
    argument: str | None,
    stack: list[tuple[str, str]],
) -> str | None:
    if closing or argument is not None:
        return f"invalid list item token: {token}"
    if not stack or stack[-1][0] not in {"list", "olist"}:
        return "[*] must be directly nested in [list] or [olist]"
    return None


def _check_closing_bbcode_tag(
    *,
    token: str,
    tag: str,
    argument: str | None,
    stack: list[tuple[str, str]],
) -> str | None:
    if argument is not None:
        return f"closing BBCode tag cannot have an argument: {token}"
    if not stack:
        return f"closing BBCode tag has no opener: {token}"
    if stack[-1][0] != tag:
        return f"mismatched BBCode tag: expected [/{stack[-1][0]}], found {token}"
    stack.pop()
    return None


def _scan_canonical_bbcode_tokens(
    description: str,
    findings: list[str],
) -> list[re.Match[str]]:
    matches: list[re.Match[str]] = []
    cursor = 0
    for bracket_match in STEAM_BRACKET_TOKEN_PATTERN.finditer(description):
        if re.search(r"[\[\]]", description[cursor : bracket_match.start()]):
            findings.append("stray unmatched '[' or ']' outside a BBCode token")

        token = bracket_match.group(0)
        canonical_match = STEAM_BBCODE_TOKEN_PATTERN.fullmatch(token)
        if canonical_match is None:
            findings.append(f"malformed BBCode token: {token}")
        else:
            matches.append(canonical_match)
        cursor = bracket_match.end()

    if re.search(r"[\[\]]", description[cursor:]):
        findings.append("stray unmatched '[' or ']' outside a BBCode token")
    return matches


def _assert_balanced_steam_bbcode(description: str, *, source: str) -> None:
    findings: list[str] = []
    stack: list[tuple[str, str]] = []

    if "`" in description:
        findings.append("Markdown backticks are not allowed")
    if MARKDOWN_STAR_BULLET_PATTERN.search(description):
        findings.append("Markdown '*' bullets are not allowed; use [*] list items")

    for match in _scan_canonical_bbcode_tokens(description, findings):
        token = match.group(0)
        tag = match.group("tag").casefold()
        closing = match.group("closing") is not None
        argument = match.group("argument")

        if tag not in STEAM_BBCODE_ALLOWED_TAGS:
            findings.append(f"unsupported BBCode tag: {token}")
            continue
        if tag == "*":
            finding = _check_list_item_token(
                token=token,
                closing=closing,
                argument=argument,
                stack=stack,
            )
            if finding is not None:
                findings.append(finding)
            continue
        if closing:
            finding = _check_closing_bbcode_tag(
                token=token,
                tag=tag,
                argument=argument,
                stack=stack,
            )
            if finding is not None:
                findings.append(finding)
            continue
        if argument is not None and tag != "url":
            findings.append(f"only [url] may have an argument: {token}")
        stack.append((tag, token))

    findings.extend(f"unclosed BBCode tag: {token}" for _tag, token in reversed(stack))
    assert not findings, f"{source} contains invalid Steam BBCode:\n" + "\n".join(findings)


@pytest.mark.parametrize(
    "description",
    [
        "[bad tag]",
        "[bogus!]text[/bogus!]",
        "[url=]",
        "[[b]]text[[/b]]",
        "stray [",
        "stray ]",
    ],
)
def test_steam_bbcode_contract_rejects_malformed_bracket_syntax(description: str) -> None:
    """Bracket-like syntax must be canonical BBCode rather than ignored text."""
    with pytest.raises(AssertionError, match="invalid Steam BBCode"):
        _assert_balanced_steam_bbcode(description, source="test description")
```

同ファイルへ、英語・日本語原稿のファイル別診断と Harmony URL の出現回数を検査する
テストを追加する。

```python
def test_workshop_descriptions_use_balanced_steam_bbcode() -> None:
    """Both localized descriptions must use Steam-supported BBCode only."""
    for path, (language_heading, version_text) in WORKSHOP_DESCRIPTION_LANGUAGE_CONTRACTS.items():
        description = _read(path)

        _assert_balanced_steam_bbcode(description, source=path)
        assert "[h1]Caves of Qud Japanese Mod[/h1]" in description, (
            f"{path}: missing Workshop title heading"
        )
        assert language_heading in description, f"{path}: missing language heading {language_heading!r}"
        assert version_text in description, f"{path}: missing target version {version_text!r}"
        for required_tag in ("[list]", "[olist]", "[code]"):
            assert required_tag in description, f"{path}: missing required tag {required_tag}"

        harmony_link = f"[url={WORKSHOP_HARMONY_THREAD_URL}]"
        assert description.count(harmony_link) == 1, (
            f"{path}: expected exactly one linked Harmony thread URL"
        )
        assert description.count(WORKSHOP_HARMONY_THREAD_URL) == 1, (
            f"{path}: Harmony thread URL must not also appear naked"
        )
```

- [ ] **Step 2: 既定英語原稿の VDF 保持テストを追加する**

`scripts/tests/test_build_workshop_upload.py` へ次を追加する。

```python
def test_checked_in_default_description_renders_bbcode_with_literal_newlines(tmp_path: Path) -> None:
    """The checked-in English BBCode keeps literal line breaks in rendered VDF."""
    metadata_path = Path(__file__).resolve().parents[2] / "steam" / "workshop_metadata.json"
    metadata = load_metadata(metadata_path)
    assert metadata.description_file is not None
    description = metadata.description_file.read_text(encoding="utf-8")

    vdf = render_vdf(
        metadata,
        content_folder=tmp_path / "QudJP",
        preview_file=tmp_path / "QudJP" / "preview.png",
        changenote="説明文の書式を更新しました。",
        description=description,
    )

    expected_description_field = f'  "description" "{vdf_escape(description)}"\n'
    assert expected_description_field in vdf
    assert "[h1]Overview[/h1]\n" in vdf
    assert "[olist]\n[*]Subscribe" in vdf
    assert r"[h1]Overview[/h1]\n" not in vdf
```

- [ ] **Step 3: Red を確認する**

Run:

```bash
uv run pytest \
  scripts/tests/test_target_game_version_contract.py::test_workshop_descriptions_use_balanced_steam_bbcode \
  scripts/tests/test_build_workshop_upload.py::test_checked_in_default_description_renders_bbcode_with_literal_newlines \
  -q
```

Expected: 既存原稿に `[h1]Overview[/h1]`、`[list]`、`[olist]` がなく、Markdown のバッククォートと行頭 `*` が残っているため FAIL。

### Task 2: 英語概要欄を Steam BBCode へ移行する

**Files:**
- Modify: `steam/workshop_description.en.txt`

- [ ] **Step 1: 英語原稿を次の内容へ置き換える**

```text
[h1]Caves of Qud Japanese Mod[/h1]

[b]Compatible with Caves of Qud 1.0.5[/b]

Nearly all Japanese translations in this mod were generated using generative AI and then reviewed. Contextual misunderstandings, mistranslations, or unusual wording may remain. Please report anything that looks wrong. Official localization work has also begun, so this project may be discontinued in the future.

[hr][/hr]

[h1]Overview[/h1]

This is an unofficial, fan-made mod for playing Caves of Qud in Japanese. It translates the UI, conversations, ability and mutation descriptions, pop-ups, message logs, and some procedurally generated text. It also includes a CJK font for Japanese text.

[h2]Main Features[/h2]

[list]
[*]Japanese dialogue translations
[*]Menus, options, character creation, ability screens, and other UI
[*]Pop-ups, message logs, the journal, death screens, and more
[*]Some procedurally generated text, historical text, and descriptions
[*]A bundled CJK font for Japanese text
[/list]

[h1]Installation[/h1]

[olist]
[*]Subscribe to this Workshop item.
[*]Enable [b]Caves of Qud Japanese Mod[/b] in the in-game Mod Manager.
[*]Restart the game.
[/olist]

[h2]Existing Saves[/h2]

The mod should generally work with existing saves. Back up important save data before enabling it. Previously generated text already stored in a save, as well as some dynamic text, may remain in English.

[h1]Apple Silicon Macs[/h1]

[b]Recommended: launch Caves of Qud through the bundled Rosetta 2 wrapper.[/b]

Native Apple Silicon launches may fail to apply this mod's UI patches because of the game's bundled Harmony library. QudJP does not automatically bundle or replace the game's [b]0Harmony.dll[/b].

[h2]Rosetta 2 Launch Steps[/h2]

[olist]
[*]Open this mod's Steam Workshop download folder.
[*]Double-click [b]Launch CavesOfQud (Rosetta).command[/b].
[*]Use this wrapper each time if the translations do not work when launching normally through Steam.
[/olist]

[h2]Default Wrapper Location[/h2]

[code]~/Library/Application Support/Steam/steamapps/workshop/content/333640/3718988020/Launch CavesOfQud (Rosetta).command[/code]

If you use another Steam library, look for the same [b]steamapps/workshop/content/333640/3718988020[/b] folder in that library.

[h2]If the Game Is Not Found[/h2]

[list]
[*]Select [b]CoQ.app[/b] when the wrapper displays a file picker.
[*]If Rosetta 2 is missing, follow the installation prompt shown by the wrapper.
[/list]

[h2]Optional Native Harmony 2.4.2 Patch[/h2]

Advanced users who need native Apple Silicon launch can use the separately documented Harmony 2.4.2 patch. Installation, restoration, compatibility, and game-update precautions are maintained in the [url=https://steamcommunity.com/workshop/filedetails/discussion/3718988020/572669660098532087/]Harmony 2.4.2 distribution thread[/url].

[h1]Reports and Contributions[/h1]

Report problems in the Workshop comments or on [url=https://github.com/ToaruPen/coq-japanese_stable]GitHub[/url]. Including Player.log is helpful, but remove sensitive information such as usernames and local paths before attaching it to a public Issue.

[h2]Player.log Locations[/h2]

[b]macOS[/b]
[code]~/Library/Logs/Freehold Games/CavesOfQud/Player.log[/code]

[b]Windows[/b]
[code]%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log[/code]

Contributions are welcome.

[h1]Important Notes[/h1]

[list]
[*]This is not an official Japanese localization by Freehold Games.
[*]Game updates may break some translated displays or patches.
[*]Some untranslated text, layout issues, and unnatural translations may remain.
[/list]

[h2]Known Issues[/h2]

[list]
[*]Procedurally generated content in general
[*]The character generation screen
[*]Misaligned color tags in the message log
[*]Other minor UI issues and unidentified gaps
[/list]
```

- [ ] **Step 2: 英語原稿だけで契約が前進することを確認する**

Run:

```bash
uv run pytest \
  scripts/tests/test_target_game_version_contract.py::test_workshop_descriptions_use_balanced_steam_bbcode \
  scripts/tests/test_build_workshop_upload.py::test_checked_in_default_description_renders_bbcode_with_literal_newlines \
  -q
```

Expected: VDF テストは PASS。英日同期テストは日本語原稿が未移行なので FAIL。

### Task 3: 日本語概要欄を同じ構造へ移行する

**Files:**
- Modify: `steam/workshop_description.ja.txt`

- [ ] **Step 1: 日本語原稿を次の内容へ置き換える**

```text
[h1]Caves of Qud Japanese Mod[/h1]

[b]Caves of Qud 1.0.5 対応[/b]

原文からの日本語訳のほぼすべてを生成 AI で作成し、その後に内容を確認しています。文脈の解釈違い、誤訳、不自然な表現が残っている可能性があるため、問題を見つけた場合はご報告ください。公式ローカリゼーションも始まっているため、このプロジェクトは将来終了する可能性があります。

[hr][/hr]

[h1]概要[/h1]

Caves of Qud を日本語で遊ぶための非公式ファン制作 Mod です。UI、会話、能力・変異説明、ポップアップ、メッセージログ、一部の自動生成テキストを日本語化し、日本語表示用の CJK フォントを同梱しています。

[h2]主な内容[/h2]

[list]
[*]会話テキストの日本語化
[*]メニュー、オプション、キャラクター作成、能力画面などの UI 日本語化
[*]ポップアップ、メッセージログ、ジャーナル、死亡表示などの日本語化
[*]一部の自動生成テキスト、歴史文、説明文の日本語化
[*]日本語表示用 CJK フォントの同梱
[/list]

[h1]導入方法[/h1]

[olist]
[*]この Workshop アイテムをサブスクライブします。
[*]ゲーム内の Mod Manager で [b]Caves of Qud Japanese Mod[/b] を有効にします。
[*]ゲームを再起動します。
[/olist]

[h2]既存セーブ[/h2]

既存セーブでも基本的に使用できます。導入前に重要なセーブデータをバックアップしてください。セーブ内にすでに保存されている生成済みテキストや、一部の動的テキストは英語のまま残る場合があります。

[h1]Apple Silicon Mac[/h1]

[b]推奨: 同梱の Rosetta 2 起動ツールから Caves of Qud を起動してください。[/b]

Apple Silicon でネイティブ起動すると、ゲーム同梱の Harmony が原因で、この Mod の UI パッチが適用されない場合があります。QudJP はゲーム本体の [b]0Harmony.dll[/b] を自動で同梱・上書きしません。

[h2]Rosetta 2 で起動する[/h2]

[olist]
[*]この Mod の Steam Workshop ダウンロードフォルダを開きます。
[*][b]Launch CavesOfQud (Rosetta).command[/b] をダブルクリックします。
[*]Steam からの通常起動で翻訳が効かない環境では、毎回この起動ツールを使用します。
[/olist]

[h2]起動ツールの既定場所[/h2]

[code]~/Library/Application Support/Steam/steamapps/workshop/content/333640/3718988020/Launch CavesOfQud (Rosetta).command[/code]

別の Steam ライブラリを使用している場合は、そのライブラリ内の [b]steamapps/workshop/content/333640/3718988020[/b] を探してください。

[h2]ゲームが見つからない場合[/h2]

[list]
[*]起動ツールが選択画面を表示したら、Steam ライブラリ内の [b]CoQ.app[/b] を選択します。
[*]Rosetta 2 が未導入の場合は、起動ツールが表示するインストール案内に従います。
[/list]

[h2]任意導入のネイティブ Harmony 2.4.2 パッチ[/h2]

Apple Silicon ネイティブ起動が必要な上級者向けに、Harmony 2.4.2 の任意導入手段を用意しています。導入、復元、互換性、ゲーム更新時の注意事項は [url=https://steamcommunity.com/workshop/filedetails/discussion/3718988020/572669660098532087/]Harmony 2.4.2 配布スレッド[/url]で管理しています。

[h1]問題報告・Contribution[/h1]

問題があれば Workshop のコメント欄または [url=https://github.com/ToaruPen/coq-japanese_stable]GitHub[/url] で報告してください。Player.log を添付すると調査に役立ちますが、公開 Issue に添付する前にユーザー名やローカルパスなどの機微情報を削除してください。

[h2]Player.log の場所[/h2]

[b]macOS[/b]
[code]~/Library/Logs/Freehold Games/CavesOfQud/Player.log[/code]

[b]Windows[/b]
[code]%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Player.log[/code]

Contribution も歓迎します。

[h1]注意事項[/h1]

[list]
[*]これは Freehold Games 公式の日本語化ではありません。
[*]ゲーム本体の更新により、一部の表示やパッチが壊れる可能性があります。
[*]未翻訳、表示崩れ、不自然な訳が残っている場合があります。
[/list]

[h2]既知の問題[/h2]

[list]
[*]プロシージャル生成テキスト全般
[*]キャラクター生成画面
[*]メッセージログの色タグのずれ
[*]その他の細かな UI 表示や未確認箇所
[/list]
```

- [ ] **Step 2: Green を確認する**

Run:

```bash
uv run pytest \
  scripts/tests/test_target_game_version_contract.py::test_workshop_descriptions_use_balanced_steam_bbcode \
  scripts/tests/test_build_workshop_upload.py::test_checked_in_default_description_renders_bbcode_with_literal_newlines \
  -q
```

Expected: `2 passed`。

### Task 4: 回帰検証と公開予定本文の確認

**Files:**
- Verify: `steam/workshop_description.en.txt`
- Verify: `steam/workshop_description.ja.txt`
- Verify: `steam/workshop_metadata.json`
- Verify: `scripts/tests/test_target_game_version_contract.py`
- Verify: `scripts/tests/test_build_workshop_upload.py`

- [ ] **Step 1: 関連テストを実行する**

Run:

```bash
uv run pytest \
  scripts/tests/test_target_game_version_contract.py \
  scripts/tests/test_build_workshop_upload.py \
  -q
```

Expected: 全テスト PASS、失敗 0。

- [ ] **Step 2: Python の静的検査とフルスイートを実行する**

Run:

```bash
just python-check
just python-test
```

Expected: Ruff と型・形式検査が PASS。pytest フルスイートが失敗 0。

- [ ] **Step 3: 差分・秘密情報・対象文字列を確認する**

Run:

```bash
git diff --check
npx secretlint .
rg -n 'mprotect returned EACCES|Harmony-Fat|arch -x86_64|`|^\s*\*\s' \
  steam/workshop_description.en.txt steam/workshop_description.ja.txt
rg -n '\[h1\]|\[h2\]|\[list\]|\[olist\]|\[code\]|\[url=' \
  steam/workshop_description.en.txt steam/workshop_description.ja.txt
```

Expected: `git diff --check` と secretlint は成功。削除対象の高度な手順と Markdown 記法は 0 件。Steam BBCode は英日両方の意図した箇所だけに存在。

- [ ] **Step 4: 登録予定本文を利用者へ提示する**

`git diff -- steam/workshop_description.en.txt steam/workshop_description.ja.txt` と、両ファイルの最終本文を提示する。Steam への保存はまだ行わない。

### Task 5: 明示許可後に Steam へ保存して確認する

**Files:**
- External update: Steam Workshop item `3718988020` English description
- External update: Steam Workshop item `3718988020` Japanese description

- [ ] **Step 1: 現在の会話で Steam 保存許可を確認する**

英語・日本語の登録予定本文を提示したうえで、公開概要欄を変更してよいか明示的に尋ねる。許可がない場合はここで停止する。

- [ ] **Step 2: 英語概要欄を保存する**

Steam Workshop のタイトル・説明編集画面で言語を English にし、`steam/workshop_description.en.txt` の内容だけを説明欄へ設定して保存する。タイトル、公開範囲、タグ、プレビュー画像は変更しない。

- [ ] **Step 3: 日本語概要欄を保存する**

同じ編集画面で言語を日本語にし、`steam/workshop_description.ja.txt` の内容だけを説明欄へ設定して保存する。タイトル、公開範囲、タグ、プレビュー画像は変更しない。

- [ ] **Step 4: 非認証の公開ページを検証する**

Run:

```bash
curl -fsSL 'https://steamcommunity.com/sharedfiles/filedetails/?id=3718988020&l=english' |
  rg 'Overview|Compatible with Caves of Qud 1\.0\.5|Rosetta 2 Launch Steps|Harmony 2\.4\.2 distribution thread'

curl -fsSL 'https://steamcommunity.com/sharedfiles/filedetails/?id=3718988020&l=japanese' |
  rg '概要|Caves of Qud 1\.0\.5 対応|Rosetta 2 で起動する|Harmony 2\.4\.2 配布スレッド'
```

Expected: 英語・日本語それぞれの見出し、対象バージョン、Rosetta 手順、Harmony スレッドへのリンクが公開 HTML から取得できる。

- [ ] **Step 5: 最終状態を報告する**

Workshop item ID `3718988020`、英日保存結果、公開ページ確認、実行したテスト、未コミット差分を報告する。コミットや PR はユーザーから明示依頼があるまで作成しない。
