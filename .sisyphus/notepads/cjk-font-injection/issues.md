# Issues — CJK Font Injection

## 2026-03-15 Inventory / Compare UI status
- `compare` tooltip/popup は item title と本文が表示される段階まで回復した。
- ただし compare 側は、内容量に応じて決まる動的矩形の形が崩れる場合がある。
- `inventory` 右側の item-name 表示は未解決で、同時点のスクリーンショットでは本文が出ていない。
- したがって、現時点の優先順位は `inventory` 表示復旧が先、compare は表示回復済みとして矩形安定化を次点に置く。
- 上記状態のビルドは再配備済み（`python3 scripts/sync_mod.py` 実行済み）。
- 次回確認時は `Player.log` の `InventoryLineReplacement/v1` / `InventoryLineReplacementStateNextFrame/v1` / `legacyReplacement[` / `ComparePopupTextRepair/v1` / `CompareSceneProbe/v1` を優先して見る。

## 2026-03-15 Inventory / Compare UI findings (latest)
- `compare` は `Qud.UI.BaseLineWithTooltip.StartTooltip(...)` を起点にした probe へ切り替えたことで、`UI Manager/Tooltip Container/DualPolatLooker/...` と `.../PolatLooker/...` の可視 tooltip root を直接観測できるようになった。
- 最新 `Player.log` では `CompareSceneProbe/v1` / `ComparePopupContainerProbe/v1` が `DisplayName` と `LongDescription` を直接報告しており、compare 側で item title と説明文が表示されることをログでも確認できる。
- `inventory` は依然未解決。`InventoryLineReplacementFailure/v1` では元 leaf `Modes/Item/TextShell/Text` と replacement `QudJPReplacementText` の両方が `enabled=True` / `activeInHierarchy=True` なのに `chars=0 pageCount=0` で、ASCII `TEST` sentinel でも `sentinelChars=0` になる。
- したがって inventory の残課題は翻訳文字列や CJK glyph ではなく、inventory 行 subtree 固有の TMP 描画失敗である可能性が高い。
- legacy fallback は stopgap と判断して無効化済み。現在の検証版は `QudJPReplacementText` を `Item` 直下ではなく `TextShell` 配下に戻し、同じ subtree/state 条件で TMP が復活するかを確認している。
- 次回確認時は `Player.log` の `InventoryLineReplacementFailure/v1` / `InventoryLineReplacementStateNextFrame/v1` / `sentinelChars=` / `ComparePopupContainerProbe/v1` / `CompareSceneProbe/v1` を優先して見る。

## 2026-03-15 Inventory / Compare UI findings (latest log refresh)
- 最新 `Player.log` では build marker `ui-child-snapshot-v3` と `FontManager: CJK font registered` が出ており、`build_log.txt` も `Success :)` で止まっているため、今回の観測対象ビルド配備自体は正常。
- `InventoryRenderProbe/v6` は `sample='た' hasSample=True atlasMode='Dynamic' atlasCount=1` を返しているため、inventory item-name 不可視の主因は glyph 未収録ではない。
- `InventoryRenderProbe/v6` の時点では inventory 行の TMP は `enabled=True` だが `active=False`、一方で delayed probe では `InventoryScrollerLine` subtree 自体は `activeInHierarchy=True` に遷移している。初期 `setData` 時点の非活性状態と、表示フレーム到達後の `TextShell/Text` failure は分けて考える必要がある。
- `CompareBranchProbeDelayed/v1[inventory]` は repair 後に inventory subtree の TMP 数が `122 -> 132` へ増えており、`QudJPReplacementText` 自体は生成されている。それでも `InventoryLineReplacementStateNextFrame/v1` では replacement が `chars=0 pageCount=0 active=False enabled=False` に戻る。
- 追加観測として `DelayedInventoryLineRepairScheduler` から `InventoryLineReplacementLeafState/v1` / `InventoryLineReplacementSentinel/v1` / `InventoryLineReplacementDirectFontSentinel/v1` を出すようにした。次回確認ではこの 3 系列を最優先で見て、`font/material/internalFont/sharedMaterial/stencil/submesh` の差分を確認する。
- 上記ビルドは `python3 scripts/sync_mod.py` で再配備済み。

## 2026-03-15 Inventory / Compare UI findings (post-restart probe result)
- 再起動後の `Player.log` でも `InventoryLineReplacementSentinel/v1` は全対象行で `sentinel='TEST' sentinelChars=0 sentinelPageCount=0` のままだった。ASCII sentinel でも 0 なので、CJK glyph や翻訳文字列内容は原因ではない。
- `InventoryLineReplacementDirectFontSentinel/v1` は `fontMatchesPrimary=True internalFontMatchesPrimary=True` を返しており、`FontManager.ForcePrimaryFont(...)` 自体は `TextShell/Text` の内部 `m_fontAsset` まで届いている。
- 同 probe では `preMaterial/postMaterial='TextMeshPro/Mobile/Distance Field'`、`stencil=0`、`faceA=1`、`canvasA=1`、`rect=734.6x21` ないし `761.75x21` で、material/stencil/canvas alpha/rect zero が直近原因である可能性は下がった。
- `InventoryLineReplacementLeafState/v1` では一部行に `subMeshes=2` + `sub0.font='SourceCodePro-Regular SDF'` が残るが、submesh なしの行でも `sentinelChars=0` なので failure 条件の本体は submesh の有無だけでは説明できない。
- `InventoryLineReplacementStateNextFrame/v1` では replacement `QudJPReplacementText` が再度 `active=False enabled=False chars=0` に戻されるため、inventory 行の後続更新処理が replacement を潰している可能性が高い。次の切り分け候補は `Qud.UI.InventoryLine.Update()` / `SelectableTextMenuItem` 系のフレーム後更新。

## 2026-03-15 Inventory / Compare UI findings (next instrumentation)
- `InventoryLineUpdateProbePatch` の no-op を置き換え、`InventoryLineUpdateProbe/v1` を追加した。failing leaf と `QudJPReplacementText` の `activeSelf/activeInHierarchy/enabled/chars/pages/rect/parent/text` を、状態が変化した時だけ 1 行にまとめて出す。
- `TextShellReplacementRenderer` には `InventoryLineReplacementDisable/v1` を追加した。`TryDisableReplacement(...)` に入る直前の `original.enabled` / `activeSelf` / `activeInHierarchy` / `chars` / `pages` / `text` を一度だけ出す。
- `SelectableTextMenuItemObservability` は `GetComponent<TMP_Text>()` から `GetComponentInChildren<TMP_Text>(includeInactive: true)` に切り替えた。inventory row が `SelectableTextMenuItem` を経由している場合でも、leaf に近い TMP を拾いやすくした。
- 上記 instrumentation 追加後、`dotnet build Mods/QudJP/Assemblies/QudJP.csproj` と `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は再度成功した。

## 2026-03-15 Inventory / Compare UI findings (update probe result)
- 最新 `Player.log` の `InventoryLineUpdateProbe/v1` では、visible inventory 行 (`#1`, `#3`, `#5`, `#7`, `#8`, `#9`) が `frame=8081` の時点で `leaf active=True enabled=True chars=0` を示している。つまり `InventoryLine.Update()` に入る時点で元 leaf は既に壊れている。
- 同じ行は `frame=8084` で `replacement[1] path='Modes/Item/TextShell/QudJPReplacementText' activeSelf=False active=False enabled=False chars=0` を示しており、replacement も Update 観測時には既に無効化済みだった。
- 今回のログには `InventoryLineReplacementDisable/v1` が一度も出ていない。したがって `TryDisableReplacement(...)` 分岐が replacement を潰している根拠は得られず、少なくとも今回の failure は disable-branch 由来ではない。
- 代わりに `InventoryLineReplacementFailure/v1` は全可視行で継続しており、`replacement activeInHierarchy=True` の直後に `chars=0` のまま失敗している。つまり replacement は生成直後の failure path (`replacement.textInfo.characterCount == 0`) で自前に `enabled=false` / `SetActive(false)` へ落としている可能性が高い。
- これにより「後続 Update が replacement を潰している」という前の主仮説は弱まり、次の焦点は `SyncReplacement(...)` 後から failure 判定までの immediate creation path に移った。

## 2026-03-15 Inventory / Compare UI findings (immediate path material retry instrumentation)
- `InventoryLineReplacementFailure/v1` を拡張し、replacement 側の `fontMaterial` / `internalFont` / `internalSharedMaterial` / `sharedEqualsFontMaterial` を出すようにした。
- あわせて failure probe 内で diagnostic retry を追加し、`replacement.fontSharedMaterial = replacement.font.material` を一時適用したときの `materialRetryCurrentChars/materialRetryCurrentPages/materialRetrySentinelChars/materialRetrySentinelPages` を出すようにした。
- これで次回ログでは「shared material を font.material に合わせるだけで replacement が復活するか」を、挙動変更なしで確認できる。
- 上記変更後も `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` と `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は通過し、`python3 scripts/sync_mod.py` で再配備済み。

## 2026-03-15 Inventory / Compare UI findings (material retry result)
- 最新 `InventoryLineReplacementFailure/v1` では、全可視行で `sharedEqualsFontMaterial=True` かつ `fontMaterial='TextMeshPro/Mobile/Distance Field'`、`internalSharedMaterial='TextMeshPro/Mobile/Distance Field'` だった。replacement は shared material と font.material の不一致状態にはなっていない。
- 同じ probe で `materialRetryCurrentChars=0` / `materialRetrySentinelChars=0` が全行で継続したため、`replacement.fontSharedMaterial = replacement.font.material` の強制だけでは failure は改善しない。
- したがって immediate failure の主因を shared material 差し替えに求める根拠は薄くなった。少なくとも現時点では font/material pair 自体は failure の説明力が低い。
- 一方で replacement の `font=''` / `internalFont=''` は依然として空欄のままだが、同 probe では `sharedEqualsFontMaterial=True` なので、「font asset reference が本当に失われているのか」「diagnostic string 取得が空欄になるだけなのか」を切り分ける必要がある。
- 次の焦点は `GetOrCreateReplacement(...)` 直後から `SyncReplacement(...)` / `ForcePrimaryFont(...)` 後までの replacement object 自体の内部参照（font asset, textComponent state, text parsing flags）を、failure 判定前に直接取ること。

## 2026-03-15 Inventory / Compare UI findings (creation-stage instrumentation)
- `InventoryLineReplacementFailure/v1` に creation-stage snapshot を追加した。対象 stage は `afterGetOrCreate` / `afterSync` / `beforeForceMesh` / `afterForceMesh` の 4 つ。
- 各 stage では replacement の `activeSelf/activeInHierarchy/enabled/havePropertiesChanged/chars/pages/rect/overflow/wrap/maxVisibleCharacters/maxVisibleLines/pageToDisplay/canvasA/stencil/faceA/font/internalFont/material/internalSharedMaterial/subMeshes` を記録する。
- これで次回ログでは「GetOrCreate 直後に既に font/internalFont が空なのか」「SyncReplacement/ForcePrimaryFont 後に空になるのか」「ForceMeshUpdate 後にだけ壊れるのか」を 1 本の failure probe で追える。
- 上記変更後も `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` と `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は成功し、`python3 scripts/sync_mod.py` で再配備済み。

## 2026-03-15 Inventory / Compare UI findings (latest log + single tooltip)
- 最新 `InventoryLineReplacementFailure/v1` では `afterSync` の時点で `chars>0 pageCount=1` が取れている一方、`afterTextAssign` / `afterActivate` / `afterDirty` ではすぐ `chars=0 pageCount=0` に戻る。少なくとも replacement は `GetOrCreateReplacement(...)` 直後から壊れているわけではなく、`SyncReplacement(...)` 後の再代入・再活性化フェーズで崩れている。
- しかも `afterSync` でも `font=''` / `internalFont=''` のまま `chars>0` なので、diagnostic 上の空欄だけをもって「font asset 未設定」と断定はできない。現時点では `chars` が崩れるトリガーの方が重要。
- 単体 tooltip（比較なし）のログでは `DescriptionInventoryActionProbe` 後に `ComparePopupContainerProbe/v1` が `UI Manager/PopupMessage/MenuControll/.../Content/Message=''` を報告しており、本文フィールドが空文字のまま表示されている。スクリーンショットの「比較なし tooltip で本文が欠落する」症状と一致する。
- 一方 compare tooltip は `UI Manager/Tooltip Container/PolatLooker/.../LongDescription` と `.../DisplayName` に本文・タイトルが入っている。つまり比較あり／なしで UI root が異なり、単体 tooltip は `PopupMessage` 経路、compare tooltip は `Tooltip Container/PolatLooker` 経路を通っている。
- 既存コード上でも `BaseLineWithTooltipStartTooltipPatch` は `ScheduleCompareSceneProbe()` しか呼ばず、単体 tooltip 専用の修復処理はない。さらに `CompareSceneProbe/v1` の token 条件は compare 専用なので、single tooltip は観測 blind spot になりやすい。
- 次回に向けて `LookTooltipContentPatch` に `LookTooltipContentProbe/v1` を追加し、UI に渡る前の tooltip content 文字列自体を直接記録するようにした。また inventory creation path も `afterTextAssign` / `afterActivate` / `afterDirty` へ細分化した。

## 2026-03-15 Inventory / Compare UI findings (analysis-mode synthesis)
- 最新ログでも inventory failure は `afterSync` / `afterTextAssign` では `chars>0`、`afterActivate` で `chars=0` に落ちる。Oracle の所見も「font/material ではなく activation 後の UI lifecycle 側」を最有力としており、現時点の主仮説と一致した。
- 最新 compare tooltip run では `Tooltip Container/DualPolatLooker` と `Tooltip Container/PolatLooker` の `DisplayName` / `LongDescription` が `chars>0` で安定しており、compare 側は live container 上で生存している。
- 一方 single tooltip は latest run では `LookTooltipContentProbe/v1` も `DescriptionInventoryActionProbe` も出ておらず、今回のログだけでは未再現。したがって「前回の PopupMessage 空本文」は履歴上の事実だが、最新 run のみでは未確定と扱う。
- blind spot を埋めるため、`ComparePopupTextFixer` に `TryRepairAnyActivePopup(...)` を追加し、header token に依存せず `PopupMessage` / `Tooltip Container` / `PolatLooker` 配下の active popup root を拾って `PopupContainerTextRepair/v1` を出すようにした。`DelayedSceneProbeScheduler` は compare repair に続いてこの generic popup repair も毎 attempt 実行する。
- これにより次回の single tooltip run では、`LookTooltipContentProbe/v1` が出なくても `PopupContainerTextRepair/v1` と `ComparePopupContainerProbe/v1` から `PopupMessage` 側 live text の有無を追える。

## 2026-03-15 Inventory / Compare UI findings (tooltip recovery + afterCanvasForce probe)
- 最新ログでは single tooltip も compare tooltip も live text が埋まっていた。single tooltip は `PopupContainerTextRepair/v1` で `Tooltip Container/PolatLooker/VLayout` 配下の `DisplayName chars=16` / `LongDescription chars=141` / `WoundLevel chars=9` が確認でき、compare tooltip も `DualPolatLooker` 配下で左右両カラムの `DisplayName` / `LongDescription` が `chars>0` だった。
- これで現状の未解決点は inventory のみ。`InventoryLineReplacementFailure/v1` は依然 `replaced=0` で、`afterSync` / `afterTextAssign` は成功し `afterActivate` で 0 化する。
- explore + librarian + oracle の所見は一致しており、現時点の最有力仮説は「inactive -> active の同フレームで `characterCount` を読みすぎており、TMP/canvas 側の再構築前の一時的 0 を failure と誤判定している」こと。
- この仮説を切るため、`TextShellReplacementRenderer` の activation 直後に `TryForceCanvasUpdate()` を追加し、新しい creation stage `afterCanvasForce` を記録するようにした。reflection 経由で `Canvas.ForceUpdateCanvases()` を呼び、依存を増やさずに end-of-frame 相当の canvas 更新を同期実行する。
- 上記変更は `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` / `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` を通過し、`python3 scripts/sync_mod.py` で再配備済み。次回の最優先確認点は `InventoryLineReplacementFailure/v1` の `afterCanvasForce` が `chars>0` に戻るかどうか。

## 2026-03-15 Inventory / Compare UI findings (afterCanvasForce result + activation split)
- 最新 inventory run では全対象行で `afterSync>0`, `afterTextAssign>0`, `afterActivate=0`, `afterCanvasForce=0`, `afterForceMesh=0` だった。`Canvas.ForceUpdateCanvases()` を挟んでも回復しないため、same-frame の一時的 0 を早取りしているだけという仮説は大きく後退した。
- この結果を受けて、次の切り分けは `enabled=true` と `SetActive(true)` のどちらが真のトリガーかを分離することにした。
- `TextShellReplacementRenderer` の順序を検証版に変更し、`replacement.gameObject.SetActive(true)` を先に実行して `afterSetActive` / `afterSetActiveCanvasForce` を採り、その後で `replacement.enabled = true` を実行して `afterEnable` / `afterEnableCanvasForce` を採るようにした。
- これで次回ログでは、GameObject 活性化だけで 0 化するのか、TMP component の enable で 0 化するのかを直接判定できる。
- この順序分離版も `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` / `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` を通過し、`python3 scripts/sync_mod.py` で再配備済み。

## 2026-03-15 Inventory / Compare UI findings (enable boundary confirmed)
- 最新 run の順序分離ログでは全対象行で `afterSetActive>0` / `afterSetActiveCanvasForce>0` のまま維持され、`afterEnable=0` / `afterEnableCanvasForce=0` / `afterForceMesh=0` へ落ちた。つまり破綻トリガーは `SetActive(true)` ではなく `replacement.enabled = true` 側である。
- これで inventory failure は「TMP component enable 時の内部状態リセット」寄りにさらに絞られた。GameObject activation 自体は問題ではない。
- 次の切り分けとして、`enabled=true` 直後に `FontManager.ForcePrimaryFont(replacement)` を再適用して `afterEnableFontRefresh` を採る検証段を追加した。ここで `chars>0` に戻るなら、enable 時に飛ぶ TMP 内部 font/material state を再適用で復旧できる可能性が高い。
- この追加段も build/test 通過済みで、`python3 scripts/sync_mod.py` により再配備済み。次回の最優先確認点は `InventoryLineReplacementFailure/v1` の `afterEnableFontRefresh`。

## 2026-03-15 Inventory / Compare UI findings (afterEnableFontRefresh result + resync probe)
- 最新 run では `afterEnableFontRefresh` も 0 のままだった。したがって enable 後に `ForcePrimaryFont(...)` をもう一度当てるだけでは回復しない。
- stage 間比較では、`afterSetActive` と `afterEnable` の違いは実質 `enabled=False -> True` だけで、`overflow/wrap/stencil/faceA/material/internalSharedMaterial/subMeshes` は同じままだった。したがって enable による内部レイアウト/生成状態の破綻が疑わしい。
- 次の切り分けとして、enable 後に `SyncReplacement(replacement, original)` を full で再実行し、その結果を `afterEnableResync` で採る検証段を追加した。
- ここで `afterEnableResync>0` に戻るなら、修正候補は「enable 後に full resync を再適用する」。この追加段も build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI findings (afterEnableResync result + sibling probe)
- 最新 run では `afterEnableResync` も 0 のままで、enable 後の full resync でも回復しなかった。
- ここまでで `SetActive(true)`、`Canvas.ForceUpdateCanvases()`、`ForcePrimaryFont(...)` 再適用、`SyncReplacement(...)` 再実行のいずれも単独では回復しないことが確認できた。
- 次の切り分けとして、same `TextShell` 配下で original と replacement が同時に enabled なのが干渉要因かを確認するため、enable 後に `original.enabled = false` を一時適用して `replacement.ForceMeshUpdate(...)` した結果を `afterOriginalDisableRefresh` で採る検証段を追加した。
- ここで `afterOriginalDisableRefresh>0` に戻るなら、修正候補は original leaf を先に退避/無効化してから replacement を有効化する順序へ寄る。変更は build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI findings (sibling probe result + private TMP state)
- 最新 run では `afterOriginalDisableRefresh` も 0 のままで、original leaf を一時無効化しても replacement は回復しなかった。same `TextShell` 配下の sibling 干渉も主因ではなさそう。
- ここまでの診断で、`enabled=true` が真の failure 境界である一方、font 再適用・full resync・canvas force・original disable のどれでも直らないことが確認できた。
- 次の切り分け用に creation-stage snapshot へ TMP private state を追加した。対象は `m_isAwake` (`isAwake`), `m_isRegisteredForEvents` (`registered`), `m_ignoreActiveState` (`ignoreActiveState`), `m_canvas` 有無 (`hasCanvas`)。
- これで次回ログでは、enable 直後に TMP が `isAwake=False` / `registered=False` / `hasCanvas=False` などの内部初期化不全へ落ちていないかを直接確認できる。変更は build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI findings (English UI font drift)
- 英字 UI フォント変化の主因候補は、global font patch と popup repair の両方で primary CJK font を広く適用していたこと。特に `ComparePopupTextFixer.ApplyTmpFonts(...)` は popup root 配下の全 active TMP に `ForcePrimaryFont(...)` を当てていた。
- Oracle と external docs は一致して「英字 UI を保ちたいなら、font asset の丸ごと差し替えではなく fallback 追加を優先すべき」と結論した。
- 修正として `FontManager.ApplyToText(...)` を変更し、既存 TMP font がある場合は vanilla でも差し替えず `EnsureFallbackChain(...)` のみ行うようにした。global `TextMeshPro*.OnEnable` patch は英字 UI の元 font を保持しつつ、日本語だけ fallback で拾う想定に変わる。
- `ComparePopupTextFixer.ApplyTmpFonts(...)` も `ForcePrimaryFont(...)` から `ApplyToText(...)` へ変更した。tooltip / compare popup の mixed-language text でも英字見た目は元 font を保ちやすくなる。
- legacy `UI.Text` は fallback chain を持てないため、`ApplyToLegacyText(...)` を「非 ASCII を含む場合のみ fallback font に切り替える」方針へ寄せた。純英字 legacy UI の見た目変化を抑える狙い。
- 上記変更は `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` / `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` を通過し、`python3 scripts/sync_mod.py` で再配備済み。次回確認では英字 UI（`<search>`, `navigation`, `Set Primary Limb`, `Display Options`, `This Item`, `Equipped Item`, `Perfect`）の見た目が戻るかと、日本語 tooltip/compare が維持されるかを同時に見る。

## 2026-03-15 Inventory / Compare UI findings (Replacement OnEnable culprit)
- `ReplacementOnEnableProbe/v1` により、`QudJPReplacementText` は `TextMeshProUGUI.OnEnable` の Postfix (`FontManager.ApplyToText(...)`) に入る前は `chars>0 pageCount=1` を持つが、`afterApply` で即 `chars=0 pageCount=0` に落ちることを確認した。例: `How Thou Wouldst Rest Peaceably`, `We Require More`, `たいまつ x14 (unburnt)` などで一貫して再現。
- これにより inventory failure の直接原因は `replacement.enabled = true` 後に走る global `TextMeshProUGUI.OnEnable` patch であり、`ApplyToText(...)` が replacement 専用 TMP に対して destructive に働いていることがほぼ確定した。
- 修正として `TextMeshProUguiFontPatch.Postfix` から `QudJPReplacementText` を除外し、replacement だけは `OnEnable` 時の `ApplyToText(...)` をスキップするようにした。通常 UI の TMP には引き続き `ApplyToText(...)` が走る。
- この修正は `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` / `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` を通過し、`python3 scripts/sync_mod.py` で再配備済み。次回の最優先確認点は `ReplacementOnEnableProbe/v1` が `skipApply` のみになり、`InventoryLineReplacement/v1` の `replaced` が 0 から正へ変わるかどうか。
- 次の切り分け用に creation-stage snapshot へ TMP private state を追加した。対象は `m_isAwake` (`isAwake`), `m_isRegisteredForEvents` (`registered`), `m_ignoreActiveState` (`ignoreActiveState`), `m_canvas` 有無 (`hasCanvas`)。
- これで次回ログでは、enable 直後に TMP が `isAwake=False` / `registered=False` / `hasCanvas=False` などの内部初期化不全へ落ちていないかを直接確認できる。変更は build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI findings (inventory fallback-first follow-up)
- `ReplacementOnEnableProbe/v1: stage=skipApply` は最新 `Player.log` で確認でき、`QudJPReplacementText` は `skipApply` 後にも一時的に `chars>0` を持っていた。したがって前回の `TextMeshProUGUI.OnEnable` global patch 由来の即死は止まっている。
- それでも `InventoryLineReplacement/v1` は引き続き `replaced=0` のままで、`InventoryLineReplacementFailure/v1` では `afterSync` 成功後に original/replacement とも `chars=0` へ戻っていた。`isAwake=True`, `registered=True`, `hasCanvas=True` なので、少なくとも private TMP state 初期化不全は直近原因ではなさそう。
- 最新ログの `InventoryLineUpdateProbe/v1` では、visible inventory leaf 自体が translated text を保持したまま `chars=0` になっている。compare / tooltip 側は `SourceCodePro-Regular SDF` を維持したまま live text が出ている一方、inventory 側はこれまで `InventoryLineFontFixer` と `TextShellReplacementRenderer` が `ForcePrimaryFont(...)` を直接当て続けていた。
- ここから、inventory failure の次仮説を「inventory 系だけ残っている primary font 強制差し替えが destructive」であると更新した。修正として `InventoryLineFontFixer` と `TextShellReplacementRenderer` の inventory/replacement 経路を `ForcePrimaryFont(...)` から `ApplyToText(...)` へ変更し、既存 TMP font を保持しつつ fallback chain だけを追加する方針へ揃えた。
- この変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回の最優先確認点は、inventory leaf / replacement が `chars>0` に戻るかと、`InventoryLineReplacement/v1` の `replaced` が正になるかどうか。

## 2026-03-15 Inventory / Compare UI findings (afterEnable short-circuit + explicit font/material inherit)
- Oracle / external TMP 調査を踏まえ、現在の主仮説を「replacement は `enabled=true` 直後には描けているが、その後の補正シーケンス (`ApplyToText` / 再 `SyncReplacement` / original 再有効化後の canvas update) で壊れている」へ更新した。
- 補助発見として、`font=''` は null ではなく runtime 作成された TMP font asset の name 未設定を示している可能性が高い。また `material='TextMeshPro/Mobile/Distance Field'` も必ずしも default fallback そのものではなく、runtime 作成 font asset の material 名と一致しうる。
- ただし replacement 側が original の `font` / `fontSharedMaterial` を明示継承していなかったのは事実なので、`SyncReplacement(...)` で `replacement.font = original.font` と `replacement.fontSharedMaterial = original.fontSharedMaterial` を追加した。
- さらに `replacement.enabled = true` 直後 (`afterEnable`) に `textInfo.characterCount > 0` なら、その時点で成功扱いにして `ApplyToText(...)` / 再 `SyncReplacement(...)` / original disable-enable / `TryForceCanvasUpdate()` 以降を通さず、そのまま replacement を採用する短絡を追加した。
- この変更は `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` 成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` `255/255` passed、`python3 scripts/sync_mod.py` 再配備済み。次回ログでは `InventoryLineReplacement/v1` に `phase='afterEnable'` 付きの成功行が出るか、`replaced=0` が解消されるかを最優先で確認する。

## 2026-03-15 Inventory / Compare UI findings (observability may be destructive)
- 最新 run では `InventoryLineReplacement/v1` に `phase='afterEnable'` 成功行が実際に出るようになった。`布のローブ`, `堅焼きパン`, `たいまつ x8`, `魔樹の樹皮`, `巡礼者のワイン袋`, `水袋` 系で replacement 作成成功までは到達している。
- しかし次フレームの `InventoryLineUpdateProbe/v1` / `InventoryLineReplacementStateNextFrame/v1` では、same replacement が `active=True enabled=True` のまま `chars=0` に落ちていた。ここで注目すべきなのは、どちらの observability も対象 text に対して `ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true)` を呼んでいたこと。
- とくに `InventoryLineUpdateObservability.TryBuildTransitionLog(...)` は `InventoryLine.Update` postfix から毎フレーム走り、replacement も含めて `ForceMeshUpdate(...)` を打っていた。`TextShellReplacementRenderer.TryBuildReplacementState(...)` も next-frame probe のために replacement へ同じ `ForceMeshUpdate(...)` を打っていた。
- これらの観測コード自体が、`afterEnable` 成功した replacement を次フレームで再パースして 0 化している可能性が高いと判断した。最小修正として、inventory 専用の 2 つの probe から `ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true)` を削除した。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では、同じ rows が `afterEnable` 成功後にも可視のまま残るかをまず確認する。

## 2026-03-15 Inventory / Compare UI findings (success branch redraw + richer next-frame state)
- observability の `ForceMeshUpdate(...)` を外した後の run でも、blank rows は依然として `afterEnable` 成功後に次フレームで `chars=0` へ戻った。したがって probe-induced regression だけでは説明しきれず、通常の redraw サイクル側にも問題が残っている。
- ここで仮説を「`afterEnable` 成功直後に original を disable したことで replacement が次の通常 Canvas redraw に十分 dirty として乗っていない」へ更新した。success branch はこれまで original を disable して `SetAsLastSibling()` するだけで、その後 replacement を再 dirty 化していなかった。
- 修正として `TextShellReplacementRenderer` の `afterEnable` 成功 branch に `replacement.havePropertiesChanged = true; replacement.SetAllDirty();` を追加した。これで original disable 後の通常 redraw に replacement を明示的に載せ直す。
- あわせて `InventoryLineReplacementStateNextFrame/v1` を非破壊のまま拡張し、`propsChanged`, `font`, `material`, `canvasA`, `subMeshes` を出すようにした。次回ログで「font/material が飛んだのか」「submesh が消えたのか」「dirty 状態が残っているのか」を確認できる。
- この変更後も `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` 成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` `255/255` passed、`python3 scripts/sync_mod.py` 再配備済み。次回確認の主眼は blank rows が可視のまま残るか、残らないなら新しい next-frame state fields が何を示すか。

## 2026-03-15 Inventory / Compare UI findings (clone-based replacement experiment)
- 新しい next-frame state では、blank 化した replacement が `active=True enabled=True propsChanged=False font='SourceCodePro-Regular SDF' material='SourceCodePro-Regular SDF Material' canvasA=1 subMeshes=2` を維持したまま `chars=0 pageCount=0` になっていた。つまり font/material/canvas/submesh 不在ではなく、geometry だけが通常更新で落ちている。
- 画像上も、カテゴリ見出しは出る一方で翻訳済み item rows はほぼ blank のまま、未翻訳英字 row (`counterweighted...`) だけが見えていた。ログでも translated item rows は大量に `phase='afterEnable'` 成功後に next-frame 0 化している一方、英字 row はこの failure path に乗っていない。
- ここから新しい仮説を「fresh `new GameObject + new TextMeshProUGUI` replacement が、original leaf の hidden serialized state / CanvasRenderer/TMP 初期化状態を引き継げていない」に更新した。
- 最小実験として `GetOrCreateReplacement(...)` を変更し、replacement を `new GameObject(...)` で新規構築する代わりに `UnityEngine.Object.Instantiate(original.gameObject)` で original leaf をクローンしてから `QudJPReplacementText` にリネームし、parent/layer/active state だけ調整する方式に切り替えた。
- この変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では clone-based replacement で translated item rows が通常更新後も可視維持できるかを最優先で確認する。

## 2026-03-15 Inventory / Compare UI findings (clone experiment regressed; revert)
- clone-based replacement の最新 run では、`ReplacementOnEnableProbe/v1` が `font='SourceCodePro-Regular SDF' material='SourceCodePro-Regular SDF Material'` を持ちながらも `chars=0 pages=0` のままで、以前あった `afterEnable` 成功が消えた。`InventoryLineReplacement/v1` も `root='#1/#3/#5/#7/#8/#9' ... replaced=0` に後退した。
- 次フレーム state でも replacement は `active=False enabled=False` に落ちており、clone 化で `subMeshes=4` には増えたものの geometry 復活には結びつかなかった。したがって clone strategy は改善ではなく regression である。
- 一方で同 run の `InventoryLineReplacementSentinel/v1` は original leaf で `sentinel='TEST' sentinelChars=4` を返しており、component 自体は simple text を描けることを再確認した。`restoredChars=0` へ戻るため、主因は hidden state よりも restored/formatted text path 側に寄っている可能性が高い。
- 以上から clone-based replacement は撤回し、`GetOrCreateReplacement(...)` を `new GameObject + TextMeshProUGUI` ベースへ戻した。build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。
- 次の主仮説は「inventory の restored/formatted text（rich text / color-tag / inline symbol を含む）を `TextShell/Text` 系 leaf が通常更新で geometry 化できていない」である。次回は text-content/path 側の切り分けへ戻る。

## 2026-03-15 Inventory / Compare UI findings (empty-formatted path clarified)
- 最新 `Player.log` では blank が 2 系統あることを確認した。1) `formatted-present` だが replacement が次フレームで `chars=0` に戻る行。2) そもそも `InventoryRenderProbe/v6[empty-formatted]` になっている行。
- `empty-formatted` 行は `display='{{W|}}' raw='{{W|}}' formatted='' tmp=''` で、`InventoryLineUpdateProbe/v1` でも `text=''` になっている。これは render failure より前段の text source 側の問題である。
- コード確認では `InventoryLocalizationPatch.Postfix(...)` は `UITextSkinTranslationPatch.TranslatePreservingColors(__result, ...)` を呼ぶだけで、`TranslatePreservingColors(...)` は `ColorCodePreserver.Strip(source)` 後に stripped length が 0 なら元 `source` をそのまま返す。つまり translator 自体が `{{W|}} -> ''` を生成しているのではなく、upstream からすでに中身のない color-tag 文字列が渡ってきている。
- このため、`empty-formatted` 行は replacement persistence failure と別問題として扱うべきである。次の切り分け対象は、どの inventory row/type が `display='{{W|}}'` を返しているか、またその raw displayName source がどのメソッド由来かである。

## 2026-03-15 Inventory / Compare UI findings (row data observability for empty-formatted)
- `empty-formatted` 行の source を直接追うため、`InventoryRenderObservability` を拡張し、`InventoryRenderProbe/v6` に `dataType` と `item` 概要を追加した。`item` は `data.item` / `data.Item` から取り、`Blueprint` / `BlueprintName` / `id` / `DisplayName` / `displayName` を順に要約する。
- これにより次回 run では `display='{{W|}}' raw='{{W|}}' formatted=''` を返した row が、どの data 型・どの item に紐づくかを直接ログで確認できるようになった。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。

## 2026-03-15 Inventory / Compare UI findings (exact {{W|}} stubs fixed in Items.jp.xml)
- `InventoryRenderProbe/v6[empty-formatted]` の direct cause である `DisplayName="{{W|}}"` スタブを `Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml` で実データに置き換えた。対象は現在のログで問題を起こしていた exact `{{W|}}` 系で、書籍系は `Books.jp.xml` の `Title` を転記し、その他は item 名を埋めた。
- 修正対象には `HeirloomsCatalog`, `Sheaf1`, `DarkCalculus`, `MimicandMadpole`, `OnHumanMimicry`, `FromEntropytoHierarchy`, `DisquisitionOnTheMaladyOfTheMimic`, `Crime and Punishment`, `AphorismsAboutBirds`, `BloodandFear`, `ArtlessBeauty`, `FaunsoftheMeadow`, `MurmursPrayer`, `InMaqqomYd`, `CouncilAtGammaRock`, `ModuloMoonStair`, `GolemOperatingManual`, `Yellow Security Card`, `Gold Trollking Key`, `Ogre Ape Pelt` が含まれる。
- `xmllint --noout Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml` は成功し、`grep 'DisplayName="{{W|}}"'` でも 0 件を確認した。`python3 scripts/sync_mod.py` で再配備済み。
- 次回 run では、これまで `empty-formatted` だった行が plain visible row に戻るか、残るなら別色コードの空スタブか別 source かを確認する。

## 2026-03-15 Inventory / Compare UI findings (first-open mouse lag likely probe-driven)
- ユーザー報告の「最初に inventory を開いたときだけマウス選択が重く、二回目以降は滑らか」という症状について、最新コードと Oracle の両方が investigation code 側の first-open bias と整合すると判断した。
- とくに `InventoryLineRenderProbePatch` は `InventoryLine.setData(...)` ごとに delayed repair を予約し、`DelayedInventoryLineRepairScheduler` は各行について `TryRepairInvisibleTexts`, 全 child TMP への `ForceMeshUpdate`, replacement 試行, 複数の `Debug.Log` を走らせる。さらに `InventoryLineUpdateProbePatch` は per-frame で `InventoryLine.Update` 後の transition log を作り、`InventoryScreenChildTextProbePatch` は inventory open 時に広い hierarchy/branch snapshot を大量出力していた。
- また `LookTooltipContentPatch` は hover/tooltip content 生成のたびに `ScheduleVisibleInventoryRepairs()` を呼び、可視 inventory 全行を `Resources.FindObjectsOfTypeAll<Component>()` で走査し直していた。これはマウス hover/selection の重さと直接噛み合う。
- 対策として、まず安全な負荷削減を入れた。`InventoryLineUpdateProbePatch` を no-op 化し、`InventoryScreenChildTextProbePatch` / `InventoryUiFieldProbePatch` の高頻度 inventory snapshot ログを止め、`LookTooltipContentPatch` から `ScheduleVisibleInventoryRepairs()` 呼び出しを削除した。core replacement/repair 自体は残している。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では first-open のマウス選択が改善するかと、inventory rendering の症状がどう変わるかを同時に確認する。

## 2026-03-15 Inventory / Compare UI findings (remaining lag trimmed + inactive replacement root-cause fix)
- 最新ログでも inventory blank と初回マウス選択ラグが改善していなかったため、残存 hot path を再精査した。Oracle / explore の両方が、`InventoryLineRenderProbePatch` の setData 後 probe と `DelayedSceneProbeScheduler` の compare/tooltip 再試行がなお重いと指摘した。
- `DelayedSceneProbeScheduler` はまだ 1 hover/tooltip につき lightweight repair-only に落ちていたが、未使用の `scheduledScreenComponent` state が残っていたため整理した。あわせて tooltip/compare の scheduler は 1 attempt の repair-only で継続し、広い hierarchy/scene snapshot ログは走らない状態を確認した。
- さらに explore の重要な指摘として、`TextShellReplacementRenderer.GetOrCreateReplacement(...)` が replacement `GameObject` を `SetActive(false)` で作成していたため、TMP の canvas 初期化 (`m_canvas`) が不完全なまま replacement 手順に入り、`afterEnable` 以降の geometry 生成が失敗している可能性が高いと分かった。
- これに対して root-cause 寄りの修正として、replacement 作成時の `gameObject.SetActive(false)` を `gameObject.SetActive(true)` に変更した。`replacement.enabled = false` は残しているので、TMP/Canvas 初期化だけ先に通し、実表示は従来どおり後から制御する。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では 1) 初回 inventory open の操作感、2) `afterEnable` 成功が next frame に維持されるか、を最優先で確認する。

## 2026-03-15 Inventory / Compare UI findings (performance fix confirmed, active-on-create regressed rendering)
- ユーザー報告どおり、最新 run では first-open のマウス操作と tooltip 初回描画のもたつきは改善した。ログ上も `ComparePopupTextRepair`, `PopupContainerTextRepair`, `CompareHierarchyProbeDelayed`, `CompareSceneProbe` は消えており、tooltip/compare scheduler の重い観測が外れたことを確認できた。
- 一方で inventory rendering は悪化していた。`afterEnable` 成功が消え、`ReplacementOnEnableProbe/v1` は replacement が `font='SourceCodePro-Regular SDF'` でも `chars=0` のまま、`InventoryLineReplacement/v1` も `replaced=0` に戻った。next frame では replacement が `active=False enabled=False` に落ちていた。
- したがって `gameObject.SetActive(true)` で replacement を active-on-create にする変更は regression と判断し、元の inactive-on-create に巻き戻した。
- あわせて、役目を終えた `DelayedInventoryLineRepairScheduler` の failure probes (`InventoryLineReplacementLeafState`, `InventoryLineReplacementSentinel`, `InventoryLineReplacementDirectFontSentinel`, `InventoryLineReplacementStateNextFrame`) を scheduler から外した。これらは text を `TEST` に変えて戻す破壊的診断や、失敗時の詳細ログ生成であり、描画修復本体ではない。
- 巻き戻し＋probe 削減後も `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では「操作改善が維持されたまま rendering が以前の afterEnable-success 状態に戻るか」を確認する。

## 2026-03-15 Inventory / Compare UI findings (successful replacement was being disabled on reuse)
- rollback 後の最新 run では、inventory replacement は `phase='afterEnable'` 成功に戻っていた。`#1/#3/#5/#7/#9/#10/#11/#12/#13 InventoryScrollerLine` で `replacementChars>0` が出ている。
- ここから新しい本命仮説が見えた。`TextShellReplacementRenderer.TryRenderReplacementTexts(...)` は成功 branch で `original.enabled = false` にする一方、メソッド冒頭の早期分岐は `!original.enabled` を検知すると即 `TryDisableReplacement(original.transform.parent)` を呼ぶ。つまり成功済み line が次回の repair で再びこのメソッドに入ると、自分で replacement を無効化して blank へ戻している可能性が高い。
- 対策として、`!original.enabled` に入った際はまず既存 `QudJPReplacementText` を確認し、`active/enabled` かつ text が残っていて `ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false)` 後も `characterCount > 0` なら、replacement をそのまま再利用する `phase='reuseActive'` 分岐を追加した。成功済み replacement を誤って `TryDisableReplacement(...)` しないのが狙い。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では `InventoryLineReplacement/v1` に `phase='reuseActive'` が出るか、item row が実表示に残るかを確認する。

## 2026-03-15 Inventory / Compare UI findings (setData sync probe removed; scheduler narrowed)
- 最新 run でも lag と blank が残っていたため、inventory の repair flow をさらに絞った。`InventoryLineRenderProbePatch` の `HasFailingEligibleTextShellLeaf(__instance)` は `setData(...)` ごとに同期 `ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true)` を全 TMP child に打つため、初回 open の main-thread cost として強すぎた。
- 対策として `InventoryLineRenderProbePatch` は失敗判定をやめ、`InventoryLineFontFixer.TryApplyPrimaryFontToItemRow(...)` 後にそのまま `DelayedInventoryLineRepairScheduler.ScheduleRepair(__instance)` を呼ぶだけにした。これで同期判定コストを削減する。
- 同時に `DelayedInventoryLineRepairScheduler` も絞り、`TryApplyPrimaryFontToAllTextChildren`, `TmpTextRepairer.TryRepairInvisibleTexts`, child TMP 全体への `ForceMeshUpdate` ループを外した。scheduler は `TextShellReplacementRenderer.TryRenderReplacementTexts(...)` に集中し、成功時のみ非破壊の `InventoryLineReplacementStateNextFrame/v1` を 1 回記録する。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では first-open lag の変化と、`phase='reuseActive'` または next-frame state の変化を確認する。

## 2026-03-15 Inventory / Compare UI findings (selection-path probes disabled)
- ユーザーが補足した「未翻訳の英字アイテムは問題なく選択できるが、翻訳済み row ではマウス選択がもたつく」という症状を受けて、selection path 直下の pure diagnostic patch を洗い直した。
- `HandleSelectItemProbePatch` は `InventoryAndEquipmentStatusScreen.HandleSelectItem(...)` のたびに `CompareProbeRunner.Run(__instance)` を呼び、leaf probe、invisible-text repair、compare popup repair、hierarchy snapshots、scene snapshot、delayed scene scheduler までまとめて走らせていた。translated row だけが repair/replacement 対象になりやすいため、症状との相関が強い。
- また `SelectableTextMenuItemSelectChangedProbePatch` も Prefix/Postfix の両方で `SelectableTextMenuItemObservability.TryBuildState(...)` を呼んでおり、選択のたびに反射とログ出力を行っていた。
- これらは描画修復そのものではなく観測専用なので、`HandleSelectItemProbePatch.Postfix` を no-op 化し、`SelectableTextMenuItemSelectChangedProbePatch` の Prefix/Postfix も no-op 化した。selection パスから pure diagnostic を除去する狙いである。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では translated row 選択時のもたつきが改善するかを確認する。

## 2026-03-15 Inventory / Compare UI findings (replacement raycast + quieter repair logs)
- translated row だけ選択が引っかかる件について、replacement 自体の hit target も見直した。これまで `QudJPReplacementText` は常に `raycastTarget = false` で生成していたため、translated row は `original.enabled = false` になったあと hit 先を失っている可能性があった。
- 対策として `GetOrCreateReplacement(...)` と `SyncReplacement(...)` の両方で `replacement.raycastTarget = original.raycastTarget` に変更した。これで replacement も original と同じ pointer/raycast path に乗る。
- 同時に first-open のログ負荷をさらに落とすため、`TextMeshProUguiFontPatch` の `ReplacementOnEnableProbe/v1` を停止し、`DelayedInventoryLineRepairScheduler` から per-line `InventoryLineReplacement/v1` ログ出力を外した。repair 自体は継続するが、毎行ログは減る。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では translated row のマウス選択の改善と、inventory blank の変化を確認する。

## 2026-03-15 Inventory / Compare UI findings (preserve original enabled state on success)
- 最新症状では translated row だけ選択が引っかかる一方、英字 row は正常だった。コード上も translated row success branch だけが `original.enabled = false` に入っていたため、selection/hit path を壊している可能性が高いと判断した。
- とくに blank translated row では original leaf 自体が `chars=0` で不可視なので、`enabled` を維持しても二重描画にはなりにくい。そこで success branch（`phase='afterEnable'` 成功時、および fallback success 時）から `original.enabled = false` を削除した。
- これにより元の row component を生かしたまま replacement を上に重ねる構成となり、selection path と later repair flow の両方で translated row を壊しにくくする狙いがある。
- 変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回 run では translated row の selection と inventory 描画が同時に改善するかを確認する。
