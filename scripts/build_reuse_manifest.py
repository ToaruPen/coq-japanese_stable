"""Build a reuse manifest for LibraryCorpus sentences."""

from __future__ import annotations

import argparse
import html
import json
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

_DEFAULT_CORPUS_RAW = (
    Path.home()
    / "Library"
    / "Application Support"
    / "Steam"
    / "steamapps"
    / "common"
    / "Caves of Qud"
    / "CoQ.app"
    / "Contents"
    / "Resources"
    / "Data"
    / "StreamingAssets"
    / "Base"
    / "LibraryCorpus.json.raw.txt"
)
_DEFAULT_BOOKS_EN = _DEFAULT_CORPUS_RAW.with_name("Books.xml")
_DEFAULT_BOOKS_JA = Path("Mods/QudJP/Localization/Books.jp.xml")
_DEFAULT_CORPUS_DIR = Path("Mods/QudJP/Localization/Corpus")
_DEFAULT_OUTPUT = Path("scripts/reuse_manifest.json")

_BOOK_BLOCK_RE = re.compile(r"<book\b([^>]*)>(.*?)</book>", flags=re.DOTALL)
_PAGE_RE = re.compile(r"<page\b[^>]*>(.*?)</page>", flags=re.DOTALL)
_ATTRIBUTE_RE = re.compile(r"""\b([A-Za-z_:][\w:.-]*)=(["'])(.*?)\2""", flags=re.DOTALL)
_TAG_RE = re.compile(r"<[^>]+>")
_CDATA_RE = re.compile(r"<!\[CDATA\[(.*?)\]\]>", flags=re.DOTALL)
_WHITESPACE_RE = re.compile(r"\s+")
_ALNUM_RE = re.compile(r"[^0-9a-z]+")
_LINE_END_RE = re.compile(r"[.!?]+[\"')\\]]*$")
_ABBREVIATIONS = {
    "dr.",
    "mr.",
    "mrs.",
    "ms.",
    "prof.",
    "st.",
    "sr.",
    "jr.",
    "vs.",
    "etc.",
    "e.g.",
    "i.e.",
    "no.",
    "vol.",
}
_SOFT_WRAP_THRESHOLD = 60
_VERSE_BREAK_THRESHOLD = 55


@dataclass(frozen=True)
class _Line:
    number: int
    text: str


@dataclass(frozen=True)
class _Paragraph:
    start_line: int
    lines: list[_Line]

    @property
    def text(self) -> str:
        """Return the paragraph as normalized inline text."""
        return " ".join(line.text.strip() for line in self.lines if line.text.strip()).strip()


@dataclass(frozen=True)
class _CorpusSentence:
    identifier: int
    text: str


@dataclass(frozen=True)
class _ExcerptSpec:
    filename: str
    anchor: str


@dataclass(frozen=True)
class _ReuseContainer:
    english_key: str
    english_text: str
    japanese_text: str


_EXCERPT_SPECS = (
    _ExcerptSpec(
        filename="Machinery-of-the-Universe-excerpt.jp.txt",
        anchor=(
            "For mathematical purposes, it has sometimes been convenient to treat a problem as if one body "
            "could act upon another without any physical medium between them; but such a conception has no "
            "degree of rationality, and I know of no one who believes in it as a fact."
        ),
    ),
    _ExcerptSpec(
        filename="Meteorology-Weather-Explained-excerpt.jp.txt",
        anchor=(
            "When dealing with hoar-frost, which is just frozen dew, we shall find visible evidence of the "
            "rising of dew from the ground."
        ),
    ),
    _ExcerptSpec(
        filename="Thought-Forms-excerpt.jp.txt",
        anchor=(
            "We have often heard it said that thoughts are things, and there are many among us who are "
            "persuaded of the truth of this statement."
        ),
    ),
)


def _find_project_root() -> Path:
    """Locate the project root by traversing upward to ``pyproject.toml``."""
    current = Path(__file__).resolve().parent
    while current != current.parent:
        if (current / "pyproject.toml").exists():
            return current
        current = current.parent
    msg = "Could not find project root (no pyproject.toml found)"
    raise FileNotFoundError(msg)


def _validate_file(path: Path, *, label: str) -> None:
    """Raise when a required input file does not exist."""
    if path.is_file():
        return
    msg = f"{label} file not found: {path}"
    raise FileNotFoundError(msg)


def _validate_directory(path: Path, *, label: str) -> None:
    """Raise when a required input directory does not exist."""
    if path.is_dir():
        return
    msg = f"{label} directory not found: {path}"
    raise FileNotFoundError(msg)


def _read_lines(path: Path) -> list[_Line]:
    """Read a text file into numbered lines."""
    return [
        _Line(number=index, text=text)
        for index, text in enumerate(path.read_text(encoding="utf-8").splitlines(), 1)
    ]


def _group_paragraphs(lines: list[_Line]) -> list[_Paragraph]:
    """Group contiguous nonblank lines into paragraphs."""
    paragraphs: list[_Paragraph] = []
    current: list[_Line] = []
    for line in lines:
        if line.text.strip():
            current.append(line)
            continue
        if current:
            paragraphs.append(_Paragraph(start_line=current[0].number, lines=current))
            current = []
    if current:
        paragraphs.append(_Paragraph(start_line=current[0].number, lines=current))
    return paragraphs


def _normalize_text(text: str) -> str:
    """Collapse text for matching while preserving human readability."""
    normalized = unicodedata.normalize("NFKC", text)
    normalized = normalized.replace("\u00a0", " ")
    normalized = normalized.replace("\r", "\n")
    normalized = normalized.replace("\u201c", '"').replace("\u201d", '"')
    normalized = normalized.replace("\u2018", "'").replace("\u2019", "'")
    normalized = normalized.replace("\u2014", "-").replace("\u2013", "-")
    return _WHITESPACE_RE.sub(" ", normalized).strip()


def _match_key(text: str) -> str:
    """Return a punctuation-insensitive English matching key."""
    normalized = _normalize_text(text).casefold()
    return _ALNUM_RE.sub("", normalized)


def _looks_like_menu_line(text: str) -> bool:
    """Return whether the line looks like an in-book option entry."""
    stripped = text.strip()
    return stripped.startswith(("[[", "~"))


def _should_preserve_line_break(previous: str, current: str) -> bool:
    """Keep line boundaries for verse-like or menu-like text."""
    if _looks_like_menu_line(previous) or _looks_like_menu_line(current):
        return True
    if _LINE_END_RE.search(previous):
        return True
    if previous.rstrip().endswith((",", ";", ":")) and current[:1].isupper():
        return len(previous.strip()) < _SOFT_WRAP_THRESHOLD
    if current[:1].islower():
        return False
    return len(previous.strip()) < _VERSE_BREAK_THRESHOLD


def _build_block_text(paragraph: _Paragraph) -> tuple[str, list[tuple[int, int, int]]]:
    """Return paragraph text with tracked offsets back to source lines."""
    pieces: list[str] = []
    spans: list[tuple[int, int, int]] = []
    total_length = 0
    previous_text = ""
    for index, line in enumerate(paragraph.lines):
        stripped = line.text.strip()
        if not stripped:
            continue
        if index > 0:
            separator = "\n" if _should_preserve_line_break(previous_text, stripped) else " "
            pieces.append(separator)
            total_length += len(separator)
        start = total_length
        pieces.append(stripped)
        total_length += len(stripped)
        spans.append((start, total_length, line.number))
        previous_text = stripped
    return "".join(pieces), spans


def _last_token(text: str) -> str:
    """Return the final word-like token in lowercase."""
    match = re.search(r"([A-Za-z]\.|[A-Za-z]+[.]?)$", text)
    return match.group(1).casefold() if match else ""


def _next_nonspace_index(text: str, start: int) -> int | None:
    """Return the next non-whitespace index after ``start``."""
    for index in range(start, len(text)):
        if not text[index].isspace():
            return index
    return None


def _should_split_after_punctuation(text: str, start: int, index: int) -> bool:
    """Return whether punctuation at ``index`` ends a sentence."""
    if text[index] not in ".!?":
        return False

    window = text[start : index + 1]
    if _last_token(window) in _ABBREVIATIONS:
        return False

    if index >= 1 and text[index] == "." and text[index - 1].isupper():
        next_index = _next_nonspace_index(text, index + 1)
        if next_index is not None and text[next_index].isupper():
            return False

    next_index = _next_nonspace_index(text, index + 1)
    if next_index is None:
        return True
    next_char = text[next_index]
    return next_char.isupper() or next_char.isdigit() or next_char in "\"'([{" or next_char == "\n"


def _find_line_number(spans: list[tuple[int, int, int]], offset: int) -> int:
    """Resolve a character offset back to its source line number."""
    for start, end, line_number in spans:
        if start <= offset < end:
            return line_number
    return spans[-1][2]


def _split_english_text_with_lines(paragraph: _Paragraph) -> list[_CorpusSentence]:
    """Split a paragraph into corpus sentences while retaining line identifiers."""
    text, spans = _build_block_text(paragraph)
    sentences: list[_CorpusSentence] = []
    segment_start = 0
    index = 0

    while index < len(text):
        if text[index] == "\n":
            segment = text[segment_start:index].strip()
            if segment:
                start_offset = next(pos for pos in range(segment_start, index) if not text[pos].isspace())
                line_number = _find_line_number(spans, start_offset)
                sentences.append(_CorpusSentence(identifier=line_number, text=segment))
            segment_start = index + 1
            index += 1
            continue

        if _should_split_after_punctuation(text, segment_start, index):
            end = index + 1
            while end < len(text) and text[end] in "\"')]}":
                end += 1
            segment = text[segment_start:end].strip()
            if segment:
                start_offset = next(pos for pos in range(segment_start, end) if not text[pos].isspace())
                line_number = _find_line_number(spans, start_offset)
                sentences.append(_CorpusSentence(identifier=line_number, text=segment))
            segment_start = end
            index = end
            continue

        index += 1

    tail = text[segment_start:].strip()
    if tail:
        start_offset = next(pos for pos in range(segment_start, len(text)) if not text[pos].isspace())
        line_number = _find_line_number(spans, start_offset)
        sentences.append(_CorpusSentence(identifier=line_number, text=tail))

    return sentences


def _parse_corpus_sentences(path: Path) -> tuple[list[_CorpusSentence], list[_Paragraph]]:
    """Parse raw corpus text into sentence entries and paragraphs."""
    lines = _read_lines(path)
    paragraphs = _group_paragraphs(lines)
    sentences: list[_CorpusSentence] = []
    for paragraph in paragraphs:
        sentences.extend(_split_english_text_with_lines(paragraph))
    return sentences, paragraphs


def _page_text(page: ET.Element) -> str:
    """Extract normalized text content from an XML page element."""
    return _normalize_text("".join(page.itertext()))


def _parse_books_xml(path: Path) -> dict[str, list[str]]:
    """Parse Books XML with a regex fallback for malformed files."""
    try:
        root = ET.parse(path).getroot()  # noqa: S314 -- local project/game XML input only
    except ET.ParseError as exc:
        print(  # noqa: T201
            f"WARNING: Books XML parse failed for {path}, falling back to regex extraction: {exc}",
            file=sys.stderr,
        )
        return _parse_books_xml_regex(path)

    books: dict[str, list[str]] = {}
    for book in root.findall("book"):
        book_id = book.attrib.get("ID")
        if not book_id:
            continue
        pages = [_page_text(page) for page in book.findall("page")]
        books[book_id] = [page for page in pages if page]
    return books


def _parse_books_xml_regex(path: Path) -> dict[str, list[str]]:
    """Parse Books XML-like content with regex."""
    raw_text = path.read_text(encoding="utf-8")
    books: dict[str, list[str]] = {}
    for attributes_text, book_body in _BOOK_BLOCK_RE.findall(raw_text):
        attributes = {name: value for name, _quote, value in _ATTRIBUTE_RE.findall(attributes_text)}
        book_id = attributes.get("ID")
        if not book_id:
            continue
        pages = [_clean_xml_fragment(content) for content in _PAGE_RE.findall(book_body)]
        books[book_id] = [page for page in pages if page]
    return books


def _clean_xml_fragment(content: str) -> str:
    """Strip CDATA, tags, and entities from a page body."""
    text = _CDATA_RE.sub(r"\1", content)
    text = _TAG_RE.sub("", text)
    text = html.unescape(text)
    return _normalize_text(text)


def _split_nonblank_lines(text: str) -> list[str]:
    """Return nonblank normalized lines."""
    return [_normalize_text(line) for line in text.splitlines() if line.strip()]


def _split_paragraph_text(text: str) -> list[str]:
    """Return nonblank normalized paragraphs."""
    return [_normalize_text(part) for part in re.split(r"\n\s*\n", text) if part.strip()]


def _split_english_text(text: str) -> list[str]:
    """Split English text into sentence-like reusable units."""
    normalized = _normalize_text(text)
    if not normalized:
        return []

    lines = _split_nonblank_lines(text)
    if len(lines) > 1 and all(_LINE_END_RE.search(line) for line in lines):
        return lines

    units: list[str] = []
    start = 0
    index = 0
    while index < len(normalized):
        if _should_split_after_punctuation(normalized, start, index):
            end = index + 1
            while end < len(normalized) and normalized[end] in "\"')]}":
                end += 1
            segment = normalized[start:end].strip()
            if segment:
                units.append(segment)
            start = end
            index = end
            continue
        index += 1

    tail = normalized[start:].strip()
    if tail:
        units.append(tail)
    return units


def _split_japanese_text(text: str) -> list[str]:
    """Split Japanese text into sentence-like reusable units."""
    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    units: list[str] = []
    buffer: list[str] = []
    for character in normalized:
        if character == "\n":
            segment = _normalize_text("".join(buffer))
            if segment:
                units.append(segment)
            buffer = []
            continue
        buffer.append(character)
        if character in "\u3002\uff01\uff1f!?":
            segment = _normalize_text("".join(buffer))
            if segment:
                units.append(segment)
            buffer = []
    tail = _normalize_text("".join(buffer))
    if tail:
        units.append(tail)
    return units


def _add_mapping(mapping: dict[str, str], english_text: str, japanese_text: str) -> None:
    """Register a normalized English to Japanese mapping."""
    key = _match_key(english_text)
    if not key:
        return
    mapping.setdefault(key, _normalize_text(japanese_text))


def _add_parallel_units(mapping: dict[str, str], english_units: list[str], japanese_units: list[str]) -> None:
    """Add aligned unit pairs when their counts match."""
    if len(english_units) != len(japanese_units):
        return
    for english_unit, japanese_unit in zip(english_units, japanese_units, strict=True):
        _add_mapping(mapping, english_unit, japanese_unit)


def _validate_page_alignment(book_id: str, english_pages: list[str], japanese_pages: list[str]) -> None:
    """Fail fast when translated book page counts drift out of alignment."""
    if len(english_pages) == len(japanese_pages):
        return
    msg = f"Books page count mismatch for '{book_id}': en={len(english_pages)} ja={len(japanese_pages)}"
    raise ValueError(msg)


def _build_books_map(english_books: dict[str, list[str]], japanese_books: dict[str, list[str]]) -> dict[str, str]:
    """Build a reuse map from translated book pages."""
    mapping: dict[str, str] = {}
    for book_id, english_pages in english_books.items():
        japanese_pages = japanese_books.get(book_id)
        if not japanese_pages:
            continue
        _validate_page_alignment(book_id, english_pages, japanese_pages)
        for english_page, japanese_page in zip(english_pages, japanese_pages, strict=True):
            _add_mapping(mapping, english_page, japanese_page)
            _add_parallel_units(mapping, _split_nonblank_lines(english_page), _split_nonblank_lines(japanese_page))
            _add_parallel_units(mapping, _split_paragraph_text(english_page), _split_paragraph_text(japanese_page))
            _add_parallel_units(mapping, _split_english_text(english_page), _split_japanese_text(japanese_page))
    return mapping


def _build_book_containers(
    english_books: dict[str, list[str]],
    japanese_books: dict[str, list[str]],
) -> list[_ReuseContainer]:
    """Build page-level containment fallbacks for translated books."""
    containers: list[_ReuseContainer] = []
    for book_id, english_pages in english_books.items():
        japanese_pages = japanese_books.get(book_id)
        if not japanese_pages:
            continue
        _validate_page_alignment(book_id, english_pages, japanese_pages)
        for english_page, japanese_page in zip(english_pages, japanese_pages, strict=True):
            english_text = _normalize_text(english_page)
            english_key = _match_key(english_page)
            if not english_key:
                continue
            containers.append(
                _ReuseContainer(
                    english_key=english_key,
                    english_text=english_text,
                    japanese_text=_normalize_text(japanese_page),
                ),
            )
    return containers


def _find_paragraph_index(paragraphs: list[_Paragraph], anchor: str) -> int:
    """Locate the paragraph that contains an anchor sentence."""
    anchor_key = _match_key(anchor)
    for index, paragraph in enumerate(paragraphs):
        if anchor_key in _match_key(paragraph.text):
            return index
    msg = f"Could not locate excerpt anchor in corpus: {anchor}"
    raise ValueError(msg)


def _extract_excerpt_english_paragraphs(
    paragraphs: list[_Paragraph],
    *,
    anchor: str,
    count: int,
) -> list[str]:
    """Extract the paragraph slice corresponding to an existing excerpt."""
    start_index = _find_paragraph_index(paragraphs, anchor)
    selected = paragraphs[start_index : start_index + count]
    if len(selected) != count:
        msg = f"Corpus ended early while extracting excerpt at anchor: {anchor}"
        raise ValueError(msg)

    extracted: list[str] = []
    for index, paragraph in enumerate(selected):
        text = paragraph.text
        if index == 0:
            anchor_position = _normalize_text(text).find(_normalize_text(anchor))
            if anchor_position < 0:
                msg = f"Anchor found in paragraph index but not in text: {anchor}"
                raise ValueError(msg)
            text = _normalize_text(text)[anchor_position:]
        extracted.append(_normalize_text(text))
    return extracted


def _build_excerpts_map(paragraphs: list[_Paragraph], corpus_dir: Path) -> dict[str, str]:
    """Build a reuse map from existing translated excerpt files."""
    mapping: dict[str, str] = {}
    for spec in _EXCERPT_SPECS:
        excerpt_path = corpus_dir / spec.filename
        _validate_file(excerpt_path, label="Excerpt")
        japanese_text = excerpt_path.read_text(encoding="utf-8")
        japanese_paragraphs = _split_paragraph_text(japanese_text)
        english_paragraphs = _extract_excerpt_english_paragraphs(
            paragraphs,
            anchor=spec.anchor,
            count=len(japanese_paragraphs),
        )

        for english_paragraph, japanese_paragraph in zip(english_paragraphs, japanese_paragraphs, strict=True):
            _add_mapping(mapping, english_paragraph, japanese_paragraph)
            _add_parallel_units(
                mapping,
                _split_english_text(english_paragraph),
                _split_japanese_text(japanese_paragraph),
            )
    return mapping


def _build_excerpt_containers(
    paragraphs: list[_Paragraph],
    corpus_dir: Path,
) -> list[_ReuseContainer]:
    """Build paragraph-level containment fallbacks for translated excerpts."""
    containers: list[_ReuseContainer] = []
    for spec in _EXCERPT_SPECS:
        excerpt_path = corpus_dir / spec.filename
        _validate_file(excerpt_path, label="Excerpt")
        japanese_text = excerpt_path.read_text(encoding="utf-8")
        japanese_paragraphs = _split_paragraph_text(japanese_text)
        english_paragraphs = _extract_excerpt_english_paragraphs(
            paragraphs,
            anchor=spec.anchor,
            count=len(japanese_paragraphs),
        )
        for english_paragraph, japanese_paragraph in zip(english_paragraphs, japanese_paragraphs, strict=True):
            english_key = _match_key(english_paragraph)
            if not english_key:
                continue
            containers.append(
                _ReuseContainer(
                    english_key=english_key,
                    english_text=_normalize_text(english_paragraph),
                    japanese_text=_normalize_text(japanese_paragraph),
                ),
            )
    return containers


def _lookup_sentence_in_container(key: str, container: _ReuseContainer) -> tuple[int, str] | None:
    """Return a sentence-level fallback from a container when alignment is unambiguous."""
    english_units = _split_english_text(container.english_text)
    japanese_units = _split_japanese_text(container.japanese_text)
    if len(english_units) != len(japanese_units):
        return None

    matches: list[tuple[int, str]] = []
    for english_unit, japanese_unit in zip(english_units, japanese_units, strict=True):
        english_unit_key = _match_key(english_unit)
        if key not in english_unit_key:
            continue
        matches.append((len(english_unit_key), _normalize_text(japanese_unit)))

    if len(matches) != 1:
        return None
    return matches[0]


def _lookup_container_text(key: str, containers: list[_ReuseContainer]) -> str | None:
    """Return the shortest containing Japanese sentence for a normalized English key."""
    best_match: tuple[int, str] | None = None
    for container in containers:
        if key not in container.english_key:
            continue
        sentence_match = _lookup_sentence_in_container(key, container)
        if sentence_match is None:
            continue
        if best_match is None or sentence_match[0] < best_match[0]:
            best_match = sentence_match
    return None if best_match is None else best_match[1]


def _resolve_cli_path(path: Path, project_root: Path) -> Path:
    """Resolve relative CLI paths against the repository root."""
    if path.is_absolute():
        return path.resolve()
    return (project_root / path).resolve()


def _build_manifest(
    sentences: list[_CorpusSentence],
    books_map: dict[str, str],
    book_containers: list[_ReuseContainer],
    excerpts_map: dict[str, str],
    excerpt_containers: list[_ReuseContainer],
) -> tuple[list[dict[str, object]], dict[str, int]]:
    """Create manifest entries and summary counts."""
    manifest: list[dict[str, object]] = []
    summary = {"total": 0, "reused/books": 0, "reused/excerpt": 0, "llm": 0}

    for sentence in sentences:
        key = _match_key(sentence.text)
        if key in books_map:
            source = "reused/books"
            japanese_text: str | None = books_map[key]
        elif book_match := _lookup_container_text(key, book_containers):
            source = "reused/books"
            japanese_text = book_match
        elif key in excerpts_map:
            source = "reused/excerpt"
            japanese_text = excerpts_map[key]
        elif excerpt_match := _lookup_container_text(key, excerpt_containers):
            source = "reused/excerpt"
            japanese_text = excerpt_match
        else:
            source = "llm"
            japanese_text = None

        manifest.append(
            {
                "id": sentence.identifier,
                "en": _normalize_text(sentence.text),
                "source": source,
                "ja": japanese_text,
            },
        )
        summary["total"] += 1
        summary[source] += 1

    return manifest, summary


def main(argv: list[str] | None = None) -> int:
    """Build and write the reuse manifest."""
    project_root = _find_project_root()

    parser = argparse.ArgumentParser(
        description="Build a reuse manifest for LibraryCorpus sentences",
    )
    parser.add_argument(
        "--corpus-raw",
        type=Path,
        default=_DEFAULT_CORPUS_RAW,
        help=f"Path to raw corpus (default: {_DEFAULT_CORPUS_RAW})",
    )
    parser.add_argument(
        "--books-en",
        type=Path,
        default=_DEFAULT_BOOKS_EN,
        help=f"Path to English Books.xml (default: {_DEFAULT_BOOKS_EN})",
    )
    parser.add_argument(
        "--books-ja",
        type=Path,
        default=_DEFAULT_BOOKS_JA,
        help=f"Path to Japanese Books.jp.xml (default: {_DEFAULT_BOOKS_JA})",
    )
    parser.add_argument(
        "--corpus-dir",
        type=Path,
        default=_DEFAULT_CORPUS_DIR,
        help=f"Path to translated excerpt directory (default: {_DEFAULT_CORPUS_DIR})",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=_DEFAULT_OUTPUT,
        help=f"Output path for manifest JSON (default: {_DEFAULT_OUTPUT})",
    )
    args = parser.parse_args(argv)
    for field in ("corpus_raw", "books_en", "books_ja", "corpus_dir", "output"):
        setattr(args, field, _resolve_cli_path(getattr(args, field), project_root))

    try:
        _validate_file(args.corpus_raw, label="Raw corpus")
        _validate_file(args.books_en, label="English Books XML")
        _validate_file(args.books_ja, label="Japanese Books XML")
        _validate_directory(args.corpus_dir, label="Corpus")

        sentences, paragraphs = _parse_corpus_sentences(args.corpus_raw)
        english_books = _parse_books_xml(args.books_en)
        japanese_books = _parse_books_xml(args.books_ja)
        books_map = _build_books_map(english_books, japanese_books)
        book_containers = _build_book_containers(english_books, japanese_books)
        excerpts_map = _build_excerpts_map(paragraphs, args.corpus_dir)
        excerpt_containers = _build_excerpt_containers(paragraphs, args.corpus_dir)
        manifest, summary = _build_manifest(
            sentences,
            books_map,
            book_containers,
            excerpts_map,
            excerpt_containers,
        )
        output_json = json.dumps(manifest, ensure_ascii=False, indent=2)
    except (ET.ParseError, FileNotFoundError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)  # noqa: T201
        return 1

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output_json, encoding="utf-8")

    print(f"Total sentences: {summary['total']}")  # noqa: T201
    print(f"Reused from books: {summary['reused/books']}")  # noqa: T201
    print(f"Reused from excerpts: {summary['reused/excerpt']}")  # noqa: T201
    print(f"Needs LLM translation: {summary['llm']}")  # noqa: T201
    print(f"Manifest written to {args.output}")  # noqa: T201
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
