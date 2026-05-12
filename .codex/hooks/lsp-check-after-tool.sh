#!/usr/bin/env bash
set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
payload="$(cat || true)"

min_interval_seconds="${QUDJP_CODEX_LSP_HOOK_MIN_INTERVAL_SECONDS:-45}"
state_dir="$repo_root/.artifacts/codex-hooks"
last_run_file="$state_dir/lsp-check.last"
lock_dir="$state_dir/lsp-check.lock"
log_file="$state_dir/lsp-check.log"

json_field() {
  local field="$1"
  if ! command -v python3 >/dev/null 2>&1; then
    return 0
  fi
  PAYLOAD="$payload" FIELD="$field" python3 - <<'PY'
import json
import os

payload = os.environ.get("PAYLOAD", "")
field = os.environ["FIELD"]
try:
    data = json.loads(payload)
except json.JSONDecodeError:
    raise SystemExit(0)

value = data.get(field, "")
if isinstance(value, str):
    print(value)
PY
}

emit_additional_context() {
  local message="$1"
  if command -v python3 >/dev/null 2>&1; then
    MESSAGE="$message" python3 - <<'PY'
import json
import os

print(json.dumps({
    "continue": True,
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": os.environ["MESSAGE"],
    },
}, separators=(",", ":")))
PY
  else
    printf '%s\n' "$message" >&2
  fi
}

tool_name="$(json_field tool_name)"

is_interesting_tool=false
case "$tool_name" in
  *Write*|*Edit*|*MultiEdit*|*apply_patch*)
    is_interesting_tool=true
    ;;
  *Read*)
    if [[ "${QUDJP_CODEX_LSP_HOOK_ON_READ:-0}" == "1" ]]; then
      is_interesting_tool=true
    fi
    ;;
  Bash|*exec_command*|*multi_tool_use*)
    if [[ "${QUDJP_CODEX_LSP_HOOK_ON_EXEC:-0}" == "1" ]]; then
      is_interesting_tool=true
    fi
    ;;
esac

if [[ "${QUDJP_CODEX_LSP_HOOK_FORCE:-0}" != "1" && "$is_interesting_tool" != "true" ]]; then
  exit 0
fi

if [[ "${QUDJP_CODEX_LSP_HOOK_FORCE:-0}" != "1" ]]; then
  if ! printf '%s' "$payload" | grep -Eq '(\.cs([^A-Za-z0-9_]|$)|\.csproj([^A-Za-z0-9_]|$)|\.sln([^A-Za-z0-9_]|$)|dotnet-tools\.json|global\.json|Directory\.Build\.(props|targets)|Mods/QudJP/Assemblies/ReferenceStubs/)'; then
    exit 0
  fi

fi

mkdir -p "$state_dir"

now="$(date +%s)"
if [[ "${QUDJP_CODEX_LSP_HOOK_FORCE:-0}" != "1" && -f "$last_run_file" ]]; then
  last_run="$(cat "$last_run_file" 2>/dev/null || printf '0')"
  if [[ "$last_run" =~ ^[0-9]+$ ]] && (( now - last_run < min_interval_seconds )); then
    exit 0
  fi
fi

if ! mkdir "$lock_dir" 2>/dev/null; then
  exit 0
fi
trap 'rmdir "$lock_dir" 2>/dev/null || true' EXIT

printf '%s\n' "$now" > "$last_run_file"

if [[ "${QUDJP_CODEX_LSP_HOOK_DRY_RUN:-0}" == "1" ]]; then
  printf 'QudJP Codex LSP hook: would run just lsp-check\n' >&2
  exit 0
fi

tmp_log="$log_file.tmp"
(
  cd "$repo_root" || exit 1
  just lsp-check
) >"$tmp_log" 2>&1
status=$?
mv "$tmp_log" "$log_file"

if (( status != 0 )); then
  emit_additional_context "QudJP Codex LSP hook: just lsp-check failed after a relevant C# tool use. Inspect .artifacts/codex-hooks/lsp-check.log before relying on language-server diagnostics."
fi

exit 0
