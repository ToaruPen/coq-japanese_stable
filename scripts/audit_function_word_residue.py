"""Audit QudJP routes and tests for English function-word residue risks."""

from __future__ import annotations

import json
import re
import sys
from argparse import ArgumentParser
from collections import Counter
from pathlib import Path
from typing import Final, Literal, TypedDict

AuditDomain = Literal["source", "test"]
OutputFormat = Literal["text", "json"]


class AuditEntry(TypedDict):
    """One function-word residue audit hit."""

    domain: AuditDomain
    category: str
    classification: str
    risk: str
    owner_route_hint: str
    path: str
    line: int
    surface: str
    excerpt: str
    reason: str
    recommendation: str


VISIBLE_SURFACE_RE: Final = re.compile(
    r"\b(?:AddPlayerMessage|EmitMessage|Popup\.Show|Popup\.ShowFail|Popup\.ShowBlock|"
    r"MessageFrame|SetText|GetDisplayName|DisplayName|UITextSkin|JournalAPI|AddTag)\b"
)
DOES_RE: Final = re.compile(r"(?:^|[.\s])Does\s*\(")
POSS_RE: Final = re.compile(r"(?:^|[.\s])(?:poss|Poss)\s*\(")
GENERATED_ARTICLE_RE: Final = re.compile(r"\.(?:an|the|t)\s*(?:\(|\+|\b)|\.\s*a\s*(?:\+|\b)")
MAKE_POSSESSIVE_RE: Final = re.compile(r"\bGrammar\.MakePossessive\s*\(")
DISPLAY_NAME_STATE_RE: Final = re.compile(
    r"\[(?:swimming|sitting(?: on [^\]]+)?|empty|broken|cracked|rusted|asleep|prone)\]",
    re.IGNORECASE,
)
STRING_LITERAL_RE: Final = re.compile(r'"""(?:[^"]|"(?!"")|""(?!"))*"""|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"')
JAPANESE_RE: Final = re.compile(r"[\u3040-\u30ff\u3400-\u9fff]")
FUNCTION_WORD_RE: Final = re.compile(
    r"(?<![A-Za-z])(?:a|an|the|your|its|their|his|her|to|from|with|of|by)(?![A-Za-z])",
    re.IGNORECASE,
)
DIRECTION_PHRASE_RE: Final = re.compile(
    r"(?<![A-Za-z])(?:to|from|toward|towards) the "
    r"(?:north|south|east|west|northeast|northwest|southeast|southwest)(?![A-Za-z])",
    re.IGNORECASE,
)
ARTICLE_BEFORE_JAPANESE_PARTICLE_RE: Final = re.compile(
    r"\b(?:The|the|A|a|An|an) [^\"。、]*[\u3040-\u30ff\u3400-\u9fff]?[^\"。、]*[をにへのがは]"
)
HOTKEY_OR_UI_TOKEN_RE: Final = re.compile(
    r"(?:Ctrl\+[A-Z]|Shift\+[A-Z]|Alt\+[A-Z]|\{\{W\|(?:[A-Z]|RT|LT|\[[a-z]\])\}\}|"
    r"\[[A-Z]\]|\[[a-z]\]|<\{\{\|[A-Z]+\}\}>|\b[aA]-z\b|\b\d+d\d+\b|\\u00(?:01a|03))"
)
PRONOUN_TOKEN_RE: Final = re.compile(
    r"(?:@\w+|(?<![A-Za-z])he/him/his(?![A-Za-z])|(?<![A-Za-z])they/them/their(?![A-Za-z]))",
    re.IGNORECASE,
)
PROPER_NOUN_RE: Final = re.compile(
    r"(?:Caves of Qud|What's Eating the Watervine\?|The Corpus Choliys|the Last Sultan|Codex of Leaves)"
)
QUOTED_ENGLISH_RE: Final = re.compile(r"[「『'\"]\s*[A-Z][^」』'\"]*\b(?:a|an|the|your|its|their)\b", re.IGNORECASE)
ARTICLE_BEFORE_PARTICLE_RE: Final = re.compile(r"(?<![A-Za-z])(?:The|the|A|a|An|an) [^。、]*[をにへのがは]")
POSSESSIVE_BEFORE_PARTICLE_RE: Final = re.compile(
    r"(?<![A-Za-z])(?:your|its|their|his|her) [^。、]*[をにへのがは]",
    re.IGNORECASE,
)
ENGLISH_SENTENCE_RE: Final = re.compile(
    r"^(?:\\u[0-9A-Fa-f]{4}|\\x[0-9A-Fa-f]{2})?(?:\{\{[A-Za-z]\|)?"
    r"(?:You|The|Do you|Are you|Opening|Your)\b"
)
SOURCE_SKIP_PARTS: Final = ("/bin/", "/obj/", "/.artifacts/")
TEST_EXPECTATION_RE: Final = re.compile(r"\b(?:Is\.EqualTo|Does\.Contain|TestCase)\b")


RECOMMENDATIONS: Final[dict[str, str]] = {
    "does_message_frame_composition": (
        "Normalize the Does/MessageFrame subject or extra slot before the sink; avoid downstream mixed-output "
        "patterns."
    ),
    "possessive_composition": (
        "Route the poss()/Poss() owner phrase through a possessive normalizer before Japanese particles are added."
    ),
    "grammar_make_possessive": "Strip leading English articles before Grammar.MakePossessive appends の.",
    "generated_article_call": (
        "Treat .a/.an/.the display-name output as generated noun text and translate or strip articles at the owner "
        "route."
    ),
    "visible_string_function_word": (
        "Review the player-visible producer route and prefer owner-route translation over a broad dictionary leaf."
    ),
    "direction_phrase": (
        "Translate the direction phrase structurally instead of preserving to/from/toward the <direction>."
    ),
    "bracketed_state_suffix": (
        "Translate display-name state suffixes through the display-name state dictionary or route-specific state "
        "normalizer."
    ),
    "test_expectation_function_word": (
        "Update the test expectation if the English function word is not intentionally preserved."
    ),
    "test_expectation_direction": (
        "Update the expectation to the Japanese direction phrase and add route coverage for the producer."
    ),
    "test_expectation_bracketed_state": (
        "Update the expectation after the display-name state route is fixed, or document an explicit allow rule."
    ),
}


def audit_source_tree(source_root: Path) -> list[AuditEntry]:
    """Scan decompiled C# source for high-risk function-word producer shapes."""
    entries: list[AuditEntry] = []
    if not source_root.exists():
        return entries

    for path in sorted(source_root.rglob("*.cs")):
        path_text = path.as_posix()
        if any(part in path_text for part in SOURCE_SKIP_PARTS):
            continue
        try:
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError:
            continue
        for line_number, line in enumerate(lines, start=1):
            entries.extend(_audit_source_line(source_root, path, line_number, line))
    return entries


def audit_test_tree(tests_root: Path) -> list[AuditEntry]:
    """Scan C# test expectations for likely stale English residue assertions."""
    entries: list[AuditEntry] = []
    if not tests_root.exists():
        return entries

    for path in sorted(tests_root.rglob("*.cs")):
        try:
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError:
            continue
        for line_number, line in enumerate(lines, start=1):
            entries.extend(_audit_test_line(tests_root, path, line_number, line))
    return entries


def format_text(entries: list[AuditEntry], *, limit: int) -> str:
    """Format audit entries for terminal review."""
    shown = entries[:limit] if limit > 0 else entries
    counts = Counter(entry["category"] for entry in entries)
    lines = [
        f"function-word residue audit: {len(entries)} hit(s)",
        "counts=" + ",".join(f"{key}:{counts[key]}" for key in sorted(counts)),
    ]
    for index, entry in enumerate(shown, start=1):
        lines.append(
            f"{index}. [{entry['domain']}/{entry['category']}] "
            f"{entry['path']}:{entry['line']} surface={entry['surface']} "
            f"class={entry['classification']} risk={entry['risk']}"
        )
        lines.append(f"   reason: {entry['reason']}")
        lines.append(f"   action: {entry['recommendation']}")
        lines.append(f"   owner-route: {entry['owner_route_hint']}")
        lines.append(f"   excerpt: {entry['excerpt']}")
    if limit > 0 and len(entries) > limit:
        lines.append(f"... {len(entries) - limit} more entries omitted")
    return "\n".join(lines)


def write_output(entries: list[AuditEntry], output: Path | None, output_format: OutputFormat, limit: int) -> None:
    """Write or print audit output."""
    if output_format == "json":
        payload = {
            "summary": {
                "total": len(entries),
                "by_category": dict(sorted(Counter(entry["category"] for entry in entries).items())),
                "by_classification": dict(sorted(Counter(entry["classification"] for entry in entries).items())),
                "by_domain": dict(sorted(Counter(entry["domain"] for entry in entries).items())),
                "by_risk": dict(sorted(Counter(entry["risk"] for entry in entries).items())),
            },
            "entries": entries,
        }
        rendered = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    else:
        rendered = format_text(entries, limit=limit) + "\n"

    if output is None:
        sys.stdout.write(rendered)
        return
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(rendered, encoding="utf-8")


def _audit_source_line(root: Path, path: Path, line_number: int, line: str) -> list[AuditEntry]:
    entries: list[AuditEntry] = []
    surface = _source_surface(line)
    if surface == "other":
        return entries

    if DOES_RE.search(line):
        entries.append(_source_entry(root, path, line_number, "does_message_frame_composition", surface, line))
    if POSS_RE.search(line):
        entries.append(_source_entry(root, path, line_number, "possessive_composition", surface, line))
    if MAKE_POSSESSIVE_RE.search(line):
        entries.append(_source_entry(root, path, line_number, "grammar_make_possessive", surface, line))
    if GENERATED_ARTICLE_RE.search(line):
        entries.append(_source_entry(root, path, line_number, "generated_article_call", surface, line))
    if DIRECTION_PHRASE_RE.search(line):
        entries.append(_source_entry(root, path, line_number, "direction_phrase", surface, line))
    if DISPLAY_NAME_STATE_RE.search(line) and ("DisplayName" in line or "AddTag" in line or "SetText" in line):
        entries.append(_source_entry(root, path, line_number, "bracketed_state_suffix", surface, line))
    if VISIBLE_SURFACE_RE.search(line) and _line_has_function_word_literal(line):
        entries.append(_source_entry(root, path, line_number, "visible_string_function_word", surface, line))

    return _dedupe_entries(entries)


def _audit_test_line(root: Path, path: Path, line_number: int, line: str) -> list[AuditEntry]:
    if not TEST_EXPECTATION_RE.search(line):
        return []

    literals = [_decode_csharp_string(match.group(0)) for match in STRING_LITERAL_RE.finditer(line)]
    if "TestCase" in line and literals:
        literals = literals[-1:]

    entries: list[AuditEntry] = []
    for literal in literals:
        if not _looks_like_localized_expectation(literal):
            continue
        if DIRECTION_PHRASE_RE.search(literal):
            entries.append(_test_entry(root, path, line_number, "test_expectation_direction", literal))
        if DISPLAY_NAME_STATE_RE.search(literal):
            entries.append(_test_entry(root, path, line_number, "test_expectation_bracketed_state", literal))
        if FUNCTION_WORD_RE.search(literal) or ARTICLE_BEFORE_JAPANESE_PARTICLE_RE.search(literal):
            entries.append(_test_entry(root, path, line_number, "test_expectation_function_word", literal))
    return _dedupe_entries(entries)


def _source_surface(line: str) -> str:
    surfaces = [
        token
        for token in (
            "AddPlayerMessage",
            "EmitMessage",
            "Popup.Show",
            "Popup.ShowFail",
            "Popup.ShowBlock",
            "MessageFrame",
            "SetText",
            "GetDisplayName",
            "DisplayName",
            "JournalAPI",
            "AddTag",
        )
        if token in line
    ]
    if DOES_RE.search(line):
        surfaces.append("Does")
    if POSS_RE.search(line):
        surfaces.append("poss")
    if MAKE_POSSESSIVE_RE.search(line):
        surfaces.append("Grammar.MakePossessive")
    return "+".join(dict.fromkeys(surfaces)) if surfaces else "other"


def _line_has_function_word_literal(line: str) -> bool:
    return any(
        FUNCTION_WORD_RE.search(_decode_csharp_string(match.group(0))) for match in STRING_LITERAL_RE.finditer(line)
    )


def _looks_like_localized_expectation(text: str) -> bool:
    if not JAPANESE_RE.search(text):
        return False
    return bool(FUNCTION_WORD_RE.search(text) or DIRECTION_PHRASE_RE.search(text) or DISPLAY_NAME_STATE_RE.search(text))


def _source_entry(root: Path, path: Path, line_number: int, category: str, surface: str, line: str) -> AuditEntry:
    display_path = _display_path(root, path)
    excerpt = _compact(line)
    classification, risk, owner_route_hint = _classify_entry("source", category, display_path, excerpt)
    return {
        "domain": "source",
        "category": category,
        "classification": classification,
        "risk": risk,
        "owner_route_hint": owner_route_hint,
        "path": display_path,
        "line": line_number,
        "surface": surface,
        "excerpt": excerpt,
        "reason": _reason(category),
        "recommendation": RECOMMENDATIONS[category],
    }


def _test_entry(root: Path, path: Path, line_number: int, category: str, literal: str) -> AuditEntry:
    display_path = _display_path(root, path)
    excerpt = _compact(literal)
    classification, risk, owner_route_hint = _classify_entry("test", category, display_path, excerpt)
    return {
        "domain": "test",
        "category": category,
        "classification": classification,
        "risk": risk,
        "owner_route_hint": owner_route_hint,
        "path": display_path,
        "line": line_number,
        "surface": "test_expectation",
        "excerpt": excerpt,
        "reason": _reason(category),
        "recommendation": RECOMMENDATIONS[category],
    }


def _classify_entry(domain: AuditDomain, category: str, path: str, excerpt: str) -> tuple[str, str, str]:
    """Classify whether a broad residue hit is likely actionable or intentional."""
    if domain == "source":
        return _classify_source_entry(category, path, excerpt)
    return _classify_test_entry(category, path, excerpt)


def _classify_source_entry(category: str, path: str, excerpt: str) -> tuple[str, str, str]:
    _ = path
    by_category = {
        "does_message_frame_composition": (
            "owner_route_candidate",
            "high",
            "DoesVerbRouteTranslator / MessageFrame owner slot",
        ),
        "generated_article_call": (
            "generated_display_name_candidate",
            "medium",
            "display-name owner route or generated noun capture",
        ),
        "bracketed_state_suffix": (
            "display_name_state_candidate",
            "medium",
            "GetDisplayName state suffix dictionary/normalizer",
        ),
        "direction_phrase": (
            "direction_phrase_candidate",
            "medium",
            "direction phrase structural translator",
        ),
    }
    if category in by_category:
        return by_category[category]
    if category in {"possessive_composition", "grammar_make_possessive"}:
        return ("owner_route_candidate", "high", "possessive owner normalizer before の composition")
    if "Popup.Show" in excerpt or "AddPlayerMessage" in excerpt or "EmitMessage" in excerpt:
        return ("visible_literal_route_candidate", "medium", "producer owner route before sink fallback")
    return ("static_visible_literal_shelf", "low", "needs runtime route proof before implementation")


def _classify_test_entry(category: str, path: str, excerpt: str) -> tuple[str, str, str]:
    if _is_intentional_english_test_expectation(path, excerpt):
        return ("intentional_english_allow", "intentional", "hotkey/token/proper noun/quoted English")

    if _is_pass_through_or_fixture_expectation(path, excerpt):
        return (
            "pass_through_or_fixture",
            "observation",
            "owner-absent, fallback, source-literal, or low-level preservation fixture",
        )

    by_category = {
        "test_expectation_direction": (
            "stale_test_owner_route_candidate",
            "high",
            "direction phrase owner route",
        ),
        "test_expectation_bracketed_state": (
            "stale_test_display_state_candidate",
            "medium",
            "display-name state owner route",
        ),
    }
    if category in by_category:
        return by_category[category]
    if ARTICLE_BEFORE_PARTICLE_RE.search(excerpt) or POSSESSIVE_BEFORE_PARTICLE_RE.search(excerpt):
        return ("stale_test_particle_boundary_candidate", "high", "translated capture or function-word normalizer")
    if ENGLISH_SENTENCE_RE.search(excerpt):
        return (
            "mixed_sentence_owner_route_candidate",
            "medium",
            "producer owner route or explicit pass-through fixture",
        )
    return ("localized_expectation_review", "medium", "manual route classification required")


def _is_intentional_english_test_expectation(path: str, excerpt: str) -> bool:
    if HOTKEY_OR_UI_TOKEN_RE.search(excerpt):
        return True
    if PRONOUN_TOKEN_RE.search(excerpt):
        return True
    if PROPER_NOUN_RE.search(excerpt):
        return True
    if QUOTED_ENGLISH_RE.search(excerpt):
        return True
    if "ColorCodePreserverTests.cs" in path:
        return True
    return "LegacyGamepadPrompt" in path or "UITextSkinTranslationPatchTests.cs" in path


def _is_pass_through_or_fixture_expectation(path: str, excerpt: str) -> bool:
    if "source='" in excerpt or "translated='" in excerpt:
        return True
    if any(
        marker in path
        for marker in (
            "PopupTranslationPatchTests.cs",
            "PopupShowTranslationPatchTests.cs",
            "MessageLogPatchTests.cs",
            "PhysicsEnterCellPassByTranslationPatchTests.cs",
            "DoesVerbFamilyTests.cs",
        )
    ):
        return ENGLISH_SENTENCE_RE.search(excerpt) is not None
    return "DirectMarked" in excerpt or "Unknown" in excerpt


def _reason(category: str) -> str:
    return {
        "does_message_frame_composition": "Does(...) composes a visible sentence outside the final sink.",
        "possessive_composition": (
            "poss()/Poss() can emit English possessive phrases before Japanese particles are added."
        ),
        "grammar_make_possessive": "Grammar.MakePossessive appends の and can preserve leading English articles.",
        "generated_article_call": ".a/.an/.the generated display names can introduce English articles into owner text.",
        "visible_string_function_word": "A player-visible string literal contains English function words.",
        "direction_phrase": "English directional preposition phrase appears in visible text construction.",
        "bracketed_state_suffix": "Bracketed display-name state can remain as an English suffix.",
        "test_expectation_function_word": "A localized-looking test expectation still contains English function words.",
        "test_expectation_direction": (
            "A localized-looking test expectation still contains an English direction phrase."
        ),
        "test_expectation_bracketed_state": (
            "A localized-looking test expectation still contains a bracketed English state suffix."
        ),
    }[category]


def _dedupe_entries(entries: list[AuditEntry]) -> list[AuditEntry]:
    seen: set[tuple[str, str, int, str]] = set()
    result: list[AuditEntry] = []
    for entry in entries:
        key = (entry["domain"], entry["path"], entry["line"], entry["category"])
        if key in seen:
            continue
        seen.add(key)
        result.append(entry)
    return result


def _decode_csharp_string(token: str) -> str:
    if token.startswith('"""') and token.endswith('"""'):
        return token[3:-3]
    if token.startswith('@"') and token.endswith('"'):
        return token[2:-1].replace('""', '"')

    value = token[1:-1]
    return (
        value.replace(r"\"", '"')
        .replace(r"\\", "\\")
        .replace(r"\n", "\n")
        .replace(r"\t", "\t")
        .replace(r"\r", "\r")
    )


def _compact(text: str, *, max_length: int = 180) -> str:
    compacted = " ".join(text.strip().split())
    if len(compacted) <= max_length:
        return compacted
    return compacted[: max_length - 1] + "…"


def _display_path(root: Path, path: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def build_parser() -> ArgumentParser:
    """Build the command-line parser."""
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, help="Decompiled C# source root to scan.")
    parser.add_argument("--tests-root", type=Path, help="QudJP C# tests root to scan.")
    parser.add_argument("--output", type=Path, help="Write output to this path instead of stdout.")
    parser.add_argument("--format", choices=("text", "json"), default="text")
    parser.add_argument("--limit", type=int, default=80, help="Text output row limit; 0 means no limit.")
    return parser


def main(argv: list[str] | None = None) -> int:
    """Run the function-word residue audit CLI."""
    parser = build_parser()
    args = parser.parse_args(sys.argv[1:] if argv is None else argv)

    if args.source_root is None and args.tests_root is None:
        parser.error("at least one of --source-root or --tests-root is required")

    entries: list[AuditEntry] = []
    if args.source_root is not None:
        entries.extend(audit_source_tree(args.source_root))
    if args.tests_root is not None:
        entries.extend(audit_test_tree(args.tests_root))

    entries.sort(key=lambda entry: (entry["domain"], entry["category"], entry["path"], entry["line"]))
    write_output(entries, args.output, args.format, args.limit)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
