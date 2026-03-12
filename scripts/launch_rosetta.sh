#!/usr/bin/env bash
# =============================================================================
# launch_rosetta.sh — Launch Caves of Qud under Rosetta 2 (x86_64 emulation)
# =============================================================================
#
# WHY THIS SCRIPT EXISTS:
#   On Apple Silicon (M1/M2/M3/M4) Macs, the game's bundled Harmony library
#   (0Harmony.dll v2.2.2.0) crashes when applying patches natively on ARM64.
#
#   Root cause: Harmony uses MonoMod which calls mprotect() on MAP_JIT memory
#   pages. Apple Silicon enforces a Write XOR Execute (W^X) security policy
#   that blocks this with EACCES, causing all patches to fail silently and
#   ultimately crashing the mod loader.
#
#   Running under Rosetta 2 (x86_64 emulation) completely avoids this issue
#   because x86_64 code paths do not use MAP_JIT pages.
#
# WORKAROUND STATUS:
#   This is a temporary workaround until a proper mod-side fix is implemented
#   (e.g. upgrading to a Harmony version that handles Apple Silicon W^X, or
#   using a memory patching approach compatible with the platform ABI).
#
# USAGE:
#   ./scripts/launch_rosetta.sh
#   ./scripts/launch_rosetta.sh --help
#
# CONFIRMED BEHAVIOUR:
#   - ARM64 native:  0 patches applied, mprotect EACCES crash
#   - Rosetta x86_64: 14 patches applied, Bootstrap complete, mod works
#
# NOTE: Steam's "Open using Rosetta" checkbox and `arch -x86_64 %command%`
#   launch option do NOT reliably work. Launching the binary directly from
#   the terminal (as this script does) is the only confirmed working method.
#   The game does not require Steam to be running for this to work.
# =============================================================================

set -euo pipefail

GAME_BINARY="${HOME}/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ"

# ---------------------------------------------------------------------------
# --help
# ---------------------------------------------------------------------------
if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  sed -n '3,34p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
fi

# ---------------------------------------------------------------------------
# Platform check — macOS only
# ---------------------------------------------------------------------------
if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "ERROR: This script is for macOS only (Rosetta 2 is Apple-specific)." >&2
  echo "       On Linux/Windows, launch the game normally via Steam." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Rosetta availability check (Apple Silicon only)
# ---------------------------------------------------------------------------
if [[ "$(uname -m)" == "arm64" ]]; then
  if ! /usr/bin/pgrep -q oahd 2>/dev/null &&
    ! arch -x86_64 /usr/bin/true 2>/dev/null; then
    echo "ERROR: Rosetta 2 does not appear to be installed." >&2
    echo "       Install it by running:" >&2
    echo "           softwareupdate --install-rosetta --agree-to-license" >&2
    exit 1
  fi
fi

# ---------------------------------------------------------------------------
# Game binary check
# ---------------------------------------------------------------------------
if [[ ! -f "${GAME_BINARY}" ]]; then
  echo "ERROR: Game binary not found at:" >&2
  echo "       ${GAME_BINARY}" >&2
  echo "" >&2
  echo "Make sure Caves of Qud is installed via Steam in the default library." >&2
  exit 1
fi

if [[ ! -x "${GAME_BINARY}" ]]; then
  echo "ERROR: Game binary is not executable:" >&2
  echo "       ${GAME_BINARY}" >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Launch
# ---------------------------------------------------------------------------
echo "Launching Caves of Qud via Rosetta (x86_64)..."
echo "Binary: ${GAME_BINARY}"

exec arch -x86_64 "${GAME_BINARY}"
