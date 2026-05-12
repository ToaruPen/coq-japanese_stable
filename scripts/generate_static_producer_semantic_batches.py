"""Generate semantic planning batches for the static producer owner queue."""

# ruff: noqa: D101, D103, E501, I001

from __future__ import annotations

import argparse
import html
import json
import sys
import tempfile
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Any, Literal, TypedDict, cast

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.static_producer_closure import DEFAULT_INVENTORY_PATH, load_inventory, owner_action_queue_entries  # noqa: E402

if TYPE_CHECKING:
    from scripts.static_producer_closure import OwnerActionQueueEntry

DEFAULT_JSON_OUTPUT = REPO_ROOT / "docs/reports/static-producer-owner-queue-semantic-batches.json"
DEFAULT_MARKDOWN_OUTPUT = REPO_ROOT / "docs/reports/2026-05-12-issue-576-static-producer-owner-queue-semantic-batches.md"

EXPECTED_QUEUE_TOTALS = {
    "family_count": 337,
    "callsite_count": 940,
    "text_argument_count": 961,
}
RISK_CALLSITE_THRESHOLD = 25

Lane = Literal["owner_patch_required_quick_win", "needs_family_review_split_proof"]
Risk = Literal["low", "medium", "high"]


class BatchMetadata(TypedDict):
    lane: Lane
    semantic_theme: str
    inclusion_rule: str
    route_verification_notes: str
    recommended_worktree_parallelization: str
    suggested_test_check_commands: list[str]


@dataclass(frozen=True)
class OutputPaths:
    json_path: Path
    markdown_path: Path


BATCH_METADATA: dict[str, BatchMetadata] = {
    "quickwin-combat-skills": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Combat skills and weapon-action owner messages",
        "inclusion_rule": "owner_patch_required families in XRL.World.Parts.Skill or weapon/combat part files.",
        "route_verification_notes": (
            "Audit AddPlayerMessage/Popup.Show handoff per skill owner, then prove queue-only or popup-only negatives."
        ),
        "recommended_worktree_parallelization": (
            "One worktree per skill family cluster; avoid sharing CombatAndLogMessageQueuePatchTests edits."
        ),
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<SkillOrPatchName>' --no-restore",
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2G&FullyQualifiedName~TargetMethodResolution' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-mutations-psionics": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Mutation, psychic, and innate ability owner messages",
        "inclusion_rule": "owner_patch_required families in mutation APIs, mutation parts, or psychic/ability part files.",
        "route_verification_notes": "Preserve actor/object placeholders and direct markers across queued and popup routes.",
        "recommended_worktree_parallelization": "Split by mutation class; keep shared mutation helper changes serialized.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<MutationOrPatchName>' --no-restore",
            "just test-l2g",
            "just static-producer-check",
        ],
    },
    "quickwin-effects-status": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Status effect owner messages",
        "inclusion_rule": "owner_patch_required families in XRL.World.Effects.",
        "route_verification_notes": "Treat effect Apply/FireEvent/Expired text as effect-owned; test empty/direct-marker no-ops.",
        "recommended_worktree_parallelization": "One worktree per related effect group; keep tonic/effect helper edits isolated.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<EffectOrPatchName>' --no-restore",
            "just test-l2g",
            "just static-producer-check",
        ],
    },
    "quickwin-ui-popups": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "UI screen fixed popup/message owners",
        "inclusion_rule": "owner_patch_required families in XRL.UI or Qud.UI.",
        "route_verification_notes": "Confirm fixed labels vs caller-provided body/title arguments before generic popup fallback.",
        "recommended_worktree_parallelization": "One worktree per screen class; avoid concurrent edits to generic popup pipeline tests.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<UIScreenOrPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-conversation-ritual": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Conversation and ritual reward popup owners",
        "inclusion_rule": "owner_patch_required families in XRL.World.Conversations.Parts or ritual-like owners.",
        "route_verification_notes": "Keep conversation context text separate from generated social/Water Ritual content.",
        "recommended_worktree_parallelization": "Split by conversation part; serialize shared Water Ritual helpers.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<ConversationOrRitualPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-sifrah-minigames": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Sifrah minigame owner popup leaves",
        "inclusion_rule": "owner_patch_required families whose file, type, or member contains Sifrah.",
        "route_verification_notes": "Prove Sifrah owner target signatures and avoid masking generated slot/token text.",
        "recommended_worktree_parallelization": "One worktree per Sifrah subsystem; do not combine with non-Sifrah UI popup work.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~Sifrah' --no-restore",
            "just test-l2g",
            "just static-producer-check",
        ],
    },
    "quickwin-quest-world": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Quest, history, and world navigation owners",
        "inclusion_rule": "owner_patch_required families in quest/history/world navigation surfaces.",
        "route_verification_notes": "Separate fixed lifecycle messages from quest-generated descriptive payloads.",
        "recommended_worktree_parallelization": "Split quest lifecycle, history, and navigation into separate worktrees.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<QuestOrWorldPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-core-systems": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Core manager and system owner messages",
        "inclusion_rule": "owner_patch_required families in XRL.Core or top-level manager/system files.",
        "route_verification_notes": "Prefer narrow manager owner helpers; do not route debug/progress text through broad popup fallback.",
        "recommended_worktree_parallelization": "One worktree per manager file because tests often share system setup fixtures.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<SystemOrPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-capabilities-systems": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Capability helper owner messages",
        "inclusion_rule": "owner_patch_required families in XRL.World.Capabilities.",
        "route_verification_notes": "Verify capability helper is the true producer rather than a caller-specific wrapper.",
        "recommended_worktree_parallelization": "One capability per worktree; serialize shared capability translation helpers.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<CapabilityOrPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "General world part owner messages",
        "inclusion_rule": "owner_patch_required families in XRL.World.Parts not claimed by narrower quick-win rules.",
        "route_verification_notes": "Check whether owner route is part-local, item-local, or actor/action-local before patching.",
        "recommended_worktree_parallelization": "Shard by source file; avoid touching shared popup/message pipelines concurrently.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<PartOrPatchName>' --no-restore",
            "just test-l2g",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts-liquids-consumables": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Liquid, food, tonic, and consumable part owner messages",
        "inclusion_rule": "owner_patch_required world part families for liquids, leaks, eating, tonics, and fire/temperature consumables.",
        "route_verification_notes": "Preserve actor/item placeholders and distinguish consumable effect text from generic queue fallback.",
        "recommended_worktree_parallelization": "One worktree for liquid/leak rows and one for food/tonic rows.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<LiquidOrConsumablePatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts-tools-items": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Item tools, books, data disks, and creation/deploy prompts",
        "inclusion_rule": "owner_patch_required world part families for tools, books, disks, deployables, engraving, and item interactions.",
        "route_verification_notes": "Verify fixed tool prompts before translating object display-name or generated item text.",
        "recommended_worktree_parallelization": "Shard by source file; keep item-name or display-name helpers out of this lane unless proven.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<ToolOrItemPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts-equipment-power": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Equipment, power, charge, cybernetics, and device owner messages",
        "inclusion_rule": "owner_patch_required world part families for powered equipment, cells, sockets, cybernetics, and device activation.",
        "route_verification_notes": "Separate device state text from actor action text and prove both popup/queue surfaces when mixed.",
        "recommended_worktree_parallelization": "One worktree per equipment/device cluster; serialize shared powered-device helpers.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<DevicePatchName>' --no-restore",
            "just test-l2g",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts-teleport-travel-map": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Teleport, travel, map reveal, and navigation owner messages",
        "inclusion_rule": "owner_patch_required world part families for teleporters, map reveal, location finding, travel, and movement state.",
        "route_verification_notes": "Distinguish fixed travel failure/success prompts from generated destination or zone names.",
        "recommended_worktree_parallelization": "One mobility mechanism per worktree; keep runtime destination proof separate.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<TravelPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts-terminals-physical": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Terminals, switches, hidden/reveal, and physical interaction owner messages",
        "inclusion_rule": "owner_patch_required world part families for terminals, switches, physical state, hidden/reveal, and uniqueness prompts.",
        "route_verification_notes": "Check whether the source is a physical part owner or a UI/system wrapper before patching.",
        "recommended_worktree_parallelization": "Shard terminal/switch/physical rows independently; tests should name the owning part.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<PhysicalPartPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts-social-books-records": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Social, record, book, reward, and lore-adjacent owner messages",
        "inclusion_rule": "owner_patch_required world part families for social rewards, books, records, baetyl-like interactions, and lore prompts.",
        "route_verification_notes": "Split fixed confirmation text from generated history/secret/faction text if any appears during audit.",
        "recommended_worktree_parallelization": "One source file per worktree; keep conversation-specific helpers separate.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<SocialOrRecordPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-world-parts-physiology-misc": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "General physiology and remaining world part owner messages",
        "inclusion_rule": "owner_patch_required world part families not claimed by a narrower quick-win part lane.",
        "route_verification_notes": "Treat as source-file-sized quick wins and promote recurring helpers only after first proof.",
        "recommended_worktree_parallelization": "One source file per worktree; avoid broad shared helper edits initially.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<PartPatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "quickwin-world-systems-misc": {
        "lane": "owner_patch_required_quick_win",
        "semantic_theme": "Remaining owner-patch-required system leaves",
        "inclusion_rule": "owner_patch_required families not matched by narrower quick-win semantic rules.",
        "route_verification_notes": "Treat as one-family quick wins until a narrower recurring owner pattern emerges.",
        "recommended_worktree_parallelization": "One source file per worktree; merge only after static-producer-check is green.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<PatchName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "review-campfire-cooking-nostrums": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Campfire cooking, recipe, and nostrum mixed popup routes",
        "inclusion_rule": "needs_family_review families in XRL.World.Parts/Campfire.cs.",
        "route_verification_notes": "Split fixed menu labels from generated recipe/ingredient/runtime meal text before closure.",
        "recommended_worktree_parallelization": "One worktree for cooking availability; one for nostrums; do not patch all Campfire at once.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~Campfire' --no-restore",
            "just static-producer-check",
        ],
    },
    "review-core-turn-save-systems": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Core turn loop, action manager, save/hotload mixed routes",
        "inclusion_rule": "needs_family_review families in XRL.Core or top-level manager files.",
        "route_verification_notes": "Separate player-visible fixed leaves from debug/progress/runtime state messages.",
        "recommended_worktree_parallelization": "Handle PlayerTurn and ActionManager in separate worktrees; both have broad blast radius.",
        "suggested_test_check_commands": [
            "just static-producer-owner-queue",
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~Core' --no-restore",
            "just static-producer-check",
        ],
    },
    "review-ui-screen-workflows": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "UI screen workflow popups and menu labels",
        "inclusion_rule": "needs_family_review families in XRL.UI or Qud.UI.",
        "route_verification_notes": "Classify fixed labels/buttons separately from object-specific body text and prompts.",
        "recommended_worktree_parallelization": "One screen per worktree; TradeUI should be isolated because of high callsite volume.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~Popup' --no-restore",
            "just static-producer-check",
        ],
    },
    "review-conversation-ritual-social": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Conversation, social, and Water Ritual mixed routes",
        "inclusion_rule": "needs_family_review families in conversation parts or social/ritual owners.",
        "route_verification_notes": "Split fixed affordance text from NPC/faction/secret generated text; runtime proof may be required.",
        "recommended_worktree_parallelization": "One ritual/social family cluster per worktree; avoid sharing conversation fixtures.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
    "review-sifrah-minigame-generated": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Sifrah minigame generated and fixed popup mixtures",
        "inclusion_rule": "needs_family_review families whose file, type, or member contains Sifrah.",
        "route_verification_notes": "Split fixed token prompts from generated result/slot content and prove target signatures.",
        "recommended_worktree_parallelization": "One Sifrah subtype per worktree; keep shared Sifrah helper review serialized.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~Sifrah' --no-restore",
            "just test-l2g",
            "just static-producer-check",
        ],
    },
    "review-combat-skills-weapon": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Combat, weapon, and active skill mixed message routes",
        "inclusion_rule": "needs_family_review families in skill, weapon, combat, or action-message part files.",
        "route_verification_notes": "Split combat log queue messages from popup prompts and runtime combat roll text.",
        "recommended_worktree_parallelization": "Shard by skill tree or weapon family; keep shared combat queue tests serialized.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~Combat' --no-restore",
            "just static-producer-check",
        ],
    },
    "review-mutations-effects": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Mutation and status-effect mixed route families",
        "inclusion_rule": "needs_family_review families in mutations, effects, tonics, psychic, or status-like part files.",
        "route_verification_notes": "Separate effect-owned fixed leaves from actor-specific generated status text.",
        "recommended_worktree_parallelization": "One effect/mutation group per worktree; avoid cross-cutting tonic helpers initially.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~<EffectOrMutationName>' --no-restore",
            "just static-producer-check",
        ],
    },
    "review-items-equipment-tools": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Tools, item naming, tinkering, and trade-item routes",
        "inclusion_rule": "needs_family_review item/tool/equipment/tinkering families outside UI screen owners.",
        "route_verification_notes": "Split fixed tool prompts from object display names, item data, and generated descriptions.",
        "recommended_worktree_parallelization": "Separate tinkering/naming/tool worktrees; avoid shared item-name helpers.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
    "review-mobility-teleport-travel": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Teleportation, travel, flight, and navigation routes",
        "inclusion_rule": "needs_family_review families with teleport/travel/flight/navigation/vortex route names.",
        "route_verification_notes": "Distinguish fixed failure prompts from destination, zone, and runtime actor text.",
        "recommended_worktree_parallelization": "One mobility mechanism per worktree; runtime-required cases need separate evidence tasks.",
        "suggested_test_check_commands": [
            "dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter 'TestCategory=L2&FullyQualifiedName~Teleport' --no-restore",
            "just static-producer-check",
        ],
    },
    "review-environment-liquids-world-objects": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Liquids, physics, environment, and object-interaction routes",
        "inclusion_rule": "needs_family_review world part families for environmental/object interactions not claimed above.",
        "route_verification_notes": "Check whether emitted text is object-owned, environment-owned, or runtime-only.",
        "recommended_worktree_parallelization": "Shard by source file; keep remaining environment families isolated until ownership is proven.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
    "review-inventory-container-equipment": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Inventory, container, equipment gate, and powered-item split routes",
        "inclusion_rule": "needs_family_review inventory/container/equip/socket/floating/power families.",
        "route_verification_notes": "Split fixed affordance prompts from generated object names and equipment state text.",
        "recommended_worktree_parallelization": "Handle Inventory.FireEvent in its own worktree; small equip-gate rows can be grouped after shape proof.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
    "review-liquids-consumables-environment": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Liquids, firefighting, consumables, and environmental runtime routes",
        "inclusion_rule": "needs_family_review liquid/fire/tonic/food/environment families with generated or runtime-required shapes.",
        "route_verification_notes": "Separate fixed prompts from runtime volume/material/temperature state before owner closure.",
        "recommended_worktree_parallelization": "LiquidVolume must be isolated; Firefighting and smaller consumable rows can be separate goals.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
    "review-terminals-physical-interactions": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Terminals, switches, physics, movement, and physical interaction split routes",
        "inclusion_rule": "needs_family_review terminal/switch/physics/enclosing/stairs/vehicle/physical interaction families.",
        "route_verification_notes": "Prove whether the part, movement processor, or caller owns each emitted message.",
        "recommended_worktree_parallelization": "Physics gets a dedicated worktree; other physical interaction rows can shard by source file.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
    "review-social-quest-books-records": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Social, quest-adjacent, book, record, and lore split routes",
        "inclusion_rule": "needs_family_review social/record/book/deed/baetyl/examiner/spindle/quest-curio families.",
        "route_verification_notes": "Split fixed prompts from generated social, lore, faction, and secret text; runtime proof is common.",
        "recommended_worktree_parallelization": "Examiner, SpindleNegotiation, and baetyl-style rows should each be isolated.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
    "review-quests-world-generation": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Quest, zone, world-generation, and history routes",
        "inclusion_rule": "needs_family_review families in quest, zone, world generation, API, or history surfaces.",
        "route_verification_notes": "Separate fixed lifecycle prompts from generated location/history content.",
        "recommended_worktree_parallelization": "Separate quest, zone, and history worktrees; runtime evidence may gate generated text.",
        "suggested_test_check_commands": [
            "just static-producer-owner-queue",
            "just static-producer-check",
        ],
    },
    "review-misc-system-routes": {
        "lane": "needs_family_review_split_proof",
        "semantic_theme": "Remaining mixed system routes requiring family review",
        "inclusion_rule": "needs_family_review families not matched by narrower semantic rules.",
        "route_verification_notes": "Treat each family as split/proof work until a recurring owner pattern is proven.",
        "recommended_worktree_parallelization": "One source file per worktree; promote repeated patterns only after the first proof.",
        "suggested_test_check_commands": [
            "jq -r '.callsites[] | select(.producer_family_id==\"<FAMILY_ID>\") | [.line,.target_surface,.closure_status,.expression] | @tsv' docs/static-producer-inventory.json",
            "just static-producer-check",
        ],
    },
}


def _contains_any(text: str, tokens: tuple[str, ...]) -> bool:
    return any(token in text for token in tokens)


def _family_text(entry: OwnerActionQueueEntry) -> str:
    return " ".join(
        (
            entry["source_file"],
            entry["producer_family_id"],
            entry["type_name"],
            entry["member_name"],
        )
    ).lower()


def classify_entry(entry: OwnerActionQueueEntry) -> str:  # noqa: C901, PLR0911, PLR0912
    """Return the semantic batch id for one owner queue family."""
    source = entry["source_file"]
    text = _family_text(entry)
    owner_patch_only = entry["family_closure_status"] == "owner_patch_required"

    if owner_patch_only:
        if source.startswith("XRL.World.Parts.Skill/") or _contains_any(
            text, ("combat", "weapon", "axe_", "cudgel_", "rifle_", "shield_", "shortblades_", "longblades")
        ):
            return "quickwin-combat-skills"
        if source.startswith("XRL.World.Parts.Mutation/") or _contains_any(
            text, ("mutation", "psychic", "mentalshield", "forcebubble", "belcher")
        ):
            return "quickwin-mutations-psionics"
        if source.startswith("XRL.World.Effects/"):
            return "quickwin-effects-status"
        if source.startswith(("XRL.UI/", "Qud.UI/")):
            return "quickwin-ui-popups"
        if source.startswith("XRL.World.Conversations.Parts/") or _contains_any(text, ("ritual", "water")):
            return "quickwin-conversation-ritual"
        if "sifrah" in text:
            return "quickwin-sifrah-minigames"
        if source.startswith(("XRL.World.Quests", "XRL.World.QuestManagers/")) or _contains_any(
            text, ("quest", "historic", "pointofinterest", "navigate")
        ):
            return "quickwin-quest-world"
        if source.startswith("XRL.Core/") or source.count("/") == 0:
            return "quickwin-core-systems"
        if source.startswith("XRL.World.Capabilities/"):
            return "quickwin-capabilities-systems"
        if source.startswith("XRL.World.Parts/"):
            if _contains_any(
                text,
                (
                    "liquid",
                    "leak",
                    "tonic",
                    "food",
                    "eat",
                    "refreshallcooldownsoneat",
                    "moponeat",
                    "campfireremains",
                    "temperature",
                    "fire",
                    "torch",
                ),
            ):
                return "quickwin-world-parts-liquids-consumables"
            if _contains_any(
                text,
                (
                    "datadisk",
                    "disk",
                    "deploy",
                    "engrave",
                    "spray",
                    "toolbox",
                    "trainingbook",
                    "markovbook",
                    "genocidecurio",
                    "reclamation",
                    "supplyable",
                    "requirespowertoequip",
                ),
            ):
                return "quickwin-world-parts-tools-items"
            if _contains_any(
                text,
                (
                    "energy",
                    "socket",
                    "power",
                    "floating",
                    "magnetized",
                    "wing",
                    "cybernetic",
                    "clockwork",
                    "flywheel",
                    "stop",
                    "windup",
                    "forceemitter",
                    "electrical",
                ),
            ):
                return "quickwin-world-parts-equipment-power"
            if _contains_any(
                text,
                (
                    "teleport",
                    "travel",
                    "mapreveal",
                    "location",
                    "recoil",
                    "run",
                    "vortex",
                    "catacombs",
                ),
            ):
                return "quickwin-world-parts-teleport-travel-map"
            if _contains_any(
                text,
                (
                    "terminal",
                    "switch",
                    "door",
                    "hidden",
                    "unique",
                    "taken",
                    "hologram",
                    "mote",
                    "temporary",
                ),
            ):
                return "quickwin-world-parts-terminals-physical"
            if _contains_any(
                text,
                (
                    "baetyl",
                    "book",
                    "record",
                    "pet",
                    "kindrish",
                    "hindren",
                    "mural",
                    "reputation",
                    "altar",
                    "clam",
                ),
            ):
                return "quickwin-world-parts-social-books-records"
            return "quickwin-world-parts-physiology-misc"
        return "quickwin-world-systems-misc"

    if source == "XRL.World.Parts/Campfire.cs":
        return "review-campfire-cooking-nostrums"
    if source.startswith("XRL.Core/") or source.count("/") == 0:
        return "review-core-turn-save-systems"
    if source.startswith(("XRL.UI/", "Qud.UI/")):
        return "review-ui-screen-workflows"
    if source.startswith("XRL.World.Conversations.Parts/") or _contains_any(text, ("ritual", "conversation", "social")):
        return "review-conversation-ritual-social"
    if "sifrah" in text:
        return "review-sifrah-minigame-generated"
    if source.startswith("XRL.World.Parts.Skill/") or _contains_any(
        text,
        (
            "combat",
            "weapon",
            "missile",
            "longblades",
            "shortblades",
            "axe_",
            "rifle_",
            "shield_",
            "kick",
            "attack",
            "dismember",
            "cleave",
            "hobble",
            "slam",
        ),
    ):
        return "review-combat-skills-weapon"
    if source.startswith(("XRL.World.Parts.Mutation/", "XRL.World.Effects/")) or _contains_any(
        text,
        (
            "mutation",
            "psychic",
            "glimmer",
            "tonic",
            "disease",
            "illness",
            "poison",
            "infection",
            "stomach",
            "brainbrine",
            "glotrot",
            "ironshank",
            "monochrome",
        ),
    ):
        return "review-mutations-effects"
    if _contains_any(
        text,
        (
            "inventory",
            "container",
            "equip",
            "socket",
            "cell",
            "floating",
            "magnet",
            "power",
            "unequip",
        ),
    ):
        return "review-inventory-container-equipment"
    if _contains_any(
        text,
        (
            "item",
            "tinker",
            "cybernetic",
            "cybernetics",
            "datadisk",
            "disk",
            "spray",
            "tattoo",
            "crayon",
            "engrave",
            "recharge",
            "trade",
            "vendor",
            "mod",
            "naming",
            "repair",
            "disassembly",
            "supply",
            "food",
            "meal",
            "recipe",
        ),
    ):
        return "review-items-equipment-tools"
    if _contains_any(
        text,
        (
            "teleport",
            "travel",
            "flight",
            "flying",
            "wing",
            "terraintravel",
            "vortex",
            "navigate",
            "location",
            "zone",
        ),
    ):
        return "review-mobility-teleport-travel"
    if _contains_any(
        text,
        (
            "liquid",
            "firefighting",
            "tonic",
            "food",
            "neutron",
            "thinworld",
            "fabricate",
            "leveler",
        ),
    ):
        return "review-liquids-consumables-environment"
    if _contains_any(
        text,
        (
            "terminal",
            "switch",
            "physics",
            "enclosing",
            "stairs",
            "vehicle",
            "garbage",
            "thief",
            "elevator",
            "activate",
        ),
    ):
        return "review-terminals-physical-interactions"
    if _contains_any(
        text,
        (
            "chat",
            "conversation",
            "baetyl",
            "examiner",
            "spindle",
            "deed",
            "book",
            "curio",
            "mural",
            "quest",
            "secret",
            "kindrish",
        ),
    ):
        return "review-social-quest-books-records"
    if _contains_any(
        text,
        (
            "liquid",
            "physics",
            "fire",
            "gas",
            "phase",
            "terrain",
            "object",
            "enclosing",
            "engulf",
            "garbage",
            "baetyl",
            "clam",
            "altar",
            "socket",
            "gem",
        ),
    ):
        return "review-environment-liquids-world-objects"
    if source.startswith(
        (
            "XRL.World/",
            "XRL.World.Quests",
            "XRL.World.Zone",
            "XRL.World.Biomes/",
            "XRL.World.Capabilities/",
            "Qud.API/",
            "HistoryKit/",
        )
    ) or _contains_any(text, ("quest", "zone", "history", "factory", "biome", "journal")):
        return "review-quests-world-generation"
    if source.startswith("XRL.World.Parts/"):
        return "review-environment-liquids-world-objects"
    return "review-misc-system-routes"


def _counter_to_dict(counter: Counter[str]) -> dict[str, int]:
    return {key: counter[key] for key in sorted(counter)}


def _risk_for_batch(metadata: BatchMetadata, families: list[OwnerActionQueueEntry]) -> Risk:
    closure_statuses = Counter[str]()
    for family in families:
        closure_statuses.update(cast("dict[str, int]", family["closure_status_counts"]))

    callsites = sum(family["callsite_count"] for family in families)
    surfaces = {surface for family in families for surface in family["surface_counts"]}
    if metadata["lane"] == "needs_family_review_split_proof":
        if (
            closure_statuses.get("runtime_required", 0) > 0
            or callsites >= RISK_CALLSITE_THRESHOLD
            or len(surfaces) > 1
        ):
            return "high"
        return "medium"
    if callsites >= RISK_CALLSITE_THRESHOLD or len(surfaces) > 1:
        return "medium"
    return "low"


def _batch_payload(batch_id: str, families: list[OwnerActionQueueEntry]) -> dict[str, Any]:
    metadata = BATCH_METADATA[batch_id]
    family_statuses = Counter(family["family_closure_status"] for family in families)
    closure_statuses = Counter[str]()
    surfaces = Counter[str]()
    source_files = Counter(family["source_file"] for family in families)
    for family in families:
        closure_statuses.update(cast("dict[str, int]", family["closure_status_counts"]))
        surfaces.update(cast("dict[str, int]", family["surface_counts"]))

    return {
        "batch_id": batch_id,
        **metadata,
        "risk_level": _risk_for_batch(metadata, families),
        "counts": {
            "family_count": len(families),
            "callsite_count": sum(family["callsite_count"] for family in families),
            "text_argument_count": sum(family["text_argument_count"] for family in families),
            "source_file_count": len(source_files),
        },
        "family_status_mix": _counter_to_dict(family_statuses),
        "closure_status_mix": _counter_to_dict(closure_statuses),
        "surface_mix": _counter_to_dict(surfaces),
        "source_files": sorted(source_files),
        "producer_family_ids": [family["producer_family_id"] for family in families],
        "families": families,
    }


def build_payload(inventory_path: Path = DEFAULT_INVENTORY_PATH) -> dict[str, Any]:
    """Build the machine-checkable batch payload."""
    inventory = load_inventory(inventory_path)
    entries = owner_action_queue_entries(inventory)
    grouped: dict[str, list[OwnerActionQueueEntry]] = {}
    for entry in entries:
        grouped.setdefault(classify_entry(entry), []).append(entry)

    unknown_batch_ids = sorted(set(grouped) - set(BATCH_METADATA))
    if unknown_batch_ids:
        message = f"missing batch metadata: {', '.join(unknown_batch_ids)}"
        raise RuntimeError(message)

    assigned_ids = [family["producer_family_id"] for families in grouped.values() for family in families]
    duplicates = sorted(family_id for family_id, count in Counter(assigned_ids).items() if count > 1)
    missing = sorted({entry["producer_family_id"] for entry in entries} - set(assigned_ids))
    if duplicates or missing:
        message = f"classification did not assign each family exactly once: {duplicates=}, {missing=}"
        raise RuntimeError(message)

    batches = [
        _batch_payload(batch_id, sorted(families, key=lambda family: family["producer_family_id"]))
        for batch_id, families in sorted(grouped.items())
    ]
    totals = {
        "family_count": len(entries),
        "callsite_count": sum(entry["callsite_count"] for entry in entries),
        "text_argument_count": sum(entry["text_argument_count"] for entry in entries),
        "source_file_count": len({entry["source_file"] for entry in entries}),
        "batch_count": len(batches),
    }
    total_delta = {
        key: totals[key] - EXPECTED_QUEUE_TOTALS[key]
        for key in ("family_count", "callsite_count", "text_argument_count")
    }

    return {
        "schema_version": "1.0",
        "issue": 576,
        "queue_command": "uv run python scripts/static_producer_closure.py --format json --limit 0",
        "inventory": str(inventory_path.relative_to(REPO_ROOT)),
        "expected_queue_totals": EXPECTED_QUEUE_TOTALS,
        "queue_totals": totals,
        "expected_total_delta": total_delta,
        "total_reconciles_with_expected": all(delta == 0 for delta in total_delta.values()),
        "assignment_check": {
            "unique_family_count": len(set(assigned_ids)),
            "duplicate_family_ids": duplicates,
            "missing_family_ids": missing,
        },
        "narrative": (
            "This draft batches the remaining static producer owner queue into semantic lanes for later "
            "audited closure work. It does not register covered owner families or claim issue closure."
        ),
        "batches": batches,
    }


def _format_mix(mix: dict[str, int]) -> str:
    return ", ".join(f"{key}:{value}" for key, value in mix.items()) if mix else "-"


def render_markdown(payload: dict[str, Any]) -> str:
    """Render the human-facing planning report."""
    totals = cast("dict[str, Any]", payload["queue_totals"])
    expected_delta = cast("dict[str, int]", payload["expected_total_delta"])
    batches = cast("list[dict[str, Any]]", payload["batches"])
    quick_win_batches = [batch for batch in batches if batch["lane"] == "owner_patch_required_quick_win"]
    review_batches = [batch for batch in batches if batch["lane"] == "needs_family_review_split_proof"]

    lines = [
        "# Issue #576 Static Producer Owner Queue Semantic Batches",
        "",
        "## Issue / PR Narrative",
        "",
        (
            "Issue #576 asks for continued static producer owner-queue closure after the queue machinery landed. "
            "This draft does not close or register any owner families. It turns the current remaining queue into "
            "machine-checkable semantic batches so later autonomous `/goal` sessions can pick one coherent lane, "
            "audit every callsite shape, add focused tests, and only then register closure evidence."
        ),
        "",
        "## Current Queue Evidence",
        "",
        f"- Queue command: `{payload['queue_command']}`",
        f"- Inventory: `{payload['inventory']}`",
        (
            "- Current totals: "
            f"{totals['family_count']} families, {totals['callsite_count']} callsites, "
            f"{totals['text_argument_count']} text arguments across {totals['source_file_count']} source files"
        ),
        (
            "- Expected delta vs requested baseline: "
            f"families {expected_delta['family_count']:+}, "
            f"callsites {expected_delta['callsite_count']:+}, "
            f"text arguments {expected_delta['text_argument_count']:+}"
        ),
        (
            "- Full machine-checkable family assignment: "
            "`docs/reports/static-producer-owner-queue-semantic-batches.json`"
        ),
        "",
        "## Scope",
        "",
        "- Planning/documentation only.",
        "- No owner patches are implemented here.",
        "- No `COVERED_OWNER_FAMILIES` entries are added here.",
        "- Every queued producer family is assigned to exactly one semantic batch in the JSON artifact.",
        "",
        "## Quick-Win Owner Patch Lanes",
        "",
        _batch_table(quick_win_batches),
        "",
        "## Needs-Family-Review Split / Proof Lanes",
        "",
        _batch_table(review_batches),
        "",
        "## Execution Guidance",
        "",
        (
            "For any later closure goal, start from the batch's `producer_family_ids` in the JSON artifact. "
            "Before patching, enumerate callsites with:"
        ),
        "",
        "```bash",
        "jq -r '.callsites[]",
        '  | select(.producer_family_id=="<FAMILY_ID>")',
        "  | [.line,.target_surface,.closure_status,.expression]",
        "  | @tsv' docs/static-producer-inventory.json",
        "```",
        "",
        (
            "For `owner_patch_required_quick_win` batches, a later implementation should still prove owner route, "
            "production dictionary/translator behavior, direct-marker no-op, empty/no-op handling, queue-only or "
            "popup-only negatives when applicable, and L2G target resolution before adding closure overlay entries."
        ),
        "",
        (
            "For `needs_family_review_split_proof` batches, first split fixed leaves, owner-patch-required generated "
            "shapes, runtime-required shapes, and deferred policy-only rows. Do not register the mixed family id until "
            "every inventoried shape is covered or explicitly split into a smaller proof lane."
        ),
        "",
        "## Verification Commands",
        "",
        "```bash",
        "uv run python scripts/generate_static_producer_semantic_batches.py --check",
        "just static-producer-owner-queue",
        "just static-producer-check",
        "```",
        "",
    ]
    return "\n".join(lines)


def _batch_table(batches: list[dict[str, Any]]) -> str:
    lines = [
        "| Batch | Theme | Families / Callsites / Text Args | Status Mix | Closure Mix | Surfaces | Risk | Parallelization | Suggested Checks |",
        "| --- | --- | ---: | --- | --- | --- | --- | --- | --- |",
    ]
    for batch in batches:
        counts = cast("dict[str, int]", batch["counts"])
        checks = "<br>".join(
            f"<code>{html.escape(command).replace('|', '&#124;')}</code>"
            for command in cast("list[str]", batch["suggested_test_check_commands"])
        )
        lines.append(
            " | ".join(
                (
                    f"| `{batch['batch_id']}`",
                    str(batch["semantic_theme"]),
                    f"{counts['family_count']} / {counts['callsite_count']} / {counts['text_argument_count']}",
                    _format_mix(cast("dict[str, int]", batch["family_status_mix"])),
                    _format_mix(cast("dict[str, int]", batch["closure_status_mix"])),
                    _format_mix(cast("dict[str, int]", batch["surface_mix"])),
                    str(batch["risk_level"]),
                    str(batch["recommended_worktree_parallelization"]),
                    f"{checks} |",
                )
            )
        )
    return "\n".join(lines)


def write_outputs(payload: dict[str, Any], paths: OutputPaths) -> None:
    paths.json_path.parent.mkdir(parents=True, exist_ok=True)
    paths.markdown_path.parent.mkdir(parents=True, exist_ok=True)
    paths.json_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    paths.markdown_path.write_text(render_markdown(payload), encoding="utf-8")


def check_outputs(payload: dict[str, Any], paths: OutputPaths) -> list[str]:
    errors: list[str] = []
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_paths = OutputPaths(Path(temp_dir) / paths.json_path.name, Path(temp_dir) / paths.markdown_path.name)
        write_outputs(payload, temp_paths)
        for expected, actual in (
            (temp_paths.json_path, paths.json_path),
            (temp_paths.markdown_path, paths.markdown_path),
        ):
            if not actual.is_file():
                errors.append(f"missing generated output: {actual.relative_to(REPO_ROOT)}")
                continue
            if expected.read_text(encoding="utf-8") != actual.read_text(encoding="utf-8"):
                errors.append(f"stale generated output: {actual.relative_to(REPO_ROOT)}")
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, default=DEFAULT_INVENTORY_PATH)
    parser.add_argument("--json-output", type=Path, default=DEFAULT_JSON_OUTPUT)
    parser.add_argument("--markdown-output", type=Path, default=DEFAULT_MARKDOWN_OUTPUT)
    parser.add_argument("--check", action="store_true", help="verify generated artifacts are current")
    args = parser.parse_args(argv)

    paths = OutputPaths(cast("Path", args.json_output), cast("Path", args.markdown_output))
    payload = build_payload(cast("Path", args.inventory))

    if args.check:
        errors = check_outputs(payload, paths)
        if errors:
            sys.stderr.write("\n".join(errors) + "\n")
            return 1
    else:
        write_outputs(payload, paths)

    totals = payload["queue_totals"]
    sys.stdout.write(
        "static producer semantic batches: "
        f"{totals['family_count']} families, {totals['callsite_count']} callsites, "
        f"{totals['text_argument_count']} text arguments, {totals['batch_count']} batches\n"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
