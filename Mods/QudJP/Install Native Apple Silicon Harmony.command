#!/usr/bin/env bash
# Install Harmony 2.4.2 into the Caves of Qud app as an explicit opt-in native
# Apple Silicon workaround. This script mutates the game install only after a
# Finder-visible confirmation and creates a restorable backup first.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TITLE="QudJP Native Apple Silicon Harmony"
HARMONY_VERSION="2.4.2"
HARMONY_FULL_VERSION="2.4.2.0"
HARMONY_ZIP_NAME="Harmony-Fat.2.4.2.0.zip"
HARMONY_ZIP_URL="https://github.com/pardeike/Harmony/releases/download/v2.4.2.0/${HARMONY_ZIP_NAME}"
EXPECTED_DLL_SHA256="77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d"
BACKUP_NAME="0Harmony.dll.qudjp-backup-before-2.4.2"
HARMONY_RELATIVE_PATH="CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll"
HARMONY_APP_SUFFIX="/CoQ.app/Contents/Resources/Data/Managed/0Harmony.dll"
INSTALLED_MOD_SUFFIX="/CoQ.app/Contents/Resources/Data/StreamingAssets/Mods/QudJP"
WORKSHOP_SUFFIX="/steamapps/workshop/content/333640/3718988020"

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

quote_osa() {
  local text="$1"
  text="${text//\\/\\\\}"
  text="${text//\"/\\\"}"
  text="${text//$'\n'/\\n}"
  printf '"%s"' "${text}"
}

confirm_install() {
  local target="$1"
  local message
  message="Caves of Qud のゲーム本体ファイルを変更して、0Harmony.dll を Harmony 2.4.2 に置き換えます。\n\n対象:\n${target}\n\n変更前の DLL は同じフォルダの ${BACKUP_NAME} にバックアップします。\n\n問題が出た場合は同梱の Restore Game Harmony.command で元に戻してください。\n\n続行しますか？"
  if command -v osascript >/dev/null 2>&1; then
    osascript -e "display dialog $(quote_osa "${message}") buttons {\"キャンセル\", \"Harmony 2.4.2 をインストール\"} default button \"キャンセル\" cancel button \"キャンセル\" with title $(quote_osa "${TITLE}")" >/dev/null
  else
    printf '%s\n' "${message}" >&2
    read -r -p "Type INSTALL to continue: " answer
    [[ "${answer}" == "INSTALL" ]] || exit 0
  fi
}

require_tool() {
  command -v "$1" >/dev/null 2>&1 || fail "必要なコマンドが見つかりません: $1"
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
set selectedFile to choose file with prompt "Caves of Qud の CoQ.app 内にある Managed/0Harmony.dll を選択してください。" of type {"dll"}
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

read_harmony_identity() {
  local target="$1"
  strings -a "${target}" 2>/dev/null | grep -m 1 '0Harmony, Version=' || true
}

download_harmony() {
  local temp_dir="$1"
  local zip_path="${temp_dir}/${HARMONY_ZIP_NAME}"
  curl --proto '=https' --tlsv1.2 \
    --location --fail \
    --retry 3 --retry-delay 2 --retry-all-errors \
    --connect-timeout 10 --max-time 180 \
    --output "${zip_path}" "${HARMONY_ZIP_URL}"
  unzip -q "${zip_path}" net48/0Harmony.dll -d "${temp_dir}/extracted"

  local dll_path="${temp_dir}/extracted/net48/0Harmony.dll"
  [[ -f "${dll_path}" ]] || fail "Harmony ${HARMONY_VERSION} の 0Harmony.dll を ZIP から展開できませんでした。"

  local actual_sha
  actual_sha="$(shasum -a 256 "${dll_path}" | awk '{print $1}')"
  [[ "${actual_sha}" == "${EXPECTED_DLL_SHA256}" ]] || fail "Harmony ${HARMONY_VERSION} DLL の SHA256 が一致しません。\nexpected: ${EXPECTED_DLL_SHA256}\nactual: ${actual_sha}"

  printf '%s\n' "${dll_path}"
}

main() {
  [[ "$(uname -s)" == "Darwin" ]] || fail "このスクリプトは macOS 用です。"
  require_tool curl
  require_tool unzip
  require_tool shasum
  require_tool strings

  local target
  target="$(resolve_target)"
  validate_target "${target}"

  local identity
  identity="$(read_harmony_identity "${target}")"
  if [[ "${identity}" == *"Version=${HARMONY_FULL_VERSION}"* ]]; then
    show_message "対象の 0Harmony.dll はすでに Harmony ${HARMONY_VERSION} です。\n\n${target}"
    exit 0
  fi

  confirm_install "${target}"

  local backup="${target%0Harmony.dll}${BACKUP_NAME}"
  if [[ ! -f "${backup}" ]]; then
    cp "${target}" "${backup}"
  fi

  local temp_dir
  temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/qudjp-harmony.XXXXXX")"
  trap 'rm -rf "${temp_dir}"' EXIT

  local new_dll
  new_dll="$(download_harmony "${temp_dir}")"
  cp "${new_dll}" "${target}"

  show_message "Harmony ${HARMONY_VERSION} をインストールしました。\n\n対象:\n${target}\n\nバックアップ:\n${backup}\n\nCaves of Qud を native Apple Silicon で起動して QudJP の翻訳が有効か確認してください。"
}

main "$@"
