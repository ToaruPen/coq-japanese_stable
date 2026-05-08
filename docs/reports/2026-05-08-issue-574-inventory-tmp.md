# Issue 574 Inventory TMP Zero-Character Investigation

## Identity

- Issue: https://github.com/ToaruPen/coq-japanese_stable/issues/574
- Branch: `codex/issue-574-inventory-tmp`
- Base: `origin/main` at `7bde26c`
- Runtime log inspected: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- Runtime log mtime: `2026-05-08 08:04:12 JST`

## Finding

The upstream inventory route is:

1. `Qud.UI.InventoryLine.setData(FrameworkDataElement)` assigns item names through `text.SetText(inventoryLineData.displayName)`.
2. `XRL.UI.UITextSkin.SetText(string)` stores the visible text and applies it to the underlying `TextMeshProUGUI`.
3. `InventoryLineTranslationPatch` translates the item display name and writes it back through the owner text route.
4. `InventoryLineFontFixer` treats `TextMeshProUGUI.textInfo.characterCount == 0` as a failed original TMP render.
5. `InventoryLineActiveTextRefreshPatch` schedules delayed repair only when the active item line has no active replacement and the original TMP refresh still fails.
6. `DelayedInventoryLineRepairScheduler` calls `TextShellReplacementRenderer`, which creates `QudJPReplacementText` under the original `TextShell` and preserves it as the intended fallback display path when it renders non-empty text.

The fresh runtime log contains inventory-open evidence and original TMP zero-character evidence:

- QudJP loaded from the local game install mod path.
- Build marker: `ui-child-snapshot-v3`, assembly version `0.1.0.0`.
- `InventoryLineTranslationPatch` and inventory status-screen rows appear in the log.
- `InventoryLineReplacementDisable/v1` rows report `path='Modes/Item/TextShell/Text'`, `originalChars=0`, and `originalPages=0`.
- No `MissingMethodException`, `Vector2.get_x`, `Vector2.get_y`, `MODWARN`, or QudJP exception was found.

The same log did not contain `InventoryLineReplacement/v1`, `InventoryLineReplacementStateNextFrame/v1`, or `InventoryLineItemProbe/v1`. That made the log insufficient for closing #574, because it proved the original TMP row stayed empty but did not prove the chosen replacement display path in the same fresh run.

## Decision

Do not treat the original inventory TMP `charCount=0/pageCount=0` state as solved without Unity runtime evidence. Current source and prior runtime notes show that the original TMP can contain non-empty text while producing zero geometry in this inventory subtree. The supported behavior for this branch is therefore:

- original item-row TMP with `enabled == true`, `activeInHierarchy == true`, and `characterCount == 0` is a repair candidate;
- a successful `QudJPReplacementText` render is the intended display path for that row;
- once a matching active replacement exists, active-line refresh avoids rescheduling the original TMP path;
- fresh runtime evidence must include the replacement success markers before #574 is closed.

## Branch Change

`DelayedInventoryLineRepairScheduler` now logs bounded replacement evidence after successful replacement repair:

- `InventoryLineReplacement/v1`
- `InventoryLineReplacementStateNextFrame/v1`
- `InventoryLineItemProbe/v1`

This keeps the evidence path tied to actual `replaced > 0` success instead of logging every attempted repair. The output is capped to prevent first-open inventory spam from becoming unbounded.

After rebasing onto `origin/main`, those evidence logs are emitted only when `RuntimeDiagnostics.VerboseProbesEnabled` is true, preserving the dev-build probe gating introduced for runtime diagnostics.

## Final Runtime Evidence

A follow-up smoke after deploying this branch confirmed the intended replacement path:

- Runtime log mtime: `2026-05-09 00:46:09 JST`
- `InventoryLineReplacement/v1`: 17 rows
- `InventoryLineReplacementStateNextFrame/v1`: 16 rows
- `InventoryLineItemProbe/v1`: 15 rows
- `InventoryLineReplacementFailure/v1`: 0 rows
- `MissingMethodException`: 0 rows
- `Vector2.get_x` / `Vector2.get_y`: 0 rows
- `MODWARN`: 0 rows
- `QudJP: .*failed`: 0 rows

Representative evidence shows the original inventory `TextShell/Text` disabled with `chars=0 pageCount=0` while the sibling `QudJPReplacementText` is active, enabled, and rendering `chars>0 pageCount=1`.

## Verification Gate

Static/test verification can prove the contract and target methods. Before release, repeat a fresh runtime smoke after any further change that touches inventory replacement rendering:

1. Build and deploy the branch DLL.
2. Launch Caves of Qud and open inventory.
3. Confirm item names are visible.
4. Confirm the fresh `Player.log` has no QudJP exceptions, `MissingMethodException`, `Vector2.get_x`, or `Vector2.get_y`.
5. Confirm the fresh `Player.log` includes at least one successful `InventoryLineReplacement/v1` row and the corresponding `InventoryLineReplacementStateNextFrame/v1` or `InventoryLineItemProbe/v1` evidence.
