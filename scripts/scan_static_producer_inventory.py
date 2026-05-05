from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Final, NotRequired, TypedDict, cast

SCHEMA_VERSION: Final = "1.0"
GAME_VERSION: Final = "2.0.4"
DEFAULT_SOURCE_ROOT: Final = Path("~/dev/coq-decompiled_stable").expanduser()
TARGET_SURFACES: Final = ["EmitMessage", "Popup.Show*", "AddPlayerMessage"]

ACTIONABLE_STATUSES: Final = {"messages_candidate", "owner_patch_required"}
WRAPPER_PATHS: Final = {
    "XRL.Messages/MessageQueue.cs",
    "XRL.UI/Popup.cs",
    "XRL.World/IComponent.cs",
    "XRL/IGameSystem.cs",
    "XRL.World.AI/GoalHandler.cs",
}
DEBUG_EXACT_PATHS: Final = {
    "XRL.World.Capabilities/Wishing.cs",
    "Qud.UI/DebugConsole.cs",
}
DEBUG_PREFIXES: Final = ("XRL.Wish/",)
DIAGNOSTIC_MARKERS: Final = (
    "Debug:",
    "&KDebug:",
    "Error generating",
    "please report this error",
    "Hotload complete",
    "Total xp:",
    "secret id",
    "(missing)",
    "(invalid)",
    "Wasn't found",
    "biome:",
)


@dataclass(frozen=True, slots=True)
class _Argument:
    name: str | None
    formal_index: int | None
    expression: str


@dataclass(frozen=True, slots=True)
class _TextRole:
    name: str
    formal_index: int


POPUP_TEXT_ROLES: Final[dict[str, tuple[_TextRole, ...]]] = {
    "Show": (_TextRole("message", 0),),
    "ShowAsync": (_TextRole("message", 0),),
    "ShowFail": (_TextRole("message", 0),),
    "ShowFailAsync": (_TextRole("message", 0),),
    "ShowKeybindAsync": (_TextRole("message", 0),),
    "ShowBlock": (_TextRole("message", 0), _TextRole("title", 1)),
    "ShowSpace": (_TextRole("message", 0), _TextRole("title", 1)),
    "ShowBlockPrompt": (_TextRole("message", 0), _TextRole("prompt", 1)),
    "ShowBlockSpace": (_TextRole("message", 0), _TextRole("prompt", 1)),
    "ShowBlockWithCopy": (
        _TextRole("message", 0),
        _TextRole("prompt", 1),
        _TextRole("title", 2),
        _TextRole("copy_info", 3),
    ),
    "ShowConversation": (_TextRole("title", 0), _TextRole("intro", 2), _TextRole("options", 3)),
    "ShowOptionList": (
        _TextRole("title", 0),
        _TextRole("options", 1),
        _TextRole("intro", 4),
        _TextRole("spacing_text", 9),
        _TextRole("buttons", 14),
    ),
    "ShowOptionListAsync": (
        _TextRole("title", 0),
        _TextRole("options", 1),
        _TextRole("intro", 4),
        _TextRole("spacing_text", 9),
    ),
    "ShowColorPicker": (
        _TextRole("title", 0),
        _TextRole("intro", 2),
        _TextRole("spacing_text", 7),
        _TextRole("preview_content", 11),
    ),
    "ShowColorPickerAsync": (
        _TextRole("title", 0),
        _TextRole("intro", 2),
        _TextRole("spacing_text", 7),
        _TextRole("preview_content", 11),
    ),
}


@dataclass(frozen=True, slots=True)
class _MemberContext:
    namespace: str | None
    type_name: str
    member_name: str
    member_kind: str
    member_start_line: int
    parameter_names: frozenset[str]


@dataclass(frozen=True, slots=True)
class _Invocation:
    file: str
    line: int
    expression: str
    receiver: str | None
    method: str
    target_surface: str
    arguments: list[_Argument]
    context: _MemberContext
    closure_reason: str | None = None


@dataclass(frozen=True, slots=True)
class _MemberSpan:
    name: str
    kind: str
    start: int
    end: int
    start_line: int
    parameter_names: frozenset[str]


@dataclass(frozen=True, slots=True)
class _TypeSpan:
    name: str
    start: int
    end: int


class TextArgumentPayload(TypedDict):
    """Serialized text argument inventory record."""

    role: str
    formal_index: int
    expression: str
    expression_kind: str
    closure_status: str


class CallsitePayload(TypedDict):
    """Serialized callsite inventory record."""

    file: str
    line: int
    target_surface: str
    receiver: str | None
    method: str
    expression: str
    namespace: str | None
    type_name: str
    member_name: str
    member_kind: str
    member_start_line: int
    producer_family_id: str
    text_arguments: list[TextArgumentPayload]
    closure_status: str
    closure_reason: NotRequired[str]


class RepresentativeCallPayload(TypedDict):
    """Small callsite sample embedded in family records."""

    file: str
    line: int
    target_surface: str
    method: str
    closure_status: str
    expression: str


class FamilyPayload(TypedDict):
    """Serialized producer family inventory record."""

    producer_family_id: str
    file: str
    namespace: str | None
    type_name: str
    member_name: str
    member_kind: str
    member_start_line: int
    callsite_count: int
    text_argument_count: int
    family_closure_status: str
    closure_status_counts: dict[str, int]
    surface_counts: dict[str, int]
    representative_calls: list[RepresentativeCallPayload]


class TotalsPayload(TypedDict):
    """Aggregated inventory totals."""

    callsites: int
    families: int
    text_arguments: int
    callsite_statuses: dict[str, int]
    callsite_only_statuses: dict[str, int]
    text_argument_statuses: dict[str, int]
    text_argument_classifications: dict[str, int]
    family_statuses: dict[str, int]


class InventoryPayload(TypedDict):
    """Top-level static producer inventory payload."""

    schema_version: str
    game_version: str
    target_surfaces: list[str]
    totals: TotalsPayload
    callsites: list[CallsitePayload]
    families: list[FamilyPayload]


def scan_source_root(source_root: Path) -> InventoryPayload:
    """Scan decompiled C# sources for static producer inventory callsites."""
    callsites: list[CallsitePayload] = []
    for source_path in _iter_cs_files(source_root):
        relative_path = source_path.relative_to(source_root).as_posix()
        text = source_path.read_text(encoding="utf-8")
        callsites.extend(_serialize_callsite(invocation) for invocation in _scan_file(relative_path, text))

    callsites.sort(key=lambda callsite: (callsite["file"], callsite["line"], callsite["expression"]))
    families = _build_families(callsites)
    return {
        "schema_version": SCHEMA_VERSION,
        "game_version": GAME_VERSION,
        "target_surfaces": TARGET_SURFACES,
        "totals": _build_totals(callsites, families),
        "callsites": callsites,
        "families": families,
    }


def write_inventory(source_root: Path, output_path: Path) -> None:
    """Write the static producer inventory JSON."""
    payload = scan_source_root(source_root)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    _ = output_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    """Run the static producer inventory scanner CLI."""
    parser = argparse.ArgumentParser(description="Scan decompiled C# for static text producer callsites.")
    _ = parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT)
    _ = parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)

    source_root = cast("Path", args.source_root).expanduser()
    output_path = cast("Path", args.output)
    if not source_root.is_dir():
        _ = sys.stderr.write(f"source root does not exist or is not a directory: {source_root}\n")
        return 1

    write_inventory(source_root, output_path)
    return 0


def _iter_cs_files(source_root: Path) -> list[Path]:
    return sorted(path for path in source_root.rglob("*.cs") if path.is_file())


def _scan_file(relative_path: str, text: str) -> list[_Invocation]:
    masked = _mask_csharp_trivia_and_literals(text)
    type_spans = _find_type_spans(masked, text)
    member_spans = _find_member_spans(masked, text)
    invocations: list[_Invocation] = []

    for identifier_match in re.finditer(r"\b[A-Za-z_][A-Za-z0-9_]*\b", masked):
        method = identifier_match.group(0)
        identifier_start = identifier_match.start()
        open_paren = _skip_whitespace(masked, identifier_match.end())
        open_paren = _skip_optional_generic_type_args(masked, open_paren)
        if open_paren >= len(masked) or masked[open_paren] != "(":
            continue

        parsed_arguments = _extract_arguments(text, open_paren + 1)
        if parsed_arguments is None:
            continue
        raw_arguments, close_paren = parsed_arguments
        if _looks_like_method_declaration(masked, identifier_start, close_paren):
            continue

        receiver_info = _member_receiver_expression(text, masked, identifier_start)
        receiver = None if receiver_info is None else receiver_info[0]
        expression_start = identifier_start if receiver_info is None else receiver_info[1]
        target_surface, closure_reason = _target_surface(relative_path, method, receiver)
        if target_surface is None:
            continue

        context = _context_at(masked, identifier_start, type_spans, member_spans)
        invocations.append(
            _Invocation(
                file=relative_path,
                line=text.count("\n", 0, identifier_start) + 1,
                expression=text[expression_start : close_paren + 1].strip(),
                receiver=receiver,
                method=method,
                target_surface=target_surface,
                arguments=_assign_formal_indexes(method, raw_arguments),
                context=context,
                closure_reason=closure_reason,
            ),
        )

    return invocations


def _target_surface(relative_path: str, method: str, receiver: str | None) -> tuple[str | None, str | None]:
    if method in {"PickOption", "AskString"}:
        return None, None
    if method == "EmitMessage":
        return "EmitMessage", None
    if method == "AddPlayerMessage":
        return "AddPlayerMessage", None
    if not method.startswith("Show"):
        return None, None
    if _receiver_name(receiver) == "Popup" or (receiver is None and relative_path == "XRL.UI/Popup.cs"):
        reason = None if _popup_text_roles(method) else "unknown_popup_show_signature"
        return "Popup.Show*", reason
    return None, None


def _serialize_callsite(invocation: _Invocation) -> CallsitePayload:
    text_arguments = _text_arguments(invocation)
    is_debug = _is_debug_callsite(invocation)
    is_wrapper_sink = _is_wrapper_sink(invocation, text_arguments)

    serialized_text_arguments: list[TextArgumentPayload] = []
    for text_argument in text_arguments:
        classification = _classify_expression(invocation, text_argument)
        closure_status = _text_argument_closure(
            invocation,
            classification,
            is_debug=is_debug,
            is_wrapper_sink=is_wrapper_sink,
        )
        serialized_text_arguments.append(
            {
                "role": text_argument.name,
                "formal_index": text_argument.formal_index,
                "expression": _argument_at(invocation.arguments, text_argument.formal_index) or "",
                "expression_kind": classification,
                "closure_status": closure_status,
            },
        )

    closure_status, closure_reason = _callsite_closure(
        invocation,
        serialized_text_arguments,
        is_debug=is_debug,
        is_wrapper_sink=is_wrapper_sink,
    )
    callsite: CallsitePayload = {
        "file": invocation.file,
        "line": invocation.line,
        "target_surface": invocation.target_surface,
        "receiver": invocation.receiver,
        "method": invocation.method,
        "expression": invocation.expression,
        "namespace": invocation.context.namespace,
        "type_name": invocation.context.type_name,
        "member_name": invocation.context.member_name,
        "member_kind": invocation.context.member_kind,
        "member_start_line": invocation.context.member_start_line,
        "producer_family_id": _producer_family_id(invocation),
        "text_arguments": serialized_text_arguments,
        "closure_status": closure_status,
    }
    if closure_reason is not None:
        callsite["closure_reason"] = closure_reason
    return callsite


def _text_arguments(invocation: _Invocation) -> list[_TextRole]:
    if invocation.target_surface == "EmitMessage":
        index = _emit_message_text_index(invocation)
        return [_TextRole("message", index)] if _argument_at(invocation.arguments, index) is not None else []
    if invocation.target_surface == "AddPlayerMessage":
        return [_TextRole("message", 0)] if _argument_at(invocation.arguments, 0) is not None else []
    return [
        role
        for role in _popup_text_roles(invocation.method)
        if _argument_at(invocation.arguments, role.formal_index) is not None
    ]


def _emit_message_text_index(invocation: _Invocation) -> int:
    receiver_name = _receiver_name(invocation.receiver)
    if receiver_name in {"Messaging", "IComponent"} or (invocation.receiver or "").startswith("IComponent<"):
        return 1
    if receiver_name is not None:
        return 0

    first_argument = _argument_at(invocation.arguments, 0)
    if (
        first_argument is not None
        and _is_source_like_expression(first_argument)
        and _argument_at(invocation.arguments, 1)
    ):
        return 1
    return 0


def _popup_text_roles(method: str) -> list[_TextRole]:
    roles = POPUP_TEXT_ROLES.get(method)
    if roles is not None:
        return list(roles)
    if method.startswith("ShowYesNo"):
        return [_TextRole("message", 0)]
    return []


def _classify_expression(invocation: _Invocation, role: _TextRole) -> str:
    raw_expression = _argument_at(invocation.arguments, role.formal_index) or ""
    expression = _strip_balanced_outer_parentheses(raw_expression.strip())
    if role.name == "options":
        return "collection_text"
    if _is_wrapper_path(invocation.file) and _is_forwarded_parameter(expression, invocation.context.parameter_names):
        return "forwarded_parameter"
    if _parse_csharp_string_literal(expression) is not None:
        return "static_literal"
    if _is_literal_template(expression):
        return "literal_template"
    if _looks_like_collection_text(expression):
        return "collection_text"
    return "procedural_or_unknown"


def _text_argument_closure(  # noqa: PLR0911
    invocation: _Invocation,
    classification: str,
    *,
    is_debug: bool,
    is_wrapper_sink: bool,
) -> str:
    if is_debug:
        return "debug_ignore"
    if is_wrapper_sink:
        return "sink_observed_only"
    if invocation.target_surface == "AddPlayerMessage":
        if classification in {"static_literal", "literal_template"}:
            return "owner_patch_required"
        return "runtime_required"
    if invocation.target_surface == "Popup.Show*":
        if classification == "static_literal":
            return "messages_candidate"
        if classification == "literal_template":
            return "owner_patch_required"
        return "runtime_required"
    if classification in {"static_literal", "literal_template"}:
        return "messages_candidate"
    return "runtime_required"


def _callsite_closure(  # noqa: PLR0911
    invocation: _Invocation,
    text_arguments: list[TextArgumentPayload],
    *,
    is_debug: bool,
    is_wrapper_sink: bool,
) -> tuple[str, str | None]:
    if is_debug:
        return "debug_ignore", "debug_override"
    if is_wrapper_sink:
        return "sink_observed_only", "wrapper_forwarding"
    if not text_arguments:
        return "runtime_required", invocation.closure_reason or "no_text_arguments"

    statuses = {str(argument["closure_status"]) for argument in text_arguments}
    if statuses == {"debug_ignore"}:
        return "debug_ignore", "debug_override"
    if statuses == {"sink_observed_only"}:
        return "sink_observed_only", "wrapper_forwarding"
    if "runtime_required" in statuses:
        return "runtime_required", None
    if "owner_patch_required" in statuses:
        return "owner_patch_required", None
    return "messages_candidate", None


def _build_families(callsites: list[CallsitePayload]) -> list[FamilyPayload]:
    grouped: dict[str, list[CallsitePayload]] = {}
    for callsite in callsites:
        grouped.setdefault(str(callsite["producer_family_id"]), []).append(callsite)

    families: list[FamilyPayload] = []
    for family_id, family_callsites in sorted(grouped.items()):
        first = family_callsites[0]
        closure_units = _family_closure_units(family_callsites)
        closure_statuses = Counter(closure_units)
        surface_counts = Counter(str(callsite["target_surface"]) for callsite in family_callsites)
        families.append(
            {
                "producer_family_id": family_id,
                "file": first["file"],
                "namespace": first["namespace"],
                "type_name": first["type_name"],
                "member_name": first["member_name"],
                "member_kind": first["member_kind"],
                "member_start_line": first["member_start_line"],
                "callsite_count": len(family_callsites),
                "text_argument_count": sum(len(callsite["text_arguments"]) for callsite in family_callsites),
                "family_closure_status": _family_closure(set(closure_statuses)),
                "closure_status_counts": dict(sorted(closure_statuses.items())),
                "surface_counts": dict(sorted(surface_counts.items())),
                "representative_calls": [
                    {
                        "file": callsite["file"],
                        "line": callsite["line"],
                        "target_surface": callsite["target_surface"],
                        "method": callsite["method"],
                        "closure_status": callsite["closure_status"],
                        "expression": callsite["expression"],
                    }
                    for callsite in family_callsites[:3]
                ],
            },
        )
    return families


def _family_closure_units(family_callsites: list[CallsitePayload]) -> list[str]:
    units: list[str] = []
    for callsite in family_callsites:
        text_arguments = callsite["text_arguments"]
        if text_arguments:
            units.extend(str(argument["closure_status"]) for argument in text_arguments)
        else:
            units.append(str(callsite["closure_status"]))
    return units


def _family_closure(statuses: set[str]) -> str:  # noqa: PLR0911
    effective = statuses - {"debug_ignore"}
    if not effective:
        return "debug_ignore"
    if effective == {"sink_observed_only"}:
        return "sink_observed_only"
    if len(effective) == 1:
        return next(iter(effective))
    if effective & {"runtime_required"} and effective & ACTIONABLE_STATUSES:
        return "needs_family_review"
    if {"messages_candidate", "owner_patch_required"} <= effective:
        return "needs_family_review"
    if "runtime_required" in effective:
        return "runtime_required"
    if "owner_patch_required" in effective:
        return "owner_patch_required"
    return "messages_candidate"


def _build_totals(callsites: list[CallsitePayload], families: list[FamilyPayload]) -> TotalsPayload:
    text_argument_statuses: Counter[str] = Counter()
    text_argument_classifications: Counter[str] = Counter()
    callsite_statuses: Counter[str] = Counter()
    callsite_only_statuses: Counter[str] = Counter()
    family_statuses: Counter[str] = Counter()

    for callsite in callsites:
        callsite_statuses[str(callsite["closure_status"])] += 1
        text_arguments = callsite["text_arguments"]
        if not text_arguments:
            callsite_only_statuses[str(callsite["closure_status"])] += 1
        for text_argument in text_arguments:
            text_argument_statuses[str(text_argument["closure_status"])] += 1
            text_argument_classifications[str(text_argument["expression_kind"])] += 1

    for family in families:
        family_statuses[str(family["family_closure_status"])] += 1

    return {
        "callsites": len(callsites),
        "families": len(families),
        "text_arguments": sum(len(callsite["text_arguments"]) for callsite in callsites),
        "callsite_statuses": dict(sorted(callsite_statuses.items())),
        "callsite_only_statuses": dict(sorted(callsite_only_statuses.items())),
        "text_argument_statuses": dict(sorted(text_argument_statuses.items())),
        "text_argument_classifications": dict(sorted(text_argument_classifications.items())),
        "family_statuses": dict(sorted(family_statuses.items())),
    }


def _assign_formal_indexes(method: str, arguments: list[_Argument]) -> list[_Argument]:
    named_indexes = _named_argument_indexes(method, arguments)
    used_formal_indexes: set[int] = set()
    next_positional_index = 0
    assigned: list[_Argument] = []
    for argument in arguments:
        formal_index: int | None
        if argument.name is None:
            while next_positional_index in used_formal_indexes:
                next_positional_index += 1
            formal_index = next_positional_index
            next_positional_index += 1
        else:
            formal_index = named_indexes.get(argument.name)
            if formal_index is None:
                assigned.append(_Argument(argument.name, None, argument.expression))
                continue
        used_formal_indexes.add(formal_index)
        assigned.append(_Argument(argument.name, formal_index, argument.expression))
    return assigned


def _named_argument_indexes(method: str, arguments: list[_Argument]) -> dict[str, int]:
    if method in {"EmitMessage", "AddPlayerMessage"}:
        if method == "AddPlayerMessage":
            return {"Message": 0}
        source_first = (
            len(arguments) > 1
            and _is_source_like_expression(arguments[0].expression)
            and any(argument.name in {"Source", "Msg"} for argument in arguments)
        )
        message_index = 1 if source_first else 0
        return {"Source": 0, "Message": message_index, "Msg": 1}
    role_indexes = {role.name: role.formal_index for role in _popup_text_roles(method)}
    aliases = {
        "message": "Message",
        "title": "Title",
        "prompt": "Prompt",
        "intro": "Intro",
        "options": "Options",
        "spacing_text": "SpacingText",
        "buttons": "Buttons",
        "preview_content": "PreviewContent",
    }
    named: dict[str, int] = {}
    for role_name, formal_name in aliases.items():
        if role_name in role_indexes:
            named[formal_name] = role_indexes[role_name]
    return named


def _argument_at(arguments: list[_Argument], formal_index: int) -> str | None:
    for argument in arguments:
        if argument.formal_index == formal_index:
            return argument.expression
    return None


def _producer_family_id(invocation: _Invocation) -> str:
    return f"{invocation.file}::{invocation.context.type_name}.{invocation.context.member_name}"


def _context_at(
    masked: str,
    index: int,
    type_spans: list[_TypeSpan],
    member_spans: list[_MemberSpan],
) -> _MemberContext:
    namespace = _namespace_at(masked, index)
    type_span = _innermost_type_span(type_spans, index)
    member_span = _innermost_member_span(member_spans, index)
    if type_span is None:
        type_name = "<top-level>"
    elif namespace:
        type_name = f"{namespace}.{type_span.name}"
    else:
        type_name = type_span.name
    if member_span is None:
        member_name = "<type-scope>" if type_span else "<top-level>"
        return _MemberContext(namespace, type_name, member_name, "type", 1, frozenset())
    return _MemberContext(
        namespace,
        type_name,
        member_span.name,
        member_span.kind,
        member_span.start_line,
        member_span.parameter_names,
    )


def _namespace_at(masked: str, index: int) -> str | None:
    matches = list(re.finditer(r"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)", masked[:index]))
    if not matches:
        return None
    return matches[-1].group(1)


def _find_type_spans(masked: str, text: str) -> list[_TypeSpan]:
    spans: list[_TypeSpan] = []
    pattern = re.compile(r"\b(?:class|struct|interface|record)\s+([A-Za-z_][A-Za-z0-9_]*)[^{;]*\{")
    for match in pattern.finditer(masked):
        open_brace = masked.find("{", match.start())
        close_brace = _find_matching_brace(masked, open_brace)
        if close_brace is not None:
            spans.append(_TypeSpan(match.group(1), match.start(), close_brace))
    return sorted(spans, key=lambda span: (span.start, span.end, text[span.start : span.end]))


def _find_member_spans(masked: str, text: str) -> list[_MemberSpan]:
    spans: list[_MemberSpan] = []
    spans.extend(_find_method_spans(masked, text))
    spans.extend(_find_property_spans(masked, text, spans))
    return sorted(spans, key=lambda span: (span.start, span.end))


def _find_method_spans(masked: str, text: str) -> list[_MemberSpan]:
    spans: list[_MemberSpan] = []
    pattern = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(([^()]*)\)\s*(?:where\b[^{]*)?\{", re.MULTILINE)
    for match in pattern.finditer(masked):
        name = match.group(1)
        if name in {"if", "for", "foreach", "while", "switch", "catch", "using", "lock", "fixed", "delegate"}:
            continue
        if _previous_identifier(masked, match.start(1)) in {"new", "return", "throw"}:
            continue
        open_brace = masked.find("{", match.end() - 1)
        close_brace = _find_matching_brace(masked, open_brace)
        if close_brace is None:
            continue
        spans.append(
            _MemberSpan(
                name=name,
                kind="method",
                start=match.start(1),
                end=close_brace,
                start_line=text.count("\n", 0, match.start(1)) + 1,
                parameter_names=_parameter_names(match.group(2)),
            ),
        )
    return spans


def _find_property_spans(masked: str, text: str, method_spans: list[_MemberSpan]) -> list[_MemberSpan]:
    spans: list[_MemberSpan] = []
    property_pattern = (
        r"\b(?:public|private|protected|internal|static|virtual|override|sealed|abstract|readonly|\s)+"
        r"[A-Za-z_][A-Za-z0-9_<>,.\[\]?]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{"
    )
    pattern = re.compile(property_pattern, re.MULTILINE)
    for match in pattern.finditer(masked):
        if any(span.start <= match.start(1) <= span.end for span in method_spans):
            continue
        name = match.group(1)
        if name in {"class", "struct", "interface", "record", "namespace"}:
            continue
        open_brace = masked.find("{", match.end() - 1)
        close_brace = _find_matching_brace(masked, open_brace)
        if close_brace is None:
            continue
        spans.append(
            _MemberSpan(
                name=name,
                kind="property",
                start=match.start(1),
                end=close_brace,
                start_line=text.count("\n", 0, match.start(1)) + 1,
                parameter_names=frozenset(),
            ),
        )
    return spans


def _innermost_type_span(type_spans: list[_TypeSpan], index: int) -> _TypeSpan | None:
    matches = [span for span in type_spans if span.start <= index <= span.end]
    return max(matches, key=lambda span: span.start, default=None)


def _innermost_member_span(member_spans: list[_MemberSpan], index: int) -> _MemberSpan | None:
    matches = [span for span in member_spans if span.start <= index <= span.end]
    return max(matches, key=lambda span: span.start, default=None)


def _parameter_names(parameter_text: str) -> frozenset[str]:
    names: list[str] = []
    for raw_parameter in parameter_text.split(","):
        parameter = raw_parameter.strip()
        if not parameter:
            continue
        name_match = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\s*(?:=.*)?$", parameter)
        if name_match is not None:
            names.append(name_match.group(1))
    return frozenset(names)


def _is_source_like_expression(expression: str) -> bool:
    normalized = re.sub(r"\s+", "", expression)
    source_names = {
        "Actor",
        "Source",
        "ParentObject",
        "Object",
        "GO",
        "who",
        "target",
        "Target",
        "E.Actor",
        "E.Object",
        "The.Player",
    }
    if normalized in source_names:
        return True
    if normalized.endswith(".GetBasisGameObject()"):
        return True
    return any(normalized.endswith(f".{name}") for name in source_names if "." not in name)


def _is_wrapper_sink(invocation: _Invocation, text_arguments: list[_TextRole]) -> bool:
    if not _is_wrapper_path(invocation.file):
        return False
    if not text_arguments:
        return True
    return all(
        _is_forwarded_parameter(
            _argument_at(invocation.arguments, role.formal_index) or "",
            invocation.context.parameter_names,
        )
        for role in text_arguments
    )


def _is_wrapper_path(relative_path: str) -> bool:
    return relative_path in WRAPPER_PATHS


def _is_forwarded_parameter(expression: str, parameter_names: frozenset[str]) -> bool:
    normalized = _strip_balanced_outer_parentheses(expression.strip())
    return normalized in parameter_names


def _is_debug_callsite(invocation: _Invocation) -> bool:
    basename = Path(invocation.file).name
    if invocation.file in DEBUG_EXACT_PATHS or invocation.file.startswith(DEBUG_PREFIXES):
        return True
    if re.fullmatch(r"exDebug.*\.cs", basename):
        return True
    return _has_diagnostic_marker(invocation.expression)


def _has_diagnostic_marker(expression: str) -> bool:
    lower_expression = expression.lower()
    if re.search(r"\bdebug\b", expression, flags=re.IGNORECASE):
        return True
    return any(marker.lower() in lower_expression for marker in DIAGNOSTIC_MARKERS)


def _is_literal_template(expression: str) -> bool:
    stripped = expression.lstrip()
    if stripped.startswith(('$"', '$@"', '@$"', '$"""')):
        return True
    if re.search(r"\bstring\.Format\s*\(", expression):
        return True
    return _contains_string_literal(expression) and _has_top_level_operator(expression, "+")


def _looks_like_collection_text(expression: str) -> bool:
    stripped = expression.strip()
    return (
        stripped.startswith(("new[]", "new List<", "new string[]", "{"))
        or stripped.endswith("[]")
        or re.search(r"\b(?:List|IList|IEnumerable)<\s*string\s*>", stripped) is not None
    )


def _contains_string_literal(expression: str) -> bool:
    index = 0
    while index < len(expression):
        literal_end = _skip_csharp_literal(expression, index)
        if literal_end is not None:
            return True
        index += 1
    return False


def _has_top_level_operator(expression: str, operator: str) -> bool:
    depth = 0
    index = 0
    while index < len(expression):
        skipped = _skip_csharp_trivia_or_literal(expression, index)
        if skipped != index:
            index = skipped
            continue
        char = expression[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth = max(0, depth - 1)
        elif char == operator and depth == 0:
            return True
        index += 1
    return False


def _extract_arguments(text: str, start: int) -> tuple[list[_Argument], int] | None:
    arguments: list[_Argument] = []
    index = start
    argument_start = start
    depth = 0
    while index < len(text):
        skipped = _skip_csharp_trivia_or_literal(text, index)
        if skipped != index:
            index = skipped
            continue

        char = text[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            if char == ")" and depth == 0:
                _append_argument(arguments, text[argument_start:index])
                return arguments, index
            depth = max(0, depth - 1)
        elif char == "," and depth == 0:
            _append_argument(arguments, text[argument_start:index])
            argument_start = index + 1
        index += 1
    return None


def _append_argument(arguments: list[_Argument], raw_argument: str) -> None:
    expression = raw_argument.strip()
    if not expression:
        return
    name, named_expression = _split_named_argument(expression)
    arguments.append(_Argument(name, None, named_expression.strip() if name is not None else expression))


def _split_named_argument(expression: str) -> tuple[str | None, str]:
    depth = 0
    index = 0
    while index < len(expression):
        skipped = _skip_csharp_trivia_or_literal(expression, index)
        if skipped != index:
            index = skipped
            continue

        char = expression[index]
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth = max(0, depth - 1)
        elif char == "?" and depth == 0:
            return None, expression
        elif char == ":" and depth == 0:
            name = expression[:index].strip()
            if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
                return name, expression[index + 1 :]
            return None, expression
        index += 1
    return None, expression


def _member_receiver_expression(text: str, masked: str, member_start: int) -> tuple[str, int] | None:
    dot_index = _previous_non_whitespace_index(masked, member_start)
    if dot_index is None or masked[dot_index] != ".":
        return None
    receiver_start = _receiver_expression_start(masked, dot_index)
    receiver = text[receiver_start:dot_index].strip()
    return (receiver, receiver_start) if receiver else None


def _receiver_expression_start(masked: str, receiver_end: int) -> int:
    index = receiver_end - 1
    depth = 0
    while index >= 0:
        char = masked[index]
        if char in ")]}":
            depth += 1
        elif char in "([{":
            depth -= 1
            if depth < 0:
                return index + 1
        elif depth == 0 and (char.isspace() or char in ";{}=,+-*/"):
            return index + 1
        index -= 1
    return 0


def _receiver_name(receiver: str | None) -> str | None:
    if receiver is None:
        return None
    match = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\??\s*(?:<[^<>]*>)?\s*$", receiver)
    return None if match is None else match.group(1)


def _skip_optional_generic_type_args(masked: str, index: int) -> int:
    index = _skip_whitespace(masked, index)
    if index >= len(masked) or masked[index] != "<":
        return index
    depth = 0
    cursor = index
    while cursor < len(masked):
        if masked[cursor] == "<":
            depth += 1
        elif masked[cursor] == ">":
            depth -= 1
            if depth == 0:
                return _skip_whitespace(masked, cursor + 1)
        elif masked[cursor] in ";\n{}" and depth > 0:
            return index
        cursor += 1
    return index


def _looks_like_method_declaration(masked: str, identifier_start: int, close_paren: int) -> bool:
    previous_index = _previous_non_whitespace_index(masked, identifier_start)
    if previous_index is None:
        return False
    if not _is_identifier_part(masked[previous_index]) and masked[previous_index] not in ">]?":
        return False
    previous_identifier = _previous_identifier(masked, identifier_start)
    if previous_identifier in {"await", "return", "throw", "new"}:
        return False
    after_paren = _skip_whitespace(masked, close_paren + 1)
    if after_paren >= len(masked):
        return False
    return masked.startswith("=>", after_paren) or masked[after_paren] in "{;"


def _find_matching_brace(masked: str, open_brace: int) -> int | None:
    if open_brace < 0 or open_brace >= len(masked) or masked[open_brace] != "{":
        return None
    depth = 0
    for index in range(open_brace, len(masked)):
        if masked[index] == "{":
            depth += 1
        elif masked[index] == "}":
            depth -= 1
            if depth == 0:
                return index
    return None


def _mask_csharp_trivia_and_literals(text: str) -> str:
    chars = list(text)
    index = 0
    while index < len(text):
        skipped = _skip_csharp_trivia_or_literal(text, index)
        if skipped == index:
            index += 1
            continue
        for char_index in range(index, skipped):
            if chars[char_index] != "\n":
                chars[char_index] = " "
        index = skipped
    return "".join(chars)


def _skip_csharp_trivia_or_literal(text: str, index: int) -> int:
    comment_end = _skip_csharp_comment(text, index)
    if comment_end is not None:
        return comment_end
    literal_end = _skip_csharp_literal(text, index)
    if literal_end is not None:
        return literal_end
    return index


def _skip_csharp_comment(text: str, index: int) -> int | None:
    if text.startswith("//", index):
        newline = text.find("\n", index + 2)
        return len(text) if newline == -1 else newline + 1
    if text.startswith("/*", index):
        end = text.find("*/", index + 2)
        return len(text) if end == -1 else end + 2
    return None


def _skip_csharp_literal(text: str, index: int) -> int | None:  # noqa: PLR0911
    if index >= len(text):
        return None
    if text.startswith('@"', index):
        return _consume_verbatim_string_end(text, index)
    if text.startswith('$@"', index):
        return _consume_verbatim_string_end(text, index + 1)
    if text.startswith('@$"', index):
        return _consume_verbatim_string_body_end(text, index + 3)
    if text.startswith('$"""', index) or text.startswith('"""', index):
        return _consume_raw_string_end(text, index)
    if text.startswith('$"', index):
        return _consume_regular_string_end(text, index + 1)
    if text[index] == '"':
        return _consume_regular_string_end(text, index)
    if text[index] == "'":
        return _consume_char_literal_end(text, index)
    return None


def _consume_regular_string_end(text: str, start: int) -> int | None:
    if start >= len(text) or text[start] != '"':
        return None
    index = start + 1
    while index < len(text):
        if text[index] == "\\":
            index += 2
            continue
        if text[index] == '"':
            return index + 1
        index += 1
    return None


def _consume_verbatim_string_end(text: str, start: int) -> int | None:
    if start >= len(text) or not text.startswith('@"', start):
        return None
    return _consume_verbatim_string_body_end(text, start + 2)


def _consume_verbatim_string_body_end(text: str, start: int) -> int | None:
    index = start
    while index < len(text):
        if text.startswith('""', index):
            index += 2
            continue
        if text[index] == '"':
            return index + 1
        index += 1
    return None


def _consume_raw_string_end(text: str, start: int) -> int | None:
    index = start
    while index < len(text) and text[index] == "$":
        index += 1
    if not text.startswith('"""', index):
        return None
    quote_count = 0
    while index < len(text) and text[index] == '"':
        quote_count += 1
        index += 1
    delimiter = '"' * quote_count
    end = text.find(delimiter, index)
    return None if end == -1 else end + quote_count


def _consume_char_literal_end(text: str, start: int) -> int | None:
    index = start + 1
    while index < len(text):
        if text[index] == "\\":
            index += 2
            continue
        if text[index] == "'":
            return index + 1
        index += 1
    return None


def _parse_csharp_string_literal(expression: str) -> str | None:
    if expression.startswith('"'):
        result = _consume_regular_string(expression, 0)
    elif expression.startswith('@"'):
        result = _consume_verbatim_string(expression, 0)
    elif expression.startswith('"""'):
        result = _consume_raw_string(expression, 0)
    else:
        return None
    if result is None:
        return None
    end_index, value = result
    if end_index != len(expression):
        return None
    return value


def _consume_regular_string(text: str, start: int) -> tuple[int, str] | None:
    end = _consume_regular_string_end(text, start)
    if end is None:
        return None
    try:
        return end, bytes(text[start + 1 : end - 1], "utf-8").decode("unicode_escape")
    except UnicodeDecodeError:
        return end, text[start + 1 : end - 1]


def _consume_verbatim_string(text: str, start: int) -> tuple[int, str] | None:
    end = _consume_verbatim_string_end(text, start)
    if end is None:
        return None
    return end, text[start + 2 : end - 1].replace('""', '"')


def _consume_raw_string(text: str, start: int) -> tuple[int, str] | None:
    end = _consume_raw_string_end(text, start)
    if end is None:
        return None
    quote_count = 0
    while start + quote_count < len(text) and text[start + quote_count] == '"':
        quote_count += 1
    return end, text[start + quote_count : end - quote_count]


def _strip_balanced_outer_parentheses(expression: str) -> str:
    while expression.startswith("(") and expression.endswith(")"):
        inner = expression[1:-1].strip()
        if not _is_balanced_expression(inner):
            break
        expression = inner
    return expression


def _is_balanced_expression(expression: str) -> bool:
    depth = 0
    index = 0
    while index < len(expression):
        skipped = _skip_csharp_trivia_or_literal(expression, index)
        if skipped != index:
            index = skipped
            continue
        if expression[index] in "([{":
            depth += 1
        elif expression[index] in ")]}":
            depth -= 1
            if depth < 0:
                return False
        index += 1
    return depth == 0


def _previous_non_whitespace_index(text: str, index: int) -> int | None:
    index -= 1
    while index >= 0 and text[index].isspace():
        index -= 1
    return index if index >= 0 else None


def _previous_identifier(text: str, index: int) -> str | None:
    end = _previous_non_whitespace_index(text, index)
    if end is None or not _is_identifier_part(text[end]):
        return None
    start = end
    while start >= 0 and _is_identifier_part(text[start]):
        start -= 1
    return text[start + 1 : end + 1]


def _skip_whitespace(text: str, index: int) -> int:
    while index < len(text) and text[index].isspace():
        index += 1
    return index


def _is_identifier_part(char: str) -> bool:
    return char.isalnum() or char == "_"


if __name__ == "__main__":
    raise SystemExit(main())
