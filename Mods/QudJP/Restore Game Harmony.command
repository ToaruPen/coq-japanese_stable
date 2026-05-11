#!/usr/bin/env bash
# Restore the Caves of Qud game-bundled Harmony DLL from the backup created by
# "Install Native Apple Silicon Harmony.command".

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TITLE="QudJP Native Apple Silicon Harmony Restore"
BACKUP_NAME="0Harmony.dll.qudjp-backup-before-2.4.2"
HARMONY_RELATIVE_PATH="CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll"
HARMONY_APP_SUFFIX="/CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll"
INSTALLED_MOD_SUFFIX="/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP"
WORKSHOP_SUFFIX="/steamapps/workshop/content/333640/3718988020"

quote_osa() {
  local text="$1"
  text="${text//\\/\\\\}"
  text="${text//\"/\\\"}"
  text="${text//$'\n'/\\n}"
  printf '"%s"' "${text}"
}

show_message() {
  local message="$1"
  if command -v osascript >/dev/null 2>&1; then
    osascript -e "display dialog $(quote_osa "${message}") buttons {\"OK\"} default button \"OK\" with title $(quote_osa "${TITLE}")" >/dev/null
  else
    printf '%s\n' "${message}" >&2
  fi
}

fail() {
  show_message "$1"
  exit 1
}

confirm_restore() {
  local target="$1"
  local backup="$2"
  local message
  message="QudJP が作成したバックアップから Caves of Qud の 0Harmony.dll を復元します。\n\n対象:\n${target}\n\nバックアップ:\n${backup}\n\n続行しますか？"
  if command -v osascript >/dev/null 2>&1; then
    osascript -e "display dialog $(quote_osa "${message}") buttons {\"キャンセル\", \"元に戻す\"} default button \"キャンセル\" cancel button \"キャンセル\" with title $(quote_osa "${TITLE}")" >/dev/null
  else
    printf '%s\n' "${message}" >&2
    read -r -p "Type RESTORE to continue: " answer
    [[ "${answer}" == "RESTORE" ]] || exit 0
  fi
}

append_candidate() {
  local candidate="$1"
  [[ -n "${candidate}" ]] || return 0
  [[ -f "${candidate}" ]] || return 0
  printf '%s\n' "${candidate}"
}

infer_from_workshop_location() {
  if [[ "${SCRIPT_DIR}" == *"${WORKSHOP_SUFFIX}"* ]]; then
    local steam_root="${SCRIPT_DIR%%"${WORKSHOP_SUFFIX}"*}/steamapps"
    append_candidate "${steam_root}/common/Caves of Qud/${HARMONY_RELATIVE_PATH}"
  fi
}

infer_from_installed_mod_location() {
  if [[ "${SCRIPT_DIR}" == *"${INSTALLED_MOD_SUFFIX}" ]]; then
    local app_root="${SCRIPT_DIR%"${INSTALLED_MOD_SUFFIX}"}"
    append_candidate "${app_root}${HARMONY_APP_SUFFIX}"
  fi
}

default_candidates() {
  infer_from_installed_mod_location
  infer_from_workshop_location
  append_candidate "${HOME}/Library/Application Support/Steam/steamapps/common/Caves of Qud/${HARMONY_RELATIVE_PATH}"
}

choose_harmony_dll() {
  if command -v osascript >/dev/null 2>&1; then
    osascript <<'OSA'
set selectedFile to choose file with prompt "復元対象の CoQ.app 内にある Managed/0Harmony.dll を選択してください。" of type {"dll"}
POSIX path of selectedFile
OSA
  else
    read -r -p "Path to CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll: " selected
    printf '%s\n' "${selected}"
  fi
}

resolve_target() {
  local first_candidate
  first_candidate="$(default_candidates | head -n 1 || true)"
  if [[ -n "${first_candidate}" ]]; then
    printf '%s\n' "${first_candidate}"
    return 0
  fi

  local chosen
  chosen="$(choose_harmony_dll)"
  [[ -n "${chosen}" ]] || fail "0Harmony.dll が選択されませんでした。"
  printf '%s\n' "${chosen}"
}

validate_target() {
  local target="$1"
  [[ -f "${target}" ]] || fail "0Harmony.dll が見つかりません:\n${target}"
  [[ "${target}" == *"${HARMONY_APP_SUFFIX}" ]] || fail "選択されたファイルは Caves of Qud の Managed/0Harmony.dll ではありません:\n${target}"
}

main() {
  [[ "$(uname -s)" == "Darwin" ]] || fail "このスクリプトは macOS 用です。"

  local target
  target="$(resolve_target)"
  validate_target "${target}"

  local backup="${target%0Harmony.dll}${BACKUP_NAME}"
  [[ -f "${backup}" ]] || fail "QudJP のバックアップが見つかりません。\n\n先に Install Native Apple Silicon Harmony.command で作成されたバックアップだけを復元できます。\n\n期待した場所:\n${backup}"

  confirm_restore "${target}" "${backup}"
  cp "${backup}" "${target}"

  show_message "Caves of Qud の 0Harmony.dll をバックアップから復元しました。\n\n対象:\n${target}"
}

main "$@"
