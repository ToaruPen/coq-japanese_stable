#!/usr/bin/env bash
# Decompile text-pipeline-related classes from Assembly-CSharp.dll into docs/ilspy-raw/.
# This is a LOCAL-ONLY reference — the output directory is gitignored.
#
# Usage:
#   scripts/decompile_game_dll.sh            # decompile default class list
#   scripts/decompile_game_dll.sh --all      # decompile ALL classes (slow, large)
#   scripts/decompile_game_dll.sh --list     # print class list without decompiling
#
# Prerequisites:
#   - ilspycmd installed: dotnet tool install -g ilspycmd
#   - Game DLL accessible at the default Steam path (macOS)
#   - DOTNET_ROLL_FORWARD=LatestMajor (set automatically by this script)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="${COQ_DECOMPILED_DIR:-$HOME/Dev/coq-decompiled}"

# Game DLL path (macOS Steam default)
DLL_PATH="$HOME/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll"

# ilspycmd path
ILSPY="${ILSPY_CMD:-$HOME/.dotnet/tools/ilspycmd}"

export DOTNET_ROLL_FORWARD=LatestMajor

# ---------------------------------------------------------------------------
# Text pipeline classes to decompile.
# Organized by pipeline stage.
# Add classes here as new translation routes are discovered.
# ---------------------------------------------------------------------------
TEXT_PIPELINE_CLASSES=(
  # Stage 0: XML/Asset loading
  "XRL.World.Parts.Description"

  # Stage 1: Text assembly / producers
  "XRL.World.Statistic"
  "XRL.World.Effect"
  "XRL.World.GameObject"
  # XRL.World.IComponent — generic type, ilspycmd cannot resolve. Use existing XRL_IComponent_1.cs
  "XRL.GameText"
  "XRL.Language.Grammar"
  "HistoryKit.HistoricStringExpander"

  # Stage 2: Display name composition
  "XRL.World.GetDisplayNameEvent"
  "XRL.World.DescriptionBuilder"

  # Stage 3: UI screens
  "Qud.UI.CharacterStatusScreen"
  "Qud.UI.SkillsAndPowersStatusScreen"
  "Qud.UI.FactionsStatusScreen"
  "Qud.UI.AbilityBar"
  "XRL.UI.InventoryScreen"
  "XRL.UI.EquipmentScreen"
  "Qud.UI.PickGameObjectScreen"
  "Qud.UI.TradeScreen"
  "XRL.UI.ConversationUI"

  # Stage 4: Tooltip / Look
  "XRL.UI.Look"
  "ModelShark.TooltipTrigger"

  # Stage 5: UI rendering sinks
  "XRL.UI.UITextSkin"

  # Stage 6: Message log
  "XRL.Messages.MessageQueue"
  "XRL.UI.Popup"

  # Conversation
  "XRL.World.Conversations.IConversationElement"

  # Sidebar / Menu
  "XRL.UI.Sidebar"
  "Qud.UI.MainMenu"
  "Qud.UI.OptionsScreen"
  # QudMenu — no dedicated class found; QudMenuBottomContext covers the text sink
  "Qud.UI.QudMenuBottomContext"

  # Character generation
  "XRL.CharacterBuilds.Qud.QudChooseStartingLocationModule"
  "XRL.CharacterBuilds.Qud.QudSubtypeModule"
  "XRL.CharacterBuilds.Qud.QudAttributesModule"

  # World mods / equipment
  "XRL.World.Parts.ModImprovedBlock"
  "XRL.World.Parts.ModFlaming"
  "XRL.World.Parts.ModFreezing"
  "XRL.World.Parts.ModElectrified"
  "XRL.World.Parts.ModMasterwork"

  # Death / journal
  # Death / journal
  "Qud.API.JournalAPI"
)

# ---------------------------------------------------------------------------

die() { echo "ERROR: $1" >&2; exit 1; }

[[ -f "$DLL_PATH" ]] || die "Game DLL not found: $DLL_PATH"
[[ -x "$ILSPY" ]] || die "ilspycmd not found: $ILSPY"

mkdir -p "$OUTPUT_DIR"

decompile_class() {
  local fqn="$1"
  # Sanitize filename: replace dots and backticks
  local safe_name
  safe_name="$(echo "$fqn" | sed 's/\\./_/g; s/`/_/g')"
  local out_file="$OUTPUT_DIR/${safe_name}.cs"
  local err_file="$OUTPUT_DIR/${safe_name}.err.txt"

  if "$ILSPY" -t "$fqn" "$DLL_PATH" > "$out_file" 2> "$err_file"; then
    local lines
    lines="$(wc -l < "$out_file" | tr -d ' ')"
    if [[ "$lines" -gt 0 ]]; then
      echo "  OK  $fqn ($lines lines)"
      rm -f "$err_file"
      return 0
    else
      echo "  EMPTY  $fqn (type not found?)"
      rm -f "$out_file"
      return 1
    fi
  else
    echo "  FAIL  $fqn (see $err_file)"
    rm -f "$out_file"
    return 1
  fi
}

decompile_all() {
  echo "Decompiling ALL classes to $OUTPUT_DIR (this may take several minutes)..."
  "$ILSPY" -p -o "$OUTPUT_DIR" "$DLL_PATH"
  echo "Done. Output: $OUTPUT_DIR"
}

list_classes() {
  echo "Classes to decompile (${#TEXT_PIPELINE_CLASSES[@]}):"
  printf '  %s\n' "${TEXT_PIPELINE_CLASSES[@]}"
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

case "${1:-}" in
  --all)
    decompile_all
    ;;
  --list)
    list_classes
    ;;
  *)
    echo "Decompiling ${#TEXT_PIPELINE_CLASSES[@]} text pipeline classes to $OUTPUT_DIR..."
    echo ""
    ok=0
    fail=0
    for cls in "${TEXT_PIPELINE_CLASSES[@]}"; do
      if decompile_class "$cls"; then
        ((ok++))
      else
        ((fail++))
      fi
    done
    echo ""
    echo "Done: $ok succeeded, $fail failed. Output: $OUTPUT_DIR"
    ;;
esac
