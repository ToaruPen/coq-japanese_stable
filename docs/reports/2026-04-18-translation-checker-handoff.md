# 2026-04-18 Translation Checker Handoff

## Purpose

This handoff captures the current state of the Caves of Qud Japanese localization runtime verification work so a new session can continue from the same point and verify the implemented automation path. Do not restart the investigation from scratch unless fresh evidence contradicts the notes below.

## Repository State

Workspace:

```text
/Users/toarupen/Dev/coq-japanese_stable
```

Relevant uncommitted changes at handoff time:

```text
 M docs/superpowers/specs/2026-03-28-display-path-ownership-design.md
 M pyproject.toml
 M scripts/README.md
 D scripts/tests/test_verify_inventory.py
 D scripts/verify_inventory.py
?? docs/reports/2026-04-18-translation-checker-handoff.md
?? scripts/tests/test_translation_checker.py
?? scripts/translation_checker.py
?? tmp/
```

The deleted and untracked `verify_inventory` / `translation_checker` files are an intentional rename, not a removal of functionality.

## Implemented Changes

- Renamed `scripts/verify_inventory.py` to `scripts/translation_checker.py`.
- Renamed `scripts/tests/test_verify_inventory.py` to `scripts/tests/test_translation_checker.py`.
- Updated references in:
  - `pyproject.toml`
  - `scripts/README.md`
  - `docs/superpowers/specs/2026-03-28-display-path-ownership-design.md`
- Added `--manual-load` for the Computer Use path. This starts the Rosetta launch and then waits for the operator to load the save manually before continuing final-smoke captures.
- Added `--input-backend {cgevent,osascript}`.
- Added an osascript keyboard backend using macOS System Events.
- Added numpad key-code support.
- Updated final-smoke defaults based on observed runtime behavior:
  - `--npc-poi-key d`
  - `--attack-chord ctrl+numpad6`
  - `--attack-confirm-key ""`
- Updated `scripts/README.md` with the Rosetta + Computer Use + osascript verification route.
- Added tests for:
  - numpad key-code mapping
  - osascript key-code script generation
  - parser defaults for final-smoke control options

## Verification Already Run

Commands that passed after the script/doc/test changes:

```bash
uv run --with pytest pytest scripts/tests/
uv run --with ruff ruff check scripts/ pyproject.toml
uv run python scripts/translation_checker.py --help
```

Observed results:

```text
290 passed in 2.80s
All checks passed!
```

Earlier targeted test result:

```text
27 passed
```

## Build And Deployment Already Run

The latest mod was rebuilt and redeployed earlier in the session:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj --no-incremental
python3.12 scripts/sync_mod.py
```

The sync transferred:

```text
Assemblies/QudJP.dll
```

## Valid Runtime Evidence

An initial direct/scripted launch produced `mprotect returned EACCES` and patch failures. Treat that run as invalid and ignore it for localization conclusions.

The valid run was launched through Rosetta:

```bash
./scripts/launch_rosetta.sh
```

For the valid Rosetta run, the relevant evidence window had:

```text
[QudJP]: 170
DynamicTextProbe/v1: 29
SinkObserve/v1: 15
missing key: 46
MessagePatternTranslator: no pattern: 5
mprotect returned EACCES: 0
Failed to apply patch: 0
```

Valid screenshot directory:

```text
/tmp/qudjp-runtime-valid-20260418T000944Z
```

Screenshots captured there:

```text
00-main-menu.png
01-load-game.png
02-after-load.png
03-equipment-inventory.png
04-tinkering.png
05-journal.png
06-quests.png
07-reputation.png
08-message-log.png
09-skills.png
10-attributes-powers.png
11-system-menu-popup.png
12-poi-popup.png
13-npc-conversation.png
14-attack-log-after-ctrl-right.png
15-attack-log-after-ctrl-kp6.png
16-look-popup.png
17-fire-popup.png
18-throw-or-fire-targeting.png
```

The test save used in manual validation:

```text
Name: Nimet
Level: 1
GameMode: Classic
Location: Joppa
Turn: 97
```

The save was restored to that state after the manual runs.

## Observed Translation Coverage

Current runtime localization is mixed, not complete.

Observed Japanese coverage:

- Main menu is mostly Japanese.
- Many inventory item names and item categories are Japanese.
- Tinkering empty-state text includes Japanese.
- Message log contains many translated pass-by messages.
- Tam's NPC name is translated as `タム、ドロマド商団 [座っている]`.
- Fire/no missile weapon popup is Japanese.
- Attack log can show Japanese output, for example:

```text
燃え盛る 青銅の短剣ではタム、ドロマド商団の装甲を貫けない。[2]
```

Observed remaining English / mixed areas:

- Main-menu bottom hints remain English.
- Load screen still shows `LOAD GAME`, `Location`, and `delete`.
- HUD has English such as `Message log` and bottom ability labels.
- Inventory has mixed labels such as `EQUIPMENT`, `lbs.`, search placeholders, and helper text.
- Tinkering still has English material names.
- Reputation has mixed faction names and descriptions.
- Skills / attributes screens still contain English such as `Tinkering`, `wayfaring`, and `Starting Cost`.
- Tam's conversation body and choices are mixed, including `[begin trade]` and `[End]`.
- Look popup is largely English, including `It's you` and `Physical features`, although some equipment text is translated.

## osascript / Accessibility Findings

The initial System Events path failed with:

```text
osascript is not allowed to send keystrokes
```

After Accessibility permission was enabled for the relevant app process, these direct checks succeeded:

```bash
osascript -e 'tell application "System Events" to key code 53'
osascript -e 'tell application "System Events" to key code 53 using control down'
```

Do not repeat or log any local account password in follow-up work.

The osascript attack route was manually validated against the game:

- `Ctrl+Right` via osascript did not produce the desired attack.
- `Ctrl+numpad6` via osascript did produce the desired attack log.

Evidence directory:

```text
/tmp/qudjp-osascript-attack-20260418T011845Z
```

Screenshots captured there:

```text
00-main-menu.png
01-after-load.png
02-before-osascript-ctrl-right.png
03-after-osascript-ctrl-right.png
04-after-osascript-ctrl-keypad6.png
05-message-log-via-osascript-ctrl-m.png
```

Note: `Ctrl+m` via osascript did not visibly open a message-history popup in the last manual screenshot, but the visible sidebar still captured the attack log.

## Current Automation Capabilities

The automation can now cover these parts of the intended final-smoke path:

1. Launch Caves of Qud through the Rosetta launcher.
2. Support manual Computer Use save loading with `--manual-load`.
3. Capture the loaded game screen.
4. Capture inventory / equipment and related tabs.
5. Open the POI popup with the observed default key `d`.
6. Reach Tam from the POI flow in the test save.
7. Capture an NPC conversation screen.
8. Send a working classic-style attack chord with osascript: `ctrl+numpad6`.
9. Capture evidence screenshots under a specified flow directory.
10. Preserve and restore the test save when `--save-dir`, `--save-backup-dir`, and `--restore-save` are used.

## What Still Needs Verification

The post-change full `translation_checker.py --flow final-smoke --input-backend osascript` run has not yet been completed end-to-end. Manual pieces were verified, but the integrated automated route still needs a fresh run.

Recommended next command after confirming the save path:

```bash
uv run python scripts/translation_checker.py \
  --skip-sync \
  --flow final-smoke \
  --manual-load \
  --input-backend osascript \
  --flow-screenshot-dir /tmp/qudjp-l3-final-smoke-computer-use \
  --load-ready-timeout 300
```

If save restoration is desired, add the appropriate local save paths:

```bash
  --save-dir "<Caves of Qud save directory>" \
  --save-backup-dir "/tmp/qudjp-save-backup-final-smoke" \
  --restore-save
```

Follow-up verification should confirm:

- The Rosetta launcher is the actual launch path.
- The loaded-save signal is observed or the manual-load path resumes cleanly.
- Screenshot stages are created in order.
- Inventory tabs are captured and visibly checked for Japanese / English coverage.
- NPC conversation is captured.
- Attack input produces a visible combat log.
- The message-log capture route is either fixed or intentionally replaced with the visible sidebar log capture.
- Death-flow automation is still incomplete and should be stabilized separately.

## Known Risks / Blockers

- `Ctrl+m` did not visibly open message history during the osascript manual run. If this persists, either discover the correct key path or make the sidebar combat log capture the supported evidence route.
- Death-by-repeated-attack is not yet reliable as an automated final-smoke stage. The attack input is known to work, but death screenshot automation still needs tuning.
- Player.log monitoring showed unexpected behavior in one manual Rosetta run where visible localization was active but the fresh log delta was empty. If the integrated script waits forever for the load-ready probe, investigate log truncation/offset handling before changing gameplay logic.
- `tmp/` contains prior investigation evidence and backups. Do not delete it unless explicitly asked.

## Recommended New-Session Flow

1. Read repository instructions first:

```bash
sed -n '1,220p' AGENTS.md
sed -n '1,220p' docs/RULES.md
sed -n '1,220p' docs/test-architecture.md
sed -n '1,220p' scripts/AGENTS.md
```

2. Inspect the current diff without reverting user work:

```bash
git status --short
git diff -- scripts/translation_checker.py scripts/tests/test_translation_checker.py scripts/README.md pyproject.toml docs/superpowers/specs/2026-03-28-display-path-ownership-design.md
```

3. Re-run static and unit checks:

```bash
uv run --with pytest pytest scripts/tests/
uv run --with ruff ruff check scripts/ pyproject.toml
uv run python scripts/translation_checker.py --help
```

4. Run the integrated final-smoke path with `--manual-load --input-backend osascript`.

5. Use Computer Use only where script automation cannot reliably load or visually validate the game state. Preserve all screenshots as evidence.

## Follow-up Addendum

Later automated verification showed two corrections to this handoff:

- Main-menu automation should use the in-game `Continue` shortcut `c`, then `space` for the save picker. Arrow-based selection can fall through to New Game when the Down input is dropped.
- Combat automation should use `backslash,right`, which opens `Force attack in a direction` and attacks east. The successful focused evidence run is:

```text
/tmp/qudjp-l3-combat-smoke-default-20260418T071500Z
```

That run loaded the `Nimet` QudJP save, captured the force-attack direction prompt, produced the translated non-penetrating Tam attack log, and exited with combat evidence.
