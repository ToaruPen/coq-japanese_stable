#!/usr/bin/env bash
# =============================================================================
# diagnose_conversation.sh — Build, deploy, launch, talk to NPC, collect diag
# =============================================================================
#
# Automates the conversation diagnostic cycle:
#   1. Clean + full build
#   2. Deploy mod via sync_mod.py
#   3. Launch game via Rosetta
#   4. Wait for main menu → Continue → save load
#   5. Talk to adjacent NPC (c + right)
#   6. Collect diagnostic log output
#   7. Kill game
#
# Prerequisites:
#   - Hammerspoon running with IPC enabled (require("hs.ipc") in init.lua)
#   - Save game with player adjacent to a talkable NPC (e.g. Mehmet in Joppa)
#   - ConversationDiagnosticPatch.cs present in src/Patches/
#
# USAGE:
#   ./scripts/diagnose_conversation.sh
#   ./scripts/diagnose_conversation.sh --skip-build   # Skip build+deploy
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
PLAYER_LOG="$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log"
GAME_BINARY="$HOME/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ"
HS=/Applications/Hammerspoon.app/Contents/Frameworks/hs/hs
DIAG_TAG="ConversationDiag"

cd "$PROJECT_ROOT"

# ---------------------------------------------------------------------------
# Parse args
# ---------------------------------------------------------------------------
SKIP_BUILD=false
if [[ "${1:-}" == "--skip-build" ]]; then
  SKIP_BUILD=true
fi

# ---------------------------------------------------------------------------
# Helper: send key via Hammerspoon newKeyEvent
# ---------------------------------------------------------------------------
send_key() {
  local key=$1
  local delay=${2:-300000}  # default 300ms pre-delay
  "$HS" -c "
local app = hs.application.find('CavesOfQud')
if app then app:activate() end
hs.timer.usleep($delay)
hs.eventtap.event.newKeyEvent(hs.keycodes.map['$key'], true):post()
hs.timer.usleep(50000)
hs.eventtap.event.newKeyEvent(hs.keycodes.map['$key'], false):post()
" 2>/dev/null
}

# ---------------------------------------------------------------------------
# Helper: wait for pattern in log
# ---------------------------------------------------------------------------
wait_for_log() {
  local pattern=$1
  local timeout=${2:-120}
  local interval=${3:-2}
  for _ in $(seq 1 $((timeout / interval))); do
    if grep -qE "$pattern" "$PLAYER_LOG" 2>/dev/null; then
      return 0
    fi
    sleep "$interval"
  done
  return 1
}

# ---------------------------------------------------------------------------
# Step 1: Build & Deploy
# ---------------------------------------------------------------------------
if [[ "$SKIP_BUILD" == "false" ]]; then
  echo "=== Step 1: Build & Deploy ==="
  dotnet clean Mods/QudJP/Assemblies/QudJP.csproj >/dev/null 2>&1
  dotnet build Mods/QudJP/Assemblies/QudJP.csproj --no-incremental 2>&1 | tail -3
  python3 scripts/sync_mod.py 2>&1 | head -3
  echo ""
else
  echo "=== Step 1: Skipped (--skip-build) ==="
fi

# ---------------------------------------------------------------------------
# Step 2: Kill existing game
# ---------------------------------------------------------------------------
echo "=== Step 2: Kill existing game ==="
pkill -f CoQ 2>/dev/null || true
sleep 2

# ---------------------------------------------------------------------------
# Step 3: Launch game via Rosetta
# ---------------------------------------------------------------------------
echo "=== Step 3: Launch game ==="
arch -x86_64 "$GAME_BINARY" &>/dev/null &
GAME_PID=$!
echo "  PID: $GAME_PID"

# ---------------------------------------------------------------------------
# Step 4: Wait for main menu
# ---------------------------------------------------------------------------
echo "=== Step 4: Wait for main menu ==="
if ! wait_for_log "Starting Game..." 120 2; then
  echo "  TIMEOUT waiting for main menu"
  kill "$GAME_PID" 2>/dev/null
  exit 1
fi
echo "  Main menu ready"
sleep 2

# ---------------------------------------------------------------------------
# Step 5: Navigate to Continue
# ---------------------------------------------------------------------------
echo "=== Step 5: Continue → Load save ==="
send_key down 500000
sleep 0.5
send_key space 300000

# Wait for save to load
if ! wait_for_log "AbilityBar|LVL:|満腹" 60 2; then
  echo "  First Space didn't work, retrying..."
  send_key space 300000
  if ! wait_for_log "AbilityBar|LVL:|満腹" 30 2; then
    echo "  TIMEOUT loading save"
    kill "$GAME_PID" 2>/dev/null
    exit 1
  fi
fi
echo "  Save loaded"
sleep 2

# ---------------------------------------------------------------------------
# Step 6: Talk to NPC
# ---------------------------------------------------------------------------
echo "=== Step 6: Talk to NPC ==="
LOG_BEFORE=$(wc -l < "$PLAYER_LOG")  # Capture baseline to filter newly appended lines

# Press 'c' for Talk, wait, then right arrow
send_key c 500000
sleep 1.5
send_key right 200000

echo "  Sent: c + right"
sleep 5

# ---------------------------------------------------------------------------
# Step 7: Collect diagnostic output
# ---------------------------------------------------------------------------
echo ""
echo "=== Diagnostic Results ==="
echo ""

DIAG_LINES=$(tail -n +"$((LOG_BEFORE + 1))" "$PLAYER_LOG" 2>/dev/null | grep "$DIAG_TAG" || true)
if [[ -z "$DIAG_LINES" ]]; then
  echo "  NO DIAGNOSTIC OUTPUT (patch may not have loaded)"
  echo ""
  echo "  Checking for Talk prompt:"
  grep "Talk.*direction" "$PLAYER_LOG" | tail -3 || true
else
  echo "$DIAG_LINES"
fi

echo ""
echo "=== Recent Talk-related log ==="
grep -E "Talk|ConversationDiag|HaveConversation|ShowConversation" "$PLAYER_LOG" | tail -20 || true

# ---------------------------------------------------------------------------
# Step 8: Kill game
# ---------------------------------------------------------------------------
echo ""
echo "=== Step 8: Cleanup ==="
kill "$GAME_PID" 2>/dev/null || true
echo "  Game stopped"
