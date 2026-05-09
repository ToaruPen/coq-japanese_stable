#!/usr/bin/env bash
# Launch Caves of Qud through Rosetta 2 on Apple Silicon Macs.
#
# This helper is intentionally self-contained so it can be shipped inside the
# Workshop/GitHub release ZIP. Player-facing failures use macOS dialogs so the
# launcher can be used by double-clicking it from Finder.

set -euo pipefail

LAUNCHER_TITLE="QudJP Rosetta 起動"
DEFAULT_GAME_BINARY="${HOME}/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd -P)"
export SteamAppId="333640"
export SteamGameId="333640"

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
  set dialogResult to display dialog (item 2 of argv) buttons {"キャンセル", actionName} default button actionName cancel button "キャンセル" with title (item 1 of argv)
  return button returned of dialogResult
end run
APPLESCRIPT
}

install_rosetta() {
  if ! confirm_dialog \
    "${LAUNCHER_TITLE}" \
    "Apple Silicon MacでCaves of Qudをこの方法で起動するにはRosetta 2が必要です。今インストールしますか？" \
    "Rosetta 2をインストール"; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Rosetta 2がインストールされていないため、Caves of Qudを起動しませんでした。"
    exit 1
  fi

  show_message \
    "${LAUNCHER_TITLE}" \
    "macOSがRosetta 2をインストールします。完了まで少し時間がかかる場合があります。"

  if ! /usr/sbin/softwareupdate --install-rosetta --agree-to-license; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Rosetta 2のインストールが完了しませんでした。時間をおいてもう一度このファイルを開くか、macOSのソフトウェアアップデートを確認してください。"
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
set promptText to "Caves of Qudが既定のSteamライブラリ、およびこの起動ファイルの場所から推定したSteamライブラリに見つかりませんでした。Steamライブラリ内の steamapps/common/Caves of Qud/CoQ.app を選択してください。"
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

infer_game_binary_from_launcher_location() {
  case "${SCRIPT_DIR}" in
    */steamapps/workshop/content/333640/3718988020)
      local steamapps_dir="${SCRIPT_DIR%/workshop/content/333640/3718988020}"
      printf '%s/common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ\n' "${steamapps_dir}"
      return 0
      ;;
    */CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP)
      local app_dir="${SCRIPT_DIR%/Contents/Resources/Data/StreamingAssets/Mods/QudJP}"
      printf '%s/Contents/MacOS/CoQ\n' "${app_dir}"
      return 0
      ;;
  esac

  return 1
}

show_not_executable_message() {
  local source_description="$1"

  show_message \
    "${LAUNCHER_TITLE}" \
    "${source_description}にCaves of Qudは見つかりましたが、実行できる状態ではありません。Steamを開き、ライブラリ > Caves of Qud > プロパティ > インストール済みファイル > ゲームファイルの整合性を確認 を実行してから、もう一度このファイルを開いてください。"
}

canonicalize_binary_path() {
  local binary_path="$1"
  local binary_dir
  local binary_name
  local link_target
  local symlink_depth=0

  binary_dir="$(dirname "${binary_path}")"
  binary_name="$(basename "${binary_path}")"

  if [[ ! -d "${binary_dir}" ]]; then
    return 1
  fi

  binary_dir="$(cd -P "${binary_dir}" && pwd -P)" || return 1
  binary_path="${binary_dir}/${binary_name}"

  while [[ -L "${binary_path}" ]]; do
    if ((symlink_depth >= 40)); then
      return 1
    fi
    symlink_depth=$((symlink_depth + 1))

    link_target="$(readlink "${binary_path}")" || return 1
    if [[ "${link_target}" == /* ]]; then
      binary_path="${link_target}"
    else
      binary_path="$(dirname "${binary_path}")/${link_target}"
    fi

    binary_dir="$(cd -P "$(dirname "${binary_path}")" && pwd -P)" || return 1
    binary_path="${binary_dir}/$(basename "${binary_path}")"
  done

  printf '%s\n' "${binary_path}"
}

resolve_game_binary() {
  if [[ -x "${DEFAULT_GAME_BINARY}" ]]; then
    printf '%s\n' "${DEFAULT_GAME_BINARY}"
    return 0
  fi

  if [[ -f "${DEFAULT_GAME_BINARY}" ]]; then
    show_not_executable_message "既定のSteamライブラリ"
    exit 1
  fi

  local inferred_binary
  if inferred_binary="$(infer_game_binary_from_launcher_location)"; then
    if [[ -x "${inferred_binary}" ]]; then
      printf '%s\n' "${inferred_binary}"
      return 0
    fi

    if [[ -f "${inferred_binary}" ]]; then
      show_not_executable_message "この起動ファイルの場所から推定したSteamライブラリ"
      exit 1
    fi
  fi

  show_message \
    "${LAUNCHER_TITLE}" \
    "Caves of Qudが見つかりませんでした。次の画面で、Steamライブラリ内の Caves of Qud/CoQ.app を選択してください。"

  local chosen_binary
  if ! chosen_binary="$(choose_game_binary)"; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "Caves of Qudを起動できませんでした。もう一度このファイルを開き、Steamライブラリ内の Caves of Qud/CoQ.app を選択してください。"
    exit 1
  fi

  local canonical_chosen
  if ! canonical_chosen="$(canonicalize_binary_path "${chosen_binary}")" ||
    [[ "${canonical_chosen}" != */CoQ.app/Contents/MacOS/CoQ ]] ||
    [[ ! -x "${canonical_chosen}" ]]; then
    show_message \
      "${LAUNCHER_TITLE}" \
      "選択されたファイルからCaves of Qudを起動できませんでした。Steamライブラリ内の Caves of Qud/CoQ.app を選択してください。"
    exit 1
  fi

  printf '%s\n' "${canonical_chosen}"
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
    "この起動ファイルはmacOS専用です。WindowsやLinuxでは、Steamから通常どおりCaves of Qudを起動してください。"
  exit 1
fi

if is_apple_silicon && ! rosetta_available; then
  install_rosetta
fi

GAME_BINARY="$(resolve_game_binary)"

printf '%s\n%s\n' \
  "${LAUNCHER_TITLE}" \
  "Rosetta 2経由でCaves of Qudを起動します。" >&2

exec arch -x86_64 "${GAME_BINARY}"
