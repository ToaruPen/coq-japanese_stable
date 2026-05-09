#!/usr/bin/env bash
# Launch Caves of Qud through Rosetta 2 on Apple Silicon Macs.
#
# This helper is intentionally self-contained so it can be shipped inside the
# Workshop/GitHub release ZIP. It does not install Rosetta automatically.

set -euo pipefail

GAME_BINARY="${HOME}/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ"
export SteamAppId="333640"
export SteamGameId="333640"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This launcher is only needed on macOS." >&2
  echo "On Windows or Linux, start Caves of Qud normally from Steam." >&2
  exit 1
fi

if [[ "$(uname -m)" == "arm64" ]]; then
  if ! /usr/bin/pgrep -q oahd 2>/dev/null &&
    ! arch -x86_64 /usr/bin/true 2>/dev/null; then
    echo "Rosetta 2 does not appear to be installed." >&2
    echo "Install it with:" >&2
    echo "  softwareupdate --install-rosetta --agree-to-license" >&2
    exit 1
  fi
fi

if [[ ! -f "${GAME_BINARY}" ]]; then
  echo "Caves of Qud was not found at the default Steam path:" >&2
  echo "  ${GAME_BINARY}" >&2
  echo "If your Steam library is in another location, edit this launcher and update GAME_BINARY." >&2
  exit 1
fi

if [[ ! -x "${GAME_BINARY}" ]]; then
  echo "Caves of Qud is not executable:" >&2
  echo "  ${GAME_BINARY}" >&2
  exit 1
fi

echo "Launching Caves of Qud through Rosetta 2..."
echo "Binary: ${GAME_BINARY}"

exec arch -x86_64 "${GAME_BINARY}"
