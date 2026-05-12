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
  scripts/agent_cycle.sh ast-grep-smoke
  scripts/agent_cycle.sh ast-search <lang> [pattern] [path]
  scripts/agent_cycle.sh sg <lang> [pattern] [path]
  scripts/agent_cycle.sh lsp-check [solution]
  scripts/agent_cycle.sh lsp-diagnostics [solution]
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
  for tool in just "$PYTHON_BIN"; do
    if ! command -v "$tool" >/dev/null 2>&1; then
      echo "missing tool: $tool" >&2
      missing=1
      continue
    fi
    "$tool" --version
  done

  if ! ast_grep --version; then
    missing=1
  fi
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "missing tool: dotnet" >&2
    missing=1
  elif ! dotnet tool restore >/dev/null 2>&1; then
    echo "dotnet tool restore failed; csharp-ls diagnostics are unavailable." >&2
    missing=1
  elif ! dotnet tool run csharp-ls -- --version; then
    echo "csharp-ls version check failed; csharp-ls diagnostics are unavailable." >&2
    missing=1
  fi

  require_file "$ROOT_DIR/scripts/render_skill_eval_prompts.py" || missing=1
  require_file "$ROOT_DIR/scripts/validate_skill_eval_results.py" || missing=1
  if [[ -n "$DOTFILES_ROOT" ]]; then
    require_file "$DOTFILES_ROOT/scripts/render-skill-eval-prompts.py" || missing=1
    require_file "$DOTFILES_ROOT/scripts/summarize-skill-eval-results.py" || missing=1
  else
    echo "DOTFILES_ROOT not set; dotfiles-backed skill evals and summaries are unavailable."
  fi
  require_file "$ROOT_DIR/skill-evals.json" || missing=1
  require_file "$ROOT_DIR/skill-eval-results.jsonl" || missing=1
  require_file "$ROOT_DIR/skill-eval-result.schema.json" || missing=1
  require_file "$ROOT_DIR/retrospectives/retrospective-log.md" || missing=1

  return "$missing"
}

ast_grep() {
  if [[ -x "$ROOT_DIR/node_modules/.bin/ast-grep" ]]; then
    "$ROOT_DIR/node_modules/.bin/ast-grep" "$@"
    return
  fi

  if command -v ast-grep >/dev/null 2>&1; then
    command ast-grep "$@"
    return
  fi

  if command -v npx >/dev/null 2>&1; then
    npx --no-install ast-grep "$@"
    return
  fi

  echo "missing tool: ast-grep (run npm ci, or install @ast-grep/cli)" >&2
  return 127
}

ast_grep_check() {
  cd "$ROOT_DIR"
  require_file "$ROOT_DIR/sgconfig.yml"
  local rule_count
  local test_count
  rule_count=$(find "$ROOT_DIR/rules" -type f \( -name '*.yml' -o -name '*.yaml' \) | wc -l | tr -d '[:space:]')
  test_count=$(find "$ROOT_DIR/rule-tests" -type f \( -name '*.yml' -o -name '*.yaml' \) | wc -l | tr -d '[:space:]')
  if [[ "$rule_count" == "0" && "$test_count" == "0" ]]; then
    echo "No project ast-grep rules registered; running structural-search smoke only."
  elif [[ "$rule_count" == "0" || "$test_count" == "0" ]]; then
    echo "ast-grep rules and rule tests must be added together (rules=$rule_count tests=$test_count)" >&2
    return 1
  else
    ast_grep test --skip-snapshot-tests
    ast_grep scan .
  fi
  ast_grep_smoke
}

ast_grep_smoke() {
  cd "$ROOT_DIR"
  require_file "$ROOT_DIR/sgconfig.yml"
  local fixture_path="scripts/tests/fixtures/static_producer_inventory"
  local output
  output=$(structural_search csharp 'Popup.Show($$$ARGS)' "$fixture_path")
  printf '%s\n' "$output"
  if ! grep -q 'Demo/StaticProducerCases.cs:25:' <<<"$output"; then
    echo "ast-grep smoke did not find the expected Popup.Show fixture hit" >&2
    return 1
  fi
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
  ast_grep run --lang "$lang" --pattern "$pattern" "$path"
}

lsp_diagnostics() {
  local solution=${1:-Mods/QudJP/Assemblies/QudJP.sln}
  cd "$ROOT_DIR"
  require_file "$ROOT_DIR/dotnet-tools.json"
  require_file "$ROOT_DIR/$solution"

  dotnet tool restore
  dotnet tool run csharp-ls -- --solution "$solution" --diagnose --loglevel warning
}

render_skill_evals() {
  local skill=${1:-}
  local scenario=${2:-}
  mkdir -p "$ARTIFACT_DIR"

  local -a args=(
    "$ROOT_DIR/skill-evals.json"
  )
  if [[ -n "$DOTFILES_ROOT" ]]; then
    args+=(--dotfiles-root "$DOTFILES_ROOT")
  fi
  if [[ -n "$skill" ]]; then
    args+=(--skill "$skill")
  fi
  if [[ -n "$scenario" ]]; then
    args+=(--scenario "$scenario")
  fi

  "$PYTHON_BIN" "$ROOT_DIR/scripts/render_skill_eval_prompts.py" "${args[@]}" \
    | tee "$ARTIFACT_DIR/skill-eval-prompts.md"
}

summarize_skill_evals() {
  local results=${1:-skill-eval-results.jsonl}
  require_dotfiles_root
  results=$(expand_search_path "$results")
  if [[ "$results" != /* ]]; then
    results="$ROOT_DIR/$results"
  fi

  mkdir -p "$ARTIFACT_DIR"
  "$PYTHON_BIN" "$ROOT_DIR/scripts/validate_skill_eval_results.py" "$results"
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
    ast-grep-smoke)
      ast_grep_smoke
      ;;
    sg|ast-search)
      structural_search "${1:-}" "${2:-}" "${3:-}"
      ;;
    lsp-check|lsp-diagnostics)
      lsp_diagnostics "${1:-}"
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
