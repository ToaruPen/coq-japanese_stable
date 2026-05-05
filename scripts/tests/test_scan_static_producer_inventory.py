from __future__ import annotations

import json
from pathlib import Path
from typing import TYPE_CHECKING, Protocol, cast

from scripts.scan_static_producer_inventory import (
    CallsitePayload,
    FamilyPayload,
    InventoryPayload,
    TextArgumentPayload,
    main,
    scan_source_root,
)

if TYPE_CHECKING:
    from collections.abc import Callable

FIXTURE_ROOT = Path(__file__).parent / "fixtures" / "static_producer_inventory"


class _CapturedOutput(Protocol):
    out: str
    err: str


class _Capsys(Protocol):
    def readouterr(self) -> _CapturedOutput: ...


def _payload() -> InventoryPayload:
    return scan_source_root(FIXTURE_ROOT)


def _callsites(payload: InventoryPayload) -> list[CallsitePayload]:
    return payload["callsites"]


def _families(payload: InventoryPayload) -> list[FamilyPayload]:
    return payload["families"]


def _matching_callsite(
    payload: InventoryPayload,
    predicate: Callable[[CallsitePayload], bool],
    description: str,
) -> CallsitePayload:
    matches = [callsite for callsite in _callsites(payload) if predicate(callsite)]
    assert len(matches) == 1, description
    return matches[0]


def _callsite(payload: InventoryPayload, file: str, expression_contains: str) -> CallsitePayload:
    return _matching_callsite(
        payload,
        lambda callsite: callsite["file"] == file and expression_contains in callsite["expression"],
        f"Expected one callsite in {file} containing {expression_contains!r}",
    )


def _exact_callsite(payload: InventoryPayload, file: str, expression: str) -> CallsitePayload:
    return _matching_callsite(
        payload,
        lambda callsite: callsite["file"] == file and callsite["expression"] == expression,
        f"Expected one exact callsite in {file}: {expression!r}",
    )


def _text_args(callsite: CallsitePayload) -> list[TextArgumentPayload]:
    return callsite["text_arguments"]


def test_cli_writes_deterministic_inventory_without_absolute_source_root(tmp_path: Path) -> None:
    """The CLI writes stable JSON and omits machine-local source paths."""
    output_a = tmp_path / "inventory-a.json"
    output_b = tmp_path / "inventory-b.json"

    assert main(["--source-root", str(FIXTURE_ROOT), "--output", str(output_a)]) == 0
    assert main(["--source-root", str(FIXTURE_ROOT), "--output", str(output_b)]) == 0

    assert output_a.read_text(encoding="utf-8") == output_b.read_text(encoding="utf-8")
    payload = cast("dict[str, object]", json.loads(output_a.read_text(encoding="utf-8")))
    assert payload["schema_version"] == "1.0"
    assert payload["game_version"] == "2.0.4"
    assert payload["target_surfaces"] == ["EmitMessage", "Popup.Show*", "AddPlayerMessage"]
    assert "source_root" not in payload


def test_scanner_skips_comments_strings_char_literals_and_raw_strings() -> None:
    """Call-like text inside trivia and literals is not scanned as code."""
    payload = _payload()
    serialized = json.dumps(payload, ensure_ascii=False)

    assert "Ignored comment" not in serialized
    assert "Ignored string" not in serialized
    assert "Ignored raw string" not in serialized


def test_emit_message_text_indexes_include_unqualified_source_first_call() -> None:
    """EmitMessage uses the approved receiver and source-first message indexes."""
    payload = _payload()

    static_call = _callsite(
        payload,
        "Demo/StaticProducerCases.cs",
        'Messaging.EmitMessage(ParentObject, "Static emit")',
    )
    assert static_call["target_surface"] == "EmitMessage"
    assert static_call["closure_status"] == "messages_candidate"
    assert _text_args(static_call) == [
        {
            "role": "message",
            "formal_index": 1,
            "expression": '"Static emit"',
            "expression_kind": "static_literal",
            "closure_status": "messages_candidate",
        },
    ]

    source_first = _callsite(payload, "Demo/StaticProducerCases.cs", "EmitMessage(E.Actor, stringBuilder2, false)")
    assert _text_args(source_first)[0]["formal_index"] == 1
    assert _text_args(source_first)[0]["expression"] == "stringBuilder2"
    assert _text_args(source_first)[0]["expression_kind"] == "procedural_or_unknown"
    assert source_first["closure_status"] == "runtime_required"

    instance_call = _callsite(payload, "Demo/StaticProducerCases.cs", 'ParentObject.EmitMessage("Instance emit")')
    assert _text_args(instance_call)[0]["formal_index"] == 0
    assert instance_call["closure_status"] == "messages_candidate"

    named_instance_call = _callsite(
        payload,
        "Demo/StaticProducerCases.cs",
        'ParentObject.EmitMessage(Message: "Named instance emit")',
    )
    assert _text_args(named_instance_call)[0]["formal_index"] == 0
    assert named_instance_call["closure_status"] == "messages_candidate"


def test_broad_add_player_message_receivers_are_owner_or_runtime_not_messages_candidates() -> None:
    """AddPlayerMessage is discovered broadly but never auto-classed as messages-ready."""
    payload = _payload()

    unqualified = _callsite(payload, "Demo/StaticProducerCases.cs", 'AddPlayerMessage("Player static")')
    assert unqualified["target_surface"] == "AddPlayerMessage"
    assert unqualified["closure_status"] == "owner_patch_required"
    assert _text_args(unqualified)[0]["expression_kind"] == "static_literal"

    receiver = _callsite(payload, "Demo/StaticProducerCases.cs", 'The.Player.AddPlayerMessage($"Player {name}")')
    assert receiver["target_surface"] == "AddPlayerMessage"
    assert receiver["closure_status"] == "owner_patch_required"
    assert _text_args(receiver)[0]["expression_kind"] == "literal_template"


def test_popup_roles_include_fail_keybind_option_list_and_ignored_show_title() -> None:
    """Popup.Show* role extraction follows the method-specific text argument table."""
    payload = _payload()

    show = _callsite(payload, "Demo/StaticProducerCases.cs", 'Popup.Show("Popup body", Title: "Ignored title")')
    assert show["closure_status"] == "messages_candidate"
    assert [arg["expression"] for arg in _text_args(show)] == ['"Popup body"']

    assert _callsite(payload, "Demo/StaticProducerCases.cs", 'Popup.ShowFail("Failure text")')[
        "closure_status"
    ] == "messages_candidate"
    assert _callsite(payload, "Demo/StaticProducerCases.cs", 'Popup.ShowFailAsync($"Failure {name}")')[
        "closure_status"
    ] == "owner_patch_required"
    assert _callsite(payload, "Demo/StaticProducerCases.cs", 'Popup.ShowKeybindAsync("Press key")')[
        "closure_status"
    ] == "messages_candidate"

    with_copy = _callsite(payload, "Demo/StaticProducerCases.cs", 'Popup.ShowBlockWithCopy("Copy body"')
    assert [(arg["role"], arg["formal_index"], arg["expression"]) for arg in _text_args(with_copy)] == [
        ("message", 0, '"Copy body"'),
        ("prompt", 1, '"Copy prompt"'),
        ("title", 2, '"Copy title"'),
        ("copy_info", 3, '"Copy payload"'),
    ]

    option_list = _callsite(payload, "Demo/StaticProducerCases.cs", "Popup.ShowOptionList(")
    assert [(arg["role"], arg["formal_index"], arg["expression"]) for arg in _text_args(option_list)] == [
        ("title", 0, '"Option title"'),
        ("options", 1, "options"),
        ("intro", 4, '"Option intro"'),
        ("spacing_text", 9, '"Spacing text"'),
        ("buttons", 14, '"Buttons text"'),
    ]
    assert option_list["closure_status"] == "runtime_required"

    unknown = _callsite(payload, "Demo/StaticProducerCases.cs", "Popup.ShowExperimental()")
    assert unknown["text_arguments"] == []
    assert unknown["closure_status"] == "runtime_required"
    assert unknown.get("closure_reason") == "unknown_popup_show_signature"


def test_popup_cs_forwarding_is_sink_but_internal_literal_is_candidate() -> None:
    """Popup.cs forwarding wrappers are sinks while internal literals remain producers."""
    payload = _payload()

    forwarded = _exact_callsite(payload, "XRL.UI/Popup.cs", "Show(Message)")
    assert forwarded["closure_status"] == "sink_observed_only"
    assert _text_args(forwarded)[0]["expression_kind"] == "forwarded_parameter"

    internal = _callsite(payload, "XRL.UI/Popup.cs", 'Popup.Show("Popup internal literal")')
    assert internal["closure_status"] == "messages_candidate"


def test_closure_overrides_cover_wrappers_debug_and_marker_gated_paths() -> None:
    """Closure overrides handle wrapper paths, debug paths, and diagnostic markers."""
    payload = _payload()

    icomponent = _callsite(payload, "XRL.World/IComponent.cs", "Messaging.EmitMessage(Source, Message)")
    assert icomponent["closure_status"] == "sink_observed_only"

    queue = _callsite(payload, "XRL.Messages/MessageQueue.cs", "AddPlayerMessage(Message)")
    assert queue["closure_status"] == "sink_observed_only"

    wish = _callsite(payload, "XRL.Wish/WishCommand.cs", 'ParentObject.EmitMessage("Wish debug text")')
    assert wish["closure_status"] == "debug_ignore"

    population_owner = _callsite(payload, "XRL/PopulationManager.cs", 'AddPlayerMessage("Population owner text")')
    assert population_owner["closure_status"] == "owner_patch_required"

    population_debug = _callsite(
        payload,
        "XRL/PopulationManager.cs",
        'AddPlayerMessage("Debug: population diagnostic")',
    )
    assert population_debug["closure_status"] == "debug_ignore"

    biome_debug = _callsite(payload, "XRL.World.Biomes/BiomeManager.cs", 'Popup.Show("biome: " + id)')
    assert biome_debug["closure_status"] == "debug_ignore"


def test_unknown_and_stringbuilder_expressions_are_runtime_required() -> None:
    """Procedural expressions and StringBuilder-style values require runtime evidence."""
    payload = _payload()

    dynamic_popup = _callsite(payload, "Demo/StaticProducerCases.cs", "Popup.Show(dynamicMessage)")
    assert _text_args(dynamic_popup)[0]["expression_kind"] == "procedural_or_unknown"
    assert dynamic_popup["closure_status"] == "runtime_required"

    emit_stringbuilder = _callsite(
        payload,
        "Demo/StaticProducerCases.cs",
        "EmitMessage(E.Actor, stringBuilder2, false)",
    )
    assert emit_stringbuilder["closure_status"] == "runtime_required"


def test_callsite_and_family_aggregation_group_same_member_and_flag_mixed_statuses() -> None:
    """Families group by member and surface mixed actionable/runtime statuses."""
    payload = _payload()
    families = {family["producer_family_id"]: family for family in _families(payload)}

    same_member_id = "Demo/StaticProducerCases.cs::Demo.StaticProducerCases.SameMember"
    other_member_id = "Demo/StaticProducerCases.cs::Demo.StaticProducerCases.OtherMember"
    property_id = "Demo/StaticProducerCases.cs::Demo.StaticProducerCases.PropertyProducer"

    assert families[same_member_id]["callsite_count"] > 1
    assert families[same_member_id]["family_closure_status"] == "needs_family_review"
    assert "Popup.Show*" in families[same_member_id]["surface_counts"]
    assert "runtime_required" in families[same_member_id]["closure_status_counts"]
    assert families[same_member_id]["representative_calls"]
    assert families[other_member_id]["callsite_count"] == 1
    assert families[other_member_id]["family_closure_status"] == "messages_candidate"
    assert families[property_id]["member_kind"] == "property"
    assert families[property_id]["family_closure_status"] == "messages_candidate"

    delegate_call = _callsite(payload, "Demo/StaticProducerCases.cs", 'Popup.Show("Delegate body")')
    assert delegate_call["producer_family_id"] == same_member_id
    assert not any(str(family_id).endswith(".delegate") for family_id in families)

    totals = payload["totals"]
    assert totals["callsite_statuses"]["runtime_required"] >= 1
    assert totals["family_statuses"]["needs_family_review"] >= 1


def test_missing_source_root_exits_1_with_explicit_stderr(tmp_path: Path, capsys: _Capsys) -> None:
    """A missing source root exits cleanly without creating output."""
    output_path = tmp_path / "inventory.json"
    missing_root = tmp_path / "missing-source"

    result = main(["--source-root", str(missing_root), "--output", str(output_path)])
    captured = capsys.readouterr()

    assert result == 1
    assert "source root does not exist or is not a directory" in captured.err
    assert str(missing_root) in captured.err
    assert captured.out == ""
    assert not output_path.exists()
