#!/usr/bin/env python3
"""Classify ProceduralText and VariableTemplate sites as display-only or generation-critical.

For each HistoricStringExpander call site:
- display-only  -> status = "needs_patch"
  Output goes to: EmitMessage, SetText, AddPlayerMessage, journal display,
  popup, Text property for UI, StringBuilder for display, etc.
- generation-critical -> status = "excluded"
  Output used for: world factory, entity property assignments, lookup keys,
  conditional checks, equality comparisons, faction names, zone names, etc.
"""
import json
import os
from collections import Counter

DECOMPILED = os.path.expanduser("~/Dev/coq-decompiled")


def read_file(rel_path):
    """Read decompiled source file."""
    path = os.path.join(DECOMPILED, rel_path)
    if not os.path.exists(path):
        return None
    with open(path) as f:
        return f.readlines()


# ── ProceduralText classification rules ─────────────────────────────
# Based on manual source analysis of all 242 call sites.

# Files where ALL ExpandString sites are generation-critical (excluded):
# These store results in entity properties, faction names, zone data, etc.
GENERATION_CRITICAL_FILES = {
    "HistoryKit/HistoricEvent.cs",           # Wrapper method used in generation pipeline
    "XRL.Annals/QudHistoryFactory.cs",       # World/history generation factory
    "XRL.Annals/QudHistoryHelpers.cs",       # History generation helpers
    "XRL.Annals/ImportedFoodorDrink.cs",     # Faction name generation
    "XRL.Annals/VillageProverb.cs",          # Village proverb stored as entity property
    "XRL.World.Encounters/DimensionManager.cs",  # Dimension name generation
    "XRL.Names/SettlementNames.cs",          # Settlement name generation
    "XRL.Names/NameStyle.cs",                # Name style generation
    "XRL.World.ZoneBuilders/VillageBase.cs", # Village entity properties
    "XRL.World.ZoneBuilders/VillageCodaBase.cs",  # Village coda entity props
    "XRL.World.ZoneBuilders/Village.cs",     # Village zone builder
    "XRL.World/RelicGenerator.cs",           # Relic entity properties
    "XRL.World.Capabilities/ItemNaming.cs",  # Item name generation
    "XRL.World/Faction.cs",                  # Faction properties
    "XRL.World/ZoneManager.cs",              # Zone generation
    "XRL/PsychicHunterSystem.cs",            # Psychic hunter entity generation
    "XRL.World/VillageDynamicQuestContext.cs",  # Quest context generation
    "XRL.World/DynamicQuestConversationHelper.cs",  # Quest conversation generation
    "XRL.World.Parts/SultanRegion.cs",       # Sultan region entity props
    "XRL.World.Parts/Body.cs",               # Body part generation
    "XRL.World.Parts/AnimateObject.cs",      # Animated object generation
    "XRL.World.Parts/AnimatorSpray.cs",      # Animator spray generation
    "XRL.World/GameObject.cs",               # Core game object property
    "XRL.World.Parts/GivesRep.cs",           # Reputation entry generation
    "XRL.World/Reputation.cs",               # Reputation system
    "XRL.World.Parts/RandomAltarBaetyl.cs",  # Altar quest generation
    "XRL.World.Capabilities/Wishing.cs",     # Wish processing (debug/gen)
    "XRL.Language/TextFilters.cs",           # Text filter utility (gen pipeline)
    "XRL.World.Parts/Gossip.cs",             # Gossip stored as entity property
    "XRL.World.Parts/GenerateFriendOrFoe.cs",     # Entity generation
    "XRL.World.Parts/GenerateFriendOrFoe_HEB.cs", # Entity generation
}

# Files/basenames that match generation-critical dynamic quest patterns:
GENERATION_CRITICAL_PATTERNS = [
    "DynamicQuestTemplate",   # All dynamic quest template fabricators
    "DynamicQuestManager",    # All dynamic quest managers
]

# Files where ALL ExpandString sites are display-only (needs_patch):
DISPLAY_ONLY_FILES = {
    "HistoryTestView.cs",                    # Test/debug UI display
    "Qud.API/JournalAPI.cs",                 # Journal accomplishment display text
    "XRL.UI/StatusScreen.cs",                # Status screen accomplishment display
    "XRL.World.Conversations.Parts/WaterRitualBuySecret.cs",  # Popup.Show
    "XRL.World.Effects/Rebuked.cs",          # JournalAPI.AddAccomplishment
    "XRL.World.Parts/Campfire.cs",           # All Popup.Show for cooking messages
    "XRL.World.Parts/Cookbook.cs",            # Render.DisplayName for cookbook
    "XRL.World.Parts/DynamicQuestSignpostConversation.cs",  # Conversation text
    "XRL.World.Parts/EaterCryptPlaque.cs",   # Plaque display text
    "XRL.World.Parts/EaterUrn.cs",           # Urn display text (StringFormat.ClipText)
    "XRL.World.Parts/LocateRelicQuestManager.cs",  # AddAccomplishment display
    "XRL.World.Parts/MerchantRevealer.cs",   # ShowBook display
    "XRL.World.Parts/OpeningStory.cs",       # AddAccomplishment display
    "XRL.World.Parts/RachelsTombstone.cs",   # Tombstone display
    "XRL.World.Parts/TempleDedicationPlaque.cs",  # Plaque inscription display
    "XRL.World.Parts/Tombstone.cs",          # Tombstone display
    "XRL.World.Parts/VillageSurface.cs",     # AddAccomplishment display
    "XRL.World.Parts/VillageTerrain.cs",     # Description.Short display
    "XRL.World.Skills.Cooking/CookingRecipe.cs",  # Recipe display name
    "XRL.World.Parts/BroadcastPowerReceiver.cs",  # UI appendix display
}


def classify_procedural_text(site):
    """Classify a ProceduralText site.

    Returns: (new_status, reason) or (None, reason) if can't determine.
    """
    f = site["file"]
    basename = os.path.basename(f)

    # Check exact file match for generation-critical
    if f in GENERATION_CRITICAL_FILES:
        return "excluded", f"Generation-critical file: {basename}"

    # Check pattern match for generation-critical
    for pat in GENERATION_CRITICAL_PATTERNS:
        if pat in basename:
            return "excluded", f"Generation-critical pattern: {pat}"

    # Check exact file match for display-only
    if f in DISPLAY_ONLY_FILES:
        return "needs_patch", f"Display-only file: {basename}"

    # VillageCoda in ZoneBuilders - generation (entity properties via ApplyEvent)
    if basename == "VillageCoda.cs" and "ZoneBuilders" in f:
        return "excluded", "Village coda zone builder (entity generation)"

    return None, f"Unclassified: {f}"


# ── VariableTemplate classification rules ───────────────────────────
# Based on manual source analysis of all 39 needs_translation sites.

def classify_variable_template(site):
    """Classify a VariableTemplate site (needs_translation only).

    Returns: (new_status, reason) or (None, reason) if can't determine.
    """
    f = site["file"]
    basename = os.path.basename(f)
    line = site["line"]

    # GameText.cs - all StartReplace methods are display text processing
    if basename == "GameText.cs":
        return "needs_patch", "GameText display text processing"

    # JournalAPI.cs - all sites are display
    if basename == "JournalAPI.cs":
        return "needs_patch", "Journal display text"

    # HistoryAPI.cs - ExpandVillageText for display
    if basename == "HistoryAPI.cs":
        return "needs_patch", "HistoryAPI village text display"

    # Accomplishment parts - display text via AddAccomplishment
    if "Accomplishment" in basename:
        return "needs_patch", "Accomplishment display text"

    # Preacher - EmitMessage/ParticleText
    if basename == "Preacher.cs":
        return "needs_patch", "Preacher speech display (EmitMessage)"

    # KithAndKin - conversation PrepareTextEvent
    if "KithAndKin" in basename:
        return "needs_patch", "Conversation display text"

    # WaterRitual - Popup.Show
    if "WaterRitual" in basename:
        return "needs_patch", "Water ritual display (Popup.Show)"

    # CreatureRegionSpice - sets DisplayName (a display property)
    # While it modifies the object during building, DisplayName is a visual
    # property that needs translation for display.
    if basename == "CreatureRegionSpice.cs":
        return "needs_patch", "Creature display name and description"

    # PhotosyntheticSkin - EmitMessage
    if basename == "PhotosyntheticSkin.cs":
        return "needs_patch", "Mutation display message (EmitMessage)"

    # BlowAwayGas - EmitMessage
    if basename == "BlowAwayGas.cs":
        return "needs_patch", "Gas effect display (EmitMessage)"

    # DesalinationPellet - EmitMessage
    if basename == "DesalinationPellet.cs":
        return "needs_patch", "Desalination pellet display (EmitMessage)"

    # Interactable - Popup.Show / EmitMessage
    if basename == "Interactable.cs":
        return "needs_patch", "Interactable display (Popup/EmitMessage)"

    # InteriorBlockEntrance - Popup.Show
    if basename == "InteriorBlockEntrance.cs":
        return "needs_patch", "Interior block entrance display (Popup.Show)"

    # TimeCubeProtection - EmitMessage
    if basename == "TimeCubeProtection.cs":
        return "needs_patch", "Time cube protection display (EmitMessage)"

    # BroadcastPowerReceiver - UI appendix display
    if basename == "BroadcastPowerReceiver.cs":
        return "needs_patch", "Broadcast power receiver display"

    # ReclamationSystem - Popup.Show
    if basename == "ReclamationSystem.cs":
        return "needs_patch", "Reclamation system display (Popup.Show)"

    # XRLGame.cs - quest accomplishment text
    if basename == "XRLGame.cs":
        return "needs_patch", "Quest accomplishment display"

    # VillageCoda.cs in ZoneBuilders
    if basename == "VillageCoda.cs" and "ZoneBuilders" in f:
        # L2295 -> text goes to entity.ApplyEvent (generation)
        # L2476 -> Description.Short (display)
        if line == 2295:
            return "excluded", "VillageCoda gospel event (entity property via ApplyEvent)"
        if line == 2476:
            return "needs_patch", "VillageCoda shrine description display"
        return None, f"VillageCoda ambiguous at line {line}"

    return None, f"Unclassified VT: {f}:{line}"


def main():
    with open("docs/candidate-inventory.json") as f:
        data = json.load(f)

    sites = data["sites"]

    # ── Classify ProceduralText sites ────────────────────────────
    proc = [s for s in sites if s["type"] == "ProceduralText"]
    print(f"Classifying {len(proc)} ProceduralText sites...")

    proc_results = Counter()
    proc_details = []
    proc_changes = 0

    for s in proc:
        old_status = s["status"]
        new_status, reason = classify_procedural_text(s)
        if new_status and new_status != old_status:
            s["status"] = new_status
            proc_changes += 1
        proc_results[new_status or "unchanged"] += 1
        proc_details.append((s["id"], new_status or "unchanged", reason, old_status))

    print(f"  Results: {dict(proc_results)}")
    print(f"  Changes: {proc_changes}")
    print()

    # ── Classify VariableTemplate sites (needs_translation) ──────
    vt = [s for s in sites if s["type"] == "VariableTemplate" and s["status"] == "needs_translation"]
    print(f"Classifying {len(vt)} VariableTemplate sites (needs_translation)...")

    vt_results = Counter()
    vt_details = []
    vt_changes = 0

    for s in vt:
        old_status = s["status"]
        new_status, reason = classify_variable_template(s)
        if new_status and new_status != old_status:
            s["status"] = new_status
            vt_changes += 1
        vt_results[new_status or "unchanged"] += 1
        vt_details.append((s["id"], new_status or "unchanged", reason, old_status))

    print(f"  Results: {dict(vt_results)}")
    print(f"  Changes: {vt_changes}")
    print()

    # ── Print classification details ─────────────────────────────
    print("=" * 72)
    print("ProceduralText classifications:")
    print("=" * 72)
    for site_id, status, reason, old in sorted(proc_details, key=lambda x: (x[1], x[0])):
        changed = " [CHANGED]" if status != old and status != "unchanged" else ""
        print(f"  [{status:12s}] {site_id}{changed}")
        print(f"                {reason}")

    print()
    print("=" * 72)
    print("VariableTemplate classifications:")
    print("=" * 72)
    for site_id, status, reason, old in sorted(vt_details, key=lambda x: (x[1], x[0])):
        changed = " [CHANGED]" if status != old and status != "unchanged" else ""
        print(f"  [{status:12s}] {site_id}{changed}")
        print(f"                {reason}")

    # Check for any unclassified
    unclassified = [d for d in proc_details + vt_details if d[1] == "unchanged" or d[1] is None]
    if unclassified:
        print()
        print(f"WARNING: {len(unclassified)} sites could not be classified:")
        for site_id, status, reason, old in unclassified:
            print(f"  {site_id}: {reason}")

    # ── Write back ───────────────────────────────────────────────
    with open("docs/candidate-inventory.json", "w") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")

    # Final stats
    status_counts = Counter(s["status"] for s in sites)
    print()
    print(f"Total changes: {proc_changes + vt_changes}")
    print(f"  ProceduralText: {proc_changes} sites changed")
    print(f"  VariableTemplate: {vt_changes} sites changed")
    print()
    print("New overall status distribution:")
    for status, count in status_counts.most_common():
        print(f"  {status}: {count}")


if __name__ == "__main__":
    main()
