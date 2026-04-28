# 2026-04-28 Release Slice 0 Go/No-Go Evidence

## 結論

**Go/No-Go: No-Go.**

Steam Workshop readiness を Yes と判定するための fresh QudJP runtime evidence は取れなかった。Rosetta launcher 自体は起動したが、今回のローカル game settings では `QudJP` が disabled で、fresh `Player.log` に QudJP build marker / patch success / `DynamicTextProbe` / `SinkObserve` が出なかった。さらに `translation_checker.py` の自動 combat/death smoke は macOS console lock で起動前に停止した。

次 slice は、console を unlock し、`~/Library/Application Support/Freehold Games/CavesOfQud/Local/ModSettings.json` で `QudJP.Enabled=true` の状態を人間が確認してから、Rosetta で `translation_checker.py --flow final-smoke` を実行する必要がある。combat/death evidence が不足する場合のみ、追加で `translation_checker.py --flow combat-smoke` を狭く再実行する。

## Evidence

主な成果物は `.sisyphus/evidence/release-slice-0/` に保存した。

- `commands-transcript.md`: fresh runtime evidence attempt commands の exact transcript
- `dotnet-build-no-incremental.txt`: `dotnet build Mods/QudJP/Assemblies/QudJP.csproj --no-incremental`
  - result: success, 0 warnings, 0 errors
- `sync-mod-dry-run.txt`: `uv run python scripts/sync_mod.py --dry-run`
  - result: default Steam `StreamingAssets/Mods/QudJP` への同期対象を確認
- `sync-mod-real.txt`: `uv run python scripts/sync_mod.py`
  - result: command succeeded
- `translation-checker-combat-smoke.txt`: exact command:

  ```bash
  uv run python scripts/translation_checker.py \
    --skip-sync \
    --flow combat-smoke \
    --input-backend osascript \
    --flow-screenshot-dir .sisyphus/evidence/release-slice-0/combat-smoke \
    --attack-sequence ctrl+numpad6 \
    --attack-confirm-key "" \
    --message-log-chord "" \
    --death-attack-count 3 \
    --load-ready-timeout 90 \
    --launch-timeout 90 \
    > .sisyphus/evidence/release-slice-0/translation-checker-combat-smoke.txt \
    2>&1
  ```

  - result: `Error: macOS console session is locked. Unlock the Mac before running translation_checker.py.`
- `launch-rosetta-direct.stdout.txt`, `launch-rosetta-direct.exit.txt`, `Player-after-rosetta.log`
  - command:

    ```bash
    ./scripts/launch_rosetta.sh \
      > .sisyphus/evidence/release-slice-0/launch-rosetta-direct.stdout.txt \
      2> .sisyphus/evidence/release-slice-0/launch-rosetta-direct.stderr.txt &
    pid=$!
    printf '%s\n' "$pid" > .sisyphus/evidence/release-slice-0/launch-rosetta-direct.pid.txt
    sleep 45
    if kill -0 "$pid" 2>/dev/null; then
      kill -TERM "$pid" 2>/dev/null
    fi
    wait "$pid"
    printf '%s\n' "$?" > .sisyphus/evidence/release-slice-0/launch-rosetta-direct.exit.txt
    ```

  - result: launcher printed `Launching Caves of Qud via Rosetta (x86_64)...`; exit code `143`
- `player-log-markers-after.txt`
  - result: `INFO - Enabled mods:` は空。`QudJP`, build marker, `DynamicTextProbe`, `SinkObserve` はなし
- `ModSettings.snapshot.json`
  - result: `QudJP.Enabled=false`, `LLMOfQud.Enabled=true`
- `runtime-triage-fresh.json`
  - command: `uv run python scripts/triage_untranslated.py --log "$HOME/Library/Logs/Freehold Games/CavesOfQud/Player.log" --output .sisyphus/evidence/release-slice-0/runtime-triage-fresh.json`
  - result: `summary.total=0`, Phase F entries `0`
- `validate-xml-strict-baseline.txt`
  - command: `uv run python scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json`
  - result: success with the known baseline warning only

## Issue #363

Fresh runtime triage was collected, but it is not useful for QudJP coverage because QudJP was disabled in the game settings for this run.

Classification:

- actionable from current `Player.log`: none (`runtime-triage-fresh.json` total `0`)
- Phase F-only observations: none, because no QudJP Phase F emitters ran
- blocker: local `ModSettings.json` has `QudJP.Enabled=false`; `Player-after-rosetta.log` shows `INFO - Enabled mods:` with no enabled QudJP entry

## Slice Tracking

| Issue | Local status | Tracking note |
| --- | --- | --- |
| #370 | Closed with evidence | Local closeout evidence is recorded in `docs/reports/2026-04-28-issue-370-no-context-rebucket-closeout.md`: current static inventory and runtime triage artifacts have zero `<no-context>` rows; the older 48-row artifact is stale and superseded. |
| #422 | Done / APPROVED; final PR verified | The Annals batch was reviewed and APPROVED as one PR unit. Completed candidates for this slice remain `DiscoveredLocation#gospel`, `FoundGuild#gospel`, `CorruptAdministrator#gospel#case:0#if:then`, `CorruptAdministrator#gospel#case:1#if:then`, and `SecretRitual#gospel#if:then#arm:1`. Final verification passed: schema 63 candidates, merge 53 patterns clean, extractor golden 80 passed, focused L2 46 passed, and annals collision/markup/reachability 376 passed. `ChariotDrivesOffCliff#gospel#bl:else` was attempted, then rejected in review for spice phrase over-fragmentation and non-publication-quality output; it was reverted. The exact candidate ID is not present in `candidates_pending.json`, and no dictionary/L2 fixture leakage was found, so do not track it as pending from this evidence. The BattleItem broad `BattleItem#tombInscription` fallback is `skip`/superseded; no dictionary/L2 translation was added. Keep the broader Annals follow-up visible: remaining candidates still exist outside this completed slice and should stay in the Annals follow-up queue. |
| #437 | Keep open | Minimal glossary promotion is done. Do not close yet; this slice only records the promotion state, not full issue completion. |
| #433 | Keep open / blocker | Blocked for this release slice. No closure evidence was produced here. |
| #434 | Keep open / blocker | Blocked for this release slice. No closure evidence was produced here. |
| #438 | Keep open / blocker | Evidence classifies the empty `Tzedech` welcome text as upstream-preserved empty slot/noise, but the issue should remain open until the maintainer accepts that disposition. |

## Issue #376

Display-name / death-popup color markup corruption was **not reproduced** in this slice.

Reason:

- automated runtime path did not launch because the macOS console session was locked
- direct Rosetta launch produced a fresh log, but QudJP was disabled, so no QudJP death/display-name route ran
- no screenshot or live death popup evidence was captured

Static fallback:

- `dotnet-test-issue-376-l1.txt`: related L1 filter succeeded with 71 passed and 6 skipped
- `dotnet-test-issue-376-l2.txt`: related L2 filter succeeded with 157 passed
- `dotnet-test-color-catalog.txt`: color route/catalog filter succeeded with 3 passed and 4 skipped

Concern:

The skipped L1 tests are issue #376 scaffolds marked `issue-376 — production code pending; static analysis layer not yet implemented`. Therefore current evidence does not close #376. It only confirms adjacent existing tests still pass.

## Issue #438

Classification: **upstream-preserved empty slot/noise; not a QudJP missing translation.**

Evidence:

- `issue-438-empty-text-rg.txt`: the only `Conversations.jp.xml` empty text hit is `Mods/QudJP/Localization/Conversations.jp.xml:261`
- `issue-438-jp-context.txt`: this is `conversation ID="Tzedech"`, `node ID="Welcome"`, with `<text />`
- `issue-438-base-context.txt`: upstream base `Conversations.xml` has the same node as `<text></text>` at the matching `Tzedech` / `Welcome` slot
- `issue-438-warning-baseline.txt`: `scripts/validate_xml_warning_baseline.json` already records the warning as `Empty text in element 'text'`
- `validate-xml-strict-baseline.txt`: strict validation passes with the baseline warning

No localization asset change is recommended for #438 based on this evidence.

## Next Slice

Run the fresh readiness check only after:

1. macOS console session is unlocked.
2. QudJP is enabled in the game Mod Manager or `ModSettings.json`.
3. `LLMOfQud` is disabled or removed for the run, because fresh `build_log.txt` currently shows a compile error in that unrelated mod.
4. Run `translation_checker.py --flow final-smoke` through Rosetta as the main readiness pass. This should produce screenshots, QudJP marker/probe lines, and any death/combat-end evidence that the final-smoke flow collects.
5. Rerun `translation_checker.py --flow combat-smoke` only if final-smoke does not produce sufficient combat/death evidence or if #376 needs a narrower retry.
