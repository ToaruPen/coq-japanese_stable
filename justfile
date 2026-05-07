# QudJP task runner

python := "uv run python"
decompiled_root := env_var("HOME") + "/dev/coq-decompiled_stable"
decompiled_annals_root := env_var("HOME") + "/dev/coq-decompiled_stable/XRL.Annals"

default:
  just --list

# Build the shipped QudJP assembly.
build:
  dotnet build Mods/QudJP/Assemblies/QudJP.csproj

# Build the QudJP assembly for local development.
build-dev: build

# Clean and rebuild the shipped QudJP assembly without incremental artifacts.
rebuild:
  dotnet clean Mods/QudJP/Assemblies/QudJP.csproj
  dotnet build Mods/QudJP/Assemblies/QudJP.csproj --no-incremental

# Clean and rebuild the QudJP assembly for local development.
rebuild-dev: rebuild

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

# Format Python scripts.
python-format:
  ruff format scripts/

# Run Python tests.
python-test:
  uv run pytest scripts/tests/

# Run focused Python tests by pytest -k expression.
python-test-filter pattern:
  uv run pytest scripts/tests/ -k {{quote(pattern)}}

# Run localization asset checks.
localization-check:
  {{python}} scripts/check_encoding.py Mods/QudJP/Localization scripts
  {{python}} scripts/check_glossary_consistency.py Mods/QudJP/Localization
  {{python}} scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json

# Check placeholder and markup-token parity in JSON localization assets.
translation-token-check:
  {{python}} scripts/check_translation_tokens.py Mods/QudJP/Localization

# Regenerate the duplicate source-key conflict baseline for translation-token checks.
translation-token-baseline:
  {{python}} scripts/check_translation_tokens.py Mods/QudJP/Localization --write-duplicate-conflict-baseline scripts/translation_token_duplicate_baseline.json

# Require release-note fragments for localization changes.
release-note-check base_ref="origin/main" head_ref="HEAD":
  {{python}} scripts/release_notes.py check-fragment --base-ref "{{base_ref}}" --head-ref "{{head_ref}}"

# Check changed Markdown reports for recurring GitHub rendering pitfalls.
markdown-report-check base_ref="origin/main" head_ref="HEAD":
  {{python}} scripts/check_markdown_reports.py --base-ref "{{base_ref}}" --head-ref "{{head_ref}}"

# Render release and Workshop changenote drafts from unreleased fragments.
render-release-notes version git_hash date:
  {{python}} scripts/release_notes.py render --version "{{version}}" --git-hash "{{git_hash}}" --date "{{date}}" --changelog-output /tmp/qudjp-changelog-entry.md --workshop-output /tmp/qudjp-workshop-changenote.txt

# Build the release ZIP under dist/.
build-release:
  {{python}} scripts/build_release.py

# Spot-check required files in a release ZIP.
release-zip-check release_zip="":
  #!/usr/bin/env bash
  set -euo pipefail
  if [ -n "{{release_zip}}" ]; then
    chosen_zip="{{release_zip}}"
  else
    chosen_zip="$({{python}} - <<'PY'
  from pathlib import Path

  release_archives = sorted(
      Path("dist").glob("QudJP-v*.zip"),
      key=lambda path: (path.stat().st_mtime, path.name),
  )
  if not release_archives:
      raise SystemExit("dist/: no QudJP-v*.zip release archive found")
  print(release_archives[-1])
  PY
  )"
  fi
  export QUDJP_RELEASE_ZIP="$chosen_zip"
  {{python}} - <<'PY'
  import os
  import zipfile
  from pathlib import Path

  requested = os.environ.get("QUDJP_RELEASE_ZIP", "")
  if not requested:
      raise SystemExit("QUDJP_RELEASE_ZIP is empty")
  zip_path = Path(requested)

  required = {
      "QudJP/manifest.json",
      "QudJP/preview.png",
      "QudJP/LICENSE",
      "QudJP/NOTICE.md",
      "QudJP/Bootstrap.cs",
      "QudJP/Assemblies/QudJP.dll",
  }
  required_prefixes = {
      "QudJP/Localization/",
      "QudJP/Fonts/",
  }
  with zipfile.ZipFile(zip_path) as zf:
      names = set(zf.namelist())
  missing = sorted(required - names)
  missing_prefixes = sorted(
      prefix for prefix in required_prefixes if not any(name.startswith(prefix) for name in names)
  )
  allowed_exact = {
      *required,
      "QudJP/",
      "QudJP/Assemblies/",
      "QudJP/Localization/",
      "QudJP/Fonts/",
  }
  extra = sorted(
      name for name in names if name not in allowed_exact and not any(name.startswith(prefix) for prefix in required_prefixes)
  )
  if missing or missing_prefixes or extra:
      raise SystemExit(
          f"{zip_path}: missing files={missing}, missing dirs={missing_prefixes}, extra files={extra}"
      )
  print(f"{zip_path}: required release files present")
  PY
  {{python}} scripts/verify_release_dll.py "$chosen_zip"

# Run the Workshop shipping preflight for an already-tagged release.
workshop-preflight version:
  #!/usr/bin/env bash
  set -euo pipefail
  git status --short --branch
  if [ -n "$(git status --porcelain --untracked-files=all)" ]; then \
    echo "workshop-preflight requires a clean worktree before building release artifacts" >&2; \
    exit 1; \
  fi
  test "$(git rev-list -n1 v{{version}})" = "$(git rev-parse HEAD)"
  just build
  just python-check
  uv run pytest scripts/tests/test_build_release.py scripts/tests/test_build_workshop_upload.py scripts/tests/test_sync_mod.py scripts/tests/test_tokenize_corpus.py -q
  just localization-check
  just translation-token-check
  just build-release
  just release-zip-check dist/QudJP-v{{version}}.zip

# Build Steam Workshop staging and the steamcmd VDF.
build-workshop-upload release_zip="" changenote_file="/tmp/qudjp-workshop-changenote.txt":
  if [ -n "{{release_zip}}" ]; then \
    {{python}} scripts/build_workshop_upload.py --release-zip "{{release_zip}}" --changenote-file "{{changenote_file}}"; \
  else \
    {{python}} scripts/build_workshop_upload.py --changenote-file "{{changenote_file}}"; \
  fi

# Download and verify the GitHub Release ZIP used for Workshop staging.
download-release-zip version:
  #!/usr/bin/env bash
  set -euo pipefail
  version={{quote(version)}}
  if [[ ! "${version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    printf 'error: version must be X.Y.Z: %q\n' "${version}" >&2
    exit 1
  fi
  tag="v${version}"
  zip_name="QudJP-${tag}.zip"
  checksum_name="${zip_name}.sha256"
  asset_dir="dist/release-assets/${tag}"
  mkdir -p "${asset_dir}"
  gh release download "${tag}" \
    --pattern "${zip_name}" \
    --pattern "${checksum_name}" \
    --dir "${asset_dir}" \
    --clobber
  (cd "${asset_dir}" && shasum -a 256 -c "${checksum_name}")
  printf '%s\n' "${asset_dir}/${zip_name}"

# Sync the built mod into the local game install.
sync-mod:
  {{python}} scripts/sync_mod.py

# Sync the built mod into the local game install as a local dev build.
sync-mod-dev:
  {{python}} scripts/sync_mod.py --dev

# Preview the local mod sync without copying files.
sync-mod-dry-run:
  {{python}} scripts/sync_mod.py --dry-run

# Preview the local dev sync without copying files.
sync-mod-dev-dry-run:
  {{python}} scripts/sync_mod.py --dev --dry-run

# Sync the built mod without copying fonts.
sync-mod-exclude-fonts:
  {{python}} scripts/sync_mod.py --exclude-fonts

# Sync the built mod to an explicit Mods/QudJP destination.
sync-mod-to destination:
  {{python}} scripts/sync_mod.py --destination {{quote(destination)}}

# Sync the built mod to an explicit Mods/QudJP destination as a local dev build.
sync-mod-dev-to destination:
  {{python}} scripts/sync_mod.py --dev --destination {{quote(destination)}}

# Rebuild and sync the local mod into the game install.
deploy-mod: rebuild sync-mod

# Rebuild and sync the local dev mod into the game install.
deploy-dev: rebuild-dev sync-mod-dev

# Rebuild and sync the local mod to an explicit Mods/QudJP destination.
deploy-mod-to destination:
  just rebuild
  just sync-mod-to {{quote(destination)}}

# Rebuild and sync the local dev mod to an explicit Mods/QudJP destination.
deploy-dev-to destination:
  just rebuild-dev
  just sync-mod-dev-to {{quote(destination)}}

# Run the Phase F runtime evidence verification commands.
runtime-evidence-check: test-l1
  uv run pytest scripts/tests/test_triage_log_parser.py scripts/tests/test_triage_models.py scripts/tests/test_triage_classifier.py scripts/tests/test_triage_integration.py -q
  uv run pytest scripts/tests/test_triage_integration.py -q -k sample_log_smoke

# Run the broad local verification gate.
check: build test-l1 test-l2 test-l2g python-check python-test localization-check translation-token-check markdown-report-check localization-coverage-map-check

# Run the CI-like PR gate before pushing broad C#, script, or localization changes.
pr-check base_ref="origin/main" head_ref="HEAD": ci-dotnet roslyn-build python-check python-test localization-check translation-token-check localization-coverage-map-check
  {{python}} scripts/release_notes.py check-fragment --base-ref "{{base_ref}}" --head-ref "{{head_ref}}"
  {{python}} scripts/check_markdown_reports.py --base-ref "{{base_ref}}" --head-ref "{{head_ref}}"

# Build and test QudJP with the same Release configuration used by CI.
ci-dotnet:
  dotnet build Mods/QudJP/Assemblies/QudJP.csproj --configuration Release
  dotnet build Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --configuration Release
  dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --configuration Release --no-build

# Build the Annals Roslyn extractor.
roslyn-build-annals:
  dotnet build scripts/tools/AnnalsPatternExtractor/AnnalsPatternExtractor.csproj --configuration Release --no-incremental

# Build the static producer Roslyn scanner.
roslyn-build-static-producer:
  dotnet build scripts/tools/StaticProducerInventoryScanner/StaticProducerInventoryScanner.csproj --configuration Release --no-incremental

# Build the semantic probe Roslyn CLI.
roslyn-build-semantic-probe:
  dotnet build scripts/tools/RoslynSemanticProbe/RoslynSemanticProbe.csproj --configuration Release --no-incremental

# Build the text construction Roslyn inventory tool.
roslyn-build-text-construction:
  dotnet build scripts/tools/TextConstructionInventory/TextConstructionInventory.csproj --configuration Release --no-incremental

# Build all repo-local Roslyn analysis tools.
roslyn-build: roslyn-build-annals roslyn-build-static-producer roslyn-build-semantic-probe roslyn-build-text-construction

# Run focused pytest coverage for repo-local Roslyn analysis tools.
roslyn-test:
  uv run pytest scripts/tests/test_extract_annals_patterns.py scripts/tests/test_roslyn_extractor_smoke.py scripts/tests/test_roslyn_semantic_probe.py scripts/tests/test_roslyn_text_construction_inventory.py scripts/tests/test_scan_static_producer_inventory.py scripts/tests/test_static_producer_closure.py scripts/tests/test_text_construction_surface_policy.py -q

# Run Ruff for Roslyn Python files and basedpyright for the typed static-producer gate.
roslyn-python-check:
  ruff check scripts/extract_annals_patterns.py scripts/roslyn_semantic_probe.py scripts/scan_static_producer_inventory.py scripts/static_producer_closure.py scripts/text_construction_surface_policy.py scripts/tests/test_extract_annals_patterns.py scripts/tests/test_roslyn_extractor_smoke.py scripts/tests/test_roslyn_semantic_probe.py scripts/tests/test_roslyn_text_construction_inventory.py scripts/tests/test_scan_static_producer_inventory.py scripts/tests/test_static_producer_closure.py scripts/tests/test_text_construction_surface_policy.py
  uvx basedpyright scripts/roslyn_semantic_probe.py scripts/scan_static_producer_inventory.py scripts/static_producer_closure.py scripts/text_construction_surface_policy.py scripts/tests/test_roslyn_semantic_probe.py scripts/tests/test_scan_static_producer_inventory.py scripts/tests/test_static_producer_closure.py scripts/tests/test_text_construction_surface_policy.py scripts/tests/test_roslyn_extractor_smoke.py

# Run build, focused tests, and static checks for Roslyn analysis tooling.
roslyn-check: roslyn-build roslyn-test roslyn-python-check

# Run the generic Roslyn semantic probe.
semantic-probe *args:
  {{python}} scripts/roslyn_semantic_probe.py {{args}}

# Run focused validation for the generic Roslyn semantic probe.
semantic-probe-check: roslyn-build-semantic-probe
  uv run pytest scripts/tests/test_roslyn_semantic_probe.py scripts/tests/test_roslyn_extractor_smoke.py -q
  ruff check scripts/roslyn_semantic_probe.py scripts/tests/test_roslyn_semantic_probe.py scripts/tests/test_roslyn_extractor_smoke.py
  uvx basedpyright scripts/roslyn_semantic_probe.py scripts/tests/test_roslyn_semantic_probe.py scripts/tests/test_roslyn_extractor_smoke.py

# Run optional real-source smoke for the generic Roslyn semantic probe.
semantic-probe-real-smoke:
  QUDJP_RUN_SEMANTIC_PROBE_REAL=1 uv run pytest scripts/tests/test_roslyn_semantic_probe.py -q -m semantic_probe_real

# Validate the executable localization coverage map.
localization-coverage-map-check:
  uv run pytest scripts/tests/test_localization_coverage_map.py -q
  ruff check scripts/localization_coverage_map.py scripts/tests/test_localization_coverage_map.py
  uvx basedpyright scripts/localization_coverage_map.py scripts/tests/test_localization_coverage_map.py

# Generate static producer inventory to a disposable local output.
static-producer-preview source_root=decompiled_root output="/tmp/qudjp-static-producer-inventory.json":
  {{python}} scripts/scan_static_producer_inventory.py --source-root {{quote(source_root)}} --output {{quote(output)}}
  {{python}} -c 'import json, sys; doc=json.load(open(sys.argv[1], encoding="utf-8")); print(doc["totals"])' {{quote(output)}}

# Regenerate the tracked static producer inventory artifact.
static-producer-regenerate-tracked source_root=decompiled_root:
  just static-producer-preview {{quote(source_root)}} docs/static-producer-inventory.json

# Run the static producer scanner's focused validation gate.
static-producer-check: roslyn-build-static-producer
  uv run pytest scripts/tests/test_scan_static_producer_inventory.py scripts/tests/test_static_producer_closure.py scripts/tests/test_roslyn_extractor_smoke.py -q
  ruff check scripts/scan_static_producer_inventory.py scripts/static_producer_closure.py scripts/tests/test_scan_static_producer_inventory.py scripts/tests/test_static_producer_closure.py scripts/tests/test_roslyn_extractor_smoke.py
  uvx basedpyright scripts/scan_static_producer_inventory.py scripts/static_producer_closure.py scripts/tests/test_scan_static_producer_inventory.py scripts/tests/test_static_producer_closure.py scripts/tests/test_roslyn_extractor_smoke.py

# Print the static producer owner work queue grouped by decompiled C# source file.
static-producer-owner-queue limit="30":
  {{python}} scripts/static_producer_closure.py --limit {{quote(limit)}}

# Extract Annals candidate patterns to a disposable local output.
annals-pattern-preview source_root=decompiled_annals_root include="Resheph*.cs" output="/tmp/qudjp-annals-candidates.json":
  {{python}} scripts/extract_annals_patterns.py --source-root {{quote(source_root)}} --include {{quote(include)}} --output {{quote(output)}} --force

# Extract Annals candidate patterns to the tracked review artifact.
annals-pattern-extract-tracked source_root=decompiled_annals_root include="Resheph*.cs" output="scripts/_artifacts/annals/candidates_pending.json":
  {{python}} scripts/extract_annals_patterns.py --source-root {{quote(source_root)}} --include {{quote(include)}} --output {{quote(output)}} --force

# Generate a local text construction inventory and optional Markdown summary.
text-construction-inventory source_root=decompiled_root output="/tmp/roslyn-text-construction-inventory.json" summary_output="":
  #!/usr/bin/env bash
  set -euo pipefail
  if [ -n {{quote(summary_output)}} ]; then
    dotnet run --project scripts/tools/TextConstructionInventory/TextConstructionInventory.csproj -- --source-root {{quote(source_root)}} --output {{quote(output)}} --summary-output {{quote(summary_output)}}
  else
    dotnet run --project scripts/tools/TextConstructionInventory/TextConstructionInventory.csproj -- --source-root {{quote(source_root)}} --output {{quote(output)}}
  fi

# Generate and classify player-visible text-construction surfaces for C# owner work.
text-construction-surface-queue source_root=decompiled_root output="/tmp/roslyn-text-construction-inventory.json" limit="50":
  just text-construction-inventory {{quote(source_root)}} {{quote(output)}} ""
  {{python}} scripts/text_construction_surface_policy.py --inventory {{quote(output)}} --limit {{quote(limit)}}

# Verify agent-loop tools and dotfiles script availability.
tool-check:
  bash scripts/agent_cycle.sh tool-check

# Run ast-grep rule tests and scan using sgconfig.yml.
ast-grep-check:
  bash scripts/agent_cycle.sh ast-grep-check

# Run the ast-grep structural-search smoke fixture.
ast-grep-smoke:
  bash scripts/agent_cycle.sh ast-grep-smoke

# Run an ast-grep structural search.
sg lang pattern path=".":
  AST_GREP_PATTERN={{quote(pattern)}} AST_GREP_PATH={{quote(path)}} bash scripts/agent_cycle.sh sg {{quote(lang)}}

# Search C# structure. Defaults to the decompiled game source.
sg-cs pattern path="":
  AST_GREP_PATTERN={{quote(pattern)}} AST_GREP_PATH={{quote(path)}} bash scripts/agent_cycle.sh sg csharp

# Search Python structure.
sg-py pattern path="scripts":
  AST_GREP_PATTERN={{quote(pattern)}} AST_GREP_PATH={{quote(path)}} bash scripts/agent_cycle.sh sg python

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
