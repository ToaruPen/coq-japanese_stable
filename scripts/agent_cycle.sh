#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
DOTFILES_ROOT=${DOTFILES_ROOT:-}
ARTIFACT_DIR=${AGENT_LOOP_ARTIFACT_DIR:-"$ROOT_DIR/scripts/_artifacts/agent-loop"}
PYTHON_BIN=${PYTHON_BIN:-python3.12}

usage() {
  cat <<'USAGE'
Usage:
  scripts/agent_cycle.sh tool-check
  scripts/agent_cycle.sh ast-grep-check
  scripts/agent_cycle.sh sg <lang> [pattern] [path]
  scripts/agent_cycle.sh render-skill-evals [skill] [scenario]
  scripts/agent_cycle.sh summarize-skill-evals [results-jsonl]
  scripts/agent_cycle.sh retrospective-open
  scripts/agent_cycle.sh cycle [skill] [scenario]
USAGE
}

require_file() {
  local path=$1
  if [[ ! -f "$path" ]]; then
    echo "missing required file: $path" >&2
    return 1
  fi
}

require_dotfiles_root() {
  if [[ -z "$DOTFILES_ROOT" ]]; then
    echo "missing env: DOTFILES_ROOT (path to dotfiles repo)" >&2
    return 1
  fi

  if [[ ! -d "$DOTFILES_ROOT" ]]; then
    echo "missing directory: DOTFILES_ROOT=$DOTFILES_ROOT" >&2
    return 1
  fi
}

tool_check() {
  local missing=0
  for tool in just ast-grep "$PYTHON_BIN"; do
    if ! command -v "$tool" >/dev/null 2>&1; then
      echo "missing tool: $tool" >&2
      missing=1
      continue
    fi
    "$tool" --version
  done

  if require_dotfiles_root; then
    require_file "$DOTFILES_ROOT/scripts/render-skill-eval-prompts.py" || missing=1
    require_file "$DOTFILES_ROOT/scripts/summarize-skill-eval-results.py" || missing=1
  else
    missing=1
  fi
  require_file "$ROOT_DIR/skill-evals.json" || missing=1
  require_file "$ROOT_DIR/skill-eval-results.jsonl" || missing=1
  require_file "$ROOT_DIR/skill-eval-result.schema.json" || missing=1
  require_file "$ROOT_DIR/retrospectives/retrospective-log.md" || missing=1

  return "$missing"
}

ast_grep_check() {
  cd "$ROOT_DIR"
  require_file "$ROOT_DIR/sgconfig.yml"
  ast-grep test --skip-snapshot-tests
  ast-grep scan .
}

expand_search_path() {
  local path=$1
  case "$path" in
    \~)
      printf '%s\n' "$HOME"
      ;;
    \~/*)
      printf '%s/%s\n' "$HOME" "${path#"~/"}"
      ;;
    *)
      printf '%s\n' "$path"
      ;;
  esac
}

structural_search() {
  local lang=${1:-${AST_GREP_LANG:-}}
  local pattern=${2:-${AST_GREP_PATTERN:-}}
  local path=${3:-${AST_GREP_PATH:-}}

  if [[ -z "$lang" ]]; then
    echo "missing ast-grep language" >&2
    return 2
  fi
  if [[ -z "$pattern" ]]; then
    echo "missing ast-grep pattern" >&2
    return 2
  fi
  if [[ -z "$path" ]]; then
    if [[ "$lang" == "csharp" ]]; then
      path="$HOME/dev/coq-decompiled_stable"
      if [[ ! -d "$path" ]]; then
        echo "missing default csharp path: $path (pass [path] explicitly)" >&2
        return 2
      fi
    else
      path="$ROOT_DIR"
    fi
  fi

  path=$(expand_search_path "$path")
  ast-grep run --lang "$lang" --pattern "$pattern" "$path"
}

render_skill_evals() {
  local skill=${1:-}
  local scenario=${2:-}
  require_dotfiles_root
  mkdir -p "$ARTIFACT_DIR"

  local -a args=(
    "$PYTHON_BIN"
    "$DOTFILES_ROOT/scripts/render-skill-eval-prompts.py"
    "$DOTFILES_ROOT"
    "$ROOT_DIR/skill-evals.json"
  )
  if [[ -n "$skill" ]]; then
    args+=(--skill "$skill")
  fi
  if [[ -n "$scenario" ]]; then
    args+=(--scenario "$scenario")
  fi

  "${args[@]}" | tee "$ARTIFACT_DIR/skill-eval-prompts.md"
}

summarize_skill_evals() {
  local results=${1:-skill-eval-results.jsonl}
  require_dotfiles_root
  results=$(expand_search_path "$results")
  if [[ "$results" != /* ]]; then
    results="$ROOT_DIR/$results"
  fi

  mkdir -p "$ARTIFACT_DIR"
  "$PYTHON_BIN" "$DOTFILES_ROOT/scripts/summarize-skill-eval-results.py" "$results" \
    | tee "$ARTIFACT_DIR/skill-eval-summary.md"
}

retrospective_open() {
  local log_path="$ROOT_DIR/retrospectives/retrospective-log.md"
  require_file "$log_path"

  if grep -nE -- "^- Status: \`open\`$" "$log_path"; then
    echo "open retrospective entries found in $log_path"
  else
    echo "no open retrospective entries"
  fi
}

cycle() {
  local skill=${1:-}
  local scenario=${2:-}

  tool_check
  ast_grep_check
  render_skill_evals "$skill" "$scenario"
  summarize_skill_evals "$ROOT_DIR/skill-eval-results.jsonl"
  retrospective_open
}

main() {
  local command=${1:-}
  shift || true

  case "$command" in
    tool-check)
      tool_check
      ;;
    ast-grep-check)
      ast_grep_check
      ;;
    sg)
      structural_search "${1:-}" "${2:-}" "${3:-}"
      ;;
    render-skill-evals)
      render_skill_evals "${1:-}" "${2:-}"
      ;;
    summarize-skill-evals)
      summarize_skill_evals "${1:-skill-eval-results.jsonl}"
      ;;
    retrospective-open)
      retrospective_open
      ;;
    cycle)
      cycle "${1:-}" "${2:-}"
      ;;
    -h|--help|help|"")
      usage
      ;;
    *)
      usage >&2
      return 2
      ;;
  esac
}

main "$@"
