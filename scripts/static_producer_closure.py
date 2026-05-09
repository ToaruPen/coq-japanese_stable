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


COVERED_OWNER_FAMILIES: Final = (
    CoveredOwnerFamily(
        family_id="XRL.World/GameObject.cs::XRL.World.GameObject.Heal",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/GameObjectHealTranslationPatch.cs",
                ("TryTranslateQueuedMessage", '"Heal"'),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs",
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
                ("typeof(GameObjectHealTranslationPatch)", '"Heal"'),
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
                "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
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
        family_id="XRL.World.Parts/PetEitherOr.cs::XRL.World.Parts.PetEitherOr.explode",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PetEitherOrExplodeTranslationPatch.cs",
                ("TryTranslateQueuedMessage", "PetEitherOr.Explode"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs",
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
                ("typeof(PetEitherOrExplodeTranslationPatch)", '"explode"'),
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
                "Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs",
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
                ("typeof(ZoneWindChangeTranslationPatch)", '"WindChange"'),
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
                "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
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
                "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
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
                "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
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
                "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
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
                "Mods/QudJP/Assemblies/src/Patches/PopupShowTranslationPatch.cs",
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
                "Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs",
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
                "Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs",
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
                "Mods/QudJP/Assemblies/src/Patches/CombatAndLogMessageQueuePatch.cs",
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
    CoveredOwnerFamily(
        family_id="XRL.World/BeguilingSifrah.cs::XRL.World.BeguilingSifrah.ResultCriticalFailure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultCriticalFailure", "CriticalFailure"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("BeguilingSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/BeguilingSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesBeguilingResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateBeguilingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyBeguilingSifrahProducerTarget.ResultCriticalFailure)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.BeguilingSifrah|ResultCriticalFailure|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/BeguilingSifrah.cs::XRL.World.BeguilingSifrah.ResultFailure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultFailure", "Failure"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("BeguilingSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/BeguilingSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesBeguilingResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateBeguilingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyBeguilingSifrahProducerTarget.ResultFailure)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.BeguilingSifrah|ResultFailure|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/BeguilingSifrah.cs::XRL.World.BeguilingSifrah.ResultPartialSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultPartialSuccess", "PartialSuccess"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("BeguilingSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/BeguilingSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesBeguilingResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateBeguilingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyBeguilingSifrahProducerTarget.ResultPartialSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.BeguilingSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/BeguilingSifrah.cs::XRL.World.BeguilingSifrah.ResultSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultSuccess", "InterestedButUnable"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("BeguilingSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/BeguilingSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesBeguilingResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateBeguilingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyBeguilingSifrahProducerTarget.ResultSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.BeguilingSifrah|ResultSuccess|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/BeguilingSifrah.cs::XRL.World.BeguilingSifrah.ResultExceptionalSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/BeguilingSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultExceptionalSuccess", "InterestedButUnable"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("BeguilingSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/BeguilingSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesBeguilingResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateBeguilingPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyBeguilingSifrahProducerTarget.ResultExceptionalSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.BeguilingSifrah|ResultExceptionalSuccess|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/ProselytizationSifrah.cs::XRL.World.ProselytizationSifrah.ResultCriticalFailure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultCriticalFailure", "CriticalFailure"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("ProselytizationSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/ProselytizationSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyProselytizationSifrahProducerTarget.ResultCriticalFailure)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.ProselytizationSifrah|ResultCriticalFailure|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/ProselytizationSifrah.cs::XRL.World.ProselytizationSifrah.ResultFailure",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultFailure", "Failure"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("ProselytizationSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/ProselytizationSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyProselytizationSifrahProducerTarget.ResultFailure)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.ProselytizationSifrah|ResultFailure|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/ProselytizationSifrah.cs::XRL.World.ProselytizationSifrah.ResultPartialSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultPartialSuccess", "PartialSuccess"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("ProselytizationSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/ProselytizationSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyProselytizationSifrahProducerTarget.ResultPartialSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.ProselytizationSifrah|ResultPartialSuccess|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/ProselytizationSifrah.cs::XRL.World.ProselytizationSifrah.ResultSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultSuccess", "SympatheticButUnable"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("ProselytizationSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/ProselytizationSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyProselytizationSifrahProducerTarget.ResultSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.ProselytizationSifrah|ResultSuccess|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
    CoveredOwnerFamily(
        family_id="XRL.World/ProselytizationSifrah.cs::XRL.World.ProselytizationSifrah.ResultExceptionalSuccess",
        inventory_statuses=("owner_patch_required",),
        evidence_files=(
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/ProselytizationSifrahTranslationPatch.cs",
                ("TryTranslatePopupMessage", "ResultExceptionalSuccess", "SympatheticButUnable"),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs",
                ("ProselytizationSifrahTranslationPatch.TryTranslatePopupMessage",),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2/ProselytizationSifrahTranslationPatchTests.cs",
                (
                    "Patch_TranslatesProselytizationResultPopups_WhenOwnerPatched",
                    "Patch_DoesNotTranslateProselytizationPopup_WhenOwnerAbsent",
                    "Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched",
                    "Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched",
                    "nameof(DummyProselytizationSifrahProducerTarget.ResultExceptionalSuccess)",
                ),
            ),
            EvidenceFile(
                "Mods/QudJP/Assemblies/QudJP.Tests/L2G/TargetMethodResolutionTests.cs",
                (
                    "OwnerProducerTargetMethods_ResolveExpectedFullSignatures",
                    "XRL.World.ProselytizationSifrah|ResultExceptionalSuccess|System.Void|XRL.World.GameObject",
                ),
            ),
        ),
    ),
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
