# 2026-04-29 Release Slice 1 Runtime Smoke Evidence

## Conclusion

**Go/No-Go: No-Go for Workshop readiness.**

The requested `final-smoke` automation could not run because the macOS console
session was locked. Exact checker failure:

```text
Error: macOS console session is locked. Unlock the Mac before running translation_checker.py.
```

After the blocked checker attempt, a boot-only Rosetta fallback produced a
fresh `Player.log` with QudJP enabled and the expected QudJP runtime markers.
It did not load a save, capture screenshots, drive combat, or exercise the
death/display-name flow, so treat it as fresh QudJP-enabled boot evidence, not
as a completed runtime smoke.

## Evidence Root

All local evidence for this slice is under:

```text
.sisyphus/evidence/release-slice-1-runtime-smoke-20260428T150935Z/
```

Key files and results:

- `worktree-status.txt` — branch `codex/release-runtime-smoke-evidence`, HEAD `367fdfbcee7bd654f3df234b60287264dfafd82f`
- `console-lock-state.txt` — `IOConsoleLocked=True`
- `prerequisites-tools.txt` — Rosetta and tool path checks
- `game-paths.txt` — default Steam `CoQ.app`, executable, and `Assembly-CSharp.dll` paths present
- `dotnet-build.txt` — `dotnet build Mods/QudJP/Assemblies/QudJP.csproj`
- `sync-mod-dry-run.txt` — `python3.12 scripts/sync_mod.py --dry-run`
- `sync-mod-real.txt` — `python3.12 scripts/sync_mod.py`
- `translation-checker-final-smoke.txt` and `.exit.txt` — blocked checker run, exit `1`
- `direct-rosetta-launch-summary.txt` — boot-only fallback launched through Rosetta, terminated with SIGTERM after 45 seconds, exit `143`
- `direct-rosetta-after.txt` — fresh `Player.log` mtime `Apr 29 00:12:50 2026`
- `runtime-logs-after-direct/Player.log` — copied fresh runtime log
- `player-log-marker-counts-after-direct.txt` — QudJP marker summary
- `runtime-triage-after-direct.json` — fresh boot-only triage output
- `validate-xml-strict-baseline.txt` — XML validation output

## Prerequisites

| Check | Result |
| --- | --- |
| Worktree | correct worktree path and branch; no main checkout touched |
| Rosetta | `arch -x86_64 /usr/bin/true` exit `0` |
| Tool paths | `dotnet`, `uv`, `python3.12`, `osascript`, `screencapture`, `arch`, and `ioreg` found |
| Game binary | `$HOME/Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/MacOS/CoQ` present |
| Console session | blocked: `IOConsoleLocked=True` |
| Mod settings | `~/Library/Application Support/Freehold Games/CavesOfQud/Local/ModSettings.json` was not present; fresh boot log still reported QudJP enabled |

## Build and Sync

- `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` succeeded with `0` warnings and `0` errors.
- `python3.12 scripts/sync_mod.py --dry-run` is supported and listed the expected 124-file transfer set.
- `python3.12 scripts/sync_mod.py` completed successfully and deployed the same 124-file transfer set to the platform default QudJP mod directory.

## Runtime Attempt

The requested final smoke command used `--skip-sync`, `--input-backend
osascript`, the requested combat fallback attack sequences,
`--death-attack-count 30`, and `--require-combat-evidence`.

Exact command run:

```bash
python3.12 scripts/translation_checker.py \
  --skip-sync \
  --flow final-smoke \
  --input-backend osascript \
  --flow-screenshot-dir .sisyphus/evidence/release-slice-1-runtime-smoke-20260428T150935Z/final-smoke \
  --attack-sequence ctrl+numpad6 \
  --attack-sequence backslash,right \
  --death-attack-count 30 \
  --require-combat-evidence \
  > .sisyphus/evidence/release-slice-1-runtime-smoke-20260428T150935Z/translation-checker-final-smoke.txt \
  2>&1
```

After the checker returned, its shell exit status was saved separately:

```bash
checker_exit=$?
printf '%s\n' "$checker_exit" \
  > .sisyphus/evidence/release-slice-1-runtime-smoke-20260428T150935Z/translation-checker-final-smoke.exit.txt
```

Result:

- exit: `1`
- blocker: locked macOS console session
- screenshots: none; the locked console stopped the checker before launch/input,
  so screenshot generation for `--flow-screenshot-dir` was not reached and an
  empty or absent `final-smoke/` directory is expected for this failure mode

`combat-smoke` was not useful in the same locked state.

## Boot-Only Rosetta Fallback

To preserve some runtime signal, `scripts/launch_rosetta.sh` ran for 45 seconds
and was then terminated with SIGTERM. `direct-rosetta-launch-summary.txt`
records the launch summary and exit `143`, which reflects that 45-second
termination window. This is not a substitute for final smoke.

Fresh `Player.log` evidence after that fallback:

- log mtime: `Apr 29 00:12:50 2026`
  (`runtime-logs-after-direct/Player.log`, `direct-rosetta-after.txt`)
- marker counts: `[QudJP] Build marker` `1`, `Enabled mods` `1` with
  `Caves of Qud 日本語化`, `DynamicTextProbe` `18`, `SinkObserve` `2`,
  `FinalOutputProbe` `11`, `mprotect` `0`, `ERROR` `0`, `LLMOfQud` `0`
  (`player-log-marker-counts-after-direct.txt`)

Fresh boot-only triage:

```json
{
  "total": 0,
  "static_leaf": 0,
  "route_patch": 0,
  "logic_required": 0,
  "preserved_english": 0,
  "unexpected_translation_of_preserved_token": 0,
  "unresolved": 0
}
```

## Issue Disposition

| Issue | Disposition |
| --- | --- |
| #363 runtime smoke | Still blocked for full smoke. Boot-only triage is clean, but no save/combat/death flow ran. |
| #376 display-name/color-tag runtime evidence | Not closed. No death popup, combat death, or display-name color-tag screenshot/log evidence was captured. |
| #400 release roll-up | Still No-Go. Fresh QudJP-enabled boot evidence exists, but Workshop readiness requires an unlocked-console final smoke with screenshots and combat/death evidence. |

## XML Validation

`python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict
--warning-baseline scripts/validate_xml_warning_baseline.json` exited `0`.
The only warning was the known baseline warning in `Conversations.jp.xml`:
`Empty text in element 'text'`.

## Next Required Steps

1. Unlock the macOS console session.
2. Re-run the same `translation_checker.py --flow final-smoke` command from
   this slice.
3. Only run `combat-smoke` if final smoke starts successfully but lacks combat,
   death, or #376-specific evidence.
