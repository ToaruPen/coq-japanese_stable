#!/usr/bin/env bash
# Launch Caves of Qud through Rosetta 2 on Apple Silicon Macs.
#
# This helper is intentionally self-contained so it can be shipped inside the
# Workshop/GitHub release ZIP. Player-facing failures use macOS dialogs so the
# launcher can be used by double-clicking it from Finder.

set -euo pipefail

LAUNCHER_TITLE="QudJP Rosetta Launcher"
DEFAULT_GAME_BINARY="${HOME}/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ"

show_message() {
  local title="$1"
  local message="$2"

  if command -v osascript >/dev/null 2>&1; then
    osascript - "${title}" "${message}" <<'APPLESCRIPT' >/dev/null 2>&1 || true
on run argv
  display dialog (item 2 of argv) buttons {"OK"} default button "OK" with title (item 1 of argv)
end run
APPLESCRIPT
  fi
  printf '%s\n%s\n' "${title}" "${message}" >&2
}

confirm_dialog() {
  local title="$1"
  local message="$2"
  local action="$3"

  if ! command -v osascript >/dev/null 2>&1; then
    return 1
  fi

  osascript - "${title}" "${message}" "${action}" <<'APPLESCRIPT' >/dev/null
on run argv
  set actionName to item 3 of argv
  set dialogResult to display dialog (item 2 of argv) buttons {"Cancel", actionName} default button actionName cancel button "Cancel" with title (item 1 of argv)
  return button returned of dialogResult
end run
APPLESCRIPT
}

install_rosetta() {
  if ! confirm_dialog \
    "${LAUNCHER_TITLE}" \
    "Rosetta 2 is required to launch Caves of Qud this way on Apple Silicon. Install Rosetta 2 now?" \
    "Install Rosetta 2"; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Rosetta 2 is not installed. The game was not launched."
    exit 1
  fi

  show_message \
    "${LAUNCHER_TITLE}" \
    "macOS will install Rosetta 2 now. This may take a moment."

  if ! /usr/sbin/softwareupdate --install-rosetta --agree-to-license; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Rosetta 2 installation did not complete. Please try this launcher again after installing Rosetta 2."
    exit 1
  fi
}

choose_game_binary() {
  if ! command -v osascript >/dev/null 2>&1; then
    return 1
  fi

  local selected_app
  selected_app="$(
    osascript <<'APPLESCRIPT'
set promptText to "Caves of Qud was not found in the default Steam library. Please select CoQ.app from your Steam library."
set selectedFile to choose file with prompt promptText
return POSIX path of selectedFile
APPLESCRIPT
  )" || return 1

  if [[ "${selected_app}" == *.app/ ]]; then
    printf '%sContents/MacOS/CoQ\n' "${selected_app}"
    return 0
  fi

  if [[ "${selected_app}" == *"/CoQ.app" ]]; then
    printf '%s/Contents/MacOS/CoQ\n' "${selected_app}"
    return 0
  fi

  printf '%s\n' "${selected_app}"
}

resolve_game_binary() {
  if [[ -x "${DEFAULT_GAME_BINARY}" ]]; then
    printf '%s\n' "${DEFAULT_GAME_BINARY}"
    return 0
  fi

  if [[ -f "${DEFAULT_GAME_BINARY}" ]]; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Caves of Qud was found in the default Steam library, but it is not executable. Please verify the game installation in Steam and try again."
    exit 1
  fi

  show_message \
    "${LAUNCHER_TITLE}" \
    "Caves of Qud was not found in the default Steam library. In the next window, select CoQ.app from your Steam library."

  local chosen_binary
  if ! chosen_binary="$(choose_game_binary)"; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Caves of Qud could not be launched. Please select CoQ.app from your Steam library and try again."
    exit 1
  fi

  if [[ ! -x "${chosen_binary}" ]]; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Caves of Qud could not be launched. Please select CoQ.app from your Steam library and try again."
    exit 1
  fi

  printf '%s\n' "${chosen_binary}"
}

is_apple_silicon() {
  [[ "$(uname -m)" == "arm64" ]]
}

rosetta_available() {
  /usr/bin/pgrep -q oahd 2>/dev/null || arch -x86_64 /usr/bin/true 2>/dev/null
}

if [[ "$(uname -s)" != "Darwin" ]]; then
  show_message \
    "${LAUNCHER_TITLE}" \
    "This launcher is only needed on macOS. On Windows or Linux, start Caves of Qud normally from Steam."
  exit 1
fi

if is_apple_silicon && ! rosetta_available; then
  install_rosetta
fi

GAME_BINARY="$(resolve_game_binary)"

printf '%s\n%s\n' \
  "${LAUNCHER_TITLE}" \
  "Launching Caves of Qud through Rosetta 2." >&2

exec arch -x86_64 "${GAME_BINARY}"
