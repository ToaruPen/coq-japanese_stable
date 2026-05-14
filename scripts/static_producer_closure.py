"""Current-repo closure overlay for the static producer inventory."""

from __future__ import annotations

import json
import sys
from argparse import ArgumentParser
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Final, Literal, TypedDict, cast

if TYPE_CHECKING:
    from scripts.scan_static_producer_inventory import FamilyPayload, InventoryPayload

REPO_ROOT: Final = Path(__file__).resolve().parents[1]
DEFAULT_INVENTORY_PATH: Final = REPO_ROOT / "docs" / "static-producer-inventory.json"
COVERED_BY_OWNER_PATCH: Final = "covered_by_owner_patch"
OWNER_ACTION_STATUSES: Final = frozenset({"owner_patch_required", "needs_family_review"})
HACKING_SIFRAH_RESULT_SIGNATURE_SUFFIX: Final = (
    "System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.HackingSifrah"
)
CRIPPLE_APPLY_TARGET_METHOD_FULL_SIGNATURE: Final = (
    "XRL.World.Effects.Cripple|Apply|System.Boolean|XRL.World.GameObject"
)
COMBAT_MELEE_ATTACK_SIGNATURE_PARTS: Final = (
    "XRL.World.Parts.Combat",
    "MeleeAttackWithWeaponInternal",
    "XRL.World.Parts.MeleeAttackResult",
    "XRL.World.GameObject",
    "XRL.World.GameObject",
    "XRL.World.GameObject",
    "XRL.World.Anatomy.BodyPart",
    "System.String",
    "System.Int32",
    "System.Int32",
    "System.Int32",
    "System.Int32",
    "System.Int32",
    "System.Boolean",
    "System.Boolean",
)
COMBAT_MELEE_ATTACK_FULL_SIGNATURE: Final = "|".join(COMBAT_MELEE_ATTACK_SIGNATURE_PARTS)
OutputFormat = Literal["text", "json"]


class OwnerActionQueueEntry(TypedDict):
    """Actionable static-producer owner work for one producer family."""

    source_file: str
    producer_family_id: str
    type_name: str
    member_name: str
    member_start_line: int
    family_closure_status: str
    callsite_count: int
    text_argument_count: int
    surface_counts: dict[str, int]
    closure_status_counts: dict[str, int]
    representative_lines: list[int]


class SourceFileQueueEntry(TypedDict):
    """Actionable static-producer owner work grouped by decompiled C# source file."""

    source_file: str
    family_count: int
    callsite_count: int
    text_argument_count: int
    family_statuses: dict[str, int]
    surface_counts: dict[str, int]
    families: list[OwnerActionQueueEntry]


@dataclass(frozen=True)
class EvidenceFile:
    """A source or test file that must contain evidence for a covered family."""

    path: str
    required_substrings: tuple[str, ...]


@dataclass(frozen=True)
class CoveredOwnerFamily:
    """A producer family that is closed by current owner-patch tests."""

    family_id: str
    inventory_statuses: tuple[str, ...]
    evidence_files: tuple[EvidenceFile, ...]


@dataclass(frozen=True)
class OwnerPopupRouteEvidenceSpec:
    """Shared evidence anchors for owner-popup route closure."""

    patch_file: str
    patch_type: str
    test_file: str
    positive_test: str
    negative_test: str
    direct_marker_test: str
    empty_test: str


@dataclass(frozen=True)
class SifrahResultPopupFamilySpec:
    """Shared metadata for Sifrah result popup closure families."""

    source_file: str
    type_name: str
    evidence: OwnerPopupRouteEvidenceSpec
    target_type_name: str
    method_details: tuple[tuple[str, str], ...]


def _owner_popup_route_evidence(
    *,
    spec: OwnerPopupRouteEvidenceSpec,
    target_method_token: str,
    full_signature: str,
    patch_required_substrings: tuple[str, ...],
) -> tuple[EvidenceFile, ...]:
    return (
        EvidenceFile(
            spec.patch_file,
            ("TryTranslatePopupMessage", *patch_required_substrings),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
            (f"{spec.patch_type}.TryTranslatePopupMessage",),
        ),
        EvidenceFile(
            spec.test_file,
            (
                spec.positive_test,
                spec.negative_test,
                spec.direct_marker_test,
                spec.empty_test,
                target_method_token,
            ),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
            (
                "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                full_signature,
            ),
        ),
    )


def _sifrah_result_popup_families(spec: SifrahResultPopupFamilySpec) -> tuple[CoveredOwnerFamily, ...]:
    return tuple(
        CoveredOwnerFamily(
            family_id=f"{spec.source_file}::{spec.type_name}.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=_owner_popup_route_evidence(
                spec=spec.evidence,
                target_method_token=f"nameof({spec.target_type_name}.{method_name})",
                full_signature=f"{spec.type_name}|{method_name}|System.Void|XRL.World.GameObject",
                patch_required_substrings=(method_name, detail),
            ),
        )
        for method_name, detail in spec.method_details
    )


def _examiner_result_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    evidence = OwnerPopupRouteEvidenceSpec(
        patch_file="Mods/QudJP/Assemblies/src/Patches/ExaminerTranslationPatch.cs",
        patch_type="ExaminerTranslationPatch",
        test_file="Mods/QudJP/Assemblies/QudJP.Tests/L2/ExaminerTranslationPatchTests.cs",
        positive_test="Patch_TranslatesExaminerResultPopups_WhenOwnerPatched",
        negative_test="Patch_DoesNotTranslateExaminerPopup_WhenOwnerAbsent",
        direct_marker_test="Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
        empty_test="Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
    )
    method_details = (
        ("ResultSuccess", "Understand"),
        ("ResultExceptionalSuccess", "DiscoverHidden"),
        ("ResultFailure", "Puzzled"),
        ("ResultFakeConfusionFailure", "Broke"),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/Examiner.cs::XRL.World.Parts.Examiner.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=_owner_popup_route_evidence(
                spec=evidence,
                target_method_token=f"nameof(DummyExaminerProducerTarget.{method_name})",
                full_signature=f"XRL.World.Parts.Examiner|{method_name}|System.Void|XRL.World.GameObject",
                patch_required_substrings=(method_name, detail),
            ),
        )
        for method_name, detail in method_details
    )


def _hacking_sifrah_result_families() -> tuple[CoveredOwnerFamily, ...]:
    target_sets = (
        (
            "XRL.World.Parts/Door.cs",
            "XRL.World.Parts.Door",
            (
                "HackingResultSuccess",
                "HackingResultExceptionalSuccess",
                "HackingResultPartialSuccess",
                "HackingResultFailure",
                "HackingResultCriticalFailure",
            ),
        ),
        (
            "XRL.World.Parts/PowerSwitch.cs",
            "XRL.World.Parts.PowerSwitch",
            (
                "HackingResultSuccess",
                "HackingResultExceptionalSuccess",
                "HackingResultPartialSuccess",
                "HackingResultFailure",
                "HackingResultCriticalFailure",
            ),
        ),
        (
            "XRL.World.Parts/TemplarPhylactery.cs",
            "XRL.World.Parts.TemplarPhylactery",
            (
                "HackingResultSuccess",
                "HackingResultExceptionalSuccess",
                "HackingResultPartialSuccess",
                "HackingResultFailure",
                "HackingResultCriticalFailure",
            ),
        ),
        (
            "XRL.World.Parts/CyberneticsTerminal2.cs",
            "XRL.World.Parts.CyberneticsTerminal2",
            (
                "HackingResultExceptionalSuccess",
                "HackingResultFailure",
                "HackingResultCriticalFailure",
            ),
        ),
    )

    families: list[CoveredOwnerFamily] = []
    for source_file, type_name, method_names in target_sets:
        for method_name in method_names:
            signature = f"{type_name}|{method_name}|{HACKING_SIFRAH_RESULT_SIGNATURE_SUFFIX}"
            families.append(
                CoveredOwnerFamily(
                    family_id=f"{source_file}::{type_name}.{method_name}",
                    inventory_statuses=("owner_patch_required",),
                    evidence_files=(
                        EvidenceFile(
                            "Mods/QudJP/Assemblies/src/Patches/HackingSifrahResultTranslationPatch.cs",
                            (method_name, "TryTranslatePopupMessage"),
                        ),
                        EvidenceFile(
                            "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                            ("HackingSifrahResultTranslationPatch.TryTranslatePopupMessage",),
                        ),
                        EvidenceFile(
                            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                            (
                                "HackingSifrahResult_TranslatesPopupMessages_WhenOwnerPatched",
                                "HackingSifrahResult_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                                "HackingSifrahResult_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                                "HackingSifrahResult_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                            ),
                        ),
                        EvidenceFile(
                            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                            (
                                "typeof(HackingSifrahResultTranslationPatch)",
                                signature,
                            ),
                        ),
                    ),
                )
            )
    return tuple(families)


def _quest_lifecycle_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        ("ShowStartPopup", "XRL.World.Quest|ShowStartPopup|System.Void"),
        ("ShowFailPopup", "XRL.World.Quest|ShowFailPopup|System.Void"),
        ("ShowFailStepPopup", "XRL.World.Quest|ShowFailStepPopup|System.Void|XRL.World.QuestStep"),
        ("ShowFinishPopup", "XRL.World.Quest|ShowFinishPopup|System.Void"),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World/Quest.cs::XRL.World.Quest.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/QuestLifecyclePopupTranslationPatch.cs",
                    (method_name, "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("QuestLifecyclePopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "QuestLifecyclePopup_TranslatesPopupMessages_WhenOwnerPatched",
                        "QuestLifecyclePopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "QuestLifecyclePopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "QuestLifecyclePopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(QuestLifecyclePopupTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _flight_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "StartFlying",
            "XRL.World.Capabilities.Flight|StartFlying|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Capabilities.IFlightSource",
        ),
        (
            "StopFlying",
            "XRL.World.Capabilities.Flight|StopFlying|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Capabilities.IFlightSource|System.Boolean|System.Boolean",
        ),
        (
            "Land",
            "XRL.World.Capabilities.Flight|Land|System.Void|XRL.World.GameObject|System.Boolean",
        ),
        (
            "FailFlying",
            "XRL.World.Capabilities.Flight|FailFlying|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Capabilities.IFlightSource",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Capabilities/Flight.cs::XRL.World.Capabilities.Flight.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/FlightTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("FlightTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Flight_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Flight_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Flight_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "Flight_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(FlightTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _signature(*parts: str) -> str:
    return "|".join(parts)


def _body_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        ("CheckUnsupportedPartLoss", "XRL.World.Parts.Body|CheckUnsupportedPartLoss|System.Void"),
        ("CheckPartRecovery", "XRL.World.Parts.Body|CheckPartRecovery|System.Void"),
        (
            "Dismember",
            _signature(
                "XRL.World.Parts.Body",
                "Dismember",
                "XRL.World.GameObject",
                "XRL.World.Anatomy.BodyPart",
                "XRL.World.GameObject",
                "XRL.World.IInventory",
                "System.Boolean",
                "System.Boolean",
                "XRL.World.IEvent",
            ),
        ),
        (
            "RegenerateLimb",
            _signature(
                "XRL.World.Parts.Body",
                "RegenerateLimb",
                "System.Boolean",
                "System.Boolean",
                "XRL.World.Parts.Body+DismemberedPart",
                "System.Nullable`1[[System.Int32]]",
                "System.Nullable`1[[System.Int32]]",
                "System.Int32[]",
                "System.Nullable`1[[System.Int32]]",
                "System.Int32[]",
                "System.Boolean",
            ),
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/Body.cs::XRL.World.Parts.Body.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BodyTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("BodyTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("BodyTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Body_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Body_TranslatesDismemberPopup_WhenOwnerPatched",
                        "Body_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Body_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Body_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "Body_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Body_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BodyTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _item_modding_sifrah_result_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        ("ResultFailure", "XRL.World.ItemModdingSifrah|ResultFailure|System.Void|XRL.World.GameObject"),
        ("ResultPartialSuccess", "XRL.World.ItemModdingSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject"),
        ("ResultSuccess", "XRL.World.ItemModdingSifrah|ResultSuccess|System.Void|XRL.World.GameObject"),
        ("ResultCriticalSuccess", "XRL.World.ItemModdingSifrah|ResultCriticalSuccess|System.Void|XRL.World.GameObject"),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World/ItemModdingSifrah.cs::XRL.World.ItemModdingSifrah.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/ItemModdingSifrahTranslationPatch.cs",
                    (method_name, "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("ItemModdingSifrahTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "ItemModdingSifrah_TranslatesResultPopups_WhenOwnerPatched",
                        "ItemModdingSifrah_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "ItemModdingSifrah_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "ItemModdingSifrah_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ItemModdingSifrahTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _sifrah_pure_owner_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    targets = (
        (
            "XRL.World/BaetylOfferingSifrah.cs::XRL.World.BaetylOfferingSifrah.BaetylOfferingSifrah",
            "BaetylOfferingSifrah",
            "BaetylOfferingSifrah",
            "BaetylOffering",
            "XRL.World.BaetylOfferingSifrah",
            "XRL.World.BaetylOfferingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/FormalWaterRitualSifrah.cs::XRL.World.FormalWaterRitualSifrah.FormalWaterRitualSifrah",
            "FormalWaterRitualSifrah",
            "FormalWaterRitualSifrah",
            "FormalWaterRitual",
            "XRL.World.FormalWaterRitualSifrah",
            "XRL.World.FormalWaterRitualSifrah|.ctor|System.Void|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/HagglingSifrah.cs::XRL.World.HagglingSifrah.HagglingSifrah",
            "HagglingSifrah",
            "HagglingSifrah",
            "Haggling",
            "XRL.World.HagglingSifrah",
            "XRL.World.HagglingSifrah|.ctor|System.Void|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/DisarmingSifrah.cs::XRL.World.DisarmingSifrah.DisarmingSifrah",
            "DisarmingSifrah",
            "DisarmingSifrah",
            "Disarming",
            "XRL.World.DisarmingSifrah",
            "XRL.World.DisarmingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Boolean",
            ("needs_family_review",),
        ),
        (
            "XRL.World/ExamineSifrah.cs::XRL.World.ExamineSifrah.ExamineSifrah",
            "ExamineSifrah",
            "ExamineSifrah",
            "Examine",
            "XRL.World.ExamineSifrah",
            "XRL.World.ExamineSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32|System.Int32",
            ("needs_family_review",),
        ),
        (
            "XRL.World/HackingSifrah.cs::XRL.World.HackingSifrah.HackingSifrah",
            "HackingSifrah",
            "HackingSifrah",
            "Hacking",
            "XRL.World.HackingSifrah",
            "XRL.World.HackingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32",
            ("needs_family_review",),
        ),
        (
            "XRL.World/ProselytizationSifrah.cs::XRL.World.ProselytizationSifrah.ProselytizationSifrah",
            "ProselytizationSifrah",
            "ProselytizationSifrah",
            "Proselytization",
            "XRL.World.ProselytizationSifrah",
            "XRL.World.ProselytizationSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
            ("needs_family_review",),
        ),
        (
            "XRL.World/RebukingSifrah.cs::XRL.World.RebukingSifrah.RebukingSifrah",
            "RebukingSifrah",
            "RebukingSifrah",
            "Rebuking",
            "XRL.World.RebukingSifrah",
            "XRL.World.RebukingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
            ("needs_family_review",),
        ),
        (
            "XRL.World/ItemModdingSifrah.cs::XRL.World.ItemModdingSifrah.ItemModdingSifrah",
            "ItemModdingSifrah",
            "ItemModdingSifrah",
            "ItemModding",
            "XRL.World.ItemModdingSifrah",
            "XRL.World.ItemModdingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/ItemNamingSifrah.cs::XRL.World.ItemNamingSifrah.ItemNamingSifrah",
            "ItemNamingSifrah",
            "ItemNamingSifrah",
            "ItemNaming",
            "XRL.World.ItemNamingSifrah",
            "XRL.World.ItemNamingSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/RepairSifrah.cs::XRL.World.RepairSifrah.RepairSifrah",
            "RepairSifrah",
            "RepairSifrah",
            "Repair",
            "XRL.World.RepairSifrah",
            "XRL.World.RepairSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32",
            ("needs_family_review",),
        ),
        (
            "XRL.World/PsychicCombatSifrah.cs::XRL.World.PsychicCombatSifrah.PsychicCombatSifrah",
            "PsychicCombatSifrah",
            "PsychicCombatSifrah",
            "PsychicCombat",
            "XRL.World.PsychicCombatSifrah",
            "XRL.World.PsychicCombatSifrah|.ctor|System.Void|XRL.World.GameObject|System.String|System.Int32|System.Int32|System.String",
            ("needs_family_review",),
        ),
        (
            "XRL.World/RealityDistortionSifrah.cs::XRL.World.RealityDistortionSifrah.RealityDistortionSifrah",
            "RealityDistortionSifrah",
            "RealityDistortionSifrah",
            "RealityDistortion",
            "XRL.World.RealityDistortionSifrah",
            "XRL.World.RealityDistortionSifrah|.ctor|System.Void|XRL.World.GameObject|System.String|System.String|System.Int32|System.Int32",
            ("needs_family_review",),
        ),
        (
            "XRL.World/ReverseEngineeringSifrah.cs::XRL.World.ReverseEngineeringSifrah.ReverseEngineeringSifrah",
            "ReverseEngineeringSifrah",
            "ReverseEngineeringSifrah",
            "ReverseEngineering",
            "XRL.World.ReverseEngineeringSifrah",
            "XRL.World.ReverseEngineeringSifrah|.ctor|System.Void|XRL.World.GameObject|System.Int32|System.Int32|System.Int32|XRL.World.Tinkering.TinkerData",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/ReverseEngineeringSifrah.cs::XRL.World.ReverseEngineeringSifrah.CheckEarlyExit",
            "CheckEarlyExit",
            "ReverseEngineeringCheckEarlyExit",
            "ReverseEngineeringEarlyExit",
            "XRL.World.ReverseEngineeringSifrah",
            "XRL.World.ReverseEngineeringSifrah|CheckEarlyExit|System.Boolean|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/RitualSifrahTokenAttributeSacrifice.cs::XRL.World.RitualSifrahTokenAttributeSacrifice.CheckTokenUse",
            "CheckTokenUse",
            "RitualAttributeSacrificeCheckTokenUse",
            "AttributeSacrifice",
            "XRL.World.RitualSifrahTokenAttributeSacrifice",
            "XRL.World.RitualSifrahTokenAttributeSacrifice|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/RitualSifrahTokenInvokeHigherBeing.cs::XRL.World.RitualSifrahTokenInvokeHigherBeing.CheckTokenUse",
            "CheckTokenUse",
            "RitualInvokeHigherBeingCheckTokenUse",
            "InvokeHigherBeing",
            "XRL.World.RitualSifrahTokenInvokeHigherBeing",
            "XRL.World.RitualSifrahTokenInvokeHigherBeing|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/SocialSifrahTokenSecret.cs::XRL.World.SocialSifrahTokenSecret.CheckTokenUse",
            "CheckTokenUse",
            "SocialSecretCheckTokenUse",
            "SocialSecret",
            "XRL.World.SocialSifrahTokenSecret",
            "XRL.World.SocialSifrahTokenSecret|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/TinkeringSifrahTokenBit.cs::XRL.World.TinkeringSifrahTokenBit.CheckTokenUse",
            "CheckTokenUse",
            "TinkeringBitCheckTokenUse",
            "TinkeringBit",
            "XRL.World.TinkeringSifrahTokenBit",
            "XRL.World.TinkeringSifrahTokenBit|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/TinkeringSifrahTokenCharge.cs::XRL.World.TinkeringSifrahTokenCharge.CheckTokenUse",
            "CheckTokenUse",
            "TinkeringChargeCheckTokenUse",
            "TinkeringCharge",
            "XRL.World.TinkeringSifrahTokenCharge",
            "XRL.World.TinkeringSifrahTokenCharge|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/TinkeringSifrahTokenComputePower.cs::XRL.World.TinkeringSifrahTokenComputePower.CheckTokenUse",
            "CheckTokenUse",
            "TinkeringComputePowerCheckTokenUse",
            "TinkeringComputePower",
            "XRL.World.TinkeringSifrahTokenComputePower",
            "XRL.World.TinkeringSifrahTokenComputePower|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL.World/TinkeringSifrahTokenLiquid.cs::XRL.World.TinkeringSifrahTokenLiquid.CheckTokenUse",
            "CheckTokenUse",
            "TinkeringLiquidCheckTokenUse",
            "TinkeringLiquid",
            "XRL.World.TinkeringSifrahTokenLiquid",
            "XRL.World.TinkeringSifrahTokenLiquid|CheckTokenUse|System.Boolean|XRL.SifrahGame|XRL.SifrahSlot|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
        (
            "XRL/SifrahGame.cs::XRL.SifrahGame.MakeMoveForSlot",
            "MakeMoveForSlot",
            "SifrahGameMakeMoveForSlot",
            "MakeMoveForSlot",
            "XRL.SifrahGame",
            "XRL.SifrahGame|MakeMoveForSlot|System.Boolean|System.Int32|XRL.World.GameObject",
            ("owner_patch_required",),
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=family_id,
            inventory_statuses=inventory_statuses,
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SifrahPureOwnerPopupTranslationPatch.cs",
                    (target_token, detail, "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    (
                        "SifrahPureOwnerPopupTranslationPatch.TryTranslatePopupMessage",
                        "SifrahPureOwnerPopupTranslationPatch.TryGetPureOwnerBatchPopupCandidateText",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SifrahPureOwnerPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSifrahPureOwnerPopups_WhenOwnerPatched",
                        "Patch_DoesNotTranslateSifrahPureOwnerPopup_WhenOwnerAbsent",
                        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    )
                    + (
                        (
                            "Patch_DoesNotTranslateConstructorOwnerPopup_WhenOwnerAbsent",
                            "Patch_TranslatesMasteredPrompt_WhenOwnerPatched",
                            f'"{detail}")',
                        )
                        if "needs_family_review" in inventory_statuses
                        else ()
                    )
                    + (
                        f"nameof(DummySifrahPureOwnerPopupProducerTarget.{dummy_target_token})",
                        detail,
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SifrahPureOwnerPopupTranslationPatch)",
                        declaring_type,
                        target_token,
                        full_signature,
                    ),
                ),
            ),
        )
        for (
            family_id,
            target_token,
            dummy_target_token,
            detail,
            declaring_type,
            full_signature,
            inventory_statuses,
        ) in targets
    )


def _sunder_mind_owner_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        ("CancelSunder", "XRL.World.Parts.Mutation.SunderMind|CancelSunder|System.Void"),
        ("BeginSunder", "XRL.World.Parts.Mutation.SunderMind|BeginSunder|System.Void|XRL.World.GameObject"),
        (
            "PenetrationFailure",
            "XRL.World.Parts.Mutation.SunderMind|PenetrationFailure|System.Void|XRL.World.GameObject",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts.Mutation/SunderMind.cs::XRL.World.Parts.Mutation.SunderMind.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SunderMindTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("SunderMindTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("SunderMindTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "SunderMind_TranslatesQueuedMessages_WhenOwnerPatched",
                        "SunderMind_TranslatesBeginSunderQueuedMessage_WhenOwnerPatched",
                        "SunderMind_TranslatesBeginSunderPopup_WhenOwnerPatched",
                        "SunderMind_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "SunderMind_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "SunderMind_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "SunderMind_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "SunderMind_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SunderMindTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _keybinds_screen_conflict_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "ConfirmConflictBind",
            "Qud.UI.KeybindsScreen|ConfirmConflictBind|System.Threading.Tasks.Task`1[[System.Boolean]]|System.String|System.Collections.Generic.List`1[[XRL.UI.GameCommand]]|System.String",
        ),
        (
            "ConfirmDynamicConflictBind",
            "Qud.UI.KeybindsScreen|ConfirmDynamicConflictBind|System.Threading.Tasks.Task`1[[System.Boolean]]|System.String|System.Collections.Generic.List`1[[XRL.UI.GameCommand]]|System.String",
        ),
        (
            "RequiredConflictBind",
            "Qud.UI.KeybindsScreen|RequiredConflictBind|System.Threading.Tasks.Task|System.String|System.String",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"Qud.UI/KeybindsScreen.cs::Qud.UI.KeybindsScreen.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/KeybindsScreenConflictTranslationPatch.cs",
                    (method_name, "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("KeybindsScreenConflictTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
                    ("ShowAsync",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "KeybindsScreenConflict_TranslatesConfirmPopups_WhenOwnerPatched",
                        "KeybindsScreenConflict_TranslatesRequiredConflictPopup_WhenOwnerPatched",
                        "KeybindsScreenConflict_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "KeybindsScreenConflict_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "KeybindsScreenConflict_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(KeybindsScreenConflictTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _ability_manager_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("AbilityManagerPopupTranslationPatch.TryTranslatePopupMessage",),
    )
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/AbilityManagerPopupTranslationPatch.cs",
        ("AbilityManagerPopupTranslationPatch", "TryTranslatePopupMessage"),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbilityManagerScreenTranslationPatchTests.cs",
        (
            "PopupPrefix_TranslatesNoFilteredAbilitiesMessage_WhenOwnerPatched",
            "PopupPrefix_TranslatesKeybindPrompt_WhenOwnerPatched",
            "PopupPrefix_TranslatesRebindConflictMessages_WhenOwnerPatched",
            "PopupPrefix_TranslatesRemoveBindConfirmation_WhenOwnerPatched",
            "PopupPrefix_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "PopupPrefix_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "PopupPrefix_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="Qud.UI/AbilityManagerScreen.cs::Qud.UI.AbilityManagerScreen.HandleFilterItems",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AbilityManagerPopupTranslationPatch)",
                        "Qud.UI.AbilityManagerScreen|HandleFilterItems|System.Void",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="Qud.UI/AbilityManagerScreen.cs::Qud.UI.AbilityManagerScreen.HandleRebindAsync",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AbilityManagerPopupTranslationPatch)",
                        "Qud.UI.AbilityManagerScreen",
                        "HandleRebindAsync",
                        "Qud.UI.AbilityManagerScreen+<HandleRebindAsync>",
                        "MoveNext|System.Void",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="Qud.UI/AbilityManagerScreen.cs::Qud.UI.AbilityManagerScreen.HandleRemoveBindAsync",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AbilityManagerPopupTranslationPatch)",
                        "Qud.UI.AbilityManagerScreen",
                        "HandleRemoveBindAsync",
                        "Qud.UI.AbilityManagerScreen+<HandleRemoveBindAsync>",
                        "MoveNext|System.Void",
                    ),
                ),
            ),
        ),
    )


def _cooking_runtime_families() -> tuple[CoveredOwnerFamily, ...]:
    pipeline_popup = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("CookingRuntimeTranslationPatch.TryTranslatePopupMessage",),
    )
    pipeline_queue = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("CookingRuntimeTranslationPatch.TryTranslateQueuedMessage",),
    )
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/CookingRuntimeTranslationPatch.cs",
        (
            "CookingRuntimeTranslationPatch",
            "TryTranslatePopupMessage",
            "TryTranslateQueuedMessage",
            "ModBlinkEscape",
        ),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CookingRuntimeTranslationPatchTests.cs",
        (
            "BasicCookingPopup_TranslatesRuntimeWellFedMessages_WhenOwnerPatched",
            "SpecialCookingPopup_TranslatesRuntimeMessages_WhenOwnerPatched",
            "CookingQueuedMessage_TranslatesRuntimeMessages_WhenOwnerPatched",
            "CookingQueuedMessage_TranslatesModBlinkEscapeFateIntervenes_WhenOwnerPatched",
            "CheckBlinkEscape",
            "CookingPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "CookingRuntime_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
            "CookingRuntime_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    def resolution(declaring_type: str, member_name: str) -> EvidenceFile:
        return EvidenceFile(
            "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
            (
                "CookingRuntimeTranslationPatch_TargetMethods_ResolveExpectedOwners",
                f"{declaring_type}|{member_name}|",
            ),
        )

    def owner_parts(family_id: str) -> tuple[str, str]:
        owner = family_id.split("::", maxsplit=1)[1]
        declaring_type, member_name = owner.rsplit(".", maxsplit=1)
        return declaring_type, member_name

    popup_family_ids = (
        "XRL.World.Conversations.Parts/WaterRitualCookingRecipe.cs::XRL.World.Conversations.Parts.WaterRitualCookingRecipe.HandleEvent",
        "XRL.World.Skills.Cooking/CookingRecipe.cs::XRL.World.Skills.Cooking.CookingRecipe.ApplyEffectsTo",
        "XRL.World.Effects/BasicCookingEffect_Hitpoints.cs::XRL.World.Effects.BasicCookingEffect_Hitpoints.ApplyEffect",
        "XRL.World.Effects/BasicCookingEffect_MA.cs::XRL.World.Effects.BasicCookingEffect_MA.ApplyEffect",
        "XRL.World.Effects/BasicCookingEffect_MS.cs::XRL.World.Effects.BasicCookingEffect_MS.ApplyEffect",
        "XRL.World.Effects/BasicCookingEffect_Quickness.cs::XRL.World.Effects.BasicCookingEffect_Quickness.ApplyEffect",
        "XRL.World.Effects/BasicCookingEffect_RandomStat.cs::XRL.World.Effects.BasicCookingEffect_RandomStat.ApplyEffect",
        "XRL.World.Effects/BasicCookingEffect_Regeneration.cs::XRL.World.Effects.BasicCookingEffect_Regeneration.ApplyEffect",
        "XRL.World.Effects/BasicCookingEffect_ToHit.cs::XRL.World.Effects.BasicCookingEffect_ToHit.ApplyEffect",
        "XRL.World.Effects/BasicCookingEffect_XP.cs::XRL.World.Effects.BasicCookingEffect_XP.ApplyEffect",
    )
    queue_family_ids = (
        "XRL.World.Effects/CookingDomainReflect_UnitReflectDamage.cs::XRL.World.Effects.CookingDomainReflect_UnitReflectDamage.FireEvent",
        "XRL.World.Effects/CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect.cs::XRL.World.Effects.CookingDomainReflect_Reflect100_ProceduralCookingTriggeredAction_Effect.FireEvent",
        "XRL.World.Parts/ReflectDamage.cs::XRL.World.Parts.ReflectDamage.HandleEvent",
        "XRL.World.Parts/ModBlinkEscape.cs::XRL.World.Parts.ModBlinkEscape.CheckBlinkEscape",
        "XRL.World.Effects/CookingDomainTeleport_UnitBlink.cs::XRL.World.Effects.CookingDomainTeleport_UnitBlink.FireEvent",
        "XRL.World.Effects/NoPhase_ProceduralCookingTriggeredAction_Effect.cs::XRL.World.Effects.NoPhase_ProceduralCookingTriggeredAction_Effect.FireEvent",
    )
    return tuple(
        CoveredOwnerFamily(
            family_id=family_id,
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline_popup,
                tests,
                resolution(*owner_parts(family_id)),
            ),
        )
        for family_id in popup_family_ids
    ) + tuple(
        CoveredOwnerFamily(
            family_id=family_id,
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline_queue,
                tests,
                resolution(*owner_parts(family_id)),
            ),
        )
        for family_id in queue_family_ids
    )


def _water_ritual_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/WaterRitualPopupTranslationPatch.cs",
        (
            "WaterRitualPopupTranslationPatch",
            "TryTranslatePopupMessage",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("WaterRitualPopupTranslationPatch.TryTranslatePopupMessage",),
    )
    tests_common = (
        "Patch_TranslatesWaterRitualOwnerPopups_WhenOwnerPatched",
        "Patch_DoesNotTranslateWaterRitualPopup_WhenOwnerAbsent",
        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualBegin.cs::XRL.World.Conversations.Parts.WaterRitualBegin.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualBegin",
                        "FormalRitualPromptPattern",
                        "NotEnoughLiquidPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBeginHandleEvent)",
                        "FormalRitualPrompt",
                        "NotEnoughLiquid",
                        "Do you want to play a game of Sifrah to perform the formal water ritual",
                        "You don't have enough {{B|fresh water}} to begin the ritual.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualBegin|HandleEvent|System.Boolean|XRL.World.Conversations.EnterElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualSkillPoint.cs::XRL.World.Conversations.Parts.WaterRitualSkillPoint.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualSkillPoint",
                        "SkillPointIntroPattern",
                        "SkillPointGainPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent)",
                        "SkillPointIntro",
                        "SkillPointGain",
                        "Talking to {{Y|the warden}} rouses in you an inert truth.",
                        "You gained {{C|50}} skill points!",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualSkillPoint|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualTinkeringRecipe.cs::XRL.World.Conversations.Parts.WaterRitualTinkeringRecipe.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualTinkeringRecipe",
                        "TinkeringModPattern",
                        "TinkeringRecipePattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent)",
                        "TinkeringMod",
                        "TinkeringRecipe",
                        "{{G|Hortensa}} teaches you to craft the item modification {{W|sturdy}}.",
                        "{{G|Hortensa}} teaches you to craft {{W|spring-loaded boots}}.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualTinkeringRecipe|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualBuySecret.cs::XRL.World.Conversations.Parts.WaterRitualBuySecret.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualBuySecret",
                        "BuySecretNoMoreSecretsPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuySecretHandleEvent)",
                        "BuySecretNoMoreSecrets",
                        "{{G|Tam}} has no more secrets to share.",
                        "{{G|Tam}}にはもう共有できる秘密がない。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualBuySecret|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/IWaterRitualPart.cs::XRL.World.Conversations.Parts.IWaterRitualPart.UseReputation",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "IWaterRitualPart",
                        "UseReputation",
                        "ReputationTooLowPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.IWaterRitualPartUseReputation)",
                        "ReputationTooLow",
                        "You don't have a high enough reputation with",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.IWaterRitualPart|UseReputation|System.Boolean|System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitual.cs::XRL.World.Conversations.Parts.WaterRitual.PerformRitual",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitual",
                        "PerformRitual",
                        "PerformRitualPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualPerformRitual)",
                        "PerformRitual",
                        "You share your {{B|fresh water}} with {{G|Tam}} and begin the water ritual.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitual|PerformRitual|System.Void",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualBuyItem.cs::XRL.World.Conversations.Parts.WaterRitualBuyItem.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualBuyItem",
                        "BuyItemGiftPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBuyItemHandleEvent)",
                        "BuyItemGift",
                        "{{G|Tam}} gifts you {{Y|the electrobow}}!",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualBuyItem|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualGainMutation.cs::XRL.World.Conversations.Parts.WaterRitualGainMutation.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualGainMutation",
                        "GainMutationPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualGainMutationHandleEvent)",
                        "GainMutation",
                        "Despite your genetic limitations",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualGainMutation|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualJoinParty.cs::XRL.World.Conversations.Parts.WaterRitualJoinParty.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualJoinParty",
                        "JoinPartyPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualJoinPartyHandleEvent)",
                        "JoinParty",
                        "{{G|Tam}} joins you!",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualJoinParty|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualNephilimPacify.cs::XRL.World.Conversations.Parts.WaterRitualNephilimPacify.TryGiveCircle",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualNephilimPacify",
                        "TryGiveCircle",
                        "NephilimCirclePattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualNephilimPacifyTryGiveCircle)",
                        "NephilimCircle",
                        "You receive {{Y|an amulet}}!",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualNephilimPacify|TryGiveCircle|System.Boolean",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/WaterRitualSellSecret.cs::XRL.World.Conversations.Parts.WaterRitualSellSecret.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "WaterRitualSellSecret",
                        "SellSecretNoMoreReputationPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WaterRitualPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSellSecretHandleEvent)",
                        "SellSecretNoMoreReputation",
                        "{{G|Tam}} can't grant you any more reputation.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(WaterRitualPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.WaterRitualSellSecret|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
    )


def _popup_pick_several_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.UI/Popup.cs::XRL.UI.Popup.PickSeveral",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupPickSeveralTranslationPatch.cs",
                    (
                        "PopupPickSeveralTranslationPatch",
                        "PickSeveral",
                        "SelectionLimitPattern",
                        "TryTranslatePopupMessage",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("PopupPickSeveralTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickSeveralTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSelectionLimitPopup_WhenOwnerPatched",
                        "Patch_DoesNotTranslateSelectionLimitPopup_WhenOwnerAbsent",
                        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "You cannot select more than 3 options!",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PopupPickSeveralTranslationPatch)",
                        "XRL.UI.Popup|PickSeveral|System.Collections.Generic.List`1[[System.ValueTuple`2[[System.Int32],[System.Int32]]]]",
                    ),
                ),
            ),
        ),
    )


def _conversation_reward_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/ConversationRewardPopupTranslationPatch.cs",
        (
            "ConversationRewardPopupTranslationPatch",
            "TryTranslatePopupMessage",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("ConversationRewardPopupTranslationPatch.TryTranslatePopupMessage",),
    )
    tests_common = (
        "Patch_TranslatesConversationRewardPopups_WhenOwnerPatched",
        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
        "Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched",
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/AddSlynthCandidate.cs::XRL.World.Conversations.Parts.AddSlynthCandidate.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "AddSlynthCandidate",
                        "SlynthSanctuaryPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationRewardPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyConversationRewardProducer.AddSlynthCandidateHandleEvent)",
                        "SlynthSanctuary",
                        "now a sanctuary option for the slynth",
                        "{{Y|Grit Gate}}がスリンスの聖域候補になった。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ConversationRewardPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.AddSlynthCandidate|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/LibrarianGiveBook.cs::XRL.World.Conversations.Parts.LibrarianGiveBook.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "LibrarianGiveBook",
                        "TryTranslateLibrarianCommentary",
                        "TryTranslateLibrarianXp",
                        "DoesVerbRouteTranslator.TryTranslateMarkedMessage",
                        "DoesVerbRouteTranslator.TryTranslatePlainSentence",
                        "MessagePatternTranslator.Translate",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationRewardPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyConversationRewardProducer.LibrarianGiveBookHandleEvent)",
                        "LibrarianCommentary",
                        "LibrarianXp",
                        "Patch_TranslatesMarkedLibrarianCommentary_WhenOwnerPatched",
                        "DoesVerbRouteTranslator.MarkDoesFragment",
                        "some insightful commentary on",
                        "司書は'The Corpus Choliys'について示唆に富む解説をしてくれた。",
                        "You gain {{C|75}} XP.",
                        "あなたは経験値を{{C|75}}獲得した",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ConversationRewardPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.LibrarianGiveBook|HandleEvent|System.Boolean|XRL.World.Conversations.EnterElementEvent",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
                    (
                        "some insightful commentary on (.+)",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                    (
                        "^You gain (\\\\{\\\\{C\\\\|\\\\d+\\\\}\\\\}|\\\\d+) XP[.!]?$",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/PaxInfectLimb.cs::XRL.World.Conversations.Parts.PaxInfectLimb.InfectLimb",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "PaxInfectLimb",
                        "PaxInfectLimbPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationRewardPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyConversationRewardProducer.PaxInfectLimbInfectLimb)",
                        "PaxInfectLimb",
                        "You've contracted",
                        "left armに{{G|glowcrust}}を発症した。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ConversationRewardPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.PaxInfectLimb|InfectLimb|System.Boolean|System.Collections.Generic.List`1[[XRL.World.Anatomy.BodyPart]]|XRL.World.Anatomy.BodyPart|System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/ReceiveItem.cs::XRL.World.Conversations.Parts.ReceiveItem.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "ReceiveItem",
                        "ReceiveItemPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationRewardPopupTranslationPatchTests.cs",
                    (
                        *tests_common,
                        "nameof(DummyConversationRewardProducer.ReceiveItemHandleEvent)",
                        "ReceiveItem",
                        "You receive",
                        "{{Y|an electrobow}} and {{C|three lead slugs}}を受け取った",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ConversationRewardPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.ReceiveItem|HandleEvent|System.Boolean|XRL.World.Conversations.EnteredElementEvent",
                    ),
                ),
            ),
        ),
    )


def _game_summary_tombstone_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/GameSummaryTombstonePopupTranslationPatch.cs",
        (
            "GameSummaryTombstonePopupTranslationPatch",
            "TryTranslatePopupMessage",
            "SavedPattern",
            "ErrorPattern",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("GameSummaryTombstonePopupTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameSummaryTombstonePopupTranslationPatchTests.cs",
        (
            "Patch_TranslatesTombstonePopup_WhenOwnerPatched",
            "Patch_DoesNotTranslateTombstonePopup_WhenOwnerAbsent",
            "Patch_StripsDirectMarkedTombstonePopup_WhenOwnerPatched",
            "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
            "Patch_LeavesMatchedPopupUnchanged_WhenDictionaryEntryMissing",
            "Patch_PreservesColorTagsInTombstonePath_WhenOwnerPatched",
            "Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup",
            "nameof(DummyGameSummaryTombstoneProducer.ModernSaveTombstone)",
            "nameof(DummyGameSummaryTombstoneProducer.ClassicShow)",
            "墓碑ファイルを保存しました",
            "保存中にエラーが発生しました",
        ),
    )
    dictionary = EvidenceFile(
        "Mods/QudJP/Localization/Dictionaries/ui-game-summary.ja.json",
        (
            "Your tombstone file was saved:\\n\\n{0}",
            "There was an error saving: {0}",
            "QudJP.GameSummary.TombstonePopup",
        ),
    )
    l2g = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(GameSummaryTombstonePopupTranslationPatch)",
            "Qud.UI.GameSummaryScreen|SaveTombstone|System.Void",
            "XRL.UI.GameSummaryUI|Show|System.Void|System.Int32|System.String|System.String|System.String|System.String|System.Boolean",
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="Qud.UI/GameSummaryScreen.cs::Qud.UI.GameSummaryScreen.SaveTombstone",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, dictionary, l2g),
        ),
        CoveredOwnerFamily(
            family_id="XRL.UI/GameSummaryUI.cs::XRL.UI.GameSummaryUI.Show",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, dictionary, l2g),
        ),
    )


def _powered_floating_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/PoweredFloating.cs::XRL.World.Parts.PoweredFloating.CheckFloating",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PoweredFloatingTranslationPatch.cs",
                    (
                        "PoweredFloatingTranslationPatch",
                        "CheckFloating",
                        "DoesVerbRouteTranslator.TryTranslateMarkedMessage",
                        "DoesVerbRouteTranslator.TryTranslatePlainSentence",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("PoweredFloatingTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PoweredFloatingTranslationPatchTests.cs",
                    (
                        "CheckFloating_TranslatesDoesVerbPopup_WhenOwnerPatched",
                        "CheckFloating_LeavesPlainPopupUnchanged_WhenOwnerAbsent",
                        "CheckFloating_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched",
                        "CheckFloating_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "cease",
                        "fall",
                        "to the ground; you scoop it up",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PoweredFloatingTranslationPatch)",
                        "XRL.World.Parts.PoweredFloating|CheckFloating|System.Void",
                    ),
                ),
            ),
        ),
    )


def _conversation_take_item_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Conversations.Parts/TakeItem.cs::XRL.World.Conversations.Parts.TakeItem.Execute",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/ConversationTakeItemPopupTranslationPatch.cs",
                    (
                        "ConversationTakeItemPopupTranslationPatch",
                        "TargetMethod",
                        "XRL.World.Conversations.Parts.TakeItem",
                        "TryTranslateCannotGive",
                        "TryTranslateTakeSuccess",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("ConversationTakeItemPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationTakeItemPopupTranslationPatchTests.cs",
                    (
                        "Execute_TranslatesCannotGivePopup_WhenOwnerPatched",
                        "Execute_TranslatesTakeSuccessPopup_WhenOwnerPatched",
                        "Execute_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Execute_LeavesUnknownPopupUnchanged_WhenOwnerPatched",
                        "Execute_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Execute_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "Execute_KeepsOuterOwnerScopeActive_WhenNestedScopeExits",
                        "You cannot give {{Y|奇妙な小物}}!",
                        "Q Girl takes {{Y|奇妙な小物}}.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ConversationTakeItemPopupTranslationPatch)",
                        "XRL.World.Conversations.Parts.TakeItem|Execute|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _conversation_check_lost_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.UI/ConversationUI.cs::XRL.UI.ConversationUI.CheckLost",
            inventory_statuses=("needs_family_review",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/ConversationCheckLostPopupTranslationPatch.cs",
                    (
                        "ConversationCheckLostPopupTranslationPatch",
                        "XRL.UI.ConversationUI",
                        "CheckLost",
                        "ListenerNoLongerLostSource",
                        "SpeakerNoLongerLost",
                        "TryTranslatePopupMessage",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("ConversationCheckLostPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ConversationCheckLostPopupTranslationPatchTests.cs",
                    (
                        "CheckLost_TranslatesLostRecoveryPopups_WhenOwnerPatched",
                        "CheckLost_TranslatesMarkedSpeakerLostRecoveryPopup_WhenOwnerPatched",
                        "CheckLost_TranslatesMarkedPluralSpeakerLostRecoveryPopup_WhenOwnerPatched",
                        "CheckLost_DoesNotClaimLostRecoveryPopup_WhenOwnerAbsent",
                        "CheckLost_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "CheckLost_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "CheckLost_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched",
                        "You ask about your location and are no longer lost.",
                        "Argyve asks about his location and is no longer lost.",
                        "The villagers ask",
                        "about their location and are no longer lost.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ConversationCheckLostPopupTranslationPatch)",
                        "XRL.UI.ConversationUI|CheckLost|System.Void",
                    ),
                ),
            ),
        ),
    )


def _mechanical_wings_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/MechanicalWings.cs::XRL.World.Parts.MechanicalWings.TryStartup",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MechanicalWingsPopupTranslationPatch.cs",
                    (
                        "MechanicalWingsPopupTranslationPatch",
                        "XRL.World.Parts.MechanicalWings",
                        "TryStartup",
                        "StatusPattern",
                        "MessageFrameTranslator.TryTranslateXDidY",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("MechanicalWingsPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MechanicalWingsPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesMechanicalWingsStartupPopup_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Patch_StripsDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched",
                        "Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup",
                        "The {{Y|mechanical wings}} are still starting up.",
                        "The {{Y|mechanical wings}} are unresponsive.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MechanicalWingsPopupTranslationPatch)",
                        "XRL.World.Parts.MechanicalWings|TryStartup|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _fire_suppression_discharge_message_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/FireSuppressionDischargeTranslationPatch.cs",
        (
            "FireSuppressionDischargeTranslationPatch",
            "TryTranslateQueuedMessage",
            "FireSuppressionSelfPattern",
            "FireSuppressionTargetPattern",
            "CyberneticsSelfPattern",
            "CyberneticsTargetPattern",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("FireSuppressionDischargeTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/FireSuppressionDischargeTranslationPatchTests.cs",
        (
            "Patch_TranslatesFireSuppressionDischargeMessages_WhenOwnerPatched",
            "Patch_DoesNotTranslateFireSuppressionDischargeMessage_WhenOwnerAbsent",
            "Patch_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched",
            "Patch_LeavesUnknownMessageUnchanged_WhenOwnerPatched",
            "Patch_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
            "Patch_RestoresOuterOwnerScopeAfterNestedOwnerMessage",
            "2 drams of {{C|gel}} discharges all over you.",
            "1 dram of {{C|gel}} discharges all over the snapjaw.",
            "Your {{Y|fire suppression system}} discharges 2 drams of {{C|gel}} all over you.",
            "{{G|snapjaw}}'s {{Y|fire suppression system}} discharges 1 dram of {{C|gel}} all over it.",
        ),
    )
    l2g = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(FireSuppressionDischargeTranslationPatch)",
            "XRL.World.Parts.FireSuppressionSystem|CheckFireSuppression|System.Boolean|XRL.World.GameObject",
            "XRL.World.Parts.CyberneticsFireSuppressionSystem|TurnTick|System.Void|System.Int64|System.Int32",
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/FireSuppressionSystem.cs::XRL.World.Parts.FireSuppressionSystem.CheckFireSuppression",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, l2g),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts/CyberneticsFireSuppressionSystem.cs::"
                "XRL.World.Parts.CyberneticsFireSuppressionSystem.TurnTick"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, l2g),
        ),
    )


def _cudgel_conk_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/Cudgel_Conk.cs::XRL.World.Parts.Skill.Cudgel_Conk.PerformConk",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CudgelConkPopupTranslationPatch.cs",
                    (
                        "CudgelConkPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Cudgel_Conk",
                        "PerformConk",
                        "NoHeadPattern",
                        "ConfirmSelfConkPattern",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("CudgelConkPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CudgelConkPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesNoHeadPopup_WhenOwnerPatched",
                        "Patch_TranslatesConfirmSelfConkPopup_WhenOwnerPatched",
                        "Patch_DoesNotTranslateCudgelConkPopup_WhenOwnerAbsent",
                        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "Patch_KeepsOuterOwnerScopeActive_WhenNestedScopeExits",
                        "snapjaw doesn't have anything like a head to conk.",
                        "Are you sure you want to conk yourself on {{C|the head}}?",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "CudgelConkPopupTargetMethod_ResolvesExpectedFullSignature",
                        "typeof(CudgelConkPopupTranslationPatch)",
                        "XRL.World.Parts.Skill.Cudgel_Conk|PerformConk|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _grit_gate_terminal_owner_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.UI/GritGateTerminalScreenKnowledge.cs::XRL.UI.GritGateTerminalScreenKnowledge.GritGateTerminalScreenKnowledge",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/GritGateTerminalKnowledgePopupTranslationPatch.cs",
                    (
                        "GritGateTerminalKnowledgePopupTranslationPatch",
                        "SourceHeader",
                        "LocationPrefix",
                        "TryTranslatePopupMessage",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("GritGateTerminalKnowledgePopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GritGateTerminalKnowledgePopupTranslationPatchTests.cs",
                    (
                        "Activate_TranslatesInsightPopup_WhenOwnerPatched",
                        "Activate_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Activate_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Activate_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "Ereshkigal delivers insight from the Thin World",
                        "The location of {{Y|Bethesda Susa}}",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GritGateTerminalKnowledgePopupTranslationPatch)",
                        "XRL.UI.GritGateTerminalScreenKnowledge",
                        "GritGateTerminalScreenKnowledge",
                        '"Activate"',
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.UI/GritGateTerminalScreenMessage.cs::XRL.UI.GritGateTerminalScreenMessage.GritGateTerminalScreenMessage",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/GritGateTerminalScreenMessageTranslationPatch.cs",
                    (
                        "GritGateTerminalScreenMessageTranslationPatch",
                        "AlarmMessage",
                        "TryTranslateQueuedMessage",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("GritGateTerminalScreenMessageTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/GritGateTerminalScreenMessageTranslationPatchTests.cs",
                    (
                        "Activate_TranslatesConstructorDelegateAlarmMessage_WhenOwnerPatched",
                        "Activate_DoesNotTranslateAlarmMessage_WhenOwnerAbsent",
                        "Activate_DoesNotRetranslateDirectMarkedAlarmMessage_WhenOwnerPatched",
                        "Activate_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                        "Alarms blare across the enclave.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GritGateTerminalScreenMessageTranslationPatch)",
                        "XRL.UI.GritGateTerminalScreenMessage",
                        "GritGateTerminalScreenMessage",
                        '"Activate"',
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
                    ("Alarms blare across the enclave.",),
                ),
            ),
        ),
    )


def _pick_item_take_all_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.UI/PickItem.cs::XRL.UI.PickItem.TakeAll",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PickItemTakeAllPopupTranslationPatch.cs",
                    (
                        "PickItemTakeAllPopupTranslationPatch",
                        "TakeAll",
                        "TryTranslatePopupMessage",
                        "Taking all these objects will put you over your weight limit.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("PickItemTakeAllPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PickItemTakeAllPopupTranslationPatchTests.cs",
                    (
                        "TakeAll_TranslatesOverweightConfirmationPopup_WhenOwnerPatched",
                        "TakeAll_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "TakeAll_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "TakeAll_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "Taking all these objects will put you over your weight limit.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PickItemTakeAllPopupTranslationPatch)",
                        "XRL.UI.PickItem",
                        '"TakeAll"',
                    ),
                ),
            ),
        ),
    )


def _status_screen_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/StatusScreenPopupTranslationPatch.cs",
        (
            "StatusScreenPopupTranslationPatch",
            "TryTranslatePopupMessage",
            "BuyRandomMutation",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("StatusScreenPopupTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/StatusScreenPopupTranslationPatchTests.cs",
        (
            "BuyStat_TranslatesAttributePurchasePopups_WhenOwnerPatched",
            "BuyRandomMutation_TranslatesMutationChoicePopups_WhenOwnerPatched",
            "TryTranslatePopupMessage_TranslatesGainedMutation_WhenOwnerScopeIsActive",
            "StatusScreenPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "StatusScreenPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "StatusScreenPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
        ),
    )
    resolution = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(StatusScreenPopupTranslationPatch)",
            "XRL.UI.StatusScreen|BuyStat|System.Void|XRL.World.GameObject|System.String",
            "XRL.UI.StatusScreen|BuyRandomMutation|System.Boolean|XRL.World.GameObject",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.UI/StatusScreen.cs::XRL.UI.StatusScreen.BuyStat",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, resolution),
        ),
        CoveredOwnerFamily(
            family_id="XRL.UI/StatusScreen.cs::XRL.UI.StatusScreen.BuyRandomMutation",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, resolution),
        ),
    )


def _campfire_preserve_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/CampfirePreserveTranslationPatch.cs",
        (
            "CampfirePreserveTranslationPatch",
            "TryTranslatePopupMessage",
            "You preserved",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("CampfirePreserveTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfirePreserveTranslationPatchTests.cs",
        (
            "Preserve_TranslatesGeneratedPreservedPopup_WhenOwnerPatched",
            "PreserveExotic_TranslatesGeneratedPreservedPopup_WhenOwnerPatched",
            "CampfirePreserve_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "CampfirePreserve_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "CampfirePreserve_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
        ),
    )
    resolution = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(CampfirePreserveTranslationPatch)",
            "XRL.World.Parts.Campfire|Preserve|System.Boolean",
            "XRL.World.Parts.Campfire|PreserveExotic|System.Boolean",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.Preserve",
            inventory_statuses=("needs_family_review",),
            evidence_files=(patch, pipeline, tests, resolution),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.PreserveExotic",
            inventory_statuses=("needs_family_review",),
            evidence_files=(patch, pipeline, tests, resolution),
        ),
    )


def _reality_stabilized_event_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "TryContest",
            "XRL.World.Effects.RealityStabilized|TryContest|XRL.World.Effects.RealityStabilized+ContestResult|XRL.World.GameObject|System.Int32|System.Int32",
        ),
        (
            "ShortCircuitDevice",
            "XRL.World.Effects.RealityStabilized|ShortCircuitDevice|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Event",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Effects/RealityStabilized.cs::XRL.World.Effects.RealityStabilized.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/RealityStabilizedEventTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("RealityStabilizedEventTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("RealityStabilizedEventTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "RealityStabilizedEvent_TranslatesQueuedMessages_WhenOwnerPatched",
                        "RealityStabilizedEvent_TranslatesShortCircuitPopup_WhenOwnerPatched",
                        "RealityStabilizedEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "RealityStabilizedEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "RealityStabilizedEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "RealityStabilizedEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "RealityStabilizedEvent_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(RealityStabilizedEventTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _cybernetic_rejection_syndrome_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "Apply",
            "XRL.World.Effects.CyberneticRejectionSyndrome|Apply|System.Boolean|XRL.World.GameObject",
        ),
        (
            "Remove",
            "XRL.World.Effects.CyberneticRejectionSyndrome|Remove|System.Void|XRL.World.GameObject",
        ),
        (
            "Reduce",
            "XRL.World.Effects.CyberneticRejectionSyndrome|Reduce|System.Void|System.Int32",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Effects/CyberneticRejectionSyndrome.cs::XRL.World.Effects.CyberneticRejectionSyndrome.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CyberneticRejectionSyndromeTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("CyberneticRejectionSyndromeTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "CyberneticRejectionSyndrome_TranslatesQueuedMessages_WhenOwnerPatched",
                        "CyberneticRejectionSyndrome_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "CyberneticRejectionSyndrome_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "CyberneticRejectionSyndrome_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(CyberneticRejectionSyndromeTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _geomagnetic_disc_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "SignalFailure",
            "XRL.World.Parts.GeomagneticDisc|SignalFailure|System.Void|XRL.World.GameObject",
        ),
        (
            "SignalLowPower",
            "XRL.World.Parts.GeomagneticDisc|SignalLowPower|System.Void|XRL.World.GameObject",
        ),
        (
            "ExamineFailure",
            "XRL.World.Parts.GeomagneticDisc|ExamineFailure|System.Boolean|XRL.World.IExamineEvent|System.Int32",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/GeomagneticDisc.cs::XRL.World.Parts.GeomagneticDisc.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/GeomagneticDiscTranslationPatch.cs",
                    (method_name, "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("GeomagneticDiscTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "GeomagneticDisc_TranslatesPopupMessages_WhenOwnerPatched",
                        "GeomagneticDisc_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "GeomagneticDisc_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "GeomagneticDisc_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeomagneticDiscTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _campfire_cook_availability_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Campfire.cs::XRL.World.Parts.Campfire.Cook",
            inventory_statuses=("needs_family_review",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
                    ("You can't cook with hostile creatures nearby.",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CampfireCookAvailabilityTranslationPatch.cs",
                    (
                        "XRL.World.Parts.Campfire",
                        '"Cook"',
                        "TryTranslatePopupMessage",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("CampfireCookAvailabilityTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "CampfireCookAvailability_TranslatesPopupMessages_WhenOwnerPatched",
                        "CampfireCookAvailability_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "CampfireCookAvailability_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "CampfireCookAvailability_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(CampfireCookAvailabilityTranslationPatch)",
                        "XRL.World.Parts.Campfire|Cook|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _teleprojector_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "HandleEvent",
            "XRL.World.Parts.Teleprojector|HandleEvent|System.Boolean|XRL.World.BootSequenceDoneEvent",
        ),
        (
            "ActivateTeleprojector",
            "XRL.World.Parts.Teleprojector|ActivateTeleprojector|System.Boolean",
        ),
        (
            "RoboDom",
            "XRL.World.Parts.Teleprojector|RoboDom|System.Boolean|XRL.World.MentalAttackEvent",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/Teleprojector.cs::XRL.World.Parts.Teleprojector.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/TeleprojectorTranslationPatch.cs",
                    (method_name, "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("TeleprojectorTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Teleprojector_TranslatesPopupMessages_WhenOwnerPatched",
                        "Teleprojector_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Teleprojector_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Teleprojector_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(TeleprojectorTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _tomb_anchor_system_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        ("OnEndTurn", "XRL.ITombAnchorSystem|OnEndTurn|System.Void"),
        ("Recall", "XRL.ITombAnchorSystem|Recall|System.Void|XRL.World.Zone"),
        ("AnchorCall", "XRL.ITombAnchorSystem|AnchorCall|System.Void"),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL/ITombAnchorSystem.cs::XRL.ITombAnchorSystem.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/TombAnchorSystemTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("TombAnchorSystemTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "TombAnchorSystem_TranslatesQueuedMessages_WhenOwnerPatched",
                        "TombAnchorSystem_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "TombAnchorSystem_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "TombAnchorSystem_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(TombAnchorSystemTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _cybernetics_medassist_module_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "HandleEvent",
            "XRL.World.Parts.CyberneticsMedassistModule|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        ),
        (
            "AttemptMedicalAssistance",
            "XRL.World.Parts.CyberneticsMedassistModule|AttemptMedicalAssistance|System.Void|XRL.World.Damage",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/CyberneticsMedassistModule.cs::XRL.World.Parts.CyberneticsMedassistModule.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CyberneticsMedassistModuleTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("CyberneticsMedassistModuleTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("CyberneticsMedassistModuleTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "CyberneticsMedassistModule_TranslatesPopupMessages_WhenOwnerPatched",
                        "CyberneticsMedassistModule_TranslatesQueuedMessages_WhenOwnerPatched",
                        "CyberneticsMedassistModule_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "CyberneticsMedassistModule_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "CyberneticsMedassistModule_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
                        "CyberneticsMedassistModule_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(CyberneticsMedassistModuleTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _liquid_loader_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "XRL.World.Parts/BioAmmoLoader.cs",
            "XRL.World.Parts.BioAmmoLoader",
            "HandleEvent",
            "XRL.World.Parts.BioAmmoLoader|HandleEvent|System.Boolean|XRL.World.CommandReloadEvent",
        ),
        (
            "XRL.World.Parts/BioAmmoLoader.cs",
            "XRL.World.Parts.BioAmmoLoader",
            "FireEvent",
            "XRL.World.Parts.BioAmmoLoader|FireEvent|System.Boolean|XRL.World.Event",
        ),
        (
            "XRL.World.Parts/LiquidAmmoLoader.cs",
            "XRL.World.Parts.LiquidAmmoLoader",
            "HandleEvent",
            "XRL.World.Parts.LiquidAmmoLoader|HandleEvent|System.Boolean|XRL.World.CommandReloadEvent",
        ),
        (
            "XRL.World.Parts/LiquidAmmoLoader.cs",
            "XRL.World.Parts.LiquidAmmoLoader",
            "FireEvent",
            "XRL.World.Parts.LiquidAmmoLoader|FireEvent|System.Boolean|XRL.World.Event",
        ),
        (
            "XRL.World.Parts/ModLiquidCooled.cs",
            "XRL.World.Parts.ModLiquidCooled",
            "HandleEvent",
            "XRL.World.Parts.ModLiquidCooled|HandleEvent|System.Boolean|XRL.World.CommandReloadEvent",
        ),
        (
            "XRL.World.Parts/ModLiquidCooled.cs",
            "XRL.World.Parts.ModLiquidCooled",
            "FireEvent",
            "XRL.World.Parts.ModLiquidCooled|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"{source_file}::{type_name}.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/LiquidLoaderTranslationPatch.cs",
                    (type_name, method_name, "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("LiquidLoaderTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("LiquidLoaderTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "LiquidLoader_TranslatesQueuedMessages_WhenOwnerPatched",
                        "LiquidLoader_TranslatesPopupMessages_WhenOwnerPatched",
                        "LiquidLoader_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "LiquidLoader_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "LiquidLoader_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
                        "LiquidLoader_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(LiquidLoaderTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for source_file, type_name, method_name, signature in target_signatures
    )


def _energy_loader_cannot_take_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EnergyLoaderCannotTakeTranslationPatch.cs",
        (
            "EnergyLoaderCannotTakeTranslationPatch",
            "ElectricalDischargeLoader",
            "EnergyAmmoLoader",
            "TryTranslatePopupMessage",
            "CannotTakePattern",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("EnergyLoaderCannotTakeTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EnergyLoaderCannotTakeTranslationPatchTests.cs",
        (
            "EnergyLoaderCannotTake_TranslatesPopup_WhenOwnerPatched",
            "EnergyLoaderCannotTake_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "EnergyLoaderCannotTake_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "EnergyLoaderCannotTake_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyEnergyLoaderCannotTakeTarget",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(EnergyLoaderCannotTakeTranslationPatch)",
            "XRL.World.Parts.ElectricalDischargeLoader|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.EnergyAmmoLoader|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    evidence_files = (patch, popup_pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/ElectricalDischargeLoader.cs::XRL.World.Parts.ElectricalDischargeLoader.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/EnergyAmmoLoader.cs::XRL.World.Parts.EnergyAmmoLoader.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _liquid_leak_message_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/LiquidLeakMessageTranslationPatch.cs",
        (
            "LiquidLeakMessageTranslationPatch",
            "LeakWhenBroken",
            "LeaksFluid",
            "LeakPattern",
            "TryTranslateQueuedMessage",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("LiquidLeakMessageTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/LiquidLeakMessageTranslationPatchTests.cs",
        (
            "LiquidLeak_TranslatesQueuedMessage_WhenOwnerPatched",
            "LiquidLeak_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "LiquidLeak_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "LiquidLeak_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "nameof(DummyLiquidLeakTarget.LeakWhenBrokenDistributeLiquid)",
            "nameof(DummyLiquidLeakTarget.LeaksFluidDistributeLiquid)",
            "The {{Y|broken canteen}} leaks 1 dram of {{B|water}}.",
            "The {{Y|oozing vase}} leaks 2 drams of {{C|slime}}.",
            "{{G|leaking pipes}} drip 2 drams of {{B|water}}.",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(LiquidLeakMessageTranslationPatch)",
            "XRL.World.Parts.LeakWhenBroken|DistributeLiquid|System.Void|XRL.World.Parts.LiquidVolume",
            "XRL.World.Parts.LeaksFluid|DistributeLiquid|System.Boolean",
        ),
    )
    evidence_files = (patch, pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/LeakWhenBroken.cs::XRL.World.Parts.LeakWhenBroken.DistributeLiquid",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/LeaksFluid.cs::XRL.World.Parts.LeaksFluid.DistributeLiquid",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _energy_cell_socket_access_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/EnergyCellSocket.cs::XRL.World.Parts.EnergyCellSocket.AttemptReplaceCell",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/EnergyCellSocketAccessPopupTranslationPatch.cs",
                    (
                        "EnergyCellSocketAccessPopupTranslationPatch",
                        "AttemptReplaceCell",
                        "AccessEnergyCellPattern",
                        "AccessEnergyCellOwnershipWarning",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("EnergyCellSocketAccessPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/EnergyCellSocketAccessPopupTranslationPatchTests.cs",
                    (
                        "AttemptReplaceCell_TranslatesAccessWarning_WhenOwnerPatched",
                        "AttemptReplaceCell_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "AttemptReplaceCell_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "AttemptReplaceCell_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
                        "DummyEnergyCellSocketTarget",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EnergyCellSocketAccessPopupTranslationPatch)",
                        "XRL.World.Parts.EnergyCellSocket|AttemptReplaceCell|System.Boolean|XRL.World.GameObject|XRL.World.InventoryActionEvent|System.Int32|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _campfire_remains_attempt_light_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/CampfireRemains.cs::XRL.World.Parts.CampfireRemains.AttemptLight",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CampfireRemainsAttemptLightTranslationPatch.cs",
                    (
                        "CampfireRemainsAttemptLightTranslationPatch",
                        "AttemptLight",
                        "ExtinguishingPoolPattern",
                        "TryTranslatePopupMessage",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("CampfireRemainsAttemptLightTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CampfireRemainsAttemptLightTranslationPatchTests.cs",
                    (
                        "AttemptLight_TranslatesExtinguishingPoolPopup_WhenOwnerPatched",
                        "AttemptLight_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "AttemptLight_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "AttemptLight_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
                        "DummyCampfireRemainsTarget",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(CampfireRemainsAttemptLightTranslationPatch)",
                        "XRL.World.Parts.CampfireRemains|AttemptLight|System.Void|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _troll_king_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        ("CheckSpawn", "XRL.World.Parts.TrollKing|CheckSpawn|System.Void|System.Int32"),
        ("StopBudding", "XRL.World.Parts.TrollKing|StopBudding|System.Void|System.Int32"),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/TrollKing.cs::XRL.World.Parts.TrollKing.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/TrollKingTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("TrollKingTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "TrollKing_TranslatesQueuedMessages_WhenOwnerPatched",
                        "TrollKing_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "TrollKing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "TrollKing_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(TrollKingTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _mutating_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        ("Apply", "XRL.World.Effects.Mutating|Apply|System.Boolean|XRL.World.GameObject"),
        ("HandleEvent", "XRL.World.Effects.Mutating|HandleEvent|System.Boolean|XRL.World.EndTurnEvent"),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Effects/Mutating.cs::XRL.World.Effects.Mutating.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MutatingTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("MutatingTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("MutatingTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Mutating_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Mutating_TranslatesPopupMessages_WhenOwnerPatched",
                        "Mutating_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Mutating_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Mutating_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
                        "Mutating_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MutatingTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _quills_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "HandleEvent",
            "XRL.World.Parts.Mutation.Quills|HandleEvent|System.Boolean|XRL.World.TookDamageEvent",
        ),
        (
            "FireEvent",
            "XRL.World.Parts.Mutation.Quills|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts.Mutation/Quills.cs::XRL.World.Parts.Mutation.Quills.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/QuillsTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("QuillsTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Quills_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Quills_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Quills_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "Quills_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(QuillsTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _light_manipulation_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "HandleEvent",
            "XRL.World.Parts.Mutation.LightManipulation|HandleEvent|System.Boolean|XRL.World.CommandEvent",
        ),
        (
            "Lase",
            "XRL.World.Parts.Mutation.LightManipulation|Lase|System.Boolean|XRL.World.Cell|System.Int32",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts.Mutation/LightManipulation.cs::XRL.World.Parts.Mutation.LightManipulation.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/LightManipulationTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("LightManipulationTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("LightManipulationTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "LightManipulation_TranslatesQueuedMessages_WhenOwnerPatched",
                        "LightManipulation_TranslatesPopupMessage_WhenOwnerPatched",
                        "LightManipulation_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "LightManipulation_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "LightManipulation_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
                        "LightManipulation_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(LightManipulationTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _latches_on_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "HandleEvent",
            "XRL.World.Parts.LatchesOn|HandleEvent|System.Boolean|XRL.World.UnequippedEvent",
        ),
        (
            "FireEvent",
            "XRL.World.Parts.LatchesOn|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/LatchesOn.cs::XRL.World.Parts.LatchesOn.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/LatchesOnTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("LatchesOnTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "LatchesOn_TranslatesQueuedMessages_WhenOwnerPatched",
                        "LatchesOn_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "LatchesOn_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "LatchesOn_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(LatchesOnTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _asleep_owner_families() -> tuple[CoveredOwnerFamily, ...]:
    common_l2_tests = (
        "AsleepOwner_TranslatesQueuedMessages_WhenOwnerPatched",
        "AsleepOwner_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
        "AsleepOwner_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
        "AsleepOwner_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Asleep.cs::XRL.World.Effects.Asleep.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/AsleepOwnerTranslationPatch.cs",
                    ("Apply", "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("AsleepOwnerTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    common_l2_tests,
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AsleepOwnerTranslationPatch)",
                        "XRL.World.Effects.Asleep|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Asleep.cs::XRL.World.Effects.Asleep.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/AsleepOwnerTranslationPatch.cs",
                    ("HandleEvent", "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("AsleepOwnerTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("AsleepOwnerTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        *common_l2_tests,
                        "AsleepOwner_TranslatesPopupMessages_WhenOwnerPatched",
                        "AsleepOwner_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AsleepOwnerTranslationPatch)",
                        "XRL.World.Effects.Asleep|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
                        "XRL.World.Effects.Asleep|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
                    ),
                ),
            ),
        ),
    )


def _budding_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "Apply",
            "XRL.World.Effects.Budding|Apply|System.Boolean|XRL.World.GameObject",
        ),
        (
            "Remove",
            "XRL.World.Effects.Budding|Remove|System.Void|XRL.World.GameObject",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Effects/Budding.cs::XRL.World.Effects.Budding.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BuddingTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("BuddingTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Budding_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Budding_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Budding_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "Budding_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BuddingTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _beguiling_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Beguiling.cs::XRL.World.Parts.Mutation.Beguiling.Cast",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BeguilingTranslationPatch.cs",
                    ("Cast", "TryTranslateQueuedMessage", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("BeguilingTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("BeguilingTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Beguiling_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Beguiling_TranslatesPopupMessage_WhenOwnerPatched",
                        "Beguiling_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Beguiling_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Beguiling_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
                        "Beguiling_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BeguilingTranslationPatch)",
                        "XRL.World.Parts.Mutation.Beguiling|Cast|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Mutation.Beguiling|XRL.World.Event|System.Int32",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Beguiling.cs::XRL.World.Parts.Mutation.Beguiling.Beguile",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BeguilingTranslationPatch.cs",
                    ("Beguile", "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("BeguilingTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Beguiling_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Beguiling_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Beguiling_DoesNotRetranslateDirectMarkedMessages_WhenOwnerPatched",
                        "Beguiling_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BeguilingTranslationPatch)",
                        "XRL.World.Parts.Mutation.Beguiling|Beguile|System.Boolean|XRL.World.MentalAttackEvent",
                    ),
                ),
            ),
        ),
    )


def _ascension_cable_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "TryAscend",
            "XRL.World.Parts.AscensionCable|TryAscend|System.Boolean|XRL.World.GameObject|System.Boolean",
        ),
        (
            "TryDescend",
            "XRL.World.Parts.AscensionCable|TryDescend|System.Boolean|XRL.World.GameObject|System.Boolean",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Parts/AscensionCable.cs::XRL.World.Parts.AscensionCable.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/AscensionCableTranslationPatch.cs",
                    (method_name, "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("AscensionCableTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "AscensionCable_TranslatesPopupMessages_WhenOwnerPatched",
                        "AscensionCable_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "AscensionCable_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "AscensionCable_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AscensionCableTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _carapace_tighten_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Carapace.cs::XRL.World.Parts.Mutation.Carapace.Tighten",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CarapaceTranslationPatch.cs",
                    ("Tighten", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("CarapaceTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "CarapaceTighten_TranslatesPopupMessages_WhenOwnerPatched",
                        "CarapaceTighten_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "CarapaceTighten_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "CarapaceTighten_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(CarapaceTranslationPatch)",
                        "XRL.World.Parts.Mutation.Carapace|Tighten|System.Void|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _svardym_system_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "BeginStorm",
            "XRL.SvardymSystem|BeginStorm|System.Void",
        ),
        (
            "Tick",
            "XRL.SvardymSystem|Tick|System.Void",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL/SvardymSystem.cs::XRL.SvardymSystem.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SvardymSystemTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("SvardymSystemTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "SvardymSystem_TranslatesQueuedMessages_WhenOwnerPatched",
                        "SvardymSystem_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "SvardymSystem_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "SvardymSystem_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SvardymSystemTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _phased_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "HandleEvent",
            "XRL.World.Effects.Phased|HandleEvent|System.Boolean|XRL.World.EffectAppliedEvent",
        ),
        (
            "Remove",
            "XRL.World.Effects.Phased|Remove|System.Void|XRL.World.GameObject",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=f"XRL.World.Effects/Phased.cs::XRL.World.Effects.Phased.{method_name}",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PhasedTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("PhasedTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "Phased_TranslatesQueuedMessages_WhenOwnerPatched",
                        "Phased_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Phased_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "Phased_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PhasedTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for method_name, signature in target_signatures
    )


def _persuasion_rebuke_robot_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/Persuasion_RebukeRobot.cs::XRL.World.Parts.Skill.Persuasion_RebukeRobot.Rebuke",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PersuasionRebukeRobotTranslationPatch.cs",
                    ("Rebuke", "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("PersuasionRebukeRobotTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "PersuasionRebukeRobot_TranslatesRebukeFailureMessage_WhenOwnerPatched",
                        "PersuasionRebukeRobot_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "PersuasionRebukeRobot_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "PersuasionRebukeRobot_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PersuasionRebukeRobotTranslationPatch)",
                        "XRL.World.Parts.Skill.Persuasion_RebukeRobot|Rebuke|System.Boolean|XRL.World.MentalAttackEvent",
                    ),
                ),
            ),
        ),
    )


def _nephal_properties_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/NephalProperties.cs::XRL.World.Parts.NephalProperties.TryPacify",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/NephalPropertiesTranslationPatch.cs",
                    ("TryPacify", "TryTranslatePopupMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("NephalPropertiesTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "NephalPropertiesTryPacify_TranslatesPopupMessage_WhenOwnerPatched",
                        "NephalPropertiesTryPacify_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "NephalPropertiesTryPacify_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "NephalPropertiesTryPacify_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(NephalPropertiesTranslationPatch)",
                        "XRL.World.Parts.NephalProperties|TryPacify|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _tonic_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Tonic.cs::XRL.World.Parts.Tonic.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/TonicTranslationPatch.cs",
                    ("FireEvent", "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("TonicTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "TonicFireEvent_TranslatesArmorFailureMessage_WhenOwnerPatched",
                        "TonicFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "TonicFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "TonicFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(TonicTranslationPatch)",
                        "XRL.World.Parts.Tonic|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
    )


def _tonic_applicator_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/TonicApplicatorTranslationPatch.cs",
        (
            "TonicApplicatorTranslationPatch",
            "LoveTonicApplicator",
            "SphynxSalt_Tonic_Applicator",
            "LoveNoEffectPattern",
            "SphynxSaltApplyPattern",
            "TryTranslateQueuedMessage",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("TonicApplicatorTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TonicApplicatorTranslationPatchTests.cs",
        (
            "TonicApplicator_TranslatesQueuedMessage_WhenOwnerPatched",
            "TonicApplicator_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "TonicApplicator_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "TonicApplicator_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "nameof(DummyTonicApplicatorTarget.LoveTonicFireEvent)",
            "nameof(DummyTonicApplicatorTarget.SphynxSaltFireEvent)",
            "looks you over and metabolizes the love tonic with no effect",
            "applies {{C|a sphynx salt injector}}",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(TonicApplicatorTranslationPatch)",
            "XRL.World.Parts.LoveTonicApplicator|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.SphynxSalt_Tonic_Applicator|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    evidence_files = (patch, pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/LoveTonicApplicator.cs::XRL.World.Parts.LoveTonicApplicator.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts/SphynxSalt_Tonic_Applicator.cs::"
                "XRL.World.Parts.SphynxSalt_Tonic_Applicator.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _xrl_game_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL/XRLGame.cs::XRL.XRLGame.FinishQuestStep",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/XrlGameTranslationPatch.cs",
                    ("FinishQuestStep", "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("XrlGameTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "XrlGameFinishQuestStep_TranslatesErrorMessage_WhenOwnerPatched",
                        "XrlGameFinishQuestStep_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "XrlGameFinishQuestStep_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "XrlGameFinishQuestStep_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(XrlGameTranslationPatch)",
                        "XRL.XRLGame|FinishQuestStep|System.Boolean|XRL.World.Quest|System.String|System.Int32|System.Boolean|System.String",
                    ),
                ),
            ),
        ),
    )


def _integrated_weapon_hosts_families() -> tuple[CoveredOwnerFamily, ...]:
    common_evidence = (
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/IntegratedWeaponHostsTranslationPatch.cs",
            ("IntegratedWeaponHostsTranslationPatch", "TryTranslatePopupMessage"),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
            ("IntegratedWeaponHostsTranslationPatch.TryTranslatePopupMessage",),
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Capabilities/IntegratedWeaponHosts.cs::XRL.World.Capabilities.IntegratedWeaponHosts.GenerateTurret",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "IntegratedWeaponHostsGenerateTurret_TranslatesNoAmmoPopup_WhenOwnerPatched",
                        "IntegratedWeaponHostsGenerateTurret_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "IntegratedWeaponHostsGenerateTurret_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "IntegratedWeaponHostsGenerateTurret_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(IntegratedWeaponHostsTranslationPatch)",
                        "XRL.World.Capabilities.IntegratedWeaponHosts|GenerateTurret|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.Boolean",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Capabilities/IntegratedWeaponHosts.cs::XRL.World.Capabilities.IntegratedWeaponHosts.HandleTurretWish",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "IntegratedWeaponHostsHandleTurretWish_TranslatesShowFail_WhenOwnerPatched",
                        "IntegratedWeaponHostsHandleTurretWish_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "IntegratedWeaponHostsHandleTurretWish_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "IntegratedWeaponHostsHandleTurretWish_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(IntegratedWeaponHostsTranslationPatch)",
                        "XRL.World.Capabilities.IntegratedWeaponHosts|HandleTurretWish|System.Boolean|System.Text.RegularExpressions.Match",
                    ),
                ),
            ),
        ),
    )


def _boost_statistic_families() -> tuple[CoveredOwnerFamily, ...]:
    common_evidence = (
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/BoostStatisticTranslationPatch.cs",
            ("BoostStatisticTranslationPatch", "TryTranslateQueuedMessage"),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
            ("BoostStatisticTranslationPatch.TryTranslateQueuedMessage",),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            (
                "BoostStatistic_TranslatesQueuedMessages_WhenOwnerPatched",
                "BoostStatistic_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                "BoostStatistic_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                "BoostStatistic_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            ),
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/BoostStatistic.cs::XRL.World.Effects.BoostStatistic.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BoostStatisticTranslationPatch)",
                        "XRL.World.Effects.BoostStatistic|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/BoostStatistic.cs::XRL.World.Effects.BoostStatistic.Remove",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BoostStatisticTranslationPatch)",
                        "XRL.World.Effects.BoostStatistic|Remove|System.Void|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _emboldened_families() -> tuple[CoveredOwnerFamily, ...]:
    common_evidence = (
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/EmboldenedTranslationPatch.cs",
            ("EmboldenedTranslationPatch", "TryTranslateQueuedMessage"),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
            ("EmboldenedTranslationPatch.TryTranslateQueuedMessage",),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            (
                "Emboldened_TranslatesQueuedMessages_WhenOwnerPatched",
                "Emboldened_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                "Emboldened_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                "Emboldened_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            ),
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Emboldened.cs::XRL.World.Effects.Emboldened.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EmboldenedTranslationPatch)",
                        "XRL.World.Effects.Emboldened|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Emboldened.cs::XRL.World.Effects.Emboldened.Remove",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EmboldenedTranslationPatch)",
                        "XRL.World.Effects.Emboldened|Remove|System.Void|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _fungal_spore_infection_families() -> tuple[CoveredOwnerFamily, ...]:
    common_patch_evidence = (
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/FungalSporeInfectionTranslationPatch.cs",
            (
                "FungalSporeInfectionTranslationPatch",
                "GasFungalSpores",
                "TryTranslatePopupMessage",
                "TryTranslateQueuedMessage",
            ),
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/FungalSporeInfection.cs::XRL.World.Effects.FungalSporeInfection.ApplyFungalInfection",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_patch_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("FungalSporeInfectionTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "FungalSporeInfectionApplyFungalInfection_TranslatesContractedPopup_WhenOwnerPatched",
                        "FungalSporeInfectionApplyFungalInfection_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "FungalSporeInfectionApplyFungalInfection_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "FungalSporeInfectionApplyFungalInfection_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(FungalSporeInfectionTranslationPatch)",
                        "XRL.World.Effects.FungalSporeInfection|ApplyFungalInfection|System.Boolean|XRL.World.GameObject|System.String|XRL.World.Anatomy.BodyPart",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/FungalSporeInfection.cs::XRL.World.Effects.FungalSporeInfection.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_patch_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("FungalSporeInfectionTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "FungalSporeInfectionFireEvent_TranslatesSkinItchesQueuedMessage_WhenOwnerPatched",
                        "FungalSporeInfectionFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "FungalSporeInfectionFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "FungalSporeInfectionFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(FungalSporeInfectionTranslationPatch)",
                        "XRL.World.Effects.FungalSporeInfection|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/GasFungalSpores.cs::XRL.World.Parts.GasFungalSpores.ApplyGas",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_patch_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("FungalSporeInfectionTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "GasFungalSporesApplyGas_TranslatesSkinItchesQueuedMessage_WhenOwnerPatched",
                        "FungalSporeInfectionFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "FungalSporeInfectionFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "FungalSporeInfectionFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(FungalSporeInfectionTranslationPatch)",
                        "XRL.World.Parts.GasFungalSpores|ApplyGas|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/PaxInfection.cs::XRL.World.Parts.PaxInfection.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_patch_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("FungalSporeInfectionTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "FungalSporeInfectionFireEvent_TranslatesSporeCloudQueuedMessages_WhenOwnerPatched",
                        "FungalSporeInfectionFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "FungalSporeInfectionFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "FungalSporeInfectionSporeCloudRoutes_LeaveEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(FungalSporeInfectionTranslationPatch)",
                        "XRL.World.Parts.PaxInfection|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/PuffInfection.cs::XRL.World.Parts.PuffInfection.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_patch_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("FungalSporeInfectionTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "FungalSporeInfectionFireEvent_TranslatesSporeCloudQueuedMessages_WhenOwnerPatched",
                        "FungalSporeInfectionFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "FungalSporeInfectionFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "FungalSporeInfectionSporeCloudRoutes_LeaveEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(FungalSporeInfectionTranslationPatch)",
                        "XRL.World.Parts.PuffInfection|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
    )


def _healing_families() -> tuple[CoveredOwnerFamily, ...]:
    common_evidence = (
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/HealingTranslationPatch.cs",
            ("HealingTranslationPatch", "TryTranslateQueuedMessage"),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
            ("HealingTranslationPatch.TryTranslateQueuedMessage",),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            (
                "Healing_TranslatesInterruptedQueuedMessage_WhenOwnerPatched",
                "Healing_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                "Healing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                "Healing_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            ),
        ),
        EvidenceFile(
            "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
            ("Your healing is interrupted!",),
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Healing.cs::XRL.World.Effects.Healing.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(HealingTranslationPatch)",
                        "XRL.World.Effects.Healing|HandleEvent|System.Boolean|XRL.World.UseEnergyEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Healing.cs::XRL.World.Effects.Healing.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(HealingTranslationPatch)",
                        "XRL.World.Effects.Healing|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
    )


def _stressed_families() -> tuple[CoveredOwnerFamily, ...]:
    common_evidence = (
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/StressedTranslationPatch.cs",
            ("StressedTranslationPatch", "TryTranslateQueuedMessage"),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
            ("StressedTranslationPatch.TryTranslateQueuedMessage",),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            (
                "Stressed_TranslatesQueuedMessages_WhenOwnerPatched",
                "Stressed_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                "Stressed_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                "Stressed_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            ),
        ),
        EvidenceFile(
            "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
            ("Your body flushes with adrenaline!", "Your adrenaline level returns to normal!"),
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Stressed.cs::XRL.World.Effects.Stressed.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(StressedTranslationPatch)",
                        "XRL.World.Effects.Stressed|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Stressed.cs::XRL.World.Effects.Stressed.Remove",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(StressedTranslationPatch)",
                        "XRL.World.Effects.Stressed|Remove|System.Void|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _monochrome_onset_families() -> tuple[CoveredOwnerFamily, ...]:
    common_evidence = (
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/MonochromeOnsetTranslationPatch.cs",
            ("MonochromeOnsetTranslationPatch", "MonochromePoisonOnDamage", "TryTranslateQueuedMessage"),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
            ("MonochromeOnsetTranslationPatch.TryTranslateQueuedMessage",),
        ),
        EvidenceFile(
            "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
            (
                "MonochromeOnsetFireEvent_TranslatesQueuedMessages_WhenOwnerPatched",
                "MonochromePoisonOnDamageFireEvent_TranslatesVisionBlurQueuedMessage_WhenOwnerPatched",
                "MonochromeOnsetFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                "MonochromeOnsetFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                "MonochromeOnsetFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            ),
        ),
        EvidenceFile(
            "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
            ("You feel a bit better.", "Your vision blurs.", "Your vision clears up."),
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/MonochromeOnset.cs::XRL.World.Effects.MonochromeOnset.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MonochromeOnsetTranslationPatch)",
                        "XRL.World.Effects.MonochromeOnset|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts/MonochromePoisonOnDamage.cs::"
                "XRL.World.Parts.MonochromePoisonOnDamage.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                *common_evidence,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MonochromeOnsetTranslationPatch)",
                        "XRL.World.Parts.MonochromePoisonOnDamage|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
    )


def _ironshank_onset_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/IronshankOnset.cs::XRL.World.Effects.IronshankOnset.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/IronshankOnsetTranslationPatch.cs",
                    (
                        "IronshankOnsetTranslationPatch",
                        "TryTranslateQueuedMessage",
                        "Your legs ache at the joints.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("IronshankOnsetTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "IronshankOnsetFireEvent_TranslatesQueuedMessages_WhenOwnerPatched",
                        "IronshankOnsetFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "IronshankOnsetFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "IronshankOnsetFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(IronshankOnsetTranslationPatch)",
                        "XRL.World.Effects.IronshankOnset|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
    )


def _adrenal_control_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/AdrenalControl2.cs::XRL.World.Parts.Mutation.AdrenalControl2.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/AdrenalControlTranslationPatch.cs",
                    ("AdrenalControlTranslationPatch", "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("AdrenalControlTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "AdrenalControlFireEvent_TranslatesQueuedMessages_WhenOwnerPatched",
                        "AdrenalControlFireEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "AdrenalControlFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "AdrenalControlFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AdrenalControlTranslationPatch)",
                        "XRL.World.Parts.Mutation.AdrenalControl2|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
                    ("Your adrenaline subsides.", "{{G|Your adrenaline starts to flow.}}"),
                ),
            ),
        ),
    )


def _amnesia_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Amnesia.cs::XRL.World.Parts.Mutation.Amnesia.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/AmnesiaTranslationPatch.cs",
                    ("AmnesiaTranslationPatch", "TryTranslateQueuedMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("AmnesiaTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "AmnesiaHandleEvent_TranslatesQueuedMessages_WhenOwnerPatched",
                        "AmnesiaHandleEvent_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "AmnesiaHandleEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "AmnesiaHandleEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AmnesiaTranslationPatch)",
                        "XRL.World.Parts.Mutation.Amnesia|HandleEvent|System.Boolean|XRL.World.SecretVisibilityChangedEvent",
                        "XRL.World.Parts.Mutation.Amnesia|HandleEvent|System.Boolean|XRL.World.EnteredCellEvent",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
                    ("You feel like you forgot something important.", "This place feels vaguely familiar."),
                ),
            ),
        ),
    )


def _fixed_owner_queue_families() -> tuple[CoveredOwnerFamily, ...]:
    fire_event_tests = (
        "SimpleFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched",
        "SimpleFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
        "SimpleFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
        "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
    )
    dictionary = EvidenceFile(
        "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
        (
            "You lurch suddenly!",
            "You feel your bones fracture.",
            "{{r|You surge with energy!}}",
            "You feel uneasy.",
            "You stop meditating and feel refreshed.",
            "You stop meditating.",
            "{{G|You were decapitated, but a new head regrew immediately!}}",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        (
            "BlinkingTicTranslationPatch.TryTranslateQueuedMessage",
            "BrittleBonesTranslationPatch.TryTranslateQueuedMessage",
            "ElectromagneticImpulseTranslationPatch.TryTranslateQueuedMessage",
            "FearAuraTranslationPatch.TryTranslateQueuedMessage",
            "MeditatingTranslationPatch.TryTranslateQueuedMessage",
            "RegenerationTranslationPatch.TryTranslateQueuedMessage",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/BlinkingTic.cs::XRL.World.Parts.Mutation.BlinkingTic.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BlinkingTicTranslationPatch.cs",
                    ("BlinkingTicTranslationPatch", "XRL.World.Parts.Mutation.BlinkingTic", "You lurch suddenly!"),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (*fire_event_tests, "typeof(BlinkingTicTranslationPatch)", "You lurch suddenly!"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BlinkingTicTranslationPatch)",
                        "XRL.World.Parts.Mutation.BlinkingTic|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/BlinkingTicSickness.cs::XRL.World.Effects.BlinkingTicSickness.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BlinkingTicTranslationPatch.cs",
                    ("BlinkingTicTranslationPatch", "XRL.World.Effects.BlinkingTicSickness", "You lurch suddenly!"),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (*fire_event_tests, "typeof(BlinkingTicTranslationPatch)", "You lurch suddenly!"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BlinkingTicTranslationPatch)",
                        "XRL.World.Effects.BlinkingTicSickness|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/BrittleBones.cs::XRL.World.Parts.Mutation.BrittleBones.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BrittleBonesTranslationPatch.cs",
                    ("BrittleBonesTranslationPatch", "TryTranslateQueuedMessage", "You feel your bones fracture."),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (*fire_event_tests, "typeof(BrittleBonesTranslationPatch)", "You feel your bones fracture."),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(BrittleBonesTranslationPatch)",
                        "XRL.World.Parts.Mutation.BrittleBones|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/ElectromagneticImpulse.cs::XRL.World.Parts.Mutation.ElectromagneticImpulse.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/ElectromagneticImpulseTranslationPatch.cs",
                    (
                        "ElectromagneticImpulseTranslationPatch",
                        "TryTranslateQueuedMessage",
                        "{{r|You surge with energy!}}",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        *fire_event_tests,
                        "typeof(ElectromagneticImpulseTranslationPatch)",
                        "{{r|You surge with energy!}}",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ElectromagneticImpulseTranslationPatch)",
                        "XRL.World.Parts.Mutation.ElectromagneticImpulse|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/FearAura.cs::XRL.World.Parts.Mutation.FearAura.ApplyFear",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/FearAuraTranslationPatch.cs",
                    ("FearAuraTranslationPatch", "TryTranslateQueuedMessage", "You feel uneasy."),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "FearAuraApplyFear_TranslatesFixedQueuedMessage_WhenOwnerPatched",
                        "FearAuraApplyFear_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "FearAuraApplyFear_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "You feel uneasy.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(FearAuraTranslationPatch)",
                        "XRL.World.Parts.Mutation.FearAura|ApplyFear|System.Boolean|XRL.World.MentalAttackEvent",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Meditating.cs::XRL.World.Effects.Meditating.Remove",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MeditatingTranslationPatch.cs",
                    ("MeditatingTranslationPatch", "TryTranslateQueuedMessage", "You stop meditating."),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "MeditatingRemove_TranslatesFixedQueuedMessages_WhenOwnerPatched",
                        "MeditatingRemove_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "MeditatingRemove_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "You stop meditating and feel refreshed.",
                        "You stop meditating.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MeditatingTranslationPatch)",
                        "XRL.World.Effects.Meditating|Remove|System.Void|XRL.World.GameObject",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Regeneration.cs::XRL.World.Parts.Mutation.Regeneration.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/RegenerationTranslationPatch.cs",
                    (
                        "RegenerationTranslationPatch",
                        "TryTranslateQueuedMessage",
                        "{{G|You were decapitated, but a new head regrew immediately!}}",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        *fire_event_tests,
                        "typeof(RegenerationTranslationPatch)",
                        "{{G|You were decapitated, but a new head regrew immediately!}}",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(RegenerationTranslationPatch)",
                        "XRL.World.Parts.Mutation.Regeneration|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
    )


def _effect_static_message_families() -> tuple[CoveredOwnerFamily, ...]:
    effect_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        (
            "EffectStaticApply_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "EffectStaticFireEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "EffectStaticBeginTakeAction_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "EffectStaticApply_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EffectStaticFireEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EffectStaticBeginTakeAction_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EffectStaticApply_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            "EffectStaticFireEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            "EffectStaticBeginTakeAction_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
        ),
    )
    dictionary = EvidenceFile(
        "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
        (
            "You start to feel sluggish.",
            "The hurdles that separate the will and the way begin to collapse.",
            "You feel stiff as a stone.",
            "You begin itching for a trigger.",
            "You start to prowl.",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("EffectStaticMessageTranslationPatch.TryTranslateQueuedMessage",),
    )
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EffectStaticMessageTranslationPatch.cs",
        ("EffectStaticMessageTranslationPatch", "TryTranslateQueuedMessage"),
    )
    countdown_patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EffectStaticMessageTranslationPatch.cs",
        ("TryTranslateCountdownMessage", "TryTranslateTurnRemainder", "TryTranslateCardinal"),
    )
    countdown_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        (
            "EffectStaticBeginTakeAction_TranslatesCountdownQueuedMessages_WhenOwnerPatched",
            "EffectStaticFireEvent_TranslatesCountdownQueuedMessages_WhenOwnerPatched",
            "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "EffectStaticBeginTakeAction_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EffectStaticBeginTakeAction_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/AxonsDeflated.cs::XRL.World.Effects.AxonsDeflated.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.AxonsDeflated|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/AxonsInflated.cs::XRL.World.Effects.AxonsInflated.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.AxonsInflated|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/BasiliskPoison.cs::XRL.World.Effects.BasiliskPoison.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.BasiliskPoison|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Berserk.cs::XRL.World.Effects.Berserk.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                countdown_patch,
                pipeline,
                countdown_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    ("until your berserker rage ends", "バーサークの怒りが終わるまであと"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.Berserk|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Cudgel_SmashingUp.cs::XRL.World.Effects.Cudgel_SmashingUp.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                countdown_patch,
                pipeline,
                countdown_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    ("until you stop demolishing", "解体をやめるまであと"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.Cudgel_SmashingUp|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/EmptyTheClips.cs::XRL.World.Effects.EmptyTheClips.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.EmptyTheClips|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Exhausted.cs::XRL.World.Effects.Exhausted.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    ("You are {{K|exhausted}}!", "疲労困憊"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.Exhausted|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Flagging.cs::XRL.World.Effects.Flagging.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                countdown_patch,
                pipeline,
                countdown_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    ("collapse from exhaustion", "疲労で倒れるまであと"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.Flagging|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/NocturnalApexed.cs::XRL.World.Effects.NocturnalApexed.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.NocturnalApexed|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Paralyzed.cs::XRL.World.Effects.Paralyzed.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    ("You are {{C|paralyzed}}.", "{{C|麻痺}}している。"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(EffectStaticMessageTranslationPatch)",
                        "XRL.World.Effects.Paralyzed|HandleEvent|System.Boolean|XRL.World.BeginTakeActionEvent",
                    ),
                ),
            ),
        ),
    )


def _stasis_attack_bounce_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Stasis.cs::XRL.World.Effects.Stasis.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/StasisTranslationPatch.cs",
                    ("StasisTranslationPatch", "TryTranslateAttackBounce", "ActorAttackPattern"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("StasisTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "StasisHandleEvent_TranslatesAttackBounceQueuedMessages_WhenOwnerPatched",
                        "StasisHandleEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "StasisHandleEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "bounces harmlessly off",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "XRL.World.Effects.Stasis",
                        "HandleEvent",
                        "XRL.World.BeforeApplyDamageEvent",
                    ),
                ),
            ),
        ),
    )


def _effect_generated_message_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EffectGeneratedMessageTranslationPatch.cs",
        (
            "EffectGeneratedMessageTranslationPatch",
            "TryTranslateGeneratedEffectMessage",
            "DoesVerbRouteTranslator.TryTranslatePlainSentence",
            "PossessiveLifeDrainPattern",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("EffectGeneratedMessageTranslationPatch.TryTranslateQueuedMessage",),
    )
    effect_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        (
            "EffectGeneratedHandleEvent_TranslatesLifeDrainQueuedMessages_WhenOwnerPatched",
            "EffectGeneratedApply_TranslatesShatteredArmorQueuedMessages_WhenOwnerPatched",
            "EffectGeneratedHandleEvent_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EffectGeneratedApply_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EffectGeneratedHandleEvent_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            "EffectGeneratedApply_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/LifeDrain.cs::XRL.World.Effects.LifeDrain.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "The 熊 resists your life drain!",
                        "You resist snapjaw's life drain!",
                        "EffectGeneratedHandleEvent_TranslatesLifeDrainQueuedMessages_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "typeof(EffectGeneratedMessageTranslationPatch)",
                        "XRL.World.Effects.LifeDrain|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/ShatteredArmor.cs::XRL.World.Effects.ShatteredArmor.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                effect_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "The 装置 was cracked.",
                        "装置にひびが入った",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "typeof(EffectGeneratedMessageTranslationPatch)",
                        "XRL.World.Effects.ShatteredArmor|Apply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _generated_queue_does_verb_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/GeneratedQueueDoesVerbTranslationPatch.cs",
        (
            "GeneratedQueueDoesVerbTranslationPatch",
            "DoesVerbRouteTranslator.TryTranslateMarkedMessage",
            "DoesVerbRouteTranslator.TryTranslatePlainSentence",
            "MessageFrameTranslator.TryStripDirectTranslationMarker",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("GeneratedQueueDoesVerbTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        (
            "GeneratedQueueDoesVerb_TranslatesDoesVerbMessages_WhenOwnerPatched",
            "GeneratedQueueDoesVerb_TranslatesMarkedDoesVerbMessages_WhenOwnerPatched",
            "GeneratedQueueDoesVerb_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "GeneratedQueueDoesVerb_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched",
            "GeneratedQueueDoesVerb_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
            "UseRepositoryMessageFrames",
        ),
    )
    frames = EvidenceFile(
        "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
        (
            "lost in the goop",
            "to fizz hungrily",
            "under the pressure of normality and (?:implodes|implode)",
            '"verb": "reclaim"',
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.AI.GoalHandlers/DropOffStolenGoods.cs::XRL.World.AI.GoalHandlers.DropOffStolenGoods.MoveToDropoff",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "DropOffStolenGoods",
                        "DropDownPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        *tests.required_substrings,
                        "DropOffStolenGoodsMoveToDropoff",
                        "folded carbide dagger",
                        "を{{y|shaft}}に落とした。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.AI.GoalHandlers.DropOffStolenGoods|MoveToDropoff|System.Void",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
                    (
                        '"verb": "drop"',
                        '"extra": "{0} down {1}"',
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.AI.GoalHandlers/PaxKlanqMadness.cs::XRL.World.AI.GoalHandlers.PaxKlanqMadness.TakeAction",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "PaxKlanqMadness",
                        "PaxKlanqPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        *tests.required_substrings,
                        "PaxKlanqMadnessTakeAction",
                        "shouts {{O|KLANQ}}",
                        "snapjawは{{O|KLANQ}}と叫んだ",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.AI.GoalHandlers.PaxKlanqMadness|TakeAction|System.Void",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
                    (
                        '"verb": "shout"',
                        "shouts? KLANQ",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Anatomy/BodyPart.cs::XRL.World.Anatomy.BodyPart.UnequipPartAndChildren",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        *tests.required_substrings,
                        "BodyPartUnequipPartAndChildren",
                        "Your {{Y|carbide dagger}} falls to the ground.",
                        "Your {{Y|carbide dagger}}は地面に倒れた。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.Anatomy.BodyPart|UnequipPartAndChildren|System.Void|System.Boolean|XRL.World.IInventory|System.Boolean",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
                    (
                        '"verb": "fall"',
                        '"extra": "to the ground"',
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/ExtradimensionalLoot.cs::XRL.World.Parts.ExtradimensionalLoot.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (
                        *patch.required_substrings,
                        "ExtradimensionalLoot",
                        "ExtradimensionalLootPattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        *tests.required_substrings,
                        "ExtradimensionalLootFireEvent",
                        "quantum tunnels and fully materializes in this dimension",
                        "hunterは{{Y|eigenrifle}}を落とし、偶然にもそれは量子トンネルを通ってこの次元に完全実体化した。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.Parts.ExtradimensionalLoot|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
                    (
                        "by sheer chance",
                        "quantum tunnel",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/GelatenousPalmProperties.cs::XRL.World.Parts.GelatenousPalmProperties.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "GelatenousPalmFireEvent",
                        "The steel sword is lost in the goop!",
                        "steel swordは粘液の中に沈んだ",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.Parts.GelatenousPalmProperties|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                frames,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/GraveMoss.cs::XRL.World.Parts.GraveMoss.Trigger",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "GraveMossTrigger",
                        "The 苔 starts to fizz hungrily.",
                        "苔は飢えたように泡立ち始めた",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.Parts.GraveMoss|Trigger|System.Void",
                    ),
                ),
                frames,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/QuantumRippler.cs::XRL.World.Parts.QuantumRippler.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "QuantumRipplerHandleEvent",
                        "collapses under the pressure of normality and implodes",
                        "装置は正常性の圧力に耐えきれず崩壊し、内破した",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.Parts.QuantumRippler|HandleEvent|System.Boolean|XRL.World.RealityStabilizeEvent",
                    ),
                ),
                frames,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/ReclamationCist.cs::XRL.World.Parts.ReclamationCist.PerformReclamationOf",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "PerformReclamationOf",
                        "The 回収装置 reclaims a 金属片.",
                        "回収装置は金属片を回収した。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(GeneratedQueueDoesVerbTranslationPatch)",
                        "XRL.World.Parts.ReclamationCist|PerformReclamationOf|System.Boolean|XRL.World.GameObject",
                    ),
                ),
                frames,
            ),
        ),
    )


def _auto_act_reset_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Capabilities/AutoAct.cs::XRL.World.Capabilities.AutoAct.ResetAutoexploreProperties",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/AutoActTranslationPatch.cs",
                    (
                        "AutoActTranslationPatch",
                        "ResetAutoexploreProperties",
                        "TryTranslateQueuedMessage",
                        "TryPreparePatternMessage",
                        "AutoAct",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("AutoActTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/AutoActTranslationPatchTests.cs",
                    (
                        "ResetAutoexploreProperties_TranslatesResetStatus_WithRepositoryPattern",
                        "ResetAutoexploreProperties_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "ResetAutoexploreProperties_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched",
                        "ResetAutoexploreProperties_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                        "Resetting AutoexploreAction_, AutoexploreSuppression on snapjaw",
                        "snapjaw上のAutoexploreAction_, AutoexploreSuppressionをリセットした。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(AutoActTranslationPatch)",
                        "XRL.World.Capabilities.AutoAct|ResetAutoexploreProperties|System.Boolean",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                    ("^Resetting (.+?) on (.+?)$",),
                ),
            ),
        ),
    )


def _prefixed_owner_queue_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PrefixedOwnerQueueTranslationPatch.cs",
        (
            "PrefixedOwnerQueueTranslationPatch",
            "TryTranslateQueuedMessage",
            "Translator.TryGetTranslation",
            "You are fleeing from ",
            "You are teleported by ",
            "You set a target temperature of ",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("PrefixedOwnerQueueTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PrefixedOwnerQueueTranslationPatchTests.cs",
        (
            "Patch_TranslatesPrefixedQueueMessages_WithRepositoryDictionaries",
            "Patch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "Patch_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched",
            "Patch_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
            "UseRepositoryDictionaries",
        ),
    )
    dictionary = EvidenceFile(
        "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
        (
            "You are fleeing from ",
            "You are teleported by ",
            "You set a target temperature of ",
            "{target}から逃げ出している\uff01",
            "{source}によって転送された。",
            "目標温度を{temperature}に設定した。",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.AI.GoalHandlers/Flee.cs::XRL.World.AI.GoalHandlers.Flee.TakeAction",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "FleeTakeAction",
                        "You are fleeing from {{R|snapjaw}}!",
                        "{{R|snapjaw}}から逃げ出している\uff01",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PrefixedOwnerQueueTranslationPatch)",
                        "XRL.World.AI.GoalHandlers.Flee|TakeAction|System.Void",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Infiltrate.cs::XRL.World.Parts.Mutation.Infiltrate.performInfiltrate",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "InfiltratePerformInfiltrate",
                        "You are teleported by {{Y|phase spider}}.",
                        "{{Y|phase spider}}によって転送された。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PrefixedOwnerQueueTranslationPatch)",
                        "XRL.World.Parts.Mutation.Infiltrate|performInfiltrate|System.Void|XRL.World.Cell|System.Boolean",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/TemperatureController.cs::XRL.World.Parts.TemperatureController.ConfigureTemperatureController",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "TemperatureControllerConfigureTemperatureController",
                        "You set a target temperature of -500.",
                        "You set a target temperature of .",
                        "目標温度を-500に設定した。",
                        "目標温度をに設定した。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PrefixedOwnerQueueTranslationPatch)",
                        "XRL.World.Parts.TemperatureController|ConfigureTemperatureController|System.Void|XRL.World.GameObject|System.Boolean",
                    ),
                ),
                dictionary,
            ),
        ),
    )


def _blaze_tonic_remove_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Blaze_Tonic.cs::XRL.World.Effects.Blaze_Tonic.Remove",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BlazeTonicRemoveTranslationPatch.cs",
                    (
                        "BlazeTonicRemoveTranslationPatch",
                        "TryTranslateBurnoutMessage",
                        "BurnoutPattern",
                        "Translator.Translate",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("BlazeTonicRemoveTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/world-effects-tonics.ja.json",
                    (
                        "{{blaze|blaze}} tonic",
                        "{{blaze|ブレイズ}}トニック",
                        "XRL.World.Effects.Blaze_Tonic.Description",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "BlazeTonicRemove_TranslatesBurnoutQueuedMessage_WhenOwnerPatched",
                        "BlazeTonicRemove_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "BlazeTonicRemove_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "The {{blaze|blaze}} tonic burns out of your system.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "BlazeTonicRemoveTranslationPatch",
                        "XRL.World.Effects.Blaze_Tonic",
                        "Remove",
                        "XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _latched_onto_expired_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/LatchedOnto.cs::XRL.World.Effects.LatchedOnto.Expired",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/LatchedOntoExpiredTranslationPatch.cs",
                    (
                        "LatchedOntoExpiredTranslationPatch",
                        "TryTranslateReleaseMessage",
                        "ReleasePlayerPattern",
                        "ReleaseTargetPattern",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("LatchedOntoExpiredTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "LatchedOntoExpired_TranslatesReleaseQueuedMessages_WhenOwnerPatched",
                        "LatchedOntoExpired_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "LatchedOntoExpired_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "releases}} you.",
                        "releases}} {{G|the snapjaw}}",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "LatchedOntoExpiredTranslationPatch",
                        "XRL.World.Effects.LatchedOnto",
                        "Expired",
                    ),
                ),
            ),
        ),
    )


def _giant_clam_teleport_joppa_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/GiantClamProperties.cs::XRL.World.Parts.GiantClamProperties.TeleportJoppaWorld",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/GiantClamTeleportTranslationPatch.cs",
                    (
                        "GiantClamTeleportTranslationPatch",
                        "TeleportJoppaWorld",
                        "You hear a shloop and the world around you shifts.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("GiantClamTeleportTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "GiantClamTeleport_TranslatesShloopQueuedMessages_WhenOwnerPatched",
                        "GiantClamTeleport_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "GiantClamTeleport_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "TeleportJoppaWorld",
                        "You hear a shloop and then a hitch. Nothing happens.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "GiantClamTeleportTranslationPatch",
                        "XRL.World.Parts.GiantClamProperties|TeleportJoppaWorld|System.Void|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _single_callsite_owner_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/DecoyHologramEmitter.cs::XRL.World.Parts.DecoyHologramEmitter.CreateHolograms",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
                    (
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.DecoyHologramEmitter",
                        "CreateHolograms",
                        "DecoyHologramOutOfRange",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "That is out of range (3 squares)",
                        "DecoyHologramOutOfRange",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.DecoyHologramEmitter|CreateHolograms|XRL.World.Parts.ActivePartStatus|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/RandomAltarBaetyl.cs::XRL.World.Parts.RandomAltarBaetyl.HandleBaetylRewardWish",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
                    (
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.RandomAltarBaetyl",
                        "HandleBaetylRewardWish",
                        "BaetylRewardWish",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Generated {{Y|folded carbide axe}} as reward for {{C|oil}}",
                        "BaetylRewardWish",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.RandomAltarBaetyl|HandleBaetylRewardWish|System.Boolean|System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/Axe_Dismember.cs::XRL.World.Parts.Skill.Axe_Dismember.CastForceSuccess",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
                    (
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Axe_Dismember",
                        "CastForceSuccess",
                        "AxeDismemberSelfConfirmation",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Are you sure you want to dismember yourself?",
                        "AxeDismemberSelfConfirmation",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Axe_Dismember|CastForceSuccess|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Skill.Axe_Dismember|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/Cudgel_Slam.cs::XRL.World.Parts.Skill.Cudgel_Slam.Cast",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
                    (
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Cudgel_Slam",
                        "Cast",
                        "CudgelSlamSelfConfirmation",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Are you sure you want to slam yourself?",
                        "CudgelSlamSelfConfirmation",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Cudgel_Slam|Cast|System.Boolean|XRL.World.GameObject|XRL.World.Parts.Skill.Cudgel_Slam|System.String|XRL.World.GameObject|System.Boolean|System.Int32|System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/Persuasion_Proselytize.cs::XRL.World.Parts.Skill.Persuasion_Proselytize.AttemptProselytization",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
                    (
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Persuasion_Proselytize",
                        "AttemptProselytization",
                        "ProselytizeFollowerConfirmation",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Argyve is already your follower. Do you want to proselytize him anyway?",
                        "ProselytizeFollowerConfirmation",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Persuasion_Proselytize|AttemptProselytization|System.Boolean",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/Tinkering.cs::XRL.World.Parts.Skill.Tinkering.LearnNewRecipe",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SingleCallsiteOwnerPopupTranslationPatch.cs",
                    (
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Tinkering",
                        "LearnNewRecipe",
                        "TinkeringLearnRecipe",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("SingleCallsiteOwnerPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SingleCallsiteOwnerPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSingleCallsiteOwnerPopups_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "You have a flash of insight and scribe a {{Y|laser pistol schematic}}.",
                        "TinkeringLearnRecipe",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "SingleCallsiteOwnerPopupTranslationPatch",
                        "XRL.World.Parts.Skill.Tinkering|LearnNewRecipe|System.Void|XRL.World.GameObject|System.Int32|System.Int32",
                    ),
                ),
            ),
        ),
    )


def _point_of_interest_navigation_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World/PointOfInterest.cs::XRL.World.PointOfInterest.NavigateTo",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PointOfInterestNavigationPopupTranslationPatch.cs",
                    (
                        "PointOfInterestNavigationPopupTranslationPatch",
                        "XRL.World.PointOfInterest",
                        "NavigateTo",
                        "AlreadyAtPointOfInterest",
                        "NoPointOfInterestLocation",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("PointOfInterestNavigationPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/PointOfInterestNavigationPopupTranslationPatchTests.cs",
                    (
                        "NavigateTo_TranslatesNavigationFailurePopups_WhenOwnerPatched",
                        "NavigateTo_DoesNotTranslateNavigationFailurePopup_WhenOwnerAbsent",
                        "NavigateTo_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "NavigateTo_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "NavigateTo_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched",
                        "You are already at {{Y|rust well}}.",
                        "Somehow there seems to be no location for {{Y|forgotten ruins}}.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(PointOfInterestNavigationPopupTranslationPatch)",
                        "XRL.World.PointOfInterest|NavigateTo|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _run_start_running_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Run.cs::XRL.World.Parts.Run.StartRunning",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/RunStartRunningPopupTranslationPatch.cs",
                    (
                        "RunStartRunningPopupTranslationPatch",
                        "XRL.World.Parts.Run",
                        "StartRunning",
                        "WorldMapMovementMode",
                        "power skate",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("RunStartRunningPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/RunStartRunningPopupTranslationPatchTests.cs",
                    (
                        "StartRunning_TranslatesWorldMapMovementModePopup_WhenOwnerPatched",
                        "StartRunning_DoesNotTranslateWorldMapMovementModePopup_WhenOwnerAbsent",
                        "StartRunning_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "StartRunning_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "StartRunning_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched",
                        "You cannot run on the world map.",
                        "You cannot power skate on the world map.",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(RunStartRunningPopupTranslationPatch)",
                        "XRL.World.Parts.Run|StartRunning|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _historic_event_region_reveal_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="HistoryKit/HistoricEvent.cs::HistoryKit.HistoricEvent.PerformRegionReveal",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/HistoricEventRegionRevealPopupTranslationPatch.cs",
                    (
                        "HistoricEventRegionRevealPopupTranslationPatch",
                        "HistoryKit.HistoricEvent",
                        "PerformRegionReveal",
                        "RegionRevealLocation",
                        "JournalNotificationTranslator.TryTranslate",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("HistoricEventRegionRevealPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/HistoricEventRegionRevealPopupTranslationPatchTests.cs",
                    (
                        "PerformRegionReveal_TranslatesRegionRevealPopup_WhenOwnerPatched",
                        "PerformRegionReveal_DoesNotClaimRegionRevealPopup_WhenOwnerAbsent",
                        "PerformRegionReveal_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "PerformRegionReveal_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "PerformRegionReveal_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched",
                        "You discover the location of {{Y|Omonporch}}.",
                        "{{Y|Omonporch}}の場所を発見した。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(HistoricEventRegionRevealPopupTranslationPatch)",
                        "HistoryKit.HistoricEvent|PerformRegionReveal|System.Void",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/journal-patterns.ja.json",
                    ("^You discover the location of (.+?)[.!]?$",),
                ),
            ),
        ),
    )


def _kill_missile_weapon_chirp_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.AI.GoalHandlers/Kill.cs::XRL.World.AI.GoalHandlers.Kill.TryMissileWeapon",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/KillMissileWeaponChirpTranslationPatch.cs",
                    (
                        "KillMissileWeaponChirpTranslationPatch",
                        "XRL.World.AI.GoalHandlers.Kill",
                        "TryMissileWeapon",
                        "Something chirps",
                        "to the southwest",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("KillMissileWeaponChirpTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/KillMissileWeaponChirpTranslationPatchTests.cs",
                    (
                        "TryMissileWeapon_TranslatesAudibleChirpMessage_WhenOwnerPatched",
                        "TryMissileWeapon_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "TryMissileWeapon_DoesNotRetranslateDirectMarkedMessage_WhenOwnerPatched",
                        "TryMissileWeapon_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                        "TryMissileWeapon_LeavesUnsupportedDirectionUnchanged_WhenOwnerPatched",
                        "Something chirps here.",
                        "Something chirps to the north.",
                        "ここで何かが鳴いた。",
                        "北側で何かが鳴いた。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(KillMissileWeaponChirpTranslationPatch)",
                        "XRL.World.AI.GoalHandlers.Kill|TryMissileWeapon|System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _requires_power_to_equip_check_equip_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/RequiresPowerToEquip.cs::XRL.World.Parts.RequiresPowerToEquip.CheckEquip",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/RequiresPowerToEquipCheckEquipPopupTranslationPatch.cs",
                    (
                        "RequiresPowerToEquipCheckEquipPopupTranslationPatch",
                        "XRL.World.Parts.RequiresPowerToEquip",
                        "CheckEquip",
                        "PowerLossUnequip",
                        "operating; you unequip",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("RequiresPowerToEquipCheckEquipPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/RequiresPowerToEquipCheckEquipPopupTranslationPatchTests.cs",
                    (
                        "CheckEquip_TranslatesPowerLossUnequipPopup_WhenOwnerPatched",
                        "CheckEquip_DoesNotClaimPowerLossUnequipPopup_WhenOwnerAbsent",
                        "CheckEquip_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "CheckEquip_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "CheckEquip_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched",
                        "Your {{Y|floating glowsphere}} stops operating; you unequip it.",
                        "{{Y|floating glowsphere}}は動作を停止した。あなたはそれを外した。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(RequiresPowerToEquipCheckEquipPopupTranslationPatch)",
                        "XRL.World.Parts.RequiresPowerToEquip|CheckEquip|System.Void",
                    ),
                ),
            ),
        ),
    )


def _xrl_core_owner_queue_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.Core/XRLCore.cs::XRL.Core.XRLCore.HotloadConfiguration",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/XrlCoreHotloadConfigurationTranslationPatch.cs",
                    (
                        "XrlCoreHotloadConfigurationTranslationPatch",
                        "HotloadConfiguration",
                        "Configuration hotloaded...",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("XrlCoreHotloadConfigurationTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                    (
                        "XrlCoreHotloadConfigurationPatch_TranslatesQueuedMessage_WhenOwnerPatched",
                        "XrlCoreHotloadConfigurationPatch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "XrlCoreHotloadConfigurationPatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "XrlCoreHotloadConfigurationPatch_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "設定をホットロードした...",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "XrlCoreHotloadConfigurationTranslationPatch",
                        "HotloadConfiguration",
                        "XRL.Core.XRLCore",
                        "System.Boolean",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.Core/XRLCore.cs::XRL.Core.XRLCore.RenderBaseToBuffer",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/XrlCoreLostSightTranslationPatch.cs",
                    (
                        "XrlCoreLostSightTranslationPatch",
                        "RenderBaseToBuffer",
                        "You have lost sight of ",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("XrlCoreLostSightTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                    (
                        "XrlCoreLostSightPatch_RecordsOwnerRouteTransforms_WithoutMessageLogSinkObservation_WhenPatched",
                        "You have lost sight of bloody Naruur.",
                        "bloody Naruurを見失った。",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "XrlCoreLostSightTranslationPatch",
                        "RenderBaseToBuffer",
                        "XRL.Core.XRLCore",
                        "ConsoleLib.Console.ScreenBuffer",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                    (
                        "^You have lost sight of (?:the )?(.+?)[.!]?$",
                        "{0}を見失った。",
                    ),
                ),
            ),
        ),
    )


def _brain_owner_surface_families() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Brain.cs::XRL.World.Parts.Brain.Think",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BrainThinkTranslationPatch.cs",
                    (
                        "BrainThinkTranslationPatch",
                        "Think",
                        "thinks: '",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("BrainThinkTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/BrainOwnerTranslationPatchTests.cs",
                    (
                        "Think_TranslatesQueuedThought_WhenOwnerPatched",
                        "Think_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "Think_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "Think_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        "snapjaw thinks: 'kill the intruder'",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "BrainThinkTranslationPatch",
                        "Think",
                        "XRL.World.Parts.Brain",
                        "System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Brain.cs::XRL.World.Parts.Brain.WriteFeelingSamples",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/BrainWriteFeelingSamplesPopupTranslationPatch.cs",
                    (
                        "BrainWriteFeelingSamplesPopupTranslationPatch",
                        "WriteFeelingSamples",
                        "feelings written to",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("BrainWriteFeelingSamplesPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/BrainOwnerTranslationPatchTests.cs",
                    (
                        "WriteFeelingSamples_TranslatesPopup_WhenOwnerPatched",
                        "WriteFeelingSamples_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "WriteFeelingSamples_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "WriteFeelingSamples_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "42 feelings written to AllFeelings.txt in /tmp/qud!",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "BrainWriteFeelingSamplesPopupTranslationPatch",
                        "WriteFeelingSamples",
                        "XRL.World.Parts.Brain",
                        "System.Boolean",
                    ),
                ),
            ),
        ),
    )


def _cripple_apply_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Cripple.cs::XRL.World.Effects.Cripple.Apply",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CrippleApplyTranslationPatch.cs",
                    (
                        "CrippleApplyTranslationPatch",
                        "XRL.World.Effects.Cripple",
                        "Apply",
                        "TryTranslateQueuedMessage",
                        "MessageLogProducerTranslationHelpers.TryPreparePatternMessage",
                        "Cripple.Apply",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("CrippleApplyTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                    (
                        "^You are crippled for (.+?)!$",
                        "{t0}のあいだ手足が不自由になった！",  # noqa: RUF001
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "CrippleApply_TranslatesDurationMessage_WhenPatched",
                        "CrippleApply_LeavesUnknownOwnerMessageUnchanged_WhenPatched",
                        "CrippleApply_LeavesEmptyMessageUnchanged_WhenPatched",
                        "CrippleApply_PreservesColorTaggedDurationAndQueueColor_WhenPatched",
                        "CrippleApply_DirectMarkerPassesThroughWithoutRetranslation_WhenPatched",
                        "CrippleApply_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "MessageQueueSemanticPipeline_TranslatesActiveOwnerMessage",
                        "MessageQueueSemanticPipeline_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "MessageQueueSemanticPipeline_DoesNotRetranslateDirectMarkedMessage",
                        "nameof(DummyCrippleApplyTarget.Apply)",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "CrippleApplyTargetMethod_ResolvesExpectedFullSignature",
                        CRIPPLE_APPLY_TARGET_METHOD_FULL_SIGNATURE,
                    ),
                ),
            ),
        ),
    )


def _mutation_self_target_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MutationSelfTargetPopupTranslationPatch.cs",
        (
            "MutationSelfTargetPopupTranslationPatch",
            "TryTranslatePopupMessage",
            "SelfTargetConfirmation",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("MutationSelfTargetPopupTranslationPatch.TryTranslatePopupMessage",),
    )
    dictionary = EvidenceFile(
        "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
        (
            "^Are you sure you want to target (.+?)\\\\?$",
            "{0}を標的にしてもよいか？",  # noqa: RUF001
        ),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationSelfTargetPopupTranslationPatchTests.cs",
        (
            "Patch_TranslatesSelfTargetPopup_WhenOwnerPatched",
            "Patch_DoesNotClaimSelfTargetPopup_WhenOwnerAbsent",
            "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
            "nameof(DummyMutationSelfTargetProducer.BreatherBaseCast)",
            "nameof(DummyMutationSelfTargetProducer.FlamingRayCast)",
            "nameof(DummyMutationSelfTargetProducer.FreezeBreathFireEvent)",
            "nameof(DummyMutationSelfTargetProducer.FreezingRayCast)",
        ),
    )

    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/BreatherBase.cs::XRL.World.Parts.Mutation.BreatherBase.Cast",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(patch.path, (*patch.required_substrings, "XRL.World.Parts.Mutation.BreatherBase", "Cast")),
                pipeline,
                dictionary,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MutationSelfTargetPopupTranslationPatch)",
                        "XRL.World.Parts.Mutation.BreatherBase|Cast|System.Boolean|XRL.World.Parts.Mutation.BreatherBase",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/FlamingRay.cs::XRL.World.Parts.Mutation.FlamingRay.Cast",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(patch.path, (*patch.required_substrings, "XRL.World.Parts.Mutation.FlamingRay", "Cast")),
                pipeline,
                dictionary,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MutationSelfTargetPopupTranslationPatch)",
                        "XRL.World.Parts.Mutation.FlamingRay|Cast|System.Boolean|XRL.World.Parts.Mutation.FlamingRay|System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/FreezeBreath.cs::XRL.World.Parts.Mutation.FreezeBreath.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    patch.path,
                    (*patch.required_substrings, "XRL.World.Parts.Mutation.FreezeBreath", "FireEvent"),
                ),
                pipeline,
                dictionary,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MutationSelfTargetPopupTranslationPatch)",
                        "XRL.World.Parts.Mutation.FreezeBreath|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/FreezingRay.cs::XRL.World.Parts.Mutation.FreezingRay.Cast",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(patch.path, (*patch.required_substrings, "XRL.World.Parts.Mutation.FreezingRay", "Cast")),
                pipeline,
                dictionary,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(MutationSelfTargetPopupTranslationPatch)",
                        "XRL.World.Parts.Mutation.FreezingRay|Cast|System.Boolean|XRL.World.Parts.Mutation.FreezingRay|System.String",
                    ),
                ),
            ),
        ),
    )


def _system_static_message_families() -> tuple[CoveredOwnerFamily, ...]:
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        (
            "SystemStaticCheckpointOn_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "SystemStaticSetHolyZone_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "SystemStaticFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "SystemStaticMutationFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "SystemStaticWorldTeleporterFireEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "SystemStaticQuantumJittersSunder_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "SystemStaticSpacetimeVortex_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "SystemStaticQuake_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "SystemStaticDoorSwitchFireEvent_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "SystemStaticSpawningEggSacTickEgg_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "SystemStaticLuminousInfectionTryGrowMushroom_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "SystemStaticTorchPropertiesHandleEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "SystemStaticTeleportationCast_TranslatesFixedQueuedMessages_WhenOwnerPatched",
            "SystemStaticCatacombsExitTeleporterHandleEvent_TranslatesFixedQueuedMessage_WhenOwnerPatched",
            "SystemStaticQuake_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "SystemStaticQuake_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            "SystemStatic_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "SystemStatic_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
            "FixedOwnerQueue_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
        ),
    )
    dictionary = EvidenceFile(
        "Mods/QudJP/Localization/Dictionaries/ui-messagelog-world.ja.json",
        (
            "Checkpointing enabled",
            "You feel a sense of holiness here.",
            "&CA flash of insight overcomes you!",
            "You do that with ease.",
            "That creature is of too high a level to duplicate!",
            "Your focus slips, causing you to dent spacetime in the local region.",
            "{{G|You sunder spacetime.}}",
            "The ground shakes violently!",
            "The ground shakes violently and loose rock falls from the ceiling!",
            "You are sucked through the surface of the sphere!",
            "The security door unlocks with a loud clank and swings open.",
            "The security door swings closed and locks with a loud clank.",
            "Nothing seems to happen when you hit the switch.",
            "The membrane of the egg sac snots apart.",
            "The svardym eggs hatch.",
            "The svardym egg hatches.",
            "Your torch burns out!",
            "You are shunted to another location!",
            "You teleport!",
            "You are teleported to an exit.",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("SystemStaticMessageTranslationPatch.TryTranslateQueuedMessage",),
    )
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/SystemStaticMessageTranslationPatch.cs",
        (
            "SystemStaticMessageTranslationPatch",
            "TryTranslateQueuedMessage",
            "You sprout a {{C|luminous hoarshroom}}.",
            "Your torch burns out!",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL/CheckpointingSystem.cs::XRL.CheckpointingSystem.CheckpointOn",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.CheckpointingSystem|CheckpointOn|System.Boolean",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL/HolyPlaceSystem.cs::XRL.HolyPlaceSystem.SetHolyZone",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.HolyPlaceSystem|SetHolyZone|System.Void|XRL.World.Zone|XRL.World.Faction",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/HeightenedIntelligence.cs::"
                "XRL.World.Parts.Mutation.HeightenedIntelligence.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.Mutation.HeightenedIntelligence|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/HeightenedAgility.cs::"
                "XRL.World.Parts.Mutation.HeightenedAgility.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.Mutation.HeightenedAgility|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/Metamorphosis.cs::"
                "XRL.World.Parts.Mutation.Metamorphosis.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.Mutation.Metamorphosis|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/SpacetimeVortex.cs::"
                "XRL.World.Parts.Mutation.SpacetimeVortex.Vortex"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.Mutation.SpacetimeVortex|Vortex|System.Void|XRL.World.Cell",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/QuantumJitters.cs::"
                "XRL.World.Parts.Mutation.QuantumJitters.Sunder"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.Mutation.QuantumJitters|Sunder|System.Void",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/TrembleEarthquakes.cs::XRL.World.Parts.TrembleEarthquakes.Quake",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.TrembleEarthquakes|Quake|System.Void",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/WorldTeleporter.cs::XRL.World.Parts.WorldTeleporter.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.WorldTeleporter|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/DoorSwitch.cs::XRL.World.Parts.DoorSwitch.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.DoorSwitch|FireEvent|System.Boolean|XRL.World.Event",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/SpawningEggSac.cs::XRL.World.Parts.SpawningEggSac.tickEgg",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.SpawningEggSac|tickEgg|System.Void",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/LuminousInfection.cs::XRL.World.Parts.LuminousInfection.TryGrowMushroom",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.LuminousInfection|TryGrowMushroom|System.Void",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/TorchProperties.cs::XRL.World.Parts.TorchProperties.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.TorchProperties|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Teleportation.cs::XRL.World.Parts.Mutation.Teleportation.Cast",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.Mutation.Teleportation|Cast|System.Boolean|XRL.World.Parts.Mutation.Teleportation|System.String|XRL.World.IEvent|XRL.World.Cell|XRL.World.GameObject|System.Boolean|System.Int32",
                    ),
                ),
                dictionary,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/CatacombsExitTeleporter.cs::XRL.World.Parts.CatacombsExitTeleporter.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(SystemStaticMessageTranslationPatch)",
                        "XRL.World.Parts.CatacombsExitTeleporter|HandleEvent|System.Boolean|XRL.World.ObjectEnteredCellEvent",
                    ),
                ),
                dictionary,
            ),
        ),
    )


def _existing_popup_owner_route_families() -> tuple[CoveredOwnerFamily, ...]:
    mutations_api_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationsApiTranslationPatchTests.cs",
        (
            "BuyRandomMutation_TranslatesConfirmationMessage_WhenPatched",
            "BuyRandomMutation_PreservesColorTagsInConfirmationMessage_WhenPatched",
            "TryTranslatePopupMessage_ReturnsFalse_ForEmptyInput",
            "TryTranslatePopupMessage_ReturnsFalse_ForDirectTranslationMarker",
        ),
    )
    high_scores_delete_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/HighScoresDeletePopupTranslationPatchTests.cs",
        (
            "HandleDelete_TranslatesDeleteConfirmationPopup_WhenOwnerPatched",
            "HandleDelete_DoesNotTranslateDeleteConfirmationPopup_WhenOwnerAbsent",
            "HandleDelete_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "HandleDelete_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
        ),
    )
    old_save_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/OldSaveContinueMenuPopupTranslationPatchTests.cs",
        (
            "Patch_TranslatesOldSavePopup_WhenOwnerPatched",
            "Patch_DoesNotRecordOwnerRoute_WhenOwnerAbsent",
            "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
        ),
    )
    golem_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/GolemQuestSelectionPopupTranslationPatchTests.cs",
        (
            "Patch_TranslatesGolemSelectionPopups_WhenOwnerPatched",
            "Patch_DoesNotTranslateGolemSelectionPopup_WhenOwnerAbsent",
            "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
        ),
    )
    popup_show_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        (
            "OldSaveContinueMenuPopupTranslationPatch.TryTranslatePopupMessage",
            "GolemQuestSelectionPopupTranslationPatch.TryTranslatePopupMessage",
            "HighScoresDeletePopupTranslationPatch.TryTranslatePopupMessage",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="Qud.API/MutationsAPI.cs::Qud.API.MutationsAPI.BuyRandomMutation",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MutationsApiTranslationPatch.cs",
                    ("MutationsApiTranslationPatch", "BuyRandomMutation", "BuyPromptPattern"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                    ("MutationsApiTranslationPatch.TryTranslatePopupMessage",),
                ),
                mutations_api_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "Qud.API.MutationsAPI",
                        "BuyRandomMutation",
                        "System.Int32",
                        "System.Boolean",
                        "System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="Qud.UI/HighScoresScreen.cs::Qud.UI.HighScoresScreen.HandleDelete",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/HighScoresDeletePopupTranslationPatch.cs",
                    ("HighScoresDeletePopupTranslationPatch", "HandleDelete", "DeleteConfirmationPattern"),
                ),
                popup_show_pipeline,
                high_scores_delete_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "Qud.UI.HighScoresScreen",
                        "HandleDelete",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="Qud.UI/MainMenu.cs::Qud.UI.MainMenu.ContinueMenu",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/OldSaveContinueMenuPopupTranslationPatch.cs",
                    ("OldSaveContinueMenuPopupTranslationPatch", "Qud.UI.MainMenu", "ContinueMenu"),
                ),
                popup_show_pipeline,
                old_save_tests,
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
                    ("That save file looks like it's from an older save format revision ({0}). Sorry!",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "Qud.UI.MainMenu|ContinueMenu|System.Threading.Tasks.Task`1[[XRL.XRLGame]]",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="Qud.UI/SaveManagement.cs::Qud.UI.SaveManagement.ContinueMenu",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/OldSaveContinueMenuPopupTranslationPatch.cs",
                    ("OldSaveContinueMenuPopupTranslationPatch", "Qud.UI.SaveManagement", "ContinueMenu"),
                ),
                popup_show_pipeline,
                old_save_tests,
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/ui-popup.ja.json",
                    ("That save file looks like it's from an older save format revision ({0}). Sorry!",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "Qud.UI.SaveManagement|ContinueMenu|System.Threading.Tasks.Task`1[[XRL.XRLGame]]",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Quests.GolemQuest/GolemBodySelection.cs::"
                "XRL.World.Quests.GolemQuest.GolemBodySelection.WishSpec"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/GolemQuestSelectionPopupTranslationPatch.cs",
                    ("GolemQuestSelectionPopupTranslationPatch", "GolemBodySelection", "MissingBlueprintPattern"),
                ),
                popup_show_pipeline,
                golem_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "XRL.World.Quests.GolemQuest.GolemBodySelection|WishSpec|System.Void|System.String",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Quests.GolemQuest/GolemMaterialSelection.cs::"
                "XRL.World.Quests.GolemQuest.GolemMaterialSelection.Pick"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/GolemQuestSelectionPopupTranslationPatch.cs",
                    ("GolemQuestSelectionPopupTranslationPatch", "GolemMaterialSelection", "MissingRequirementPattern"),
                ),
                popup_show_pipeline,
                golem_tests,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "XRL.World.Quests.GolemQuest.GolemMaterialSelection",
                        "Pick",
                        "XRL.World.Quests.GolemQuest.GolemMaterialSelection`2|Pick|System.Void",
                    ),
                ),
            ),
        ),
    )


def _closure_only_popup_owner_families() -> tuple[CoveredOwnerFamily, ...]:
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        (
            "TinkeringBuildPopupTranslationPatch.TryTranslatePopupMessage",
            "AbsorbablePsychePopupTranslationPatch.TryTranslatePopupMessage",
            "DeployableInfrastructureTranslationPatch.TryTranslatePopupMessage",
            "DataDiskLearnPopupTranslationPatch.TryTranslatePopupMessage",
            "LocationFinderPopupTranslationPatch.TryTranslatePopupMessage",
            "ModMagnetizedTranslationPatch.TryTranslatePopupMessage",
            "SupplyableIntegratedHostPopupTranslationPatch.TryTranslatePopupMessage",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.UI/TinkeringScreen.cs::XRL.UI.TinkeringScreen.PerformUITinkerBuild",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/TinkeringBuildPopupTranslationPatch.cs",
                    ("TinkeringBuildPopupTranslationPatch", "PerformUITinkerBuild", "TryTranslateTinkerUp"),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringBuildPopupTranslationPatchTests.cs",
                    (
                        "PerformUITinkerBuild_TranslatesMissingIngredientPopup_WhenOwnerPatched",
                        "PerformUITinkerBuild_TranslatesSuccessPopups_WhenOwnerPatched",
                        "PerformUITinkerBuild_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "PerformUITinkerBuild_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "PerformUITinkerBuild_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    ("TargetMethod_ResolvesExpectedSignature", "XRL.UI.TinkeringScreen", "PerformUITinkerBuild"),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/AbsorbablePsyche.cs::XRL.World.Parts.AbsorbablePsyche.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/AbsorbablePsychePopupTranslationPatch.cs",
                    ("AbsorbablePsychePopupTranslationPatch", "HandleEvent", "TryTranslateConfirmation"),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/AbsorbablePsychePopupTranslationPatchTests.cs",
                    (
                        "HandleEvent_TranslatesConfirmationPopup_WhenOwnerPatched",
                        "HandleEvent_TranslatesEncodePopup_WhenOwnerPatched",
                        "HandleEvent_TranslatesRadiatePopup_WhenOwnerPatched",
                        "HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "HandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "HandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "XRL.World.Parts.AbsorbablePsyche",
                        "HandleEvent",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts/DeployableInfrastructure.cs::"
                "XRL.World.Parts.DeployableInfrastructure.AttemptDeploy"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/DeployableInfrastructureTranslationPatch.cs",
                    ("DeployableInfrastructureTranslationPatch", "AttemptDeploy", "NoUsefulWayPattern"),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DeployableInfrastructurePopupTranslationPatchTests.cs",
                    (
                        "AttemptDeploy_TranslatesDeploySuccessPopup_WhenOwnerPatched",
                        "AttemptDeploy_TranslatesNoUsefulWayPopup_WhenOwnerPatched",
                        "AttemptDeploy_LeavesPopupUnchanged_WhenOwnerAbsent",
                        "AttemptDeploy_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched",
                        "AttemptDeploy_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "XRL.World.Parts.DeployableInfrastructure|AttemptDeploy|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/DataDisk.cs::XRL.World.Parts.DataDisk.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/DataDiskLearnPopupTranslationPatch.cs",
                    (
                        "DataDiskLearnPopupTranslationPatch",
                        "HandleEvent",
                        "ItemModificationPattern",
                        "BuildRecipePattern",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/DataDiskLearnPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesItemModificationLearnPopup_WhenOwnerPatched",
                        "Patch_TranslatesBuildRecipeLearnPopup_WhenOwnerPatched",
                        "Patch_DoesNotTranslateDataDiskLearnPopup_WhenOwnerAbsent",
                        "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    ("TargetMethod_ResolvesExpectedSignature", "XRL.World.Parts.DataDisk", "HandleEvent"),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/LocationFinder.cs::XRL.World.Parts.LocationFinder.TriggerFind",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/LocationFinderPopupTranslationPatch.cs",
                    ("LocationFinderPopupTranslationPatch", "TriggerFind", "DiscoverPattern", "TravelPattern"),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/LocationFinderPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesLocationFinderPopup_WhenOwnerPatched",
                        "Patch_DoesNotRecordOwnerRoute_WhenOwnerAbsent",
                        "Patch_StripsDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "XRL.World.Parts.LocationFinder|TriggerFind|System.Void",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/ModMagnetized.cs::XRL.World.Parts.ModMagnetized.CheckFloating",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/ModMagnetizedTranslationPatch.cs",
                    (
                        "ModMagnetizedTranslationPatch",
                        "CheckFloating",
                        "DoesVerbRouteTranslator.TryTranslateMarkedMessage",
                    ),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/ModMagnetizedTranslationPatchTests.cs",
                    (
                        "CheckFloating_TranslatesDoesVerbPopup_WhenOwnerPatched",
                        "CheckFloating_LeavesPlainPopupUnchanged_WhenOwnerAbsent",
                        "CheckFloating_StripsDirectMarkerWithoutRecordingTransform_WhenOwnerPatched",
                        "CheckFloating_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "XRL.World.Parts.ModMagnetized|CheckFloating|System.Void",
                    ),
                ),
            ),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts/SupplyableIntegratedHost.cs::"
                "XRL.World.Parts.SupplyableIntegratedHost.AttemptSupply"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/SupplyableIntegratedHostPopupTranslationPatch.cs",
                    ("SupplyableIntegratedHostPopupTranslationPatch", "AttemptSupply", "NoNeededSuppliesPattern"),
                ),
                pipeline,
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/SupplyableIntegratedHostPopupTranslationPatchTests.cs",
                    (
                        "Patch_TranslatesSupplyableIntegratedHostPopup_WhenOwnerPatched",
                        "Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "Patch_StripsDirectMarkedPopup_WhenOwnerPatched",
                        "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "XRL.World.Parts.SupplyableIntegratedHost|AttemptSupply|System.Boolean|XRL.World.GameObject",
                    ),
                ),
            ),
        ),
    )


def _tinkering_mod_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.UI/TinkeringScreen.cs::XRL.UI.TinkeringScreen.PerformUITinkerMod",
            inventory_statuses=("needs_family_review",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/TinkeringModPopupTranslationPatch.cs",
                    (
                        "TinkeringModPopupTranslationPatch",
                        "PerformUITinkerMod",
                        "TryTranslateCore",
                        "DoesVerbRouteTranslator.TryTranslateMarkedMessage",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("TinkeringModPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/TinkeringModPopupTranslationPatchTests.cs",
                    (
                        "PerformUITinkerMod_TranslatesPopupMessages_WhenOwnerPatched",
                        "PerformUITinkerMod_TranslatesSifrahPrompt_WhenOwnerPatched",
                        "PerformUITinkerMod_TranslatesMarkedBestowalPopup_WhenOwnerPatched",
                        "PerformUITinkerMod_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent",
                        "PerformUITinkerMod_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "PerformUITinkerMod_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "XRL.UI.TinkeringScreen",
                        "PerformUITinkerMod",
                        "System.Collections.Generic.List`1[[XRL.World.GameObject]]",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
                    ('"verb": "seem"', '"extra": "to have taken on new qualities"'),
                ),
            ),
        ),
    )


def _force_bubble_owner_families() -> tuple[CoveredOwnerFamily, ...]:
    target_signatures = (
        (
            "XRL.World.Parts/ForceEmitter.cs::XRL.World.Parts.ForceEmitter.ActivateForceEmitter",
            "ActivateForceEmitter",
            "XRL.World.Parts.ForceEmitter|ActivateForceEmitter|System.Boolean|XRL.World.IEvent",
        ),
        (
            "XRL.World.Parts/Stopsvaalinn.cs::XRL.World.Parts.Stopsvaalinn.ActivateStopsvalinn",
            "ActivateStopsvalinn",
            "XRL.World.Parts.Stopsvaalinn|ActivateStopsvalinn|System.Boolean|XRL.World.IEvent",
        ),
        (
            "XRL.World.Parts.Mutation/ForceBubble.cs::XRL.World.Parts.Mutation.ForceBubble.DestroyBubble",
            "DestroyBubble",
            "XRL.World.Parts.Mutation.ForceBubble|DestroyBubble|System.Void|System.Boolean",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=family_id,
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/ForceBubbleOwnerTranslationPatch.cs",
                    (method_name, "TryTranslateQueuedMessage", "TryTranslateForceBubbleMessage"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("ForceBubbleOwnerTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "ForceBubbleOwner_TranslatesForceBubbleQueuedMessages_WhenOwnerPatched",
                        "ForceBubbleOwner_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "ForceBubbleOwner_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "ForceBubbleOwner_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ForceBubbleOwnerTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for family_id, method_name, signature in target_signatures
    )


def _combat_skill_extension_owner_families() -> tuple[CoveredOwnerFamily, ...]:
    family_evidence = (
        (
            "XRL.World.Parts.Skill/Cudgel_Backswing.cs::XRL.World.Parts.Skill.Cudgel_Backswing.FireEvent",
            ("Cudgel_Backswing", "BackswingPattern", "ActorBackswingPattern"),
            (
                "You backswing with {{Y|your cudgel}}.",
                "The snapjaw backswings with {{Y|its cudgel}}.",
            ),
            "XRL.World.Parts.Skill.Cudgel_Backswing|FireEvent|System.Boolean|XRL.World.Event",
        ),
        (
            "XRL.World.Parts.Skill/Discipline_IronMind.cs::XRL.World.Parts.Skill.Discipline_IronMind.FireEvent",
            (
                "Discipline_IronMind",
                "You muster your will and shake off some of your confusion.",
                "You muster your will and shake off your confusion.",
            ),
            (
                "You muster your will and shake off some of your confusion.",
                "You muster your will and shake off your confusion.",
            ),
            "XRL.World.Parts.Skill.Discipline_IronMind|FireEvent|System.Boolean|XRL.World.Event",
        ),
        (
            "XRL.World.Parts.Skill/Rifle_DrawABead.cs::XRL.World.Parts.Skill.Rifle_DrawABead.ValidateMark",
            (
                "Rifle_DrawABead",
                "You lose sight of your mark.",
                "Your tracking of your mark has been disrupted.",
            ),
            (
                "You lose sight of your mark.",
                "Your tracking of your mark has been disrupted.",
            ),
            "XRL.World.Parts.Skill.Rifle_DrawABead|ValidateMark|System.Void",
        ),
        (
            "XRL.World.Parts.Skill/Shield_Slam.cs::XRL.World.Parts.Skill.Shield_Slam.Slam",
            ("Shield_Slam", "ActorResistsShieldSlamPattern", "YouResistShieldSlamPattern"),
            (
                "The snapjaw resists your shield slam.",
                "You resist {{R|the snapjaw's shield slam}}.",
            ),
            "XRL.World.Parts.Skill.Shield_Slam|Slam|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.Cell|System.Boolean",
        ),
        (
            "XRL.World.Parts.Skill/ShortBlades_Rejoinder.cs::XRL.World.Parts.Skill.ShortBlades_Rejoinder.FireEvent",
            ("ShortBlades_Rejoinder", "RejoinderPattern", "ActorRejoinderPattern"),
            (
                "You rejoinder with {{Y|your dagger}}.",
                "The snapjaw rejoinders with {{Y|its dagger}}.",
            ),
            "XRL.World.Parts.Skill.ShortBlades_Rejoinder|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )

    return tuple(
        CoveredOwnerFamily(
            family_id=family_id,
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
                    ("TryTranslateQueuedMessage", *patch_tokens),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("CombatSkillMessageTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "CombatSkillMessages_TranslateInventoriedQueuedShapes_WhenOwnerPatched",
                        "CombatSkillMessages_DoNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "CombatSkillMessages_DoNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                        "CombatSkillMessages_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                        *test_tokens,
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(CombatSkillMessageTranslationPatch)",
                        signature,
                    ),
                ),
            ),
        )
        for family_id, patch_tokens, test_tokens, signature in family_evidence
    )


def _cybernetics_wish_implant_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Capabilities/Cybernetics.cs::XRL.World.Capabilities.Cybernetics.WishImplant",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/CyberneticsWishImplantPopupTranslationPatch.cs",
                    (
                        "WishImplant",
                        "TryTranslatePopupMessage",
                        "MissingBlueprintPattern",
                        "NotCyberneticPattern",
                        "MissingBodyPartPattern",
                        "ImplantedPattern",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("CyberneticsWishImplantPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsWishImplantPopupTranslationPatchTests.cs",
                    (
                        "WishImplant_TranslatesPopupMessages_WhenOwnerPatched",
                        "WishImplant_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "WishImplant_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "WishImplant_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "No blueprint by the name 'cybertorso' could be found.",
                        "Your {{Y|feet}} are implanted with {{G|motorized treads}}!",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "TargetMethod_ResolvesExpectedSignature",
                        "typeof(CyberneticsWishImplantPopupTranslationPatch)",
                        "XRL.World.Capabilities.Cybernetics",
                        "WishImplant",
                        "System.String",
                    ),
                ),
            ),
        ),
    )


def _map_reveal_popup_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/MapReveal.cs::XRL.World.Parts.MapReveal.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MapRevealPopupTranslationPatch.cs",
                    (
                        "MapRevealPopupTranslationPatch",
                        "HandleEvent",
                        "OwnerConsumptionWarningPattern",
                        "OrdinaryPaperPattern",
                        "MapOfSurroundingsPattern",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                    ("MapRevealPopupTranslationPatch.TryTranslatePopupMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/MapRevealPopupTranslationPatchTests.cs",
                    (
                        "HandleEvent_TranslatesInventoriedPopupMessages_WhenOwnerPatched",
                        "HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                        "HandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                        "HandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                        "will consume",
                        "ordinary piece of paper",
                        "a map of your surroundings",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                        "typeof(MapRevealPopupTranslationPatch)",
                        "XRL.World.Parts.MapReveal|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
                    ),
                ),
            ),
        ),
    )


def _experience_award_xp_family() -> tuple[CoveredOwnerFamily, ...]:
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Experience.cs::XRL.World.Parts.Experience.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/ExperienceAwardXpTranslationPatch.cs",
                    ("ExperienceAwardXpTranslationPatch", "TryTranslateQueuedMessage", "Experience.HandleEvent"),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                    ("ExperienceAwardXpTranslationPatch.TryTranslateQueuedMessage",),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                    (
                        "ExperienceAwardXp_TranslatesColorizedXpGain_WhenPatched",
                        "ExperienceAwardXp_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                        "ExperienceAwardXp_DirectMarkerPassesThroughWithoutRetranslation_WhenPatched",
                        "ExperienceAwardXp_LeavesEmptyMessageUnchanged_WhenPatched",
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                    (
                        "typeof(ExperienceAwardXpTranslationPatch)",
                        '"HandleEvent"',
                        '"XRL.World.Parts.Experience"',
                        '"XRL.World.AwardXPEvent"',
                    ),
                ),
                EvidenceFile(
                    "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                    ("^You gain (\\\\{\\\\{C\\\\|\\\\d+\\\\}\\\\}|\\\\d+) XP[.!]?$",),
                ),
            ),
        ),
    )


def _mutation_absorption_healing_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MutationAbsorptionHealingTranslationPatch.cs",
        (
            "MutationAbsorptionHealingTranslationPatch",
            "TryTranslateQueuedMessage",
            "ColdAbsorption",
            "HeatAbsorption",
            "You are healed for",
            "by the (?<source>cold|heat)",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("MutationAbsorptionHealingTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        (
            "MutationAbsorptionHealing_TranslatesGeneratedHealingMessage_WhenOwnerPatched",
            "MutationAbsorptionHealing_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "MutationAbsorptionHealing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "MutationAbsorptionHealing_LeavesUnsupportedMessageUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(MutationAbsorptionHealingTranslationPatch)",
            "XRL.World.Parts.Mutation.ColdAbsorption|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.Mutation.HeatAbsorption|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/ColdAbsorption.cs::"
                "XRL.World.Parts.Mutation.ColdAbsorption.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, target_tests),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/HeatAbsorption.cs::"
                "XRL.World.Parts.Mutation.HeatAbsorption.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, target_tests),
        ),
    )


def _on_eat_reward_message_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/OnEatRewardMessageTranslationPatch.cs",
        (
            "OnEatRewardMessageTranslationPatch",
            "TryTranslateQueuedMessage",
            "MPOnEat",
            "RefreshAllCooldownsOnEat",
            "MutationPointPattern",
            "CooldownRefreshPattern",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("OnEatRewardMessageTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
        (
            "OnEatReward_TranslatesGeneratedRewardMessages_WhenOwnerPatched",
            "OnEatReward_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "OnEatReward_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "OnEatReward_LeavesUnsupportedMessageUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(OnEatRewardMessageTranslationPatch)",
            "XRL.World.Parts.MPOnEat|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.RefreshAllCooldownsOnEat|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/MPOnEat.cs::XRL.World.Parts.MPOnEat.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, target_tests),
        ),
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts/RefreshAllCooldownsOnEat.cs::"
                "XRL.World.Parts.RefreshAllCooldownsOnEat.FireEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, pipeline, tests, target_tests),
        ),
    )


def _effect_mobility_block_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EffectMobilityBlockTranslationPatch.cs",
        (
            "EffectMobilityBlockTranslationPatch",
            "TryTranslateQueuedMessage",
            "TryTranslatePopupMessage",
            "Engulfed",
            "EngulfedBlockPattern",
            "Immobilized",
            "Stuck",
            "MobilityBlockPattern",
            "TryTranslateStatus",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("EffectMobilityBlockTranslationPatch.TryTranslateQueuedMessage",),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("EffectMobilityBlockTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EffectMobilityBlockTranslationPatchTests.cs",
        (
            "EffectMobilityBlock_TranslatesQueuedMobilityBlockMessages_WhenOwnerPatched",
            "EffectMobilityBlock_TranslatesPopupMobilityBlockMessages_WhenOwnerPatched",
            "EffectMobilityBlock_TranslatesEngulfedPopup_WhenOwnerPatched",
            "EffectMobilityBlock_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "EffectMobilityBlock_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "EffectMobilityBlock_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EffectMobilityBlock_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "EffectMobilityBlock_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(EffectMobilityBlockTranslationPatch)",
            "XRL.World.Effects.Engulfed|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Effects.Immobilized|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Effects.Stuck|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    evidence_files = (patch, queue_pipeline, popup_pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Engulfed.cs::XRL.World.Effects.Engulfed.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Immobilized.cs::XRL.World.Effects.Immobilized.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/Stuck.cs::XRL.World.Effects.Stuck.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _mutation_infection_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MutationInfectionTranslationPatch.cs",
        (
            "MutationInfectionTranslationPatch",
            "FireEvent",
            "TryTranslatePopupMessage",
            "GainedMutationPattern",
            "TranslateMutationDisplayName",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("MutationInfectionTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationInfectionTranslationPatchTests.cs",
        (
            "MutationInfectionFireEvent_TranslatesGainedMutationPopup_WhenOwnerPatched",
            "MutationInfectionFireEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "MutationInfectionFireEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "MutationInfectionFireEvent_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyMutationInfectionTarget",
            "FireEvent",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(MutationInfectionTranslationPatch)",
            "XRL.World.Effects.MutationInfection|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Effects/MutationInfection.cs::XRL.World.Effects.MutationInfection.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, popup_pipeline, tests, target_tests),
        ),
    )


def _mutation_action_failure_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MutationActionFailureTranslationPatch.cs",
        (
            "MutationActionFailureTranslationPatch",
            "ElectricalGeneration",
            "TeleportOther",
            "TryTranslatePopupMessage",
            "ElectricalGenerationDrinkFailurePattern",
            "TeleportOtherSelfTargetPattern",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("MutationActionFailureTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MutationActionFailureTranslationPatchTests.cs",
        (
            "ElectricalGenerationHandleEvent_TranslatesDrinkChargeFailurePopup_WhenOwnerPatched",
            "TeleportOtherFireEvent_TranslatesSelfTargetFailurePopup_WhenOwnerPatched",
            "MutationActionFailure_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "MutationActionFailure_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "MutationActionFailure_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyMutationActionFailureTarget",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(MutationActionFailureTranslationPatch)",
            "XRL.World.Parts.Mutation.ElectricalGeneration|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
            "XRL.World.Parts.Mutation.TeleportOther|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    evidence_files = (patch, popup_pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id=(
                "XRL.World.Parts.Mutation/ElectricalGeneration.cs::"
                "XRL.World.Parts.Mutation.ElectricalGeneration.HandleEvent"
            ),
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/TeleportOther.cs::XRL.World.Parts.Mutation.TeleportOther.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _disassembly_start_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/DisassemblyStartTranslationPatch.cs",
        (
            "DisassemblyStartTranslationPatch",
            "Continue",
            "TryTranslateQueuedMessage",
            "TryTranslatePopupMessage",
            "ReverseEngineerPromptPattern",
            "StartDisassemblingPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("DisassemblyStartTranslationPatch.TryTranslateQueuedMessage",),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("DisassemblyStartTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/DisassemblyStartTranslationPatchTests.cs",
        (
            "DisassemblyContinue_TranslatesReverseEngineeringPrompt_WhenOwnerPatched",
            "DisassemblyContinue_TranslatesStartDisassemblingMessage_WhenOwnerPatched",
            "DisassemblyContinue_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "DisassemblyContinue_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "DisassemblyContinue_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "DisassemblyContinue_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyDisassemblyStartTarget",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(DisassemblyStartTranslationPatch)",
            "XRL.World.Tinkering.Disassembly|Continue|System.Boolean",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Tinkering/Disassembly.cs::XRL.World.Tinkering.Disassembly.Continue",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, popup_pipeline, tests, target_tests),
        ),
    )


def _dance_ritual_opponent_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/DanceRitualOpponentTranslationPatch.cs",
        (
            "DanceRitualOpponentTranslationPatch",
            "FireEvent",
            "TryTranslatePopupMessage",
            "BusyDancingPattern",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("DanceRitualOpponentTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/DanceRitualOpponentTranslationPatchTests.cs",
        (
            "DanceRitualOpponentFireEvent_TranslatesBusyDancingPopup_WhenOwnerPatched",
            "DanceRitualOpponentFireEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "DanceRitualOpponentFireEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "DanceRitualOpponentFireEvent_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyDanceRitualOpponentTarget",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(DanceRitualOpponentTranslationPatch)",
            "XRL.World.Parts.DanceRitualOpponent|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/DanceRitualOpponent.cs::XRL.World.Parts.DanceRitualOpponent.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, popup_pipeline, tests, target_tests),
        ),
    )


def _iexamine_process_identify_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/IExamineEventProcessIdentifyTranslationPatch.cs",
        (
            "IExamineEventProcessIdentifyTranslationPatch",
            "ProcessIdentify",
            "TryTranslatePopupMessage",
            "IdentifyRealizationPattern",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("IExamineEventProcessIdentifyTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/IExamineEventProcessIdentifyTranslationPatchTests.cs",
        (
            "ProcessIdentify_TranslatesVisibleItemIdentifyPopup_WhenOwnerPatched",
            "ProcessIdentify_TranslatesDestroyedItemIdentifyPopup_WhenOwnerPatched",
            "ProcessIdentify_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
            "ProcessIdentify_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "ProcessIdentify_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyIExamineEventProcessIdentifyTarget",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(IExamineEventProcessIdentifyTranslationPatch)",
            "XRL.World.IExamineEvent|ProcessIdentify|System.Boolean",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World/IExamineEvent.cs::XRL.World.IExamineEvent.ProcessIdentify",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, popup_pipeline, tests, target_tests),
        ),
    )


def _self_tear_explosion_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/SelfTearExplosionTranslationPatch.cs",
        (
            "SelfTearExplosionTranslationPatch",
            "Clockwork",
            "Flywheel",
            "TryTranslateQueuedMessage",
            "TearsItselfApartPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("SelfTearExplosionTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/SelfTearExplosionTranslationPatchTests.cs",
        (
            "SelfTearExplosion_TranslatesOwnerMessage_WhenOwnerPatched",
            "SelfTearExplosion_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
            "SelfTearExplosion_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "SelfTearExplosion_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummySelfTearExplosionTarget",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(SelfTearExplosionTranslationPatch)",
            "XRL.World.Parts.Clockwork|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.Flywheel|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    evidence_files = (patch, queue_pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Clockwork.cs::XRL.World.Parts.Clockwork.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Flywheel.cs::XRL.World.Parts.Flywheel.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _tenfold_path_initiatory_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/TenfoldPathInitiatoryTranslationPatch.cs",
        (
            "TenfoldPathInitiatoryTranslationPatch",
            "TryTranslateQueuedMessage",
            "TryTranslatePopupMessage",
            "TenfoldPath_Ket",
            "TenfoldPath_Vur",
            "TenfoldPath_Yis",
            "SupernalLightPattern",
            "AttackInhibitionPattern",
            "SkillPointGainPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("TenfoldPathInitiatoryTranslationPatch.TryTranslateQueuedMessage",),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("TenfoldPathInitiatoryTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TenfoldPathInitiatoryTranslationPatchTests.cs",
        (
            "TenfoldPath_TranslatesQueuedInitiatoryMessages_WhenOwnerPatched",
            "TenfoldPath_TranslatesAttackInhibition_WhenFireEventOwnerPatched",
            "TenfoldPath_TranslatesPopupSkillPointReward_WhenOwnerPatched",
            "TenfoldPath_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "TenfoldPath_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "TenfoldPath_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "TenfoldPath_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(TenfoldPathInitiatoryTranslationPatch)",
            "XRL.World.Parts.Skill.TenfoldPath_Ket|HandleEvent|System.Boolean|XRL.World.BeforeDieEvent",
            "XRL.World.Parts.Skill.TenfoldPath_Vur|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.Skill.TenfoldPath_Yis|AddSkill|System.Boolean|XRL.World.GameObject",
        ),
    )
    evidence_files = (patch, queue_pipeline, popup_pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/TenfoldPath_Ket.cs::XRL.World.Parts.Skill.TenfoldPath_Ket.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/TenfoldPath_Vur.cs::XRL.World.Parts.Skill.TenfoldPath_Vur.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Skill/TenfoldPath_Yis.cs::XRL.World.Parts.Skill.TenfoldPath_Yis.AddSkill",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _power_entry_prerequisite_popup_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PowerEntryRequirementPopupTranslationPatch.cs",
        (
            "PowerEntryRequirementPopupTranslationPatch",
            "TryTranslatePopupMessage",
            "XRL.World.Skills.PowerEntry",
            "XRL.World.Skills.PowerEntryRequirement",
            "AlreadyHaveSkillPattern",
            "HaveEntryPattern",
            "UntilHaveEntryPattern",
            "AttributeRequirementPattern",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("PowerEntryRequirementPopupTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PowerEntryRequirementPopupTranslationPatchTests.cs",
        (
            "PowerEntry_TranslatesPrerequisitePopups_WhenOwnerPatched",
            "PowerEntryRequirement_TranslatesAttributePrerequisitePopup_WhenOwnerPatched",
            "PowerEntryRequirement_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "PowerEntryRequirement_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "PowerEntryRequirement_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(PowerEntryRequirementPopupTranslationPatch)",
            "XRL.World.Skills.PowerEntry|MeetsRequirements|System.Boolean|XRL.World.GameObject|System.Boolean",
            "XRL.World.Skills.PowerEntryRequirement|MeetsRequirement|System.Boolean|XRL.World.GameObject|System.Boolean",
        ),
    )
    evidence_files = (patch, popup_pipeline, tests, target_tests)
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Skills/PowerEntry.cs::XRL.World.Skills.PowerEntry.MeetsRequirements",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Skills/PowerEntryRequirement.cs::XRL.World.Skills.PowerEntryRequirement.MeetsRequirement",
            inventory_statuses=("owner_patch_required",),
            evidence_files=evidence_files,
        ),
    )


def _magnetic_pulse_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MagneticPulseTranslationPatch.cs",
        (
            "MagneticPulseTranslationPatch",
            "TryTranslateQueuedMessage",
            "TryTranslatePopupMessage",
            "EmitMagneticPulse",
            "CompanionRippedPattern",
            "RippedFromPlayerPattern",
            "PulledTowardPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("MagneticPulseTranslationPatch.TryTranslateQueuedMessage",),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("MagneticPulseTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MagneticPulseTranslationPatchTests.cs",
        (
            "MagneticPulse_TranslatesRippedEquipmentPopups_WhenOwnerPatched",
            "MagneticPulse_TranslatesPulledQueueMessages_WhenOwnerPatched",
            "MagneticPulse_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "MagneticPulse_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "MagneticPulse_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "MagneticPulse_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(MagneticPulseTranslationPatch)",
            "XRL.World.Parts.Mutation.MagneticPulse|EmitMagneticPulse|System.Void|XRL.World.GameObject|System.Int32",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/MagneticPulse.cs::XRL.World.Parts.Mutation.MagneticPulse.EmitMagneticPulse",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, popup_pipeline, tests, target_tests),
        ),
    )


def _pet_gloaming_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PetGloamingTranslationPatch.cs",
        (
            "PetGloamingTranslationPatch",
            "TryTranslateQueuedMessage",
            "TryTranslatePopupMessage",
            "AstralTetherPattern",
            "WisdomRevealPattern",
            "StopGleamingPattern",
            "StartGleamingPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("PetGloamingTranslationPatch.TryTranslateQueuedMessage",),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("PetGloamingTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/PetGloamingTranslationPatchTests.cs",
        (
            "PetGloaming_TranslatesQueuedStateMessages_WhenOwnerPatched",
            "PetGloaming_TranslatesWisdomRevealPopup_WhenOwnerPatched",
            "PetGloaming_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "PetGloaming_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "PetGloaming_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "PetGloaming_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(PetGloamingTranslationPatch)",
            "XRL.World.Parts.PetGloaming|FireEvent|System.Boolean|XRL.World.Event",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/PetGloaming.cs::XRL.World.Parts.PetGloaming.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, popup_pipeline, tests, target_tests),
        ),
    )


def _windup_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/WindupTranslationPatch.cs",
        (
            "WindupTranslationPatch",
            "TryTranslateQueuedMessage",
            "TryTranslatePopupMessage",
            "HandleEvent",
            "PlayerUnresponsivePattern",
            "ObserverUnresponsivePattern",
            "PlayerWindPattern",
            "ObserverWindPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("WindupTranslationPatch.TryTranslateQueuedMessage",),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("WindupTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WindupTranslationPatchTests.cs",
        (
            "Windup_TranslatesPlayerPopups_WhenOwnerPatched",
            "Windup_TranslatesObserverQueueMessages_WhenOwnerPatched",
            "Windup_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "Windup_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "Windup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "Windup_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(WindupTranslationPatch)",
            "XRL.World.Parts.Windup|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Windup.cs::XRL.World.Parts.Windup.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, popup_pipeline, tests, target_tests),
        ),
    )


def _damage_penetration_debug_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/DamagePenetrationDebugTranslationPatch.cs",
        (
            "DamagePenetrationDebugTranslationPatch",
            "TryTranslateQueuedMessage",
            "RollDamagePenetrations",
            "PenetratedPattern",
            "FailedPattern",
            "BonusPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("DamagePenetrationDebugTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/DamagePenetrationDebugTranslationPatchTests.cs",
        (
            "DamagePenetrationDebug_TranslatesDebugMessages_WhenOwnerPatched",
            "DamagePenetrationDebug_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "DamagePenetrationDebug_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "DamagePenetrationDebug_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(DamagePenetrationDebugTranslationPatch)",
            "XRL.Rules.Stat|RollDamagePenetrations|System.Int32|System.Int32|System.Int32|System.Int32",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.Rules/Stat.cs::XRL.Rules.Stat.RollDamagePenetrations",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _base_pronoun_provider_customize_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/BasePronounProviderCustomizePopupTranslationPatch.cs",
        (
            "BasePronounProviderCustomizePopupTranslationPatch",
            "TryTranslatePopupMessage",
            "CustomizeProcess",
            "MoveNext",
            "FullyPluralPattern",
            "ConditionallyPluralPattern",
            "PersonPattern",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("BasePronounProviderCustomizePopupTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/BasePronounProviderCustomizePopupTranslationPatchTests.cs",
        (
            "BasePronounProviderCustomize_TranslatesPopupMessages_WhenOwnerPatched",
            "BasePronounProviderCustomize_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "BasePronounProviderCustomize_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "BasePronounProviderCustomize_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "ResolveStateMachineMoveNext",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(BasePronounProviderCustomizePopupTranslationPatch)",
            "XRL.World.BasePronounProvider",
            "CustomizeProcess",
            "XRL.World.BasePronounProvider+<CustomizeProcess>d__121|MoveNext|System.Void",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World/BasePronounProvider.cs::XRL.World.BasePronounProvider.CustomizeProcess",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, popup_pipeline, tests, target_tests),
        ),
    )


def _fugue_on_step_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/FugueOnStepTranslationPatch.cs",
        (
            "FugueOnStepTranslationPatch",
            "TryTranslateQueuedMessage",
            "Activate",
            "PlayerStepPattern",
            "ObserverStepPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("FugueOnStepTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/FugueOnStepTranslationPatchTests.cs",
        (
            "FugueOnStep_TranslatesSpacetimeStepMessages_WhenOwnerPatched",
            "FugueOnStep_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "FugueOnStep_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "FugueOnStep_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(FugueOnStepTranslationPatch)",
            "XRL.World.Parts.FugueOnStep|Activate|System.Boolean|XRL.World.GameObject",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/FugueOnStep.cs::XRL.World.Parts.FugueOnStep.Activate",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _mental_shield_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MentalShieldTranslationPatch.cs",
        (
            "MentalShieldTranslationPatch",
            "TryTranslateQueuedMessage",
            "HandleEvent",
            "BeforeApplyDamageEvent",
            "BeginMentalDefendEvent",
            "NoEffectPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("MentalShieldTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/MentalShieldTranslationPatchTests.cs",
        (
            "MentalShield_TranslatesMentalAttackNoEffectMessage_WhenOwnerPatched",
            "MentalShield_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "MentalShield_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "MentalShield_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "HandleBeforeApplyDamageEvent",
            "HandleBeginMentalDefendEvent",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(MentalShieldTranslationPatch)",
            "XRL.World.Parts.MentalShield|HandleEvent|System.Boolean|XRL.World.BeforeApplyDamageEvent",
            "XRL.World.Parts.MentalShield|HandleEvent|System.Boolean|XRL.World.BeginMentalDefendEvent",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/MentalShield.cs::XRL.World.Parts.MentalShield.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _generated_subject_queue_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/GeneratedSubjectQueueTranslationPatch.cs",
        (
            "GeneratedSubjectQueueTranslationPatch",
            "TryTranslateQueuedMessage",
            "AttackPassesPattern",
            "MolecularCannonOfflinePattern",
            "StartsToFlickerPattern",
            "PaddingSoftenedPattern",
            "YourPaddingSoftenedPattern",
            "DissipatesPattern",
            "HologramInvulnerability",
            "Decarbonizer",
            "PetEitherOr",
            "ModPadded",
            "MoteProperties",
        ),
    )
    pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("GeneratedSubjectQueueTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
        (
            "GeneratedSubjectQueuePatch_TranslatesInventoriedMessages_WhenOwnerPatched",
            "GeneratedSubjectQueuePatch_PreservesWholeMessageColorBoundary_WhenOwnerPatched",
            "GeneratedSubjectQueuePatch_DoesNotTranslateQueuedMessage_WhenOwnerPatchIsAbsent",
            "GeneratedSubjectQueuePatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "GeneratedSubjectQueuePatch_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(GeneratedSubjectQueueTranslationPatch)",
            "XRL.World.Parts.HologramInvulnerability|HandleEvent|System.Boolean|XRL.World.BeforeApplyDamageEvent",
            "XRL.World.Parts.Mutation.Decarbonizer|ShutDownTargeting|System.Boolean",
            "XRL.World.Parts.PetEitherOr|trigger|System.Void",
            "XRL.World.Parts.ModPadded|FireEvent|System.Boolean|XRL.World.Event",
            "XRL.World.Parts.MoteProperties|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
        ),
    )
    mod_padded_expected = "{}{}".format(
        "{{C|leather boots}}\u306e\u8a70\u3081\u7269",
        "\u304c\u885d\u6483\u3092\u548c\u3089\u3052\u305f\u3002",
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/HologramInvulnerability.cs::XRL.World.Parts.HologramInvulnerability.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "DummyHologramInvulnerabilityProducerTarget",
                        "glowfish's attack passes harmlessly through hologram.",
                        "glowfish\u306e\u653b\u6483\u306fhologram\u3092\u7121\u5bb3\u306b\u901a\u308a\u629c\u3051\u305f\u3002",
                    ),
                ),
                target_tests,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Decarbonizer.cs::XRL.World.Parts.Mutation.Decarbonizer.ShutDownTargeting",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "DummyDecarbonizerProducerTarget",
                        "{{C|decarbonizer}}'s molecular cannon goes offline.",
                        "{{C|decarbonizer}}\u306e\u5206\u5b50\u7832\u304c\u30aa\u30d5\u30e9\u30a4\u30f3\u306b\u306a\u3063\u305f\u3002",
                    ),
                ),
                target_tests,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/PetEitherOr.cs::XRL.World.Parts.PetEitherOr.trigger",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "DummyPetEitherOrProducerTarget.trigger",
                        "{{Y|Either}} starts to flicker.",
                        "{{Y|Either}}\u304c\u3061\u3089\u3064\u304d\u59cb\u3081\u305f\u3002",
                    ),
                ),
                target_tests,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/ModPadded.cs::XRL.World.Parts.ModPadded.FireEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "DummyModPaddedProducerTarget",
                        "{{C|leather boots}}'s padding softened the blow.",
                        mod_padded_expected,
                        "Your padding softened the blow.",
                        "\u3042\u306a\u305f\u306e\u8a70\u3081\u7269\u304c\u885d\u6483\u3092\u548c\u3089\u3052\u305f\u3002",
                    ),
                ),
                target_tests,
            ),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/MoteProperties.cs::XRL.World.Parts.MoteProperties.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(
                patch,
                pipeline,
                EvidenceFile(
                    tests.path,
                    (
                        *tests.required_substrings,
                        "DummyMotePropertiesProducerTarget",
                        "{{Y|Your glimmer mote}} dissipates.",
                        "{{Y|Your glimmer mote}}\u306f\u9727\u6563\u3057\u305f\u3002",
                    ),
                ),
                target_tests,
            ),
        ),
    )


def _tabula_rasae_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/TabulaRasaeTranslationPatch.cs",
        (
            "TabulaRasaeTranslationPatch",
            "TryTranslateQueuedMessage",
            "HandleEvent",
            "BeforeApplyDamageEvent",
            "TookDamageEvent",
            "Confusion",
            "Confuse",
            "NoEffectPattern",
            "AdaptPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("TabulaRasaeTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TabulaRasaeTranslationPatchTests.cs",
        (
            "TabulaRasae_TranslatesOwnerMessages_WhenOwnerPatched",
            "TabulaRasae_TranslatesUnknownDamageAttribute_WithCapturedText",
            "TabulaRasae_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "TabulaRasae_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "TabulaRasae_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "HandleBeforeApplyDamageEvent",
            "HandleTookDamageEvent",
            "ConfusionConfuse",
            "{{R|Your attack does not affect snapjaw.}}",
            "{{R|攻撃はsnapjawに影響を与えない。}}",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(TabulaRasaeTranslationPatch)",
            "XRL.World.Parts.TabulaRasae|HandleEvent|System.Boolean|XRL.World.BeforeApplyDamageEvent",
            "XRL.World.Parts.TabulaRasae|HandleEvent|System.Boolean|XRL.World.TookDamageEvent",
            "XRL.World.Parts.Mutation.Confusion|Confuse|System.Boolean|XRL.World.MentalAttackEvent|System.Boolean|System.Int32|System.Int32|System.Boolean",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/TabulaRasae.cs::XRL.World.Parts.TabulaRasae.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
        CoveredOwnerFamily(
            family_id="XRL.World.Parts.Mutation/Confusion.cs::XRL.World.Parts.Mutation.Confusion.Confuse",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _eat_memories_on_hit_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EatMemoriesOnHitTranslationPatch.cs",
        (
            "EatMemoriesOnHitTranslationPatch",
            "TryTranslateQueuedMessage",
            "EatMemories",
            "ForgetPattern",
            "StarvePattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("EatMemoriesOnHitTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EatMemoriesOnHitTranslationPatchTests.cs",
        (
            "EatMemoriesOnHit_TranslatesOwnerMessages_WhenOwnerPatched",
            "EatMemoriesOnHit_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "EatMemoriesOnHit_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "EatMemoriesOnHit_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyEatMemoriesOnHitTarget",
            "EatMemories",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(EatMemoriesOnHitTranslationPatch)",
            "XRL.World.Parts.EatMemoriesOnHit|EatMemories|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/EatMemoriesOnHit.cs::XRL.World.Parts.EatMemoriesOnHit.EatMemories",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _cybernetics_stasis_entangler_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/CyberneticsStasisEntanglerTranslationPatch.cs",
        (
            "CyberneticsStasisEntanglerTranslationPatch",
            "TryTranslateQueuedMessage",
            "DeployToCells",
            "AllAroundPattern",
            "SeveralNearbyPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("CyberneticsStasisEntanglerTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CyberneticsStasisEntanglerTranslationPatchTests.cs",
        (
            "CyberneticsStasisEntangler_TranslatesDeployMessages_WhenOwnerPatched",
            "CyberneticsStasisEntangler_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "CyberneticsStasisEntangler_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "CyberneticsStasisEntangler_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyCyberneticsStasisEntanglerTarget",
            "DeployToCells",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(CyberneticsStasisEntanglerTranslationPatch)",
            "XRL.World.Parts.CyberneticsStasisEntangler|DeployToCells|XRL.World.GameObject|XRL.World.Zone|XRL.World.GameObject|XRL.World.GameObject|System.Int32|System.Int32",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/CyberneticsStasisEntangler.cs::XRL.World.Parts.CyberneticsStasisEntangler.DeployToCells",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _engulfing_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EngulfingTranslationPatch.cs",
        (
            "EngulfingTranslationPatch",
            "TryTranslateQueuedMessage",
            "Engulf",
            "EngulfYouFailPattern",
            "EngulfTargetFailPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("EngulfingTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EngulfingTranslationPatchTests.cs",
        (
            "Engulfing_TranslatesEngulfFailureMessages_WhenOwnerPatched",
            "Engulfing_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "Engulfing_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "Engulfing_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyEngulfingTarget",
            "Engulf",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(EngulfingTranslationPatch)",
            "XRL.World.Parts.Engulfing|Engulf|System.Boolean|XRL.World.GameObject|XRL.World.Event",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Engulfing.cs::XRL.World.Parts.Engulfing.Engulf",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _temporary_reality_stabilize_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/TemporaryRealityStabilizeTranslationPatch.cs",
        (
            "TemporaryRealityStabilizeTranslationPatch",
            "TryTranslateQueuedMessage",
            "HandleEvent",
            "RealityStabilizeEvent",
            "WorldlinePattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("TemporaryRealityStabilizeTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/TemporaryRealityStabilizeTranslationPatchTests.cs",
        (
            "TemporaryRealityStabilize_TranslatesWorldlineMessages_WhenOwnerPatched",
            "TemporaryRealityStabilize_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "TemporaryRealityStabilize_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "TemporaryRealityStabilize_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyTemporaryRealityStabilizeTarget",
            "HandleEvent",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(TemporaryRealityStabilizeTranslationPatch)",
            "XRL.World.Parts.Temporary|HandleEvent|System.Boolean|XRL.World.RealityStabilizeEvent",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Temporary.cs::XRL.World.Parts.Temporary.HandleEvent",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _cloning_start_budded_clone_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/CloningStartBuddedCloneTranslationPatch.cs",
        (
            "CloningStartBuddedCloneTranslationPatch",
            "TryTranslateQueuedMessage",
            "TryTranslatePopupMessage",
            "StartBuddedClone",
            "DetachPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("CloningStartBuddedCloneTranslationPatch.TryTranslateQueuedMessage",),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("CloningStartBuddedCloneTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/CloningStartBuddedCloneTranslationPatchTests.cs",
        (
            "CloningStartBuddedClone_TranslatesDetachPopup_WhenOwnerPatched",
            "CloningStartBuddedClone_TranslatesDetachQueueMessage_WhenOwnerPatched",
            "CloningStartBuddedClone_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "CloningStartBuddedClone_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "CloningStartBuddedClone_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "CloningStartBuddedClone_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyCloningStartBuddedCloneTarget",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(CloningStartBuddedCloneTranslationPatch)",
            "XRL.World.Capabilities.Cloning|StartBuddedClone|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Capabilities/Cloning.cs::XRL.World.Capabilities.Cloning.StartBuddedClone",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, popup_pipeline, tests, target_tests),
        ),
    )


def _hidden_render_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/HiddenRenderTranslationPatch.cs",
        (
            "HiddenRenderTranslationPatch",
            "TryTranslateQueuedMessage",
            "Reveal",
            "RevealedPattern",
        ),
    )
    queue_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
        ("HiddenRenderTranslationPatch.TryTranslateQueuedMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/HiddenRenderTranslationPatchTests.cs",
        (
            "HiddenRender_TranslatesRevealMessages_WhenOwnerPatched",
            "HiddenRender_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "HiddenRender_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
            "HiddenRender_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyHiddenRenderTarget",
            "Reveal",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(HiddenRenderTranslationPatch)",
            "XRL.World.Parts.HiddenRender|Reveal|System.Void",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/HiddenRender.cs::XRL.World.Parts.HiddenRender.Reveal",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, queue_pipeline, tests, target_tests),
        ),
    )


def _engraver_families() -> tuple[CoveredOwnerFamily, ...]:
    patch = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/EngraverTranslationPatch.cs",
        (
            "EngraverTranslationPatch",
            "AttemptEngrave",
            "TryTranslatePopupMessage",
            "MarkOfDeathPattern",
            "EngravingPattern",
        ),
    )
    popup_pipeline = EvidenceFile(
        "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
        ("EngraverTranslationPatch.TryTranslatePopupMessage",),
    )
    tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2/EngraverTranslationPatchTests.cs",
        (
            "EngraverAttemptEngrave_TranslatesSuccessPopups_WhenOwnerPatched",
            "EngraverAttemptEngrave_DoesNotTranslateTraffic_WhenOwnerAbsent",
            "EngraverAttemptEngrave_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
            "EngraverAttemptEngrave_LeavesUnsupportedMessagesUnchanged_WhenOwnerPatched",
            "DummyEngraverTarget",
            "AttemptEngrave",
        ),
    )
    target_tests = EvidenceFile(
        "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
        (
            "typeof(EngraverTranslationPatch)",
            "XRL.World.Parts.Engraver|AttemptEngrave|System.Boolean|XRL.World.GameObject",
        ),
    )
    return (
        CoveredOwnerFamily(
            family_id="XRL.World.Parts/Engraver.cs::XRL.World.Parts.Engraver.AttemptEngrave",
            inventory_statuses=("owner_patch_required",),
            evidence_files=(patch, popup_pipeline, tests, target_tests),
        ),
    )


COVERED_OWNER_FAMILIES: Final = (
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/LiquidVolume.cs::XRL.World.Parts.LiquidVolume.Pour",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/LiquidVolumeTranslationPatch.cs",
                ("TryTranslatePopupMessage", "TryTranslateQueuedMessage", '"Pour"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/LiquidVolumeFragmentTranslator.cs",
                ("OwnershipPour", "PourOutSelf", "PourOutActor"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("LiquidVolumeTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("LiquidVolumeTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "LiquidVolumePatch_TranslatesPopupYesNoCancelMessage_WhenPatched",
                    "LiquidVolumePatch_TranslatesPopupShowMessage_WhenPatched",
                    "LiquidVolumePatch_TranslatesQueuedMessages_WhenOwnerPatched",
                    "LiquidVolumePatch_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "LiquidVolumePatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "LiquidVolumePatch_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    "nameof(DummyLiquidVolumeProducerTarget.Pour)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(LiquidVolumeTranslationPatch)",
                    "XRL.World.Parts.LiquidVolume|Pour|System.Boolean|System.Boolean&|XRL.World.GameObject|XRL.World.Cell|System.Boolean|System.Boolean|System.Int32|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/LiquidVolume.cs::XRL.World.Parts.LiquidVolume.PerformFill",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/LiquidVolumeTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"PerformFill"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/LiquidVolumeFragmentTranslator.cs",
                ("OwnershipTake", "EmptyFirst"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("LiquidVolumeTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "LiquidVolumePatch_TranslatesPopupShowMessage_WhenPatched",
                    "LiquidVolumePatch_DoesNotTranslatePopup_WhenOwnerAbsent",
                    "LiquidVolumePatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "LiquidVolumePatch_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                    "nameof(DummyLiquidVolumeProducerTarget.PerformFill)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(LiquidVolumeTranslationPatch)",
                    "XRL.World.Parts.LiquidVolume|PerformFill|System.Boolean|XRL.World.GameObject|System.Boolean&|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="Qud.API/EquipmentAPI.cs::Qud.API.EquipmentAPI.TwiddleObject",
        inventory_statuses=("needs_family_review",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/EquipmentApiTwiddleObjectTranslationPatch.cs",
                (
                    "EquipmentApiTwiddleObjectTranslationPatch",
                    "TryTranslateTelekineticRange",
                    "You cannot do that from here.",
                    "out of your telekinetic range",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("EquipmentApiTwiddleObjectTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/EquipmentApiTwiddleObjectTranslationPatchTests.cs",
                (
                    "TwiddleObject_TranslatesUsabilityPopups_WhenOwnerScoped",
                    "{{Y|telekinetic lever}} are out of your telekinetic range.",
                    "{{Y|telekinetic lever}}はあなたの念動力の範囲外だ",
                    "You cannot do that from here.",
                    "ここからはそれはできない。",
                    "TwiddleObject_DoesNotRetranslateDirectMarkedPopup_WhenOwnerScoped",
                    "TwiddleObject_LeavesEmptyPopupUnchanged_WhenOwnerScoped",
                    "TwiddleObject_LeavesUnsupportedPopupUnchanged_WhenOwnerScoped",
                    "TwiddleObject_DoesNotClaimSupportedPopup_WhenOwnerAbsent",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(EquipmentApiTwiddleObjectTranslationPatch)",
                    "Qud.API.EquipmentAPI|TwiddleObject|System.Void|XRL.World.GameObject|XRL.World.GameObject|System.Boolean&|XRL.World.InventoryAction&|System.Boolean|System.Boolean|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.Heal",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectHealTranslationPatch.cs",
                ("TryTranslateQueuedMessage", '"Heal"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("GameObjectHealTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectHeal_TranslatesHealMessage_WhenPatched",
                    "GameObjectHeal_TranslatesHpLossMessage_WhenPatched",
                    "GameObjectHeal_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectHeal_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "GameObjectHeal_LeavesEmptyMessageUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(GameObjectHealTranslationPatch)", '"Heal"', '"XRL.World.GameObject"'),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.Move",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectMoveTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "TryTranslatePopupMessage", '"Move"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("GameObjectMoveTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectMoveTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectMove_TranslatesInventoriedQueuedShapes_WithRepositoryPatterns",
                    "GameObjectMove_TranslatesSwimmingPopup_WhenOwnerPatched",
                    "GameObjectMove_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectMove_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "GameObjectMove_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(GameObjectMoveTranslationPatch)", '"Move"', '"XRL.World.GameObject"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                (
                    "Do you want to go",
                    "budge",
                    "drop down a level",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/ui-messagelog-leaf.ja.json",
                ("You cannot go that way.",),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Physics.cs::XRL.World.Parts.Physics.EnterCell",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PhysicsEnterCellPassByTranslationPatch.cs",
                ("PhysicsEnterCellPassByTranslationPatch", "PassByPrefix", "PreparePassByMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueTranslationPatch.cs",
                ("PrefixPhysicsEnterCellPassBy", "PhysicsEnterCellPassByTranslationPatch.Prefix"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/PhysicsEnterCellPassByTranslationPatchTests.cs",
                (
                    "AggregateMessageQueuePatch_TranslatesEnterCellPassByUsingRepositoryPattern_WhenPatched",
                    "AggregateMessageQueuePatch_PreservesPrefixOrder_WhenPatched",
                    "AggregateMessageQueuePatch_PassesThroughEmptyAndDirectMarkedMessages_WhenPatched",
                    "Prefix_PassesThroughEnglishWhenPatternDoesNotMatch_WhenPatched",
                    "DummyPhysicsEnterCellTarget",
                    "string.Empty",
                    "MessageFrameTranslator.MarkDirectTranslation",
                    "You pass by ",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L1/MessageLogProducerTranslationHelpersTests.cs",
                (
                    "PreparePassByMessage_MarksTranslatedMessage",
                    "PreparePassByMessage_PreservesColorTags",
                    "PreparePassByMessage_PreservesDirectTranslationMarker",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "MovementExistingSeamProducerMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Physics|EnterCell|System.Boolean|XRL.World.Cell",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                (
                    "^You pass by a (.+?)[.!]?$",
                    "^You pass by (.+?)[.!]?$",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.HandleEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatTextSurfaceTranslationPatch.cs",
                ("TryTranslateQueuedMessage", 'ShieldBlockDetail = "HandleEvent"', "IsShieldBlockMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("CombatTextSurfaceTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "CombatGetDefenderHitDice_TranslatesInventoriedShapes_WithRepositoryPatterns",
                    "You block with iron buckler! (+2 AV)",
                    "You stagger Snapjaw Scavenger with your shield block!",
                    "You are staggered by iron buckler's block!",
                    "CombatTextSurface_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "CombatTextSurface_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "CombatTextSurface_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(CombatTextSurfaceTranslationPatch)",
                    "XRL.World.Parts.Combat|HandleEvent|System.Boolean|XRL.World.GetDefenderHitDiceEvent",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                (
                    "^You block with (.+?)!",
                    "^You stagger (.+) with your shield block!$",
                    "^You are staggered by (?:the )?(.+?)(?:'s|s'|の) block!$",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Combat.cs::XRL.World.Parts.Combat.MeleeAttackWithWeaponInternal",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatTextSurfaceTranslationPatch.cs",
                (
                    "TryTranslateQueuedMessage",
                    'MeleeAttackDetail = "MeleeAttackWithWeaponInternal"',
                    "IsMeleeAttackMessage",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("CombatTextSurfaceTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "CombatMeleeAttack_TranslatesInventoriedShapes_WithRepositoryPatterns",
                    "You miss with your bronze dagger! [10 vs 14]",
                    "Snapjaw Scavenger misses you with its bronze dagger! [10 vs 14]",
                    "Your mental attack does not affect Snapjaw Scavenger.",
                    "Snapjaw Scavenger fails to deal damage with its attack! [17]",
                    "You don't penetrate Snapjaw Scavenger's armor.",
                    "Snapjaw Scavenger doesn't penetrate your armor with its bronze dagger! [17]",
                    "CombatTextSurface_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "CombatTextSurface_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "CombatTextSurface_LeavesEmptyMessagesUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(CombatTextSurfaceTranslationPatch)",
                    COMBAT_MELEE_ATTACK_FULL_SIGNATURE,
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                (
                    "^You miss with your (.+?)[.!] ",
                    "^(?:The )?(.+) misses you[.!]?$",
                    "^Your mental attack does not affect (.+?)\\\\.$",
                    "^You fail to deal damage with your attack! ",
                    "^You don't penetrate (?:the )?(.+?)(?:'s|s'|の) armor[.!]?$",
                    "^(?:The |the |[Aa]n? )?(.+?) (?:doesn't|don't) penetrate your armor",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.PerformThrow",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectPerformThrowTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "TryTranslatePopupMessage", '"PerformThrow"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("GameObjectPerformThrowTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectPerformThrowTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectPerformThrow_TranslatesInventoriedQueuedShapes_WithRepositoryPatterns",
                    "GameObjectPerformThrow_TranslatesSelfTargetPopup_WhenOwnerPatched",
                    "GameObjectPerformThrow_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectPerformThrow_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "GameObjectPerformThrow_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(GameObjectPerformThrowTranslationPatch)", '"PerformThrow"', '"XRL.World.GameObject"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                (
                    "Are you sure you want to target",
                    "hits with (?:a |an |the )?(.+?) \\\\(x(\\\\d+)\\\\) for (\\\\d+) damage",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.ToggleActivatedAbility",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectToggleActivatedAbilityTranslationPatch.cs",
                ("TryTranslateQueuedMessage", '"ToggleActivatedAbility"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("GameObjectToggleActivatedAbilityTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectToggleActivatedAbility_TranslatesToggleMessage_WhenPatched",
                    "GameObjectToggleActivatedAbility_TranslatesOffMessage_WithRepositoryPatterns",
                    "GameObjectToggleActivatedAbility_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectToggleActivatedAbility_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "GameObjectToggleActivatedAbility_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(GameObjectToggleActivatedAbilityTranslationPatch)",
                    '"ToggleActivatedAbility"',
                    '"XRL.World.GameObject"',
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/messages.ja.json",
                ("^You toggle (.+?) on", "^You toggle (.+?) off"),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.ConfirmUseImportantAsync",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
                ("ConfirmUseImportantAsync", "TryTranslatePopupMessage", "ImportantPluralPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectPopup_TranslatesConfirmUseImportantAsyncPlural_WhenOwnerPatched",
                    "GameObjectPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "GameObjectPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(GameObjectPopupTranslationPatch)",
                    "XRL.World.GameObject|ConfirmUseImportantAsync|",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.ConfirmUseImportant",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
                ("ConfirmUseImportant", "TryTranslatePopupMessage", "ImportantSingularPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectPopup_TranslatesConfirmUseImportantSingular_WhenOwnerPatched",
                    "GameObjectPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "GameObjectPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(GameObjectPopupTranslationPatch)", "XRL.World.GameObject|ConfirmUseImportant|"),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.HandleRename",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
                ("HandleRename", "DoesNotWantNamePattern", "StartCallingPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectPopup_TranslatesHandleRenameMessages_WhenOwnerPatched",
                    "GameObjectPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "GameObjectPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(GameObjectPopupTranslationPatch)", "XRL.World.GameObject|HandleRename|"),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.ChangeCompanionAbilityUse",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
                ("ChangeCompanionAbilityUse", "AbilityPossessivePattern", "TryTranslateAbilityState"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectPopup_TranslatesChangeCompanionAbilityUseMessages_WhenOwnerPatched",
                    "GameObjectPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "GameObjectPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(GameObjectPopupTranslationPatch)", "XRL.World.GameObject|ChangeCompanionAbilityUse|"),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.CheckCompanionDirection",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectPopupTranslationPatch.cs",
                ("CheckCompanionDirection", "CannotHearPattern", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "GameObjectPopup_TranslatesCheckCompanionDirectionMessage_WhenOwnerPatched",
                    "GameObjectPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "GameObjectPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "GameObjectPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(GameObjectPopupTranslationPatch)", "XRL.World.GameObject|CheckCompanionDirection|"),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Enclosing.cs::XRL.World.Parts.Enclosing.EnterEnclosure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/EnclosingTranslationPatch.cs",
                ("EnterEnclosure", "TryTranslatePopupMessage", "TryTranslateQueuedMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/EnclosingFragmentTranslator.cs",
                ("FailToGetIntoPattern", "NpcFailToGetIntoPattern", "TryTranslateQueuedMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("EnclosingTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("EnclosingTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "EnclosingPatch_TranslatesOwnerPopup_WhenPatched",
                    "EnclosingPatch_TranslatesQueuedMessage_WhenPatched",
                    "EnclosingPatch_DoesNotTranslateOwnerPopup_WhenOwnerAbsent",
                    "EnclosingPatch_DoesNotTranslateQueuedMessage_WhenOwnerAbsent",
                    "EnclosingPatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "EnclosingPatch_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "EnclosingPatch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "EnclosingPatch_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    "You fail to get yourself into stasis pod.",
                    "snapjaw tries to get itself into the stasis pod, but fails.",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(EnclosingTranslationPatch)",
                    "XRL.World.Parts.Enclosing|EnterEnclosure|System.Boolean|XRL.World.GameObject|XRL.World.IEvent",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Enclosing.cs::XRL.World.Parts.Enclosing.EnclosureExitImpeded",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/EnclosingTranslationPatch.cs",
                ("EnclosureExitImpeded", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/EnclosingFragmentTranslator.cs",
                ("CannotWhileEnclosedPattern",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("EnclosingTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "EnclosingPatch_TranslatesOwnerPopup_WhenPatched",
                    "EnclosingPatch_DoesNotTranslateOwnerPopup_WhenOwnerAbsent",
                    "EnclosingPatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "EnclosingPatch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "You cannot do that while enclosed by stasis pod.",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(EnclosingTranslationPatch)",
                    "XRL.World.Parts.Enclosing|EnclosureExitImpeded|System.Boolean|XRL.World.GameObject|System.Boolean|XRL.World.Effects.Enclosed",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/StairsDown.cs::XRL.World.Parts.StairsDown.HandleEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/StairsDownTranslationPatch.cs",
                ("HandleEvent", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/StairsFragmentTranslator.cs",
                ("StairsDownFragmentTranslator", '"descend"', "UseCommandPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("StairsDownTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "StairsPatch_TranslatesInventoryActionPopup_WhenPatched",
                    "StairsPatch_DoesNotTranslateOwnerPopup_WhenOwnerAbsent",
                    "StairsPatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "StairsPatch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Use {{W|Shift+D}} to descend.",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(StairsDownTranslationPatch)",
                    "XRL.World.Parts.StairsDown|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/StairsUp.cs::XRL.World.Parts.StairsUp.HandleEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/StairsUpTranslationPatch.cs",
                ("HandleEvent", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/StairsFragmentTranslator.cs",
                ("StairsUpFragmentTranslator", '"ascend"', "UseCommandPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("StairsUpTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "StairsPatch_TranslatesInventoryActionPopup_WhenPatched",
                    "StairsPatch_DoesNotTranslateOwnerPopup_WhenOwnerAbsent",
                    "StairsPatch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "StairsPatch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Use {{W|Shift+U}} to ascend.",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(StairsUpTranslationPatch)",
                    "XRL.World.Parts.StairsUp|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/ZoneManager.cs::XRL.World.ZoneManager.TryThawZone",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ZoneManagerTryThawZoneTranslationPatch.cs",
                ("TryTranslateQueuedMessage", '"TryThawZone"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("ZoneManagerTryThawZoneTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "ZoneManagerTryThawZone_TranslatesInventoriedColorShapes_WithRepositoryLeafDictionary",
                    "ZoneManagerTryThawZone_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "ZoneManagerTryThawZone_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "ZoneManagerTryThawZone_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(ZoneManagerTryThawZoneTranslationPatch)", '"TryThawZone"', '"XRL.World.ZoneManager"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/ui-messagelog-leaf.ja.json",
                ("ThawZone exception",),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/ZoneManager.cs::XRL.World.ZoneManager.Tick",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ZoneManagerTickTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "WarningText", '"Tick"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("ZoneManagerTickTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "ZoneManagerTick_TranslatesInlineColorWarning_WithRepositoryLeafDictionary",
                    "ZoneManagerTick_TranslatesColorArgumentWarning_WithRepositoryLeafDictionary",
                    "ZoneManagerTick_DoesNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "ZoneManagerTick_DoesNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "ZoneManagerTick_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(ZoneManagerTickTranslationPatch)", '"Tick"', '"XRL.World.ZoneManager"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/Dictionaries/ui-messagelog-leaf.ja.json",
                ("WARNING: You have the Disable Zone Caching option enabled",),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Effects/RealityStabilized.cs::XRL.World.Effects.RealityStabilized.ShowGenericInterdictMessage",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RealityStabilizedInterdictTranslationPatch.cs",
                ("ShowGenericInterdictMessage", "NormalityBase", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("RealityStabilizedInterdictTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "RealityStabilizedInterdict_TranslatesPopupMessages_WhenOwnerPatched",
                    "RealityStabilizedInterdict_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "RealityStabilizedInterdict_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "RealityStabilizedInterdict_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(RealityStabilizedInterdictTranslationPatch)",
                    "XRL.World.Effects.RealityStabilized|ShowGenericInterdictMessage|System.Void|XRL.World.GameObject|XRL.World.Event",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Effects/RealityStabilized.cs::XRL.World.Effects.RealityStabilized.ShowDistantInterdictMessage",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RealityStabilizedInterdictTranslationPatch.cs",
                ("ShowDistantInterdictMessage", "NormalityBase", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("RealityStabilizedInterdictTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "RealityStabilizedInterdict_TranslatesPopupMessages_WhenOwnerPatched",
                    "RealityStabilizedInterdict_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "RealityStabilizedInterdict_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "RealityStabilizedInterdict_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(RealityStabilizedInterdictTranslationPatch)",
                    "XRL.World.Effects.RealityStabilized|ShowDistantInterdictMessage|System.Void|XRL.World.GameObject|XRL.World.Event",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Effects/RealityStabilized.cs::XRL.World.Effects.RealityStabilized.ShowDualInterdictMessage",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RealityStabilizedInterdictTranslationPatch.cs",
                ("ShowDualInterdictMessage", "DualBase", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("RealityStabilizedInterdictTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "RealityStabilizedInterdict_TranslatesPopupMessages_WhenOwnerPatched",
                    "RealityStabilizedInterdict_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent",
                    "RealityStabilizedInterdict_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "RealityStabilizedInterdict_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(RealityStabilizedInterdictTranslationPatch)",
                    "XRL.World.Effects.RealityStabilized|ShowDualInterdictMessage|System.Void|XRL.World.GameObject|XRL.World.Event",
                ),
            ),
        ),
    ),
    *_hacking_sifrah_result_families(),
    *_quest_lifecycle_popup_families(),
    *_flight_families(),
    *_body_families(),
    *_item_modding_sifrah_result_families(),
    *_sifrah_pure_owner_popup_families(),
    *_sunder_mind_owner_families(),
    *_keybinds_screen_conflict_families(),
    *_ability_manager_popup_families(),
    *_cooking_runtime_families(),
    *_water_ritual_popup_families(),
    *_popup_pick_several_family(),
    *_grit_gate_terminal_owner_families(),
    *_pick_item_take_all_family(),
    *_status_screen_popup_families(),
    *_campfire_preserve_families(),
    *_reality_stabilized_event_families(),
    *_cybernetic_rejection_syndrome_families(),
    *_geomagnetic_disc_popup_families(),
    *_campfire_cook_availability_families(),
    *_teleprojector_popup_families(),
    *_tomb_anchor_system_families(),
    *_cybernetics_medassist_module_families(),
    *_liquid_loader_families(),
    *_energy_loader_cannot_take_families(),
    *_liquid_leak_message_families(),
    *_energy_cell_socket_access_family(),
    *_campfire_remains_attempt_light_family(),
    *_troll_king_families(),
    *_mutating_families(),
    *_quills_families(),
    *_light_manipulation_families(),
    *_latches_on_families(),
    *_asleep_owner_families(),
    *_budding_families(),
    *_beguiling_families(),
    *_ascension_cable_families(),
    *_carapace_tighten_families(),
    *_svardym_system_families(),
    *_phased_families(),
    *_persuasion_rebuke_robot_families(),
    *_nephal_properties_families(),
    *_tonic_families(),
    *_tonic_applicator_families(),
    *_xrl_game_families(),
    *_integrated_weapon_hosts_families(),
    *_boost_statistic_families(),
    *_emboldened_families(),
    *_fungal_spore_infection_families(),
    *_healing_families(),
    *_stressed_families(),
    *_monochrome_onset_families(),
    *_ironshank_onset_families(),
    *_adrenal_control_families(),
    *_amnesia_families(),
    *_fixed_owner_queue_families(),
    *_effect_static_message_families(),
    *_stasis_attack_bounce_family(),
    *_effect_generated_message_families(),
    *_generated_queue_does_verb_families(),
    *_blaze_tonic_remove_family(),
    *_latched_onto_expired_family(),
    *_giant_clam_teleport_joppa_family(),
    *_single_callsite_owner_popup_families(),
    *_point_of_interest_navigation_popup_family(),
    *_run_start_running_popup_family(),
    *_historic_event_region_reveal_popup_family(),
    *_kill_missile_weapon_chirp_family(),
    *_requires_power_to_equip_check_equip_popup_family(),
    *_xrl_core_owner_queue_families(),
    *_brain_owner_surface_families(),
    *_cripple_apply_family(),
    *_mutation_self_target_popup_families(),
    *_system_static_message_families(),
    *_existing_popup_owner_route_families(),
    *_closure_only_popup_owner_families(),
    *_tinkering_mod_popup_family(),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Tactics_Kickback.cs::XRL.World.Parts.Skill.Tactics_Kickback.HandleEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
                ("Tactics_Kickback", "TryTranslateQueuedMessage", "KickPassesThroughYouPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("CombatSkillMessageTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "CombatSkillMessages_TranslateInventoriedQueuedShapes_WhenOwnerPatched",
                    "CombatSkillMessages_DoNotTranslateQueueOnlyTraffic_WhenOwnerAbsent",
                    "CombatSkillMessages_DoNotRetranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "CombatSkillMessages_LeavesEmptyQueuedMessageUnchanged_WhenOwnerPatched",
                    "You kick at {{G|phase spider}}, but the kick passes through {{G|it}}.",
                    "snapjaw kicks glowfish backwards.",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(CombatSkillMessageTranslationPatch)",
                    "XRL.World.Parts.Skill.Tactics_Kickback|HandleEvent|System.Boolean|XRL.World.BeforeFireMissileWeaponsEvent",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Axe_Cleave.cs::XRL.World.Parts.Skill.Axe_Cleave.PerformCleave",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
                ("Axe_Cleave", "ChargeCleavePattern", "ActorCleavesTargetPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("CombatSkillMessageTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "CombatSkillMessages_TranslateInventoriedQueuedShapes_WhenOwnerPatched",
                    "cleave deeper through {{R|snapjaw's armor}}.",
                    "snapjaw cleaves through glowfish's armor.",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(CombatSkillMessageTranslationPatch)",
                    "XRL.World.Parts.Skill.Axe_Cleave|PerformCleave|System.Void|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.Int32|System.Int32|System.Nullable`1[[System.Int32]]",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Endurance_ShakeItOff.cs::XRL.World.Parts.Skill.Endurance_ShakeItOff.FireEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
                ("Endurance_ShakeItOff", "ShookOffStunPattern", "ShookOffDazingPattern"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("CombatSkillMessageTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "CombatSkillMessages_TranslateInventoriedQueuedShapes_WhenOwnerPatched",
                    "You shook off the stun.",
                    "The snapjaw shook off the dazing.",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(CombatSkillMessageTranslationPatch)",
                    "XRL.World.Parts.Skill.Endurance_ShakeItOff|FireEvent|System.Boolean|XRL.World.Event",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/TenfoldPath_Ret.cs::XRL.World.Parts.Skill.TenfoldPath_Ret.HandleEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatSkillMessageTranslationPatch.cs",
                ("TenfoldPath_Ret", "SupernalStatePattern", "A supernal force helps you shake off a mental state!"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("CombatSkillMessageTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/CombatAndLogMessageQueuePatchTests.cs",
                (
                    "CombatSkillMessages_TranslateInventoriedQueuedShapes_WhenOwnerPatched",
                    "A supernal force helps you shake off the effect!",
                    "A supernal force helps you shake off being confused!",
                    "A supernal force helps you shake off a mental state!",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(CombatSkillMessageTranslationPatch)",
                    "XRL.World.Parts.Skill.TenfoldPath_Ret|HandleEvent|System.Boolean|XRL.World.ApplyEffectEvent",
                    "XRL.World.Parts.Skill.TenfoldPath_Ret|HandleEvent|System.Boolean|XRL.World.EndTurnEvent",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.UI/TradeUI.cs::XRL.UI.TradeUI.PerformOffer",
        inventory_statuses=("needs_family_review",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs",
                ("TryTranslatePerformOfferTradeWaterMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("TryTranslatePerformOfferTradeWaterMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L1/TradeUiPopupTranslationPatchTests.cs",
                (
                    "TranslatePopupText_TranslatesPerformOfferTradeWaterMessages_WithoutDictionaryEntry",
                    "TranslatePopupText_UsesOwnerTemplateForPerformOfferTradeWaterMessage_IgnoresDictionaryEntriesAndPreservesColorTags",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupShowTranslationPatchTests.cs",
                (
                    "Prefix_TranslatesPerformOfferTradeWaterMessage_WithoutDictionaryEntry",
                    "Prefix_UsesPerformOfferTradeWaterTemplate_IgnoresDictionaryEntriesAndPreservesColorTags",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(TradeUiPopupTranslationPatch)", "XRL.UI.Popup|Show|"),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.UI/TradeUI.cs::XRL.UI.TradeUI.TryRemove",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/TradeUiVendorPopupTranslationPatch.cs",
                ("TradeUiVendorPopupTranslationPatch", "TryRemove", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("TradeUiVendorPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs",
                ("TryTranslateTradeUiPopupText", "TradeUiPopup.TryRemove"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiPopupTranslationPatchTests.cs",
                (
                    "VendorOwnerPatch_TranslatesTryRemoveShowBlock_WhenOwnerPatched",
                    "VendorOwnerPatch_DoesNotTranslateTryRemoveShowBlock_WhenOwnerAbsent",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(TradeUiVendorPopupTranslationPatch)",
                    "XRL.UI.TradeUI|TryRemove|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Collections.Generic.List`1[[XRL.World.GameObject]]|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.UI/TradeUI.cs::XRL.UI.TradeUI.DoVendorRepair",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/TradeUiVendorPopupTranslationPatch.cs",
                ("TradeUiVendorPopupTranslationPatch", "DoVendorRepair", "TryTranslatePopupMessage"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("TradeUiVendorPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/TradeUiPopupTranslationPatch.cs",
                (
                    "TradeUiPopup.RepairTooComplex",
                    "TradeUiPopup.RepairNeed",
                    "TradeUiPopup.RepairQuestion",
                    "TradeUiPopup.RepairBroken",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/TradeUiPopupTranslationPatchTests.cs",
                (
                    "VendorOwnerPatch_TranslatesRepairShowAndConfirmationPopups_WhenOwnerPatched",
                    "VendorOwnerPatch_TranslatesRepairBrokenPopups_WhenOwnerPatched",
                    "VendorOwnerPatch_DoesNotRetranslateDirectMarkedRepairPopup_WhenOwnerPatched",
                    "VendorOwnerPatch_LeavesEmptyRepairPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(TradeUiVendorPopupTranslationPatch)",
                    "XRL.UI.TradeUI|DoVendorRepair|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/PetEitherOr.cs::XRL.World.Parts.PetEitherOr.explode",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PetEitherOrExplodeTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "PetEitherOr.Explode"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("PetEitherOrExplodeTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "PetEitherOrExplodePatch_TranslatesQueuedExplodeMessages_WhenPatched",
                    "PetEitherOrExplodePatch_DoesNotTranslateQueuedExplodeMessage_WhenOwnerPatchIsAbsent",
                    "PetEitherOrExplodePatch_PreservesColoredDynamicCaptures_WhenOwnerPatched",
                    "PetEitherOrExplodePatch_DoesNotTranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "PetEitherOrExplodePatch_DoesNotTranslateEmptyQueuedMessage_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "typeof(PetEitherOrExplodeTranslationPatch)",
                    '"explode"',
                    '"XRL.World.Parts.PetEitherOr"',
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/Zone.cs::XRL.World.Zone.WindChange",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ZoneWindChangeTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "Zone.WindChange"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("ZoneWindChangeTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/WorldPartsProducerTranslationPatchTests.cs",
                (
                    "ZoneWindChangePatch_TranslatesQueuedWindMessages_WhenOwnerPatched",
                    "ZoneWindChangePatch_DoesNotTranslateQueuedWindMessage_WhenOwnerPatchIsAbsent",
                    "ZoneWindChangePatch_PreservesColorTags_WhenOwnerPatched",
                    "ZoneWindChangePatch_DoesNotTranslateUnknownWindComponents_WhenOwnerPatched",
                    "ZoneWindChangePatch_DoesNotTranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "ZoneWindChangePatch_DoesNotTranslateEmptyQueuedMessage_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                ("typeof(ZoneWindChangeTranslationPatch)", '"WindChange"', '"XRL.World.Zone"'),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.GainSP",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"GainSP"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectStatPopupTranslationPatchTests.cs",
                (
                    "Patch_TranslatesStatPopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateStatPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyGameObjectStatPopupProducerTarget.GainSP)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.GameObject|GainSP|System.Void|System.Int32|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.GainEgo",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"GainEgo"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectStatPopupTranslationPatchTests.cs",
                (
                    "Patch_TranslatesStatPopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateStatPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyGameObjectStatPopupProducerTarget.GainEgo)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.GameObject|GainEgo|System.Void|System.Int32|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.LoseEgo",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"LoseEgo"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectStatPopupTranslationPatchTests.cs",
                (
                    "Patch_TranslatesStatPopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateStatPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyGameObjectStatPopupProducerTarget.LoseEgo)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.GameObject|LoseEgo|System.Void|System.Int32|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.GainIntelligence",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"GainIntelligence"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectStatPopupTranslationPatchTests.cs",
                (
                    "Patch_TranslatesStatPopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateStatPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyGameObjectStatPopupProducerTarget.GainIntelligence)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.GameObject|GainIntelligence|System.Void|System.Int32|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.GainWillpower",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectStatPopupTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"GainWillpower"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupShowSemanticPipeline.cs",
                ("GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/GameObjectStatPopupTranslationPatchTests.cs",
                (
                    "Patch_TranslatesStatPopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateStatPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyGameObjectStatPopupProducerTarget.GainWillpower)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.GameObject|GainWillpower|System.Void|System.Int32|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Repair.cs::XRL.World.Parts.Repair.HandleEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"HandleEvent"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                (
                    "Patch_TranslatesRepairConfirmationPopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateRepairPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyRepairProducerTarget.HandleEvent)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Repair|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Repair.cs::XRL.World.Parts.Repair.RepairResultSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"RepairResultSuccess"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                (
                    "Patch_TranslatesRepairSuccessShowBlock_WhenOwnerPatched",
                    "Patch_DoesNotTranslateRepairPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyRepairProducerTarget.RepairResultSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Repair|RepairResultSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Repair.cs::XRL.World.Parts.Repair.RepairResultExceptionalSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"RepairResultExceptionalSuccess"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                (
                    "Patch_TranslatesTinkeringBitsReward_WhenOwnerPatched",
                    "Patch_DoesNotTranslateRepairPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyRepairProducerTarget.RepairResultExceptionalSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Repair|RepairResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Repair.cs::XRL.World.Parts.Repair.RepairResultPartialSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"RepairResultPartialSuccess"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                (
                    "Patch_TranslatesRepairOutcomePopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateRepairPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyRepairProducerTarget.RepairResultPartialSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Repair|RepairResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/Repair.cs::XRL.World.Parts.Repair.RepairResultFailure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("TryTranslatePopupMessage", '"RepairResultFailure"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                (
                    "Patch_TranslatesRepairOutcomePopup_WhenOwnerPatched",
                    "Patch_DoesNotTranslateRepairPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "Patch_PreservesWholeSourceAndCaptureColors_WhenOwnerPatched",
                    "nameof(DummyRepairProducerTarget.RepairResultFailure)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Repair|RepairResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Tinkering_Repair.cs::XRL.World.Parts.Skill.Tinkering_Repair.HandleEvent",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                (
                    "XRL.World.Parts.Skill.Tinkering_Repair",
                    "CannotRepairUntilUnderstand",
                    "CannotRepair",
                    "MissingBits",
                    "SpendBits",
                    "OwnershipRisk",
                    "ContainerOwnershipRisk",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                (
                    "TinkeringRepairPatch_TranslatesHandleEventShowFailMessages_WhenOwnerPatched",
                    "TinkeringRepairPatch_TranslatesMissingBitsPopup_WhenOwnerPatched",
                    "TinkeringRepairPatch_TranslatesSpendBitsConfirmation_WhenOwnerPatched",
                    "Patch_TranslatesRepairConfirmationPopup_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Skill.Tinkering_Repair|HandleEvent|System.Boolean|XRL.World.InventoryActionEvent",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Tinkering_Repair.cs::XRL.World.Parts.Skill.Tinkering_Repair.RepairResultSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("XRL.World.Parts.Skill.Tinkering_Repair", "RepairResultSuccess", "Success"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                ("TinkeringRepairPatch_TranslatesSharedRepairSuccessAndBitsPopups_WhenOwnerPatched",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Tinkering_Repair.cs::XRL.World.Parts.Skill.Tinkering_Repair.RepairResultExceptionalSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("XRL.World.Parts.Skill.Tinkering_Repair", "RepairResultExceptionalSuccess", "TinkeringBits"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                ("TinkeringRepairPatch_TranslatesSharedRepairSuccessAndBitsPopups_WhenOwnerPatched",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultExceptionalSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Tinkering_Repair.cs::XRL.World.Parts.Skill.Tinkering_Repair.RepairResultPartialSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("XRL.World.Parts.Skill.Tinkering_Repair", "RepairResultPartialSuccess", "PartialSuccess"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                ("TinkeringRepairPatch_TranslatesSharedRepairOutcomePopups_WhenOwnerPatched",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultPartialSuccess|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts.Skill/Tinkering_Repair.cs::XRL.World.Parts.Skill.Tinkering_Repair.RepairResultFailure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RepairTranslationPatch.cs",
                ("XRL.World.Parts.Skill.Tinkering_Repair", "RepairResultFailure", "Failure"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RepairTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RepairTranslationPatchTests.cs",
                ("TinkeringRepairPatch_TranslatesSharedRepairOutcomePopups_WhenOwnerPatched",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.Skill.Tinkering_Repair|RepairResultFailure|System.Void|XRL.World.GameObject|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/PlayerDanceRitual.cs::XRL.World.Parts.PlayerDanceRitual.ExecuteMove",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "ExecuteMove", "PlayerDanceRitual.Queue"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("PlayerDanceRitualTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/PlayerDanceRitualTranslationPatchTests.cs",
                (
                    "PlayerDanceRitualPatch_TranslatesExecuteMoveQueuedMessage_WhenOwnerPatched",
                    "PlayerDanceRitualPatch_DoesNotTranslateQueuedMessage_WhenOwnerPatchIsAbsent",
                    "PlayerDanceRitualPatch_DoesNotTranslateDirectMarkedQueuedMessage_WhenOwnerPatched",
                    "PlayerDanceRitualPatch_DoesNotTranslateEmptyQueuedMessage_WhenOwnerPatched",
                    "nameof(DummyPlayerDanceRitualProducerTarget.ExecuteMove)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.PlayerDanceRitual|ExecuteMove|System.Void|System.String|System.String",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/PlayerDanceRitual.cs::XRL.World.Parts.PlayerDanceRitual.PassStep",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "PassStep", "PlayerDanceRitual.Queue"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("PlayerDanceRitualTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/PlayerDanceRitualTranslationPatchTests.cs",
                (
                    "PlayerDanceRitualPatch_TranslatesPassStepQueuedMessage_WhenOwnerPatched",
                    "TryTranslateMessage_ReturnsFalseForDirectMarkerAndEmptyInput",
                    "PlayerDanceRitualPatch_DoesNotTranslateQueuedMessage_WhenOwnerPatchIsAbsent",
                    "nameof(DummyPlayerDanceRitualProducerTarget.PassStep)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.PlayerDanceRitual|PassStep|System.Void|System.String",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/PlayerDanceRitual.cs::XRL.World.Parts.PlayerDanceRitual.FailStep",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "FailStep", "PlayerDanceRitual.Queue"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/MessageQueueSemanticPipeline.cs",
                ("PlayerDanceRitualTranslationPatch.TryTranslateQueuedMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/PlayerDanceRitualTranslationPatchTests.cs",
                (
                    "PlayerDanceRitualPatch_TranslatesFailStepQueuedMessage_WhenOwnerPatched",
                    "TryTranslateMessage_ReturnsFalseForDirectMarkerAndEmptyInput",
                    "PlayerDanceRitualPatch_DoesNotTranslateQueuedMessage_WhenOwnerPatchIsAbsent",
                    "nameof(DummyPlayerDanceRitualProducerTarget.FailStep)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.PlayerDanceRitual|FailStep|System.Void|System.String",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/PlayerDanceRitual.cs::XRL.World.Parts.PlayerDanceRitual.FailDance",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs",
                ("TryTranslatePopupMessage", "FailDance"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("PlayerDanceRitualTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/PlayerDanceRitualTranslationPatchTests.cs",
                (
                    "PlayerDanceRitualPatch_TranslatesFailDancePopup_WhenOwnerPatched",
                    "PlayerDanceRitualPatch_DoesNotTranslatePopup_WhenOwnerPatchIsAbsent",
                    "TryTranslatePopup_PreservesDynamicReasonCaptures",
                    "nameof(DummyPlayerDanceRitualProducerTarget.FailDance)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.PlayerDanceRitual|FailDance|System.Void|System.String",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Parts/PlayerDanceRitual.cs::XRL.World.Parts.PlayerDanceRitual.SuccessDance",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PlayerDanceRitualTranslationPatch.cs",
                ("TryTranslatePopupMessage", "SuccessDance"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("PlayerDanceRitualTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/PlayerDanceRitualTranslationPatchTests.cs",
                (
                    "PlayerDanceRitualPatch_TranslatesSuccessDancePopup_WhenOwnerPatched",
                    "PlayerDanceRitualPatch_DoesNotTranslatePopup_WhenOwnerPatchIsAbsent",
                    "TryTranslatePopup_PreservesDynamicReasonCaptures",
                    "nameof(DummyPlayerDanceRitualProducerTarget.SuccessDance)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Parts.PlayerDanceRitual|SuccessDance|System.Void|System.String",
                ),
            ),
        ),
    ),
    *_sifrah_result_popup_families(
        SifrahResultPopupFamilySpec(
            source_file="XRL.World/BeguilingSifrah.cs",
            type_name="XRL.World.BeguilingSifrah",
            evidence=OwnerPopupRouteEvidenceSpec(
                patch_file="Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs",
                patch_type="BeguilingSifrahTranslationPatch",
                test_file="Mods/QudJP/Assemblies/QudJP.Tests/L2/BeguilingSifrahTranslationPatchTests.cs",
                positive_test="Patch_TranslatesBeguilingResultPopups_WhenOwnerPatched",
                negative_test="Patch_DoesNotTranslateBeguilingPopup_WhenOwnerAbsent",
                direct_marker_test="Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                empty_test="Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
            ),
            target_type_name="DummyBeguilingSifrahProducerTarget",
            method_details=(
                ("ResultCriticalFailure", "CriticalFailure"),
                ("ResultFailure", "Failure"),
                ("ResultPartialSuccess", "PartialSuccess"),
                ("ResultSuccess", "InterestedButUnable"),
                ("ResultExceptionalSuccess", "InterestedButUnable"),
            ),
        ),
    ),
    *_sifrah_result_popup_families(
        SifrahResultPopupFamilySpec(
            source_file="XRL.World/ProselytizationSifrah.cs",
            type_name="XRL.World.ProselytizationSifrah",
            evidence=OwnerPopupRouteEvidenceSpec(
                patch_file="Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs",
                patch_type="ProselytizationSifrahTranslationPatch",
                test_file="Mods/QudJP/Assemblies/QudJP.Tests/L2/ProselytizationSifrahTranslationPatchTests.cs",
                positive_test="Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched",
                negative_test="Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent",
                direct_marker_test="Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                empty_test="Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
            ),
            target_type_name="DummyProselytizationSifrahProducerTarget",
            method_details=(
                ("ResultCriticalFailure", "CriticalFailure"),
                ("ResultFailure", "Failure"),
                ("ResultPartialSuccess", "PartialSuccess"),
                ("ResultSuccess", "SympatheticButUnable"),
                ("ResultExceptionalSuccess", "SympatheticButUnable"),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/RebukingSifrah.cs::XRL.World.RebukingSifrah.ResultCriticalFailure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RebukingSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultCriticalFailure", "CriticalFailure"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RebukingSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RebukingSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesRebukingResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateRebukingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyRebukingSifrahProducerTarget.ResultCriticalFailure)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.RebukingSifrah|ResultCriticalFailure|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/RebukingSifrah.cs::XRL.World.RebukingSifrah.ResultPartialSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/RebukingSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultPartialSuccess", "PartialSuccess"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("RebukingSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/RebukingSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesRebukingResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateRebukingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyRebukingSifrahProducerTarget.ResultPartialSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.RebukingSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Capabilities/ItemNaming.cs::XRL.World.Capabilities.ItemNaming.Opportunity",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ItemNamingTranslationPatch.cs",
                ("TryTranslatePopupMessage", "OpportunityPattern", "Opportunity"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("ItemNamingTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/ItemNamingTranslationPatchTests.cs",
                (
                    "Patch_TranslatesOpportunityPrompt_WhenOwnerPatched",
                    "Patch_DoesNotClaimItemNamingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Capabilities.ItemNaming|Opportunity|System.Boolean|XRL.World.GameObject|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|System.Int32|System.Int32|System.Int32|System.Int32|System.Boolean",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World.Capabilities/ItemNaming.cs::XRL.World.Capabilities.ItemNaming.CheckBestowals",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ItemNamingTranslationPatch.cs",
                (
                    "TryTranslatePopupMessage",
                    "DoesVerbRouteTranslator.TryTranslateMarkedMessage",
                    "CheckBestowals.DoesVerb",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("ItemNamingTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Localization/MessageFrames/verbs.ja.json",
                ('"verb": "seem"', '"extra": "to have taken on new qualities"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/ItemNamingTranslationPatchTests.cs",
                (
                    "Patch_TranslatesMarkedCheckBestowalsPopup_WhenOwnerPatched",
                    "Patch_DoesNotClaimMarkedCheckBestowalsPopup_WhenOwnerAbsent",
                    "UseRepositoryVerbDictionary",
                    "CheckBestowals.DoesVerb",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.Capabilities.ItemNaming|CheckBestowals|System.Void|XRL.World.GameObject|XRL.World.GameObject|System.String|System.String|XRL.World.GameObject|XRL.World.GameObject|System.String|System.Boolean&|System.Int32&|System.Boolean&",
                ),
            ),
        ),
    ),
    *_examiner_result_popup_families(),
    *_force_bubble_owner_families(),
    *_combat_skill_extension_owner_families(),
    *_cybernetics_wish_implant_popup_family(),
    *_map_reveal_popup_family(),
    *_conversation_reward_popup_families(),
    *_game_summary_tombstone_popup_families(),
    *_powered_floating_popup_family(),
    *_conversation_check_lost_popup_family(),
    *_conversation_take_item_popup_family(),
    *_mechanical_wings_popup_family(),
    *_fire_suppression_discharge_message_families(),
    *_cudgel_conk_popup_family(),
    *_experience_award_xp_family(),
    *_mutation_absorption_healing_families(),
    *_on_eat_reward_message_families(),
    *_effect_mobility_block_families(),
    *_mutation_infection_families(),
    *_mutation_action_failure_families(),
    *_disassembly_start_families(),
    *_dance_ritual_opponent_families(),
    *_iexamine_process_identify_families(),
    *_self_tear_explosion_families(),
    *_tenfold_path_initiatory_families(),
    *_power_entry_prerequisite_popup_families(),
    *_magnetic_pulse_families(),
    *_pet_gloaming_families(),
    *_windup_families(),
    *_damage_penetration_debug_families(),
    *_base_pronoun_provider_customize_families(),
    *_fugue_on_step_families(),
    *_mental_shield_families(),
    *_tabula_rasae_families(),
    *_eat_memories_on_hit_families(),
    *_cybernetics_stasis_entangler_families(),
    *_engulfing_families(),
    *_temporary_reality_stabilize_families(),
    *_cloning_start_budded_clone_families(),
    *_hidden_render_families(),
    *_engraver_families(),
    *_auto_act_reset_family(),
    *_prefixed_owner_queue_families(),
    *_generated_subject_queue_families(),
)
COVERED_OWNER_FAMILY_IDS: Final = frozenset(family.family_id for family in COVERED_OWNER_FAMILIES)


def load_inventory(path: Path) -> InventoryPayload:
    """Load a static producer inventory JSON payload."""
    return cast("InventoryPayload", json.loads(path.read_text(encoding="utf-8")))


def covered_family_ids() -> frozenset[str]:
    """Return family ids that current tests close as owner-patch covered."""
    return COVERED_OWNER_FAMILY_IDS


def family_closure_status(family: FamilyPayload) -> str:
    """Return current-repo closure status for an inventory family."""
    if family["producer_family_id"] in covered_family_ids():
        return COVERED_BY_OWNER_PATCH
    return family["family_closure_status"]


def owner_action_queue(inventory: InventoryPayload) -> list[FamilyPayload]:
    """Return producer families that still need owner-route implementation work."""
    return [
        family
        for family in inventory["families"]
        if family_closure_status(family) in OWNER_ACTION_STATUSES
    ]


def owner_action_queue_entries(inventory: InventoryPayload) -> list[OwnerActionQueueEntry]:
    """Return actionable owner work as method-level queue entries."""
    return sorted(
        (_owner_action_queue_entry(family) for family in owner_action_queue(inventory)),
        key=lambda entry: (
            entry["source_file"],
            entry["member_start_line"],
            entry["producer_family_id"],
        ),
    )


def owner_action_queue_by_file(inventory: InventoryPayload) -> list[SourceFileQueueEntry]:
    """Return actionable owner work grouped by decompiled C# source file."""
    grouped: dict[str, list[OwnerActionQueueEntry]] = {}
    for entry in owner_action_queue_entries(inventory):
        grouped.setdefault(entry["source_file"], []).append(entry)

    source_entries = [_source_file_queue_entry(source_file, families) for source_file, families in grouped.items()]
    return sorted(
        source_entries,
        key=lambda entry: (
            -entry["family_count"],
            -entry["text_argument_count"],
            -entry["callsite_count"],
            entry["source_file"],
        ),
    )


def format_owner_action_queue(
    inventory: InventoryPayload,
    *,
    limit: int | None = 30,
) -> str:
    """Format the class-file owner action queue for agent handoff."""
    source_entries = owner_action_queue_by_file(inventory)
    family_total = sum(entry["family_count"] for entry in source_entries)
    callsite_total = sum(entry["callsite_count"] for entry in source_entries)
    text_argument_total = sum(entry["text_argument_count"] for entry in source_entries)

    lines = [
        "".join(
            (
                "owner action queue: ",
                f"{family_total} families, {callsite_total} callsites, ",
                f"{text_argument_total} text arguments across {len(source_entries)} source files",
            )
        )
    ]

    displayed_entries = source_entries if limit is None else source_entries[:limit]
    for index, source_entry in enumerate(displayed_entries, start=1):
        lines.append(
            "".join(
                (
                    f"{index}. {source_entry['source_file']}: ",
                    f"{source_entry['family_count']} families, ",
                    f"{source_entry['callsite_count']} callsites, ",
                    f"{source_entry['text_argument_count']} text arguments; ",
                    f"statuses={_format_counter(source_entry['family_statuses'])}; ",
                    f"surfaces={_format_counter(source_entry['surface_counts'])}",
                )
            )
        )
        lines.extend(
            "".join(
                (
                    f"   - line {family['member_start_line']} ",
                    f"{family['type_name']}.{family['member_name']} ",
                    f"[{family['family_closure_status']}], ",
                    f"{family['text_argument_count']} text args, ",
                    f"surfaces={_format_counter(family['surface_counts'])}",
                )
            )
            for family in source_entry["families"][:3]
        )

    if limit is not None and len(source_entries) > limit:
        lines.append(f"... {len(source_entries) - limit} more source files omitted")

    return "\n".join(lines)


def validate_covered_owner_families(
    inventory: InventoryPayload,
    repo_root: Path = REPO_ROOT,
) -> list[str]:
    """Validate that covered-owner registry entries still have source and test evidence."""
    errors: list[str] = []
    families = {family["producer_family_id"]: family for family in inventory["families"]}
    seen: set[str] = set()

    for covered in COVERED_OWNER_FAMILIES:
        if covered.family_id in seen:
            errors.append(f"duplicate covered family id: {covered.family_id}")
            continue
        seen.add(covered.family_id)

        family = families.get(covered.family_id)
        if family is None:
            errors.append(f"covered family missing from inventory: {covered.family_id}")
            continue

        if family["family_closure_status"] not in covered.inventory_statuses:
            expected = ", ".join(covered.inventory_statuses)
            actual = family["family_closure_status"]
            errors.append(f"{covered.family_id}: expected raw inventory status in [{expected}], got {actual}")

        for evidence in covered.evidence_files:
            path = repo_root / evidence.path
            if not path.is_file():
                errors.append(f"{covered.family_id}: evidence file missing: {evidence.path}")
                continue

            text = path.read_text(encoding="utf-8")
            errors.extend(
                f"{covered.family_id}: {evidence.path} missing {required!r}"
                for required in evidence.required_substrings
                if required not in text
            )

    return errors


def main(argv: list[str] | None = None) -> int:
    """Print or serialize the current static-producer owner action queue."""
    parser = ArgumentParser(description="Summarize static producer owner-route work by decompiled C# file.")
    _ = parser.add_argument("--inventory", type=Path, default=DEFAULT_INVENTORY_PATH)
    _ = parser.add_argument("--format", choices=("text", "json"), default="text")
    _ = parser.add_argument("--limit", type=int, default=30, help="maximum source files for text output; 0 means all")
    args = parser.parse_args(argv)

    inventory_path = cast("Path", args.inventory)
    output_format = cast("OutputFormat", args.format)
    limit_arg = cast("int", args.limit)
    limit = None if limit_arg == 0 else limit_arg

    inventory = load_inventory(inventory_path)
    evidence_errors = validate_covered_owner_families(inventory)
    if evidence_errors:
        _ = sys.stderr.write("\n".join(evidence_errors) + "\n")
        return 1

    if output_format == "json":
        source_entries = owner_action_queue_by_file(inventory)
        payload = {
            "schema_version": "1.0",
            "inventory": str(inventory_path),
            "source_file_count": len(source_entries),
            "family_count": sum(entry["family_count"] for entry in source_entries),
            "source_files": source_entries,
        }
        _ = sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
        return 0

    _ = sys.stdout.write(format_owner_action_queue(inventory, limit=limit) + "\n")
    return 0


def _owner_action_queue_entry(family: FamilyPayload) -> OwnerActionQueueEntry:
    return {
        "source_file": family["file"],
        "producer_family_id": family["producer_family_id"],
        "type_name": family["type_name"],
        "member_name": family["member_name"],
        "member_start_line": family["member_start_line"],
        "family_closure_status": family_closure_status(family),
        "callsite_count": family["callsite_count"],
        "text_argument_count": family["text_argument_count"],
        "surface_counts": dict(family["surface_counts"]),
        "closure_status_counts": dict(family["closure_status_counts"]),
        "representative_lines": [call["line"] for call in family["representative_calls"]],
    }


def _source_file_queue_entry(
    source_file: str,
    families: list[OwnerActionQueueEntry],
) -> SourceFileQueueEntry:
    family_statuses: dict[str, int] = {}
    surface_counts: dict[str, int] = {}
    for family in families:
        family_statuses[family["family_closure_status"]] = family_statuses.get(family["family_closure_status"], 0) + 1
        for surface, count in family["surface_counts"].items():
            surface_counts[surface] = surface_counts.get(surface, 0) + count

    return {
        "source_file": source_file,
        "family_count": len(families),
        "callsite_count": sum(family["callsite_count"] for family in families),
        "text_argument_count": sum(family["text_argument_count"] for family in families),
        "family_statuses": family_statuses,
        "surface_counts": surface_counts,
        "families": families,
    }


def _format_counter(counter: dict[str, int]) -> str:
    return ",".join(f"{key}:{counter[key]}" for key in sorted(counter))


if __name__ == "__main__":
    raise SystemExit(main())
