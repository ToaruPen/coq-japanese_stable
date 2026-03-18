# Issues

## 2026-03-11 Task 0 PoC
- `net9.0`/`net8.0` テスト実行はランタイム未導入で失敗（`Microsoft.NETCore.App 9/8` 不足）。
- `net48` テスト実行は `mono` ホスト未導入で失敗（ビルドのみ成功）。
- 既存 `Player.log` に他Mod由来の `mprotect returned EACCES` を確認し、macOS Harmony ランタイム失敗リスクは高い。

## 2026-03-11 Task 3 Legacy XML監査
- 現行ゲーム `Base/Books.xml` は XML 不正文字参照（line 1091, col 6）を含み、通常XMLパース不可。ID抽出は regex フォールバックが必要。
- 事前想定（辞書37ファイル）に対し、実観測は辞書JSON35ファイル。件数差分の原因確認が必要。
- Optionsで6件（`OptionDebug*`）がレガシーのみ、Booksで3件（`AcrossTheSpindle1-3`）がレガシーのみ。

## 2026-03-11 Task 0: Known Issues
- NUnit3TestAdapter 5.0.0 NU1701 warning for netstandard2.0 target — not critical
- [ModuleInitializer] on Mono runtime: unverified, Part B pending
- Part B (game runtime Harmony verification): PENDING manual game launch

## 2026-03-11 Task MODWARN属性修正
- `Player.log` の MODWARN 対象8ファイルを Base XML と比較し、未対応属性のみ削除（翻訳本文は保持）。
- 削除した未対応属性:
  - `Skills.jp.xml`: `skill` / `power` の `Load` と `DisplayName`
  - `HiddenMutations.jp.xml`: ルート `mutations` の `Load` と `mutation` の `DisplayName`
  - `EmbarkModules.jp.xml`: `module` / `window` / `mode` の `Load`
  - `Genotypes.jp.xml`: `genotype` の `Load`
  - `SparkingBaetyls.jp.xml` / `Relics.jp.xml` / `Quests.jp.xml` / `ActivatedAbilities.jp.xml`: ルート要素の `Load`
- `python3 - <<...ET.parse...>>` で8ファイルすべてのXMLパース成功（well-formed）。
- `python3 scripts/validate_xml.py Mods/QudJP/Localization/Skills.jp.xml` は OK。
- `python3 scripts/validate_xml.py Mods/QudJP/Localization/HiddenMutations.jp.xml` は OK。
- `python3 scripts/validate_xml.py Mods/QudJP/Localization/` は完走。既存の警告（重複ID/空テキスト等）は出るが、今回修正対象8ファイルの属性不整合に関する新規エラーはなし。

## 2026-03-11 F1 Plan Compliance Audit (re-run)
- `python3 scripts/check_encoding.py Mods/QudJP/Localization/` が `Mods/QudJP/Localization/AGENTS.md` を `[MOJIBAKE]` として1件報告（UTF-8エンコード問題というより内容語彙の検知）。
- `Mods/QudJP/Assemblies/src/FontManager.cs:17` が CJK フォールバック未実装ログを出しており、Must Have「CJKフォント対応」で監査NG。

## 2026-03-15 Inventory / Compare UI progress
- `compare` tooltip/popup は item title と本文が表示される段階まで回復した。
- ただし compare 側は、内容量に応じて決まる動的矩形の形が崩れる場合がある。
- `inventory` 右側の item-name 表示は未解決で、同時点のスクリーンショットでは本文が出ていない。
- 当面の優先順位は `inventory` 表示復旧が先、compare は表示回復済みとして矩形安定化を次点に置く。
- 上記状態のビルドは再配備済み（`python3 scripts/sync_mod.py` 実行済み）。
- 次回確認時は `Player.log` の `InventoryLineReplacement/v1` / `InventoryLineReplacementStateNextFrame/v1` / `legacyReplacement[` / `ComparePopupTextRepair/v1` / `CompareSceneProbe/v1` を優先して見る。

## 2026-03-15 Inventory / Compare UI progress (latest)
- `compare` は `Qud.UI.BaseLineWithTooltip.StartTooltip(...)` を起点にした probe へ切り替えたことで、`DualPolatLooker` / `PolatLooker` の可視 tooltip root を直接観測できる段階まで進んだ。
- 最新 `Player.log` の `CompareSceneProbe/v1` / `ComparePopupContainerProbe/v1` では `DisplayName` と `LongDescription` が直接報告され、compare 側の item title / 説明文表示はログでも確認済み。
- `inventory` は未解決。`InventoryLineReplacementFailure/v1` では元 leaf と replacement TMP の両方が `chars=0 pageCount=0`、さらに `TEST` sentinel でも `sentinelChars=0` となるため、翻訳文字列や CJK glyph ではなく subtree/state 固有の TMP failure が疑わしい。
- legacy fallback は stopgap と判断して無効化済み。現在は replacement TMP を `TextShell` 配下に戻し、同じ subtree/state 条件で `chars>0` に復帰するかを検証中。
- 当面の優先順位は `inventory` の root-cause 切り分けが最優先。次回確認では `InventoryLineReplacementFailure/v1` / `InventoryLineReplacementStateNextFrame/v1` / `sentinelChars=` と compare 側の `ComparePopupContainerProbe/v1` / `CompareSceneProbe/v1` を見る。

## 2026-03-15 Inventory / Compare UI progress (latest log refresh)
- 最新 `Player.log` で build marker `ui-child-snapshot-v3`、`FontManager: CJK font registered`、`Harmony patching complete: 45 method(s) patched.` を確認。`build_log.txt` も `Success :)` のため、現在の調査は正常配備ビルド上で進めている。
- `InventoryRenderProbe/v6` は `sample='た' hasSample=True` や `sample='堅' hasSample=True` を返しており、inventory item-name failure を glyph 欠落起因とみなす根拠は薄い。
- delayed inventory probe では inventory subtree の TMP 数が `122 -> 132` に増えているため、replacement TMP は作成されている。ただし `InventoryLineReplacementStateNextFrame/v1` では replacement が全件 `chars=0 pageCount=0 active=False enabled=False` に戻っており、生成後の描画フェーズで失敗している。
- 次の切り分け用に `InventoryLineReplacementLeafState/v1` / `InventoryLineReplacementSentinel/v1` / `InventoryLineReplacementDirectFontSentinel/v1` を追加で出すようにした。ここで `internalFontMatchesPrimary` や `sharedMaterial` 差分が見えれば、TMP 内部状態の破綻位置をさらに絞れる。
- 上記ビルドは `python3 scripts/sync_mod.py` で再配備済み。次回の優先ログは `InventoryLineReplacementLeafState/v1` / `InventoryLineReplacementSentinel/v1` / `InventoryLineReplacementDirectFontSentinel/v1` / `InventoryLineReplacementStateNextFrame/v1`。

## 2026-03-15 Inventory / Compare UI progress (post-restart probe result)
- 再起動後の追加 probe でも `InventoryLineReplacementSentinel/v1` は全件 `sentinelChars=0` のままだった。ASCII `TEST` でも 0 なので、inventory failure は CJK glyph coverage の問題ではない。
- `InventoryLineReplacementDirectFontSentinel/v1` は `fontMatchesPrimary=True internalFontMatchesPrimary=True` を返したため、inventory failure は「primary font 未適用」でもない。
- 同時に `canvasA=1`、`stencil=0`、`faceA=1`、`rect>0` が確認できたので、直近の候補は alpha zero / stencil mask / zero rect ではない。
- replacement `QudJPReplacementText` は次フレームで再び `active=False enabled=False chars=0` に戻る。したがって現在の主仮説は、inventory row の後続更新が `TextShell` 配下の TMP をフレーム後に再無効化していること。
- 次の作業候補は `Qud.UI.InventoryLine.Update()` / `SelectableTextMenuItem` 由来の後続更新タイミングを直接 probe し、いつ `QudJPReplacementText` と元 leaf が潰されるかを特定すること。

## 2026-03-15 Inventory / Compare UI progress (update-hook instrumentation)
- `Qud.UI.InventoryLine.Update()` を捕まえる `InventoryLineUpdateProbe/v1` を追加した。failing leaf と replacement の state が変わった時だけ出すため、次回ログで `setData` 後に誰が state を落としたかを追いやすくした。
- `TextShellReplacementRenderer.TryDisableReplacement(...)` の直前にも `InventoryLineReplacementDisable/v1` を追加し、replacement を潰す分岐条件を直接見えるようにした。
- `SelectableTextMenuItemObservability` は direct component ではなく child TMP を取るようにしたので、menu-item 系 update 由来の影響があれば今までより拾いやすい。
- 上記変更後も `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` と `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は通過済み。次の確認対象ログは `InventoryLineUpdateProbe/v1` と `InventoryLineReplacementDisable/v1`。

## 2026-03-15 Inventory / Compare UI progress (update probe result)
- 最新ログでは `InventoryLineUpdateProbe/v1` が `frame=8081` で既に `leaf active=True enabled=True chars=0` を返しており、inventory failure は `Update()` より前に成立している。
- 同じ行は `frame=8084` で replacement が `activeSelf=False active=False enabled=False chars=0` になっていたが、`InventoryLineReplacementDisable/v1` は出ていない。
- したがって今回の failure は `TryDisableReplacement(...)` で潰されたというより、replacement 自体が生成直後に `characterCount == 0` のまま failure path へ落ちて自前で無効化された可能性が高い。
- これで `InventoryLine.Update()` / `SelectableTextMenuItem` の後続更新が主犯という仮説は後退した。次の切り分けは `SyncReplacement(...)` 直後から `replacement.textInfo.characterCount == 0` 判定までの immediate creation path を重点化する。

## 2026-03-15 Inventory / Compare UI progress (material retry instrumentation)
- `InventoryLineReplacementFailure/v1` に replacement の `fontMaterial` / `internalFont` / `internalSharedMaterial` / `sharedEqualsFontMaterial` を追加した。
- さらに diagnostic retry として `replacement.fontSharedMaterial = replacement.font.material` を一時適用した場合の `materialRetryCurrentChars` / `materialRetrySentinelChars` もログ化するようにした。
- 次のログで retry 後だけ `chars>0` になるなら、immediate failure の主因は replacement shared material 経路にかなり絞れる。
- 変更は build/test 通過済みで、`python3 scripts/sync_mod.py` により再配備済み。次回の最優先タグは更新後の `InventoryLineReplacementFailure/v1`。

## 2026-03-15 Inventory / Compare UI progress (material retry result)
- 最新ログでは全可視行で `sharedEqualsFontMaterial=True`、`materialRetryCurrentChars=0`、`materialRetrySentinelChars=0` だった。shared material を `font.material` に揃えても failure は変化しない。
- これにより immediate failure の主因を replacement shared material 経路に置く仮説は後退した。
- 次の切り分け対象は replacement object の内部 font asset 参照と text parsing state。`GetOrCreateReplacement(...)` 直後と `SyncReplacement(...)` / `ForcePrimaryFont(...)` 後を failure 判定前に直接観測する必要がある。

## 2026-03-15 Inventory / Compare UI progress (creation-stage instrumentation)
- `InventoryLineReplacementFailure/v1` に creation-stage snapshot を追加した。`afterGetOrCreate` / `afterSync` / `beforeForceMesh` / `afterForceMesh` の 4 段階で replacement state を記録する。
- これで replacement object の font/internalFont/material/internalSharedMaterial/submesh 状態が、どの stage で壊れるかを同一行で追える。
- build/test は引き続き通過済みで、再配備も完了している。次回の最優先確認タグは更新後の `InventoryLineReplacementFailure/v1`。

## 2026-03-15 Inventory / Compare UI progress (latest log + single tooltip)
- 最新 inventory failure log では `afterSync` でだけ `chars>0 pageCount=1` が見え、その後 `afterTextAssign` / `afterActivate` / `afterDirty` で 0 に落ちる。inventory replacement の immediate failure は `SyncReplacement(...)` 以前ではなく、その直後の再活性化フェーズに絞られた。
- compare tooltip は引き続き `Tooltip Container/PolatLooker` 配下の `DisplayName` と `LongDescription` が埋まっている。
- 単体 tooltip は `DescriptionInventoryActionProbe` の直後に `PopupMessage/MenuControll/.../Content/Message=''` が出ており、本文コンテナ自体が空。スクリーンショットの compare-less tooltip 欠落と整合する。
- 既存実装では `BaseLineWithTooltipStartTooltipPatch` が compare scene probe を予約するだけで、single tooltip 専用修復は走らない。また `CompareSceneProbe/v1` は compare 専用 token に依存するため、single tooltip は観測 blind spot になる。
- 次の確認用に `LookTooltipContentProbe/v1` を追加し、`Look.GenerateTooltipContent(...)` の返り値そのものをログ化するようにした。これで「UI へ渡る前から空なのか」「PopupMessage への受け渡しで空になるのか」を切り分けられる。

## 2026-03-15 Inventory / Compare UI progress (analysis-mode synthesis)
- 最新 inventory log と Oracle 所見は一致しており、現時点の inventory 主仮説は「activation 後の state 遷移問題」。`afterSync` / `afterTextAssign` 成功、`afterActivate` 消失の境界が最重要ポイント。
- compare tooltip は latest run でも `DualPolatLooker` / `PolatLooker` 配下の `DisplayName` / `LongDescription` が `chars>0` で、正常経路として扱ってよい。
- single tooltip は latest run では未再現だったため、blind spot を埋めるための generic popup repair/probe を追加した。`PopupContainerTextRepair/v1` は header token に依存せず `PopupMessage` / `Tooltip Container` / `PolatLooker` 配下の active popup root を直接拾う。
- `DelayedSceneProbeScheduler` は compare repair に加えて generic popup repair も毎 attempt 実行するよう更新し、single tooltip run でも `PopupMessage` 側の live text・font適用・invisible repair 結果を追えるようにした。

## 2026-03-15 Inventory / Compare UI progress (tooltip recovered, inventory focus narrowed)
- 最新ログでは single tooltip / compare tooltip とも live container 上で `chars>0` を確認できたため、tooltip 系は一旦回復扱いにしてよい。
- inventory は引き続き未解決だが、failure 境界は `afterActivate` にかなり絞られている。
- external TMP research では `textInfo.characterCount` が `GenerateTextMesh()` 冒頭で一時的に 0 へ clear されること、inactive → active 遷移同フレームでは stale/temporary 0 を観測しうることが確認できた。
- これを受けて `TextShellReplacementRenderer` に `afterCanvasForce` stage を追加し、activation 直後に reflection 経由の `Canvas.ForceUpdateCanvases()` を挟むようにした。次のログで `afterCanvasForce` だけ `chars>0` に戻るなら、inventory failure は same-frame 誤判定の可能性が高い。
- 変更は build/test 通過・再配備済み。次回の最優先タグは更新後 `InventoryLineReplacementFailure/v1` の `afterCanvasForce`。

## 2026-03-15 Inventory / Compare UI progress (afterCanvasForce result + activation split)
- 最新 inventory run では `afterCanvasForce` も 0 のままだったため、same-frame 誤判定だけでは説明できない。
- 次の切り分け対象は `SetActive(true)` と `enabled=true` のどちらが真の failure trigger か。
- 検証版では activation 順序を分離し、`afterSetActive` / `afterSetActiveCanvasForce` / `afterEnable` / `afterEnableCanvasForce` を採るようにした。
- これで次回ログから「GameObject activation で壊れるのか」「TMP component enable で壊れるのか」を直接判定できる。build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI progress (enable boundary confirmed)
- 最新 run では `afterSetActive` / `afterSetActiveCanvasForce` は成功のまま、`afterEnable` で 0 化した。したがって真の failure trigger は `SetActive(true)` ではなく `enabled=true`。
- 次の切り分け用に `afterEnableFontRefresh` を追加し、enable 直後に `FontManager.ForcePrimaryFont(replacement)` を再適用する検証を入れた。
- ここで `chars>0` に戻るなら、enable によって失われた TMP 内部参照/状態を再適用で戻せる可能性が高い。変更は build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI progress (afterEnableFontRefresh result + resync probe)
- 最新 run では `afterEnableFontRefresh` も 0 のままで、enable 後の単純な font 再適用では回復しなかった。
- 次の検証として `afterEnableResync` を追加し、enable 後に `SyncReplacement(replacement, original)` を full で再実行した結果を採るようにした。
- ここで `chars>0` に戻るなら、修正候補は enable 後の full resync。変更は build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI progress (sibling probe result + private TMP state)
- 最新 run では `afterOriginalDisableRefresh` も 0 のままで、original sibling 干渉も主因ではなさそう。
- 次の切り分けとして creation-stage snapshot に TMP private state (`isAwake`, `registered`, `ignoreActiveState`, `hasCanvas`) を追加した。
- これで enable 直後に TMP の内部初期化/Canvas 登録が壊れているかを直接ログで追える。変更は build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI progress (fallback-first inventory pass)
- 最新 `Player.log` では `ReplacementOnEnableProbe/v1` が `stage=skipApply` になっており、`QudJPReplacementText` は一度 `chars>0` を持つことも確認できた。したがって前回の global `TextMeshProUGUI.OnEnable` patch が replacement を即死させる経路は止まっている。
- それでも `InventoryLineReplacement/v1` は `replaced=0` のままで、`InventoryLineUpdateProbe/v1` では visible inventory leaf 自体が translated text を保持したまま `chars=0` になっている。compare / tooltip 側が `SourceCodePro-Regular SDF` で生きていることと対照的に、inventory 系だけはまだ `ForcePrimaryFont(...)` を直接当てる経路が残っていた。
- これを受けて、新しい修正方針を「inventory も tooltip と同じく fallback-first に統一する」と定めた。`InventoryLineFontFixer` と `TextShellReplacementRenderer` の inventory / replacement 経路を `ForcePrimaryFont(...)` から `ApplyToText(...)` へ変更し、既存 font を保持したまま fallback chain だけ追加するよう更新した。
- 上記変更後の `dotnet build Mods/QudJP/Assemblies/QudJP.csproj` は成功、`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj` は `255/255` passed、`python3 scripts/sync_mod.py` で再配備済み。次回ログ確認では inventory の `chars` と `replaced` が改善するか、tooltip/compare が維持されるかを同時に見る。

## 2026-03-15 Inventory / Compare UI progress (afterEnable success preservation)
- Oracle は「replacement は `enabled=true` 直後に一度描けているのに、その後の補正シーケンスで 0 化されている」可能性を最有力と判断した。外部 TMP 調査も、新規 TMP の font/material 初期化と canvas rebuild のタイミング差で `chars>0 -> 0` が起こりうることを補強した。
- これを受けて、`TextShellReplacementRenderer` に 2 つの修正を入れた。1) `SyncReplacement(...)` で original の `font` / `fontSharedMaterial` を replacement に明示継承。2) `replacement.enabled = true` 直後に `chars>0` ならその場で成功扱いにして、それ以降の `ApplyToText(...)` / resync / canvas update 系を通さない。
- 変更後の build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回ランタイム確認では `InventoryLineReplacement/v1` に `phase='afterEnable'` 付き成功ログが出るか、item row が実際に可視になるかを確認する。

## 2026-03-15 Inventory / Compare UI progress (inventory probes stripped of ForceMeshUpdate)
- 次回 run では実際に `phase='afterEnable'` 成功ログが出たが、その直後の `InventoryLineUpdateProbe/v1` / `InventoryLineReplacementStateNextFrame/v1` では replacement が `active=True enabled=True` のまま `chars=0` に落ちた。
- ここで、inventory 専用 observability が replacement に対して毎フレーム/次フレームで `ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true)` を呼んでいることを確認した。観測コード自体が成功した replacement を壊している可能性が高い。
- 最小修正として `InventoryLineUpdateObservability` と `TextShellReplacementRenderer.TryBuildReplacementState(...)` から該当 `ForceMeshUpdate(...)` を削除した。build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。
- 次回確認では「afterEnable 成功 rows が、そのまま次フレーム以降も可視に残るか」を最優先で見る。ここで改善すれば probe-induced regression がほぼ確定する。

## 2026-03-15 Inventory / Compare UI progress (success branch redraw nudge)
- probe の `ForceMeshUpdate(...)` を外した後の run でも next-frame 0 化は続いたため、通常 redraw 側の問題が残っていると判断した。
- 新しい仮説は「`afterEnable` 成功直後に original を disable したあと、replacement を通常 Canvas redraw に十分 dirty として乗せ直せていない」である。これに対して success branch に `replacement.havePropertiesChanged = true; replacement.SetAllDirty();` を追加した。
- さらに `InventoryLineReplacementStateNextFrame/v1` を拡張し、next-frame の replacement について `propsChanged`, `font`, `material`, `canvasA`, `subMeshes` を非破壊で記録するようにした。
- 変更後の build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では blank rows の可視維持と、新しい next-frame state fields の値を見て次の分岐を決める。

## 2026-03-15 Inventory / Compare UI progress (replacement clone strategy)
- 新しい next-frame state では replacement が `font/material/canvas/subMeshes` を保持したまま geometry だけ落ちていたため、fresh TMP replacement 自体の hidden serialized state 差分がまだ主因候補として残った。
- そこで `GetOrCreateReplacement(...)` を `new GameObject + new TextMeshProUGUI` から `UnityEngine.Object.Instantiate(original.gameObject)` ベースへ変更し、original leaf をクローンした replacement を使う実験に切り替えた。これで original が持っている TMP/CanvasRenderer の隠れ状態もまとめて継承させる。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run の最優先確認点は、translated item rows が通常更新後も可視のまま残るかどうか。

## 2026-03-15 Inventory / Compare UI progress (clone strategy reverted)
- clone-based replacement の最新 run では `afterEnable` 成功が消え、`ReplacementOnEnableProbe/v1` が `chars=0` のまま、`InventoryLineReplacement/v1` も `replaced=0` に後退した。replacement は next frame で `active=False enabled=False` になっており、clone strategy は regression と判断した。
- 同時に sentinel probe では original leaf が `TEST` は描ける (`sentinelChars=4`) 一方、restored original text へ戻すと再び `chars=0` になった。これにより、hidden state よりも restored/formatted text path 自体に問題がある可能性がさらに強まった。
- clone strategy は撤回し、`GetOrCreateReplacement(...)` は fresh `new GameObject + TextMeshProUGUI` 方式へ戻した。build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。
- 次回の調査焦点は hidden state ではなく text-content/path 側（rich text, inline symbols, formatting restore path）の切り分けである。

## 2026-03-15 Inventory / Compare UI progress (two blank classes)
- 最新ログでは blank を 2 系統に分けられた。A) `formatted-present` で replacement success 後に next frame 0 化する行。B) `InventoryRenderProbe/v6[empty-formatted]` で `display='{{W|}}' raw='{{W|}}' formatted=''` になっている行。
- B 系統についてコードを確認したところ、`TranslatePreservingColors(...)` は stripped text が空なら元 source を返すだけなので、translator/restore 処理が空文字を作っているわけではない。つまり `{{W|}}` は upstream text source の時点で中身が空である。
- これにより今後は、B 系統は source generation 側の問題、A 系統は replacement persistence 側の問題として分離して調査する方針に整理した。

## 2026-03-15 Inventory / Compare UI progress (empty-formatted source tracing)
- B 系統 (`empty-formatted`) の source を直接特定するため、`InventoryRenderObservability` のログに `dataType` と `item` 概要を追加した。次回 run で `{{W|}}` 行がどの row data / item に対応するかを直接見られる。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回ログ確認では `InventoryRenderProbe/v6[empty-formatted]` の `dataType` と `item` を最優先で確認する。

## 2026-03-15 Inventory / Compare UI progress (Items.jp.xml exact white-stub cleanup)
- `{{W|}}` が upstream source であることを受けて、`Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml` の exact white empty stubs を埋めた。書籍系は `Books.jp.xml` の title を反映し、その他は item 名を設定した。
- `xmllint --noout Mods/QudJP/Localization/ObjectBlueprints/Items.jp.xml` は成功、`grep 'DisplayName="{{W|}}"'` は 0 件、`python3 scripts/sync_mod.py` で再配備済み。
- 次回 run では `empty-formatted` の消滅有無を最優先で確認する。残る場合は white 以外の空色タグや別 source を追う。

## 2026-03-15 Inventory / Compare UI progress (first-open lag mitigation pass)
- 初回 inventory open だけマウス選択が重い件について、investigation code 側の first-open bias が濃厚と判断した。理由は、`InventoryLine.setData(...)` / `InventoryLine.Update()` / inventory screen open / tooltip hover の各所で大量の inventory-specific probe と repair が走っていたため。
- 最小安全策として、`InventoryLineUpdateProbePatch` を no-op 化し、`InventoryScreenChildTextProbePatch` / `InventoryUiFieldProbePatch` の高頻度 inventory snapshot を停止し、`LookTooltipContentPatch` から `ScheduleVisibleInventoryRepairs()` を削除した。repair core は残しているので、描画 workaround 全停止ではない。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では first-open の操作感改善と rendering 変化を確認する。

## 2026-03-15 Inventory / Compare UI progress (scheduler cleanup + active-on-create replacement)
- 最新ログでは first-open lag がなお残っていたため、残存 hot path を再評価した。`InventoryLineRenderProbePatch` の setData 後処理と、tooltip/compare scheduler がまだ主な負荷源候補である。
- tooltip/compare 側は `DelayedSceneProbeScheduler` を repair-only の 1 attempt にしているが、未使用 state を整理して lightweight 状態を確定させた。
- blank row 本体については、`GetOrCreateReplacement(...)` が replacement を inactive のまま生成していた点を root-cause 候補として修正した。replacement は active に生成しつつ `enabled = false` に保ち、TMP/Canvas の初期化だけ先に済ませる。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では first-open lag と replacement persistence の両方を確認する。

## 2026-03-15 Inventory / Compare UI progress (active-on-create reverted, failure probes removed)
- 次 run で first-open lag 改善は実際に確認できたが、rendering は悪化した。`afterEnable` 成功が消えて `replaced=0` に戻り、replacement は next frame で `active=False enabled=False` になったため、active-on-create は regression と判断した。
- そのため `GetOrCreateReplacement(...)` の active-on-create は撤回し、inactive-on-create に戻した。
- 同時に `DelayedInventoryLineRepairScheduler` から failure-only probes を外した。これで scheduler は repair core を残しつつ、`TEST` sentinel や next-frame diagnostics による余計な干渉を避ける構成になった。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では lag 改善維持と rendering の戻り具合を確認する。

## 2026-03-15 Inventory / Compare UI progress (preserve successful replacement on later repairs)
- rollback 後の最新ログでは `afterEnable` 成功が戻っていたため、repair core 自体は replacement を作れていると判断した。
- 次の本命として、「success branch で `original.enabled = false` にした line を、次回 `TryRenderReplacementTexts(...)` が `!original.enabled` と見て `TryDisableReplacement(...)` している」可能性を修正した。
- 既存 replacement が active/enabled かつ text を持ち、軽い `ForceMeshUpdate(..., forceTextReparsing: false)` 後も `characterCount > 0` なら、その replacement を `phase='reuseActive'` で再利用する分岐を追加した。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では `reuseActive` の出現と item row の残留を確認する。

## 2026-03-15 Inventory / Compare UI progress (remove sync failure check from setData)
- 最新症状に対して、inventory first-open lag の大きな hot path である `HasFailingEligibleTextShellLeaf` を `InventoryLine.setData(...)` から外した。repair scheduling は残すが、失敗判定のための同期 `ForceMeshUpdate` は削減する。
- さらに scheduler は replacement repair に集中するように絞り、general invisible-text repair や child TMP 全走査の `ForceMeshUpdate` ループを外した。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では lag の改善度と replacement の持続を確認する。

## 2026-03-15 Inventory / Compare UI progress (selection diagnostics removed)
- translated row 選択時のもたつき対策として、selection path の pure diagnostic patch を停止した。`HandleSelectItemProbePatch` の `CompareProbeRunner.Run(__instance)` と、`SelectableTextMenuItemSelectChangedProbePatch` の Prefix/Postfix 観測を no-op 化した。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では translated row のクリック/hover 体感が改善するかを確認する。

## 2026-03-15 Inventory / Compare UI progress (replacement becomes raycast-capable)
- translated row だけ selection が引っかかる件について、replacement の `raycastTarget = false` 固定が原因候補として残ったため、replacement も original の `raycastTarget` を継承するよう変更した。
- あわせて `ReplacementOnEnableProbe` と per-line replacement log を停止し、first-open のログ負荷をさらに削減した。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では translated row のマウス選択改善を確認する。

## 2026-03-15 Inventory / Compare UI progress (do not disable original on replacement success)
- translated row だけの選択不具合に対して、replacement success branch で `original.enabled = false` にしていた点を見直した。blank translated row では original leaf が `chars=0` で不可視のため、enabled を維持しても視覚的競合は小さい。
- success branch から `original.enabled = false` を削除し、元 row の selection/hit path を残したまま replacement を重ねる構成に変更した。
- build/test は通過し、`python3 scripts/sync_mod.py` で再配備済み。次回 run では translated row の selection と描画が改善するかを確認する。

## 2026-03-15 Inventory / Compare UI progress (English UI font drift fix)
- 英字 UI フォント drift は、global TMP patch と popup repair が primary CJK font を広く差し替えていたことが主因候補。
- 修正として `FontManager.ApplyToText(...)` を「既存 font は保持し、fallback chain だけ追加」に寄せた。
- `ComparePopupTextFixer.ApplyTmpFonts(...)` も `ForcePrimaryFont(...)` ではなく `ApplyToText(...)` を使うよう変更した。
- legacy `UI.Text` は非 ASCII を含む場合のみ fallback font へ切り替えるようにし、純英字 UI の見た目変化を抑える方向にした。
- 変更は build/test 通過・再配備済み。次回は英字 UI の見た目が戻るか、日本語 tooltip/compare が維持されるかを確認する。

## 2026-03-15 Inventory / Compare UI progress (replacement OnEnable culprit)
- `ReplacementOnEnableProbe/v1` により、`QudJPReplacementText` は `TextMeshProUGUI.OnEnable` patch の `ApplyToText(...)` 後に `chars>0 -> 0` へ落ちることが確認できた。inventory replacement failure の直接原因候補がここに絞られた。
- 修正として `TextMeshProUguiFontPatch.Postfix` から `QudJPReplacementText` を除外し、replacement 専用 TMP だけは `OnEnable` 時の `ApplyToText(...)` をスキップするようにした。
- build/test 通過・再配備済み。次回確認では `ReplacementOnEnableProbe/v1` が `skipApply` になり、inventory replacement が実際に描画へ進むかを見る。

## 2026-03-15 Inventory / Compare UI progress (afterEnableResync result + sibling probe)
- 最新 run では `afterEnableResync` も 0 のままで、enable 後の full resync でも回復しなかった。
- 次の診断として `afterOriginalDisableRefresh` を追加し、enable 後に original leaf を一時無効化した状態で replacement を再描画した結果を採るようにした。
- ここで `chars>0` に戻るなら、same `TextShell` 下で original/replacement が同時に enabled なのが干渉要因である可能性が高い。変更は build/test 通過・再配備済み。

## 2026-03-15 Inventory / Compare UI progress (sibling probe result + private TMP state)
- 最新 run では `afterOriginalDisableRefresh` も 0 のままで、original sibling 干渉も主因ではなさそう。
- 次の切り分けとして creation-stage snapshot に TMP private state (`isAwake`, `registered`, `ignoreActiveState`, `hasCanvas`) を追加した。
- これで enable 直後に TMP の内部初期化/Canvas 登録が壊れているかを直接ログで追える。変更は build/test 通過・再配備済み。
