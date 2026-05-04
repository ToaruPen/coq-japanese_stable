# QudJP task runner

default:
  just --list

# Build the shipped QudJP assembly.
build:
  dotnet build Mods/QudJP/Assemblies/QudJP.csproj

# Run fast C# L1 tests.
test-l1:
  dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1

# Run C# L2 tests.
test-l2:
  dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2

# Run C# L2 tests that require the game DLL reference.
test-l2g:
  dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G

# Run Python static checks.
python-check:
  ruff check scripts/

# Run Python tests.
python-test:
  uv run pytest scripts/tests/

# Run localization asset checks.
localization-check:
  python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts
  python3.12 scripts/check_glossary_consistency.py Mods/QudJP/Localization
  python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json

# Check placeholder and markup-token parity in JSON localization assets.
translation-token-check:
  python3.12 scripts/check_translation_tokens.py Mods/QudJP/Localization

# Sync the built mod into the local game install.
sync-mod:
  python3.12 scripts/sync_mod.py

# Run the broad local verification gate.
check: build test-l1 test-l2 test-l2g python-check python-test localization-check translation-token-check

# Verify agent-loop tools and dotfiles script availability.
tool-check:
  bash scripts/agent_cycle.sh tool-check

# Run ast-grep rule tests and scan using sgconfig.yml.
ast-grep-check:
  bash scripts/agent_cycle.sh ast-grep-check

# Run an ast-grep structural search.
sg lang pattern path=".":
  AST_GREP_PATTERN='{{pattern}}' AST_GREP_PATH='{{path}}' bash scripts/agent_cycle.sh sg "{{lang}}"

# Search C# structure. Defaults to the decompiled game source.
sg-cs pattern path="":
  AST_GREP_PATTERN='{{pattern}}' AST_GREP_PATH='{{path}}' bash scripts/agent_cycle.sh sg csharp

# Search Python structure.
sg-py pattern path="scripts":
  AST_GREP_PATTERN='{{pattern}}' AST_GREP_PATH='{{path}}' bash scripts/agent_cycle.sh sg python

# Render skill-eval prompts from this repo's manifest.
render-skill-evals skill="" scenario="":
  bash scripts/agent_cycle.sh render-skill-evals "{{skill}}" "{{scenario}}"

# Summarize recorded skill-eval JSONL results.
summarize-skill-evals results="skill-eval-results.jsonl":
  bash scripts/agent_cycle.sh summarize-skill-evals "{{results}}"

# Show open retrospective entries.
retrospective-open:
  bash scripts/agent_cycle.sh retrospective-open

# Run the local agent feedback loop: tools, ast-grep, skill-eval render, summary, retrospectives.
agent-cycle skill="" scenario="":
  bash scripts/agent_cycle.sh cycle "{{skill}}" "{{scenario}}"
