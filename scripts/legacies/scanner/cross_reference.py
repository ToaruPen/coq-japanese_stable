"""Phase 1d scanner bridge: cross-reference classified sites with existing translations.

This legacy candidate-inventory step remains a bridge/view-only surface for
current static consumers, not the source of truth. The first-PR static
consumer boundary stays explicit: Roslyn pilot surfaces are pilot-aware,
scanner inventory consumers stay bridge-only, and runtime/triage work is
deferred.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, replace
from pathlib import Path, PurePosixPath

if __package__ in {None, ""}:
    _PROJECT_ROOT = Path(__file__).resolve().parents[3]
    _PROJECT_ROOT_STR = str(_PROJECT_ROOT)
    if _PROJECT_ROOT_STR not in sys.path:
        sys.path.insert(0, _PROJECT_ROOT_STR)

from scripts.legacies.scanner.inventory import (
    InventoryDraft,
    InventorySite,
    SiteStatus,
    SiteType,
    describe_first_pr_static_consumer_boundary,
    read_inventory_draft_json,
    write_candidate_inventory_json,
)

DEFAULT_INPUT_PATH = Path(".scanner-cache/inventory_draft.json")
DEFAULT_OUTPUT_PATH = Path("docs/candidate-inventory.json")
DEFAULT_SOURCE_ROOT = Path("~/dev/coq-decompiled_stable")
_IDENTIFIER_ATTRIBUTES = ("ID", "Name", "Command", "Class")
_METHOD_DECLARATION_RE = re.compile(
    r"""
    ^\s*
    (?:
        (?:public|private|protected|internal|static|sealed|virtual|override|extern|unsafe|new|async)\s+
    )*
    [A-Za-z_][A-Za-z0-9_<>,.\[\]?]*      # return type
    \s+
    (?P<name>[A-Za-z_][A-Za-z0-9_]*)     # method name
    \s*\(
    [^;]*$
    """,
    re.VERBOSE,
)
_SECTION_START_RE = re.compile(r"^\s*\[HarmonyPatch\]\s*$", re.MULTILINE)
_CLASS_NAME_RE = re.compile(r"\b(?:public|internal)\s+static\s+class\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)")
_CONST_STRING_RE = re.compile(
    r"\b(?:private|internal|public)\s+const\s+string\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\"(?P<value>[^\"]*)\";"
)
_TYPE_VAR_RE = re.compile(
    r"""
    \bvar\s+(?P<var>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*
    (?:
        GameTypeResolver\.FindType|AccessTools\.TypeByName
    )
    \(
        \s*(?P<expr>(?:\"[^\"]*\"|[A-Za-z_][A-Za-z0-9_.]*)(?:\s*\+\s*(?:\"[^\"]*\"|[A-Za-z_][A-Za-z0-9_.]*))*)
    """,
    re.VERBOSE,
)
_DIRECT_METHOD_EXPR_RE = re.compile(
    r"""
    AccessTools\.Method\(
        \s*(?P<expr>(?:\"[^\"]*\"|[A-Za-z_][A-Za-z0-9_.]*)(?:\s*\+\s*(?:\"[^\"]*\"|[A-Za-z_][A-Za-z0-9_.]*))*)
        \s*(?:,|\))
    """,
    re.VERBOSE,
)
_METHOD_ON_TYPE_VAR_RE = re.compile(
    r'AccessTools\.Method\(\s*(?P<var>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*"(?P<method>[^"]+)"'
)
_KNOWN_TARGET_PAIR_RE = re.compile(r'\(\s*"(?P<type>[^"]+)"\s*,\s*"(?P<method>[^"]+)"\s*\)')
_FIND_METHOD_RE = re.compile(r'FindMethod\(\s*methodName:\s*"(?P<method>[^"]+)"')
_RESOLVE_METHOD_RE = re.compile(r'ResolveMethod\(\s*methodName:\s*"(?P<method>[^"]+)"')
_MESSAGE_ROUTE_SIGNATURE = "XRL.Messages.MessageQueue:AddPlayerMessage"
_POPUP_ROUTE_SIGNATURES = (
    "XRL.UI.Popup:ShowBlock",
    "XRL.UI.Popup:ShowOptionList",
    "XRL.UI.Popup:ShowConversation",
)
_DISPLAY_NAME_ROUTE_SIGNATURES = (
    "XRL.World.GetDisplayNameEvent:GetFor",
    "XRL.World.GetDisplayNameEvent:ProcessFor",
)
_PSEUDO_LEAF_IDENTIFIER_KEYS = frozenset({"BodyText", "SelectedModLabel"})


@dataclass(frozen=True, slots=True)
class TranslationIndex:
    """In-memory index of existing translation assets and structural patches."""

    dictionary_keys: dict[str, set[str]]
    xml_ids: dict[str, set[str]]
    patch_targets: dict[str, set[str]]


def build_translation_index(repo_root: Path) -> TranslationIndex:
    """Build deterministic dictionary/XML/patch indexes from the repository."""
    dictionaries = _collect_dictionary_keys(repo_root / "Mods" / "QudJP" / "Localization" / "Dictionaries")
    xml_ids = _collect_xml_ids(repo_root / "Mods" / "QudJP" / "Localization")
    patch_targets = _collect_patch_targets(repo_root / "Mods" / "QudJP" / "Assemblies" / "src" / "Patches")
    return TranslationIndex(
        dictionary_keys=dictionaries,
        xml_ids=xml_ids,
        patch_targets=patch_targets,
    )


def cross_reference_inventory_file(
    inventory_draft_path: Path,
    repo_root: Path,
    *,
    source_root: Path | None = None,
    output_path: Path | None = None,
) -> InventoryDraft:
    """Read, cross-reference, and optionally persist a candidate inventory."""
    draft = read_inventory_draft_json(inventory_draft_path)
    candidate = cross_reference_inventory(draft, repo_root, source_root=source_root)
    if output_path is not None:
        write_candidate_inventory_json(output_path, candidate)
    return candidate


def cross_reference_inventory(
    draft: InventoryDraft,
    repo_root: Path,
    *,
    source_root: Path | None = None,
) -> InventoryDraft:
    """Mark sites translated when existing dictionaries, XML, or patches already cover them."""
    index = build_translation_index(repo_root)
    candidate_sites = tuple(_cross_reference_site(site, index, source_root=source_root) for site in draft.sites)
    return replace(draft, sites=candidate_sites)


def _cross_reference_site(
    site: InventorySite,
    index: TranslationIndex,
    *,
    source_root: Path | None,
) -> InventorySite:
    dictionary_matches = _dictionary_matches(site, index)
    xml_matches = _xml_matches(site, index)
    patch_matches = _patch_matches(site, index, source_root=source_root)
    if dictionary_matches or xml_matches or patch_matches:
        return replace(
            site,
            status=SiteStatus.TRANSLATED,
            existing_dictionary=_join_matches(dictionary_matches),
            existing_xml=_join_xml_matches(site, xml_matches),
            existing_patch=_join_matches(patch_matches),
        )
    return replace(
        site,
        status=_default_status(site),
        existing_dictionary=None,
        existing_xml=None,
        existing_patch=None,
    )


def _dictionary_matches(site: InventorySite, index: TranslationIndex) -> set[str]:
    """Return dictionary files whose keys exactly match the site's leaf text."""
    if site.type is not SiteType.LEAF or site.key is None:
        return set()
    return set(index.dictionary_keys.get(site.key, set()))


def _xml_matches(site: InventorySite, index: TranslationIndex) -> set[str]:
    """Return XML files whose identifiers match a blueprint-sourced site."""
    if site.source != "xml-blueprint" or site.source_id is None:
        return set()
    return set(index.xml_ids.get(site.source_id, set()))


def _patch_matches(
    site: InventorySite,
    index: TranslationIndex,
    *,
    source_root: Path | None,
) -> set[str]:
    """Return matching structural patch names for a site."""
    matches: set[str] = set()

    exact_signature = _site_method_signature(site, source_root)
    if exact_signature is not None:
        matches.update(index.patch_targets.get(exact_signature, set()))

    if site.sink == "AddPlayerMessage":
        matches.update(index.patch_targets.get(_MESSAGE_ROUTE_SIGNATURE, set()))

    if site.sink == "Popup":
        for signature in _POPUP_ROUTE_SIGNATURES:
            matches.update(index.patch_targets.get(signature, set()))

    if site.type is SiteType.BUILDER or site.sink == "GetDisplayName":
        for signature in _DISPLAY_NAME_ROUTE_SIGNATURES:
            matches.update(index.patch_targets.get(signature, set()))

    return matches


def _default_status(site: InventorySite) -> SiteStatus:
    """Map a classified site to its default candidate-inventory status."""
    if _is_pseudo_leaf_noise(site):
        return SiteStatus.EXCLUDED
    if site.type is SiteType.PROCEDURAL_TEXT:
        return SiteStatus.EXCLUDED
    if site.type is SiteType.UNRESOLVED or site.needs_runtime:
        return SiteStatus.UNRESOLVED
    if site.type is SiteType.MESSAGE_FRAME:
        return SiteStatus.NEEDS_PATCH
    if site.is_proven_fixed_leaf:
        return SiteStatus.NEEDS_TRANSLATION
    return SiteStatus.NEEDS_REVIEW


def _is_pseudo_leaf_noise(site: InventorySite) -> bool:
    """Return whether a proven leaf is actually placeholder/widget scaffolding."""
    if site.type is not SiteType.LEAF or site.key is None:
        return False
    if not site.key or site.key.isspace():
        return True
    return site.key in _PSEUDO_LEAF_IDENTIFIER_KEYS


def _collect_dictionary_keys(dictionary_root: Path) -> dict[str, set[str]]:
    """Index exact dictionary keys by file name."""
    keys: dict[str, set[str]] = {}
    for path in sorted(dictionary_root.rglob("*.ja.json")):
        dictionary_name = path.relative_to(dictionary_root).as_posix()
        payload = json.loads(path.read_text(encoding="utf-8"))
        for entry in payload.get("entries", []):
            key = entry.get("key")
            if not isinstance(key, str) or not key:
                continue
            keys.setdefault(key, set()).add(dictionary_name)
    return keys


def _collect_xml_ids(xml_root: Path) -> dict[str, set[str]]:
    """Index root-level XML translation identifiers by file name."""
    xml_ids: dict[str, set[str]] = {}
    for path in sorted(xml_root.glob("*.jp.xml")):
        tree = ET.parse(path)  # noqa: S314 - repo-local translation assets, not user-supplied XML
        for element in tree.iter():
            for attribute_name in _IDENTIFIER_ATTRIBUTES:
                value = element.attrib.get(attribute_name)
                if not value:
                    continue
                xml_ids.setdefault(value, set()).add(path.name)
    return xml_ids


def _collect_patch_targets(patch_root: Path) -> dict[str, set[str]]:
    """Index Harmony patch target signatures by patch class name."""
    targets: dict[str, set[str]] = {}
    for path in sorted(patch_root.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        constants = _parse_const_strings(text)
        for section in _split_harmony_sections(text):
            patch_name = _parse_patch_name(section)
            if patch_name is None:
                continue
            for signature in _extract_patch_targets(section, constants):
                targets.setdefault(signature, set()).add(patch_name)
    return targets


def _parse_const_strings(text: str) -> dict[str, str]:
    """Parse simple `const string` declarations used by patch target helpers."""
    constants: dict[str, str] = {}
    for match in _CONST_STRING_RE.finditer(text):
        constants[match.group("name")] = match.group("value")
    return constants


def _split_harmony_sections(text: str) -> list[str]:
    """Split a patch file into `[HarmonyPatch]`-scoped class sections."""
    matches = list(_SECTION_START_RE.finditer(text))
    if not matches:
        return []
    sections: list[str] = []
    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        sections.append(text[start:end])
    return sections


def _parse_patch_name(section: str) -> str | None:
    """Extract the Harmony patch class name for one section."""
    match = _CLASS_NAME_RE.search(section)
    if match is None:
        return None
    return match.group("name")


def _extract_patch_targets(section: str, constants: dict[str, str]) -> set[str]:
    """Extract confident target signatures from one Harmony patch section."""
    targets: set[str] = set()
    type_vars = _parse_type_variables(section, constants)

    for match in _KNOWN_TARGET_PAIR_RE.finditer(section):
        targets.add(f"{match.group('type')}:{match.group('method')}")

    for match in _METHOD_ON_TYPE_VAR_RE.finditer(section):
        target_type = type_vars.get(match.group("var"))
        if target_type is not None:
            targets.add(f"{target_type}:{match.group('method')}")

    for match in _DIRECT_METHOD_EXPR_RE.finditer(section):
        signature = _evaluate_string_expression(match.group("expr"), constants)
        if signature is not None and ":" in signature:
            targets.add(signature)

    target_type_name = _resolve_identifier("TargetTypeName", constants)
    if target_type_name is not None:
        for match in _FIND_METHOD_RE.finditer(section):
            targets.add(f"{target_type_name}:{match.group('method')}")

    grammar_type_name = _resolve_identifier("GrammarPatchTarget.TypeName", constants)
    if grammar_type_name is not None:
        for match in _RESOLVE_METHOD_RE.finditer(section):
            targets.add(f"{grammar_type_name}:{match.group('method')}")

    return targets


def _parse_type_variables(section: str, constants: dict[str, str]) -> dict[str, str]:
    """Parse local variables bound to `GameTypeResolver.FindType` or `AccessTools.TypeByName`."""
    type_vars: dict[str, str] = {}
    for match in _TYPE_VAR_RE.finditer(section):
        resolved = _evaluate_string_expression(match.group("expr"), constants)
        if resolved is not None:
            type_vars[match.group("var")] = resolved
    return type_vars


def _evaluate_string_expression(expression: str, constants: dict[str, str]) -> str | None:
    """Evaluate a simple C# string concatenation expression."""
    parts = [part.strip() for part in expression.split("+")]
    resolved_parts: list[str] = []
    for part in parts:
        if part.startswith('"') and part.endswith('"'):
            resolved_parts.append(part[1:-1])
            continue
        resolved = _resolve_identifier(part, constants)
        if resolved is None:
            return None
        resolved_parts.append(resolved)
    return "".join(resolved_parts)


def _resolve_identifier(identifier: str, constants: dict[str, str]) -> str | None:
    """Resolve a bare or dotted identifier from the parsed const-string table."""
    if identifier in constants:
        return constants[identifier]
    short_name = identifier.rsplit(".", maxsplit=1)[-1]
    return constants.get(short_name)


def _site_method_signature(site: InventorySite, source_root: Path | None) -> str | None:
    """Best-effort `Type:Method` signature for the site's containing source method."""
    if source_root is None or not site.file.endswith(".cs"):
        return None
    source_path = source_root.expanduser().resolve() / site.file
    if not source_path.exists():
        return None
    lines = source_path.read_text(encoding="utf-8").splitlines()
    method_name = _find_enclosing_method_name(lines, site.line)
    if method_name is None:
        return None
    type_name = PurePosixPath(site.file).with_suffix("").as_posix().replace("/", ".")
    return f"{type_name}:{method_name}"


def _find_enclosing_method_name(lines: list[str], line_number: int) -> str | None:
    """Find the nearest enclosing method declaration above the given line number."""
    upper_bound = min(max(line_number - 1, 0), len(lines))
    for index in range(upper_bound - 1, -1, -1):
        candidate = lines[index].strip()
        if not candidate or candidate.startswith("//"):
            continue
        match = _METHOD_DECLARATION_RE.match(candidate)
        if match is None:
            continue
        return match.group("name")
    return None


def _join_matches(matches: set[str]) -> str | None:
    """Join a set of evidence file names or patch names deterministically."""
    if not matches:
        return None
    return ", ".join(sorted(matches))


def _join_xml_matches(site: InventorySite, matches: set[str]) -> str | None:
    """Join XML file matches with the matched source identifier."""
    if not matches or site.source_id is None:
        return None
    return ", ".join(f"{name}#{site.source_id}" for name in sorted(matches))


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Parse CLI arguments for Phase 1d cross-reference."""
    parser = argparse.ArgumentParser(
        formatter_class=argparse.RawDescriptionHelpFormatter,
        description=(
            "Run Phase 1d translation cross-reference. "
            "This legacy scanner step is a bridge/view-only surface for current static consumers "
            "and not the source of truth."
        ),
        epilog=describe_first_pr_static_consumer_boundary(),
    )
    parser.add_argument(
        "--inventory-draft",
        default=str(DEFAULT_INPUT_PATH),
        help="Path to Phase 1b inventory_draft.json.",
    )
    parser.add_argument(
        "--repo-root",
        default=str(Path(__file__).resolve().parents[3]),
        help="Repository root that contains Mods/QudJP/.",
    )
    parser.add_argument(
        "--source-root",
        default=str(DEFAULT_SOURCE_ROOT),
        help="Path to decompiled C# source root for exact method matching.",
    )
    parser.add_argument(
        "--output",
        default=str(DEFAULT_OUTPUT_PATH),
        help=(
            "Path to write bridge/view-only candidate-inventory.json for current static consumers; "
            "not the source of truth."
        ),
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Execute Phase 1d cross-reference from the command line."""
    args = _parse_args(argv)
    candidate = cross_reference_inventory_file(
        Path(args.inventory_draft),
        Path(args.repo_root),
        source_root=Path(args.source_root),
        output_path=Path(args.output),
    )
    translated = sum(site.status is SiteStatus.TRANSLATED for site in candidate.sites)
    sys.stdout.write(f"Phase 1d cross-reference complete: {translated} translated of {len(candidate.sites)} sites.\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
