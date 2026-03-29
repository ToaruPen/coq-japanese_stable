結論として、QudJP の最終表示 owner は route ごとにかなり差があり、強い owner は ConversationDisplayTextPatch・GetDisplayName*・FactionsLineTranslationPatch・Journal*DisplayTextPatch で、UITextSkinTranslationPatch・MessageLogPatch・PopupTranslationPatch の多くは observer であり、単独では最終表示 ownership を持ちません。
前提


Assumption A: coq-decompiled.tar.gz は QudJP が想定するゲーム build と実質整合している。PopupTranslationPatch などに「game version 2.0.4」の前提が埋め込まれています。


Assumption B: 他 mod の Harmony 競合は無視し、QudJP 単体で判定する。


Basis / evidence: qudjp 側 patch 実装と coq 側 decompiled 呼び出し元・描画先を両方追跡した。


uncertain: build drift があると target method 解決や UI 分岐が変わり、判定がずれる可能性はあります。


Ownership matrix
Route family主 owner / observer最終 sink判定主なリスク1. Popup / popup message / popup conversationmixed。PopupShowTranslationPatch / PopupMessageTranslationPatch は強め、PopupTranslationPatch 本体は弱い observerPopupMessage または console popupStatically narrowable but runtime confirmation required直呼び ShowBlock / ShowOptionList、popup suppression、old/new UI 分岐2. Message logMessageLogPatch は observer。実 owner は producer patch ごとMessageLogWindow / SidebarStatically narrowable but runtime confirmation requiredproducer coverage が限定的、下流で prefix/format 追加3. Conversation display textConversationDisplayTextPatch が強 ownerConversationUI.Render / RenderClassicStatically provablehotkey prefix、line clip4. Inventory and equipmentmixed。item name は強い、description は tooltip 別 routeInventoryLine / EquipmentLine / compare tooltipStatically narrowable but runtime confirmation requiredcached displayName、tooltip 分岐、slot/category 文字列5. Character statusmixed。detail pane は強いが screen 全体は穴ありCharacterStatusScreen の各 UITextSkinStatically narrowable but runtime confirmation requiredclassText / levelText の owner 不在6. Skills and powersmixed。details pane は強いが left list は弱いSkillsAndPowersStatusScreen / SkillsAndPowersLineStatically narrowable but runtime confirmation requiredSkillsAndPowersLine.setData / SPNode.ModernUIText 未所有7. FactionsFactionsLineTranslationPatch が最終 field ownerFactionsLineStatically provableほぼ layout のみ8. Descriptions / tooltipsupstream owner は強いが tooltip sink patch は弱いLook tooltip / BaseLineWithTooltipStatically narrowable but runtime confirmation requiredLookTooltipContentPatch は owner ではない、RTF/markup/trimming9. Journalentry row は Journal*DisplayTextPatch が強 ownerJournalLineStatically provablerecipe/category row は別 route10. Zone display namescore owner は強いが cache と banner 別 route ありstatus bar / minimap / sidebar / bannerStatically narrowable but runtime confirmation requiredZone._DisplayName と WorldFactory cache11. Player status bar / ability barmixed。status dictionary と target/effect/header は強い、ability button row は弱いPlayerStatusBar.Update / AbilityBar.UpdateStatically narrowable but runtime confirmation requiredability button row 未所有、direct .text + Apply()12. UITextSkin / sink familiesowner ではない observer familyUITextSkin.SetText → TMPRuntime-requiredlater overwrite、bypass、多数の非-UITextSkin sink

1. Popup / popup message / popup conversation
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/PopupTranslationPatch.cs, PopupShowTranslationPatch.cs, PopupMessageTranslationPatch.cs, QudMenuBottomContextTranslationPatch.cs


game: coq/XRL.UI/Popup.cs, coq/Qud.UI/PopupMessage.cs, coq/Qud.UI/QudBaseMenuController.cs, coq/Qud.UI/SelectableTextMenuItem.cs, coq/Qud.UI/QudMenuBottomContext.cs


A. Full rendering path


Popup.Show* 系:


mod PopupShowTranslationPatch.Prefix が Popup.Show / ShowYesNo / ShowYesNoCancel の message 引数を producer route として翻訳。


game XRL.UI.Popup.Show は、popup suppressed なら MessageQueue.AddPlayerMessage(Message)、通常なら ShowBlock(...) に流す。


mod PopupTranslationPatch.Prefix は ShowBlock / ShowOptionList / ShowConversation を捕まえるが、TranslatePopupTextForRoute() の中身は基本的に marker 除去 + unclaimed log で、未翻訳英語をその場で確定翻訳する owner ではない。


game Popup.ShowBlock は Markup.Transform、capitalize、MessageQueue.AddPlayerMessage を通した後、UIManager.UseNewPopups なら WaitNewPopupMessage(...)、旧 UI なら RenderBlock(...) → _TextConsole.DrawBuffer(...)。




PopupMessage.ShowPopup 系:


mod PopupMessageTranslationPatch.Prefix が message / buttons / items / title / contextTitle を前処理。


game Qud.UI.PopupMessage.ShowPopup が Message.SetText("{{y|" + message + "}}")、Title.SetText("{{W|" + title + "}}")、contextText.SetText(contextTitle)、controller.menuData = items、bottomContextOptions = buttons を設定。


QudBaseMenuController.UpdateElements() が menuItems[num].data = menuData[num]; menuItems[num].UpdateData();


SelectableTextMenuItem.SelectChanged() が最終的に item.SetText("{{W|...}}") / item.SetText("{{c|...}}") を実行。


bottom context は QudMenuBottomContext.RefreshButtons() → 各 SelectableTextMenuItem.Update()。




Popup.ShowConversation:


上流では ConversationDisplayTextPatch が node/choice text を翻訳。


PopupTranslationPatch の ShowConversation 側は observer 寄り。


game Popup.ShowConversation は Options[i] を QudMenuItem.text = "[n] " + Options[i] + "\n\n" に組み直して WaitNewPopupMessage(...) に渡す。




B. Ownership classification
Statically narrowable but runtime confirmation required


PopupTranslationPatch 自体は最終 owner ではありません。


強い owner は PopupShowTranslationPatch / PopupMessageTranslationPatch / 上流 ConversationDisplayTextPatch に分散しています。


direct caller が Popup.ShowBlock / ShowOptionList を叩くと、弱い observer patch にしか当たらない経路があります。


C. Risk points


PopupTranslationPatch.TranslatePopupTextForRoute() は未翻訳文字列をその場で翻訳せず、そのまま返す。


Popup.Show suppressed path は popup を出さず message log へ落ちる。


Popup.ShowBlock と PopupMessage.ShowPopup は色付け・markup・hotkey・改行を下流で追加する。


old/new popup UI で最終 sink が違う。


TryTranslatePopupProducerText() の coverage は exact leaf と一部 template 依存で、全面的ではない。


D. Evidence needed


L2G + L3 が必要です。


最低限、以下を分けて確認すべきです。


UIManager.UseNewPopups = true/false


Popup.Show から入る経路


Popup.ShowBlock / ShowOptionList を直呼びする経路


PopupMessage.ShowPopup を直呼びする経路


popup suppressed 経路




検証点は PopupMessage.Message / Title / menu item text / bottom context buttons / old console ScreenBuffer の最終可視文字列です。



2. Message log
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/MessageLogPatch.cs, PhysicsEnterCellPassByTranslationPatch.cs, ZoneManagerSetActiveZoneMessageQueuePatch.cs, MessageLogProducerTranslationHelpers.cs


game: coq/XRL.Messages/MessageQueue.cs, coq/XRL.Core/XRLCore.cs, coq/Qud.UI/MessageLogWindow.cs, coq/XRL.UI/Sidebar.cs


A. Full rendering path


mod MessageLogPatch.Prefix は MessageQueue.AddPlayerMessage(string,string,bool) を捕まえるが、処理は direct marker 除去 + unclaimed log です。


真の owner は message family ごとの producer patch です。


ZoneManagerSetActiveZoneMessageQueuePatch は Priority.First で zone banner を先に翻訳/mark。


PhysicsEnterCellPassByTranslationPatch は "You pass by ..." を先に翻訳/mark。




game MessageQueue.AddPlayerMessage は capitalize、color wrap を行い、XRLCore.Core.Game.Player.Messages.Add(Message) を呼ぶ。


MessageQueue.Add は Markup.Transform(Message) を通し、Messages.Add(Message)、XRLCore.CallNewMessageLogEntryCallbacks(Message) を実行。


modern UI:


MessageLogWindow.Init() が callback 登録。


AddMessage → _AddMessage → messageLog.Add(RTF.FormatToRTF(":: " + log))




classic UI:


Sidebar が Player.Messages.GetLines(0,12) を読み、


Text.DrawBottomToTop(...) で描画。




B. Ownership classification
Statically narrowable but runtime confirmation required


sink 自体は静的に追えます。


ただし MessageLogPatch は owner ではなく、最終 ownership は各 producer patch の有無に依存します。


C. Risk points


AddPlayerMessage の capitalize と color wrap。


MessageQueue.Add の Markup.Transform。


modern log は ::  prefix を足す。


classic sidebar は >^k &y 等の表示 prefix を足す。


producer patch がないメッセージ family はそのまま英語で通る可能性が高い。


D. Evidence needed


L2G + L3。


ケースを分けて、MessageLogWindow と Sidebar の両方を検証する必要があります。


raw English message


direct marker 付き message


pass-by message


zone banner message




owner を主張するなら、「どの producer patch が翻訳し、MessageLogPatch が marker を剥がし、最終ログに何が見えるか」を family ごとに 1 本ずつテストするのが必要です。



3. Conversation display text
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/ConversationDisplayTextPatch.cs


game: coq/XRL.World.Conversations/IConversationElement.cs, coq/XRL.World.Conversations/Choice.cs, coq/XRL.UI/ConversationUI.cs, coq/XRL.UI/Popup.cs


A. Full rendering path


game IConversationElement.Prepare() が GetText() → PrepareTextEvent.Send(...) → GameText.VariableReplace(...) を実行。


game IConversationElement.GetDisplayText(bool) が DisplayTextEvent.Send(...)、必要なら GameText.VariableReplace(...)、必要なら color wrap を行う。


game Choice.GetDisplayText(bool) はその返り値に GetTag() を追加する。


mod ConversationDisplayTextPatch.Postfix は その最終戻り値 に対して走る。ここで trailing action marker を除去し、翻訳する。


new UI:


ConversationUI.Render() が CurrentNode.GetDisplayText(WithColor:true) と CurrentChoices.Select(x => x.GetDisplayText(...)) を取得し、Popup.ShowConversation(...) へ渡す。




classic UI:


ConversationUI.RenderClassic() が RenderableLines / RenderableSelection を作り、内部で Element.GetDisplayText(WithColor:true) を読み、ScreenBuffer.WriteAt(...) で描画する。




B. Ownership classification
Statically provable


表示に使われる node/choice text のソースは両 UI branch とも GetDisplayText() です。


patch はその leaf return に postfix で入っており、下流は hotkey 付与・clip・wrap だけです。


C. Risk points


patch は末尾の  [marker] を意図的に落とす。設計上の情報消失はあります。


new UI conversation popup は選択肢に番号 prefix と改行を追加する。


classic UI は width に応じて clip する。


D. Evidence needed


L2/L2G で十分。


UIManager.UseNewPopups を true/false で切り替え、Render() と RenderClassic() の両方を走らせる。


検証点は popup intro/options または classic ScreenBuffer の文字列で、GetDisplayText() の翻訳結果がそのまま最終表示に入っていることです。



4. Inventory and equipment
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/GetDisplayNamePatch.cs, GetDisplayNameProcessPatch.cs, InventoryLocalizationPatch.cs, InventoryAndEquipmentStatusScreenTranslationPatch.cs, DescriptionLongDescriptionPatch.cs


game: coq/XRL.World/GameObject.cs, coq/XRL.World/GetDisplayNameEvent.cs, coq/Qud.UI/InventoryLineData.cs, coq/Qud.UI/InventoryLine.cs, coq/Qud.UI/EquipmentLine.cs, coq/Qud.UI/BaseLineWithTooltip.cs, coq/XRL.UI/Look.cs


A. Full rendering path


item name:


GameObject.DisplayName → GetDisplayNameEvent.GetFor(...) → ProcessFor(GameObject,bool)


mod GetDisplayNamePatch と GetDisplayNameProcessPatch が __result を翻訳。


InventoryLineData.displayName は go?.DisplayName を cache。


InventoryLine.setData() は text.SetText(inventoryLineData.displayName)。


EquipmentLine.setData() は itemText.SetText(gameObject?.DisplayName ?? "{{K|-}}")。




item description / compare tooltip:


InventoryLine / EquipmentLine は tooltipGo / tooltipCompareGo を設定。


BaseLineWithTooltip.StartTooltip() → Look.GenerateTooltipInformation(go)


そこで Description.GetLongDescription(StringBuilder) が呼ばれ、mod DescriptionLongDescriptionPatch が append 部分だけ翻訳。


同時に go.GetDisplayName(...) は display-name patch 済み。


BaseLineWithTooltip.StartTooltip() は DisplayName / LongDescription / WoundLevel 等を ParameterizedTextField.value に詰めて tooltip 表示。




command/help text:


InventoryAndEquipmentStatusScreen.UpdateViewFromData() の後で mod postfix が CMD_OPTIONS などの Description を書き換える。




B. Ownership classification
Statically narrowable but runtime confirmation required


item name subrouteだけなら Statically provable に近いです。


ただし family 全体は tooltip・screen command・slot/category 表示を含むので mixed です。


C. Risk points


InventoryLineData.displayName は cache する。


tooltip は hover/focus 経路で別パイプライン。


EquipmentLine の部位ラベル (GetCardinalDescription) は別 source。


category 名、重量、hotkey、slot 表示は別 route。


D. Evidence needed


L2G が必要、tooltip は L3 推奨です。


少なくとも以下を分けてテストします。


InventoryLine.text


EquipmentLine.itemText


compare tooltip の ParameterizedTextField


command description




cache 影響を見るため、同一 object の再描画も確認すべきです。



5. Character status
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/CharacterStatusScreenTranslationPatch.cs, CharacterStatusScreenMutationDetailsPatch.cs, CharacterStatusScreenAttributeHighlightPatch.cs, CharacterStatusScreenHighlightEffectPatch.cs, CharacterStatusScreenTextTranslator.cs


game: coq/Qud.UI/CharacterStatusScreen.cs


A. Full rendering path


CharacterStatusScreen.UpdateViewFromData() は


nameText.SetText(GO.DisplayName)


classText.SetText(GO.GetGenotype() + " " + GO.GetSubtype())


levelText.SetText("Level: ... HP ... XP ... Weight ...")


attributePointsText / mutationPointsText
を設定。




mod CharacterStatusScreenTranslationPatch.Postfix は points 2 つだけ を翻訳。


HandleHighlightMutation() は mutationNameText, mutationRankText, mutationTypeText, mutationsDetails を設定し、mod postfix がその exact UI field を再書換え。


HandleHighlightAttribute() は primary/secondary/resistanceAttributesDetails のどれかに SetText(...); mod postfix が実際にセットされた field を再書換え。


HandleHighlightEffect() は mutationsDetails に effect detail を書き、mod postfix がそれを再書換え。


B. Ownership classification
Statically narrowable but runtime confirmation required


detail pane 系は強いです。


ただし screen 全体では classText と levelText に直接 owner patch が見つかりません。


basis: CharacterStatusScreenTextTranslator.cs に Level: 向け翻訳ロジックはありますが、repo 内検索ではそれを levelText に適用する hook は確認できませんでした。


C. Risk points


classText は GetGenotype()+GetSubtype() で別 source。


levelText は screen method 内で直接組み立て。


mutationPointsText は media size によって MP: / full label に分岐。


highlight 대상によって別 field が使われる。


D. Evidence needed


L2G + L3。


UpdateViewFromData(), HandleHighlightMutation(), HandleHighlightAttribute(), HandleHighlightEffect() を分けて検証。


特に classText と levelText は別途 assertion が必要です。ここは gap 検出テストにすべきです。



6. Skills and powers
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/SkillsAndPowersStatusScreenTranslationPatch.cs, SkillsAndPowersStatusScreenDetailsPatch.cs


game: coq/Qud.UI/SkillsAndPowersStatusScreen.cs, coq/Qud.UI/SkillsAndPowersLine.cs, coq/XRL.UI/SPNode.cs


A. Full rendering path


SkillsAndPowersStatusScreen.ShowScreen() は


nameBlockText.SetText(Grammar.MakePossessive(GO.DisplayName) + " Skills")


statBlockText.SetText(...)
を直接設定。




UpdateData() / UpdateViewFromData() は spText.SetText("Skill Points (SP): ...") と line data 構築を行い、mod postfix は spText を翻訳。


UpdateDetailsFromNode(SPNode) は detailsText, skillNameText, learnedText, requirementsText, requiredSkillsText, requiredSkillsHeader を設定し、mod SkillsAndPowersStatusScreenDetailsPatch が exact field を再書換え。


左リストは SkillsAndPowersLine.setData() が


skillText.SetText(d.entry.Name)


skillRightText.SetText(...)


powerText.SetText(d.entry.ModernUIText(GO))
を行う。




SPNode.ModernUIText() は Skill.Name, Power.Name, requirement/exclusion を組み立てる。


この左リスト側に対応する dedicated patch は repo 上では見つかりませんでした。


B. Ownership classification
Statically narrowable but runtime confirmation required


右 details pane はかなり強い owner です。


ただし family 全体では left list / screen header が弱いです。


C. Risk points


SkillsAndPowersLine.setData と SPNode.ModernUIText に owner patch がない。


nameBlockText / statBlockText も弱い。


power row は requirement/exclusion を動的合成するので、exact leaf 辞書だけでは足りない可能性が高い。


D. Evidence needed


右 pane は L2G で確認可能。


left list / power row / header は L3 必須。


skill row、power row、learned/unlearned、insufficient SP、prereq/exclusion、page scroll の各ケースが必要です。



7. Factions
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/FactionsLineDataTranslationPatch.cs, FactionsLineTranslationPatch.cs


game: coq/Qud.UI/FactionsStatusScreen.cs, coq/Qud.UI/FactionsLineData.cs, coq/Qud.UI/FactionsLine.cs


A. Full rendering path


FactionsStatusScreen.UpdateViewFromData() が FactionsLineData.set(id,label,icon,expanded) を作る。


mod FactionsLineDataTranslationPatch は upstream で label と searchText を調整。


controller が FactionsLine.setData(data) を呼ぶ。


game FactionsLine.setData() は barText, barReputationText, detailsText, detailsText2, detailsText3 を設定。


mod FactionsLineTranslationPatch.Postfix はその 直後 に現在値を読んで翻訳し、SetText で書き戻す。details wrapping もここで確定。


B. Ownership classification
Statically provable


最終 on-screen line を埋める exact method に postfix で入っています。


下流に別の text source は見当たりません。


C. Risk points


実 owner は FactionsLineDataTranslationPatch ではなく FactionsLineTranslationPatch。


もし別の後続 method が FactionsLine field を上書きするなら bypass になりますが、少なくとも coq/Qud.UI/FactionsLine.cs では確認できませんでした。


D. Evidence needed


L2/L2G で十分。


expanded/collapsed 両方で barText, barReputationText, detailsText* の 5 field を assert すれば ownership を証明できます。



8. Descriptions / tooltips
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/DescriptionLongDescriptionPatch.cs, DescriptionShortDescriptionPatch.cs, LookTooltipContentPatch.cs, BaseLineWithTooltipStartTooltipPatch.cs, GetDisplayNamePatch.cs, GetDisplayNameProcessPatch.cs


game: coq/XRL.UI/Look.cs, coq/Qud.UI/BaseLineWithTooltip.cs


A. Full rendering path


general tooltip:


Look.GenerateTooltipInformation(go) が


Description.GetLongDescription(StringBuilder) を呼ぶ


go.GetDisplayName(...) を取る




mod DescriptionLongDescriptionPatch と GetDisplayName* がここを翻訳。


Look.GenerateTooltipContent() は DisplayName, LongDescription, SubHeader, WoundLevel を連結し Markup.Transform()。


mod LookTooltipContentPatch.Postfix はその戻り値に入るが、中身は UITextSkinTranslationPatch.TranslatePreservingColors() 呼び出しで、実質 observer。


Look.ShowItemTooltipAsync() / SetupItemTooltipAsync() が tooltip.SetText("BodyText", RTF.FormatToRTF(...))。




compare tooltip:


BaseLineWithTooltip.StartTooltip() は Look.GenerateTooltipInformation(go/compareGo) を取得し、


ParameterizedTextField.value = RTF.FormatToRTF(Markup.Color("y", ...))


tooltip.ShowManually(...)


mod BaseLineWithTooltipStartTooltipPatch は probe だけで owner ではない。




short description:


DescriptionShortDescriptionPatch は別 caller の GetShortDescription(...) 用で、主 tooltip path ではない。




B. Ownership classification
Statically narrowable but runtime confirmation required


本当の owner は tooltip sink patch ではなく、Description.GetLongDescription と GetDisplayName の upstream です。


tooltip family 自体はパイプラインが 2 本あり、下流 format も多いです。


C. Risk points


LookTooltipContentPatch を owner と見なすと誤判定になります。


Markup.Transform, RTF.FormatToRTF, Trim() が下流でかかる。


SubHeader / WoundLevel は別 producer。


compare tooltip は yellow recolor をかける。


D. Evidence needed


L2G + L3。


GenerateTooltipInformation の field 値と、実 tooltip の field 値を分けて確認する必要があります。


general tooltip と compare tooltip を別 testcase にするべきです。



9. Journal
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/JournalEntryDisplayTextPatch.cs, JournalMapNoteDisplayTextPatch.cs, JournalAccomplishmentAddTranslationPatch.cs, JournalMapNoteAddTranslationPatch.cs, JournalObservationAddTranslationPatch.cs


game: coq/Qud.UI/JournalStatusScreen.cs, coq/Qud.UI/JournalLineData.cs, coq/Qud.UI/JournalLine.cs


A. Full rendering path


entry object の GetDisplayText() に mod postfix が入り、translated __result を返す。


JournalStatusScreen.UpdateViewFromData() が JournalLineData を組む。


JournalLineData.set() は searchText = entry?.GetDisplayText()?.ToLower() を保存するので、検索用 text も同じ owner を使う。


JournalLine.setData() は sb.Append(journalLineData.entry?.GetDisplayText() ?? "") のあと、text.SetText(sb.ToString()) または small screen なら StringFormat.ClipText(...) で表示する。


B. Ownership classification
Statically provable


journal entry row の表示 text source は entry.GetDisplayText() で一本化されています。


patch はその leaf return に直接入っています。


C. Risk points


recipe note や category header は別 route。


small screen の clip はあります。


D. Evidence needed


L2/L2G で十分。


accomplishment / observation / map note / tomb engraving wrapper をそれぞれ 1 つずつテストし、JournalLineData.searchText と最終 JournalLine.text の両方を確認すれば十分です。



10. Zone display names
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/ZoneDisplayNameTranslationPatch.cs, ZoneManagerSetActiveZoneTranslationPatch.cs, ZoneManagerSetActiveZoneMessageQueuePatch.cs, MessageLogProducerTranslationHelpers.cs


game: coq/XRL.World/ZoneManager.cs, coq/XRL.World/Zone.cs, coq/XRL.World/WorldFactory.cs, coq/Qud.UI/PlayerStatusBar.cs, coq/XRL.UI/Sidebar.cs


A. Full rendering path


core:


ZoneManager.GetZoneDisplayName(...) 3 overload に mod ZoneDisplayNameTranslationPatch.Postfix が入る。




cached consumers:


Zone.DisplayName は _DisplayName ??= The.ZoneManager.GetZoneDisplayName(ZoneID)。


WorldFactory.ZoneDisplayName(id) も ZoneIDToDisplay に cache。




UI:


PlayerStatusBar.BeginEndTurn() が currentCell.ParentZone.DisplayName を playerStringData["Zone"] / ["ZoneOnly"] に積む。


PlayerStatusBar.Update() が ZoneText.SetText(...) と minimap label SetText(" " + ZoneOnly)。


classic sidebar は WorldFactory.Factory.ZoneDisplayName(The.Player.CurrentZone.ZoneID) を直接書く。




zone banner / log:


ZoneManager.SetActiveZone() は WorldFactory.Factory.ZoneDisplayName(zoneID) を使って MessageQueue.AddPlayerMessage(..., 'C')。


mod ZoneManagerSetActiveZoneMessageQueuePatch が Priority.First でそれを intercept し、ZoneManagerSetActiveZoneTranslationPatch.TryTranslateQueuedMessage() で banner text を翻訳/mark。




B. Ownership classification
Statically narrowable but runtime confirmation required


GetZoneDisplayName() 自体は強い owner point です。


ただし Zone._DisplayName と WorldFactory.ZoneIDToDisplay の 2 段 cache、および banner 用 message route が別に存在します。


C. Risk points


stale cache。


SetZoneDisplayName / UpdateZoneDisplayName による別更新。


banner は time 連結と message log format を通る。


同じ zone 名が UI ごとに Zone.DisplayName と WorldFactory.ZoneDisplayName に分かれている。


D. Evidence needed


L2G + L3。


重要なのは「cache warm 後の再表示」です。


fresh visit、same zone revisit、SetActiveZone banner、status bar、minimap label、classic sidebar を別々に確認する必要があります。



11. Player status bar / ability bar
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/PlayerStatusBarProducerTranslationPatch.cs, PlayerStatusBarProducerTranslationHelpers.cs, AbilityBarAfterRenderTranslationPatch.cs, AbilityBarUpdateAbilitiesTextPatch.cs


game: coq/Qud.UI/PlayerStatusBar.cs, coq/Qud.UI/AbilityBar.cs


A. Full rendering path


player status bar:


PlayerStatusBar.BeginEndTurn(XRLCore) が playerStringData に Zone, ZoneOnly, PlayerName, FoodWater, Weight, Temp, Time, HPBar を詰める。


mod PlayerStatusBarProducerTranslationPatch.Postfix が dictionary value を field 名ごとに翻訳。


PlayerStatusBar.Update() が各 UITextSkin へ SetText(...)。


同 patch の Update() postfix は XPBar.text の現在値を読んで翻訳し直す。




ability bar:


AbilityBar.AfterRender(...) が effectText, targetText, targetHealthText string field を作る。


mod AbilityBarAfterRenderTranslationPatch がその string field 自体を書換える。


AbilityBar.Update() が EffectText.SetText(...), TargetText.SetText(...), TargetHealthText.SetText(...) を行う。


AbilityBar.UpdateAbilitiesText() は CycleCommandText.GetComponent<UITextSkin>().text = ...; Apply(); と AbilityCommandText... を直に叩く。mod AbilityBarUpdateAbilitiesTextPatch はその後で header/cycle text を再書換え。


ただし ability button row 本体は AbilityBar.Update() ループ内で component.Text.SetText(...) されるだけで、dedicated owner patch は見つかりませんでした。




B. Ownership classification
Statically narrowable but runtime confirmation required


status bar dictionary route、ability target/effect/header route は強いです。


ただし family 全体では ability button row が弱いです。


C. Risk points


PlayerName は DisplayNameOnlyDirect を使うので GetDisplayName route ではない。


zone 表示は zone-name cache 問題を継承。


AbilityBar.UpdateAbilitiesText() は generic UITextSkin.SetText patch を bypass する direct .text + Apply()。


ability button row は cooldown / toggle / hotkey を動的合成する。


D. Evidence needed


status bar 本体と target/effect/header は L2G で追えます。


ability button row は L3 必須 です。


disabled / cooldown / toggle on/off / multiple page / modern UI hotkey のケースを全部見る必要があります。



12. UITextSkin / sink families
対象 file


mod: qudjp/Mods/QudJP/Assemblies/src/Patches/UITextSkinTranslationPatch.cs, SinkPrereqUiMethodTranslationPatch.cs, SinkPrereqSetDataTranslationPatch.cs, SinkPrereqTextFieldTranslator.cs


game: coq/XRL.UI/UITextSkin.cs


A. Full rendering path


game UITextSkin.SetText(string) は this.text = text、formattedText = null、Apply() を行う。


Apply() は text.ToRTFCached(...) を経て tmp.text に流す。


mod UITextSkinTranslationPatch.Prefix は TranslatePreservingColors(text, ...) を呼ぶが、実装は


direct marker があれば除去


そうでなければ unclaimed log


最後は原文を返す
という observer です。




SinkPrereqUiMethodTranslationPatch / SinkPrereqSetDataTranslationPatch / SinkPrereqTextFieldTranslator も、既に field に入った text を読んで同じ translator を再適用するだけで、ownership を作りません。


さらに bypass がある:


AbilityBar.UpdateAbilitiesText() は .text = ...; Apply() を直に呼ぶ。


old popup / classic conversation / classic message log は UITextSkin を通らず ScreenBuffer / _TextConsole.DrawBuffer / Text.DrawBottomToTop に行く。




B. Ownership classification
Runtime-required


ただし重要なのは、これは「強い owner かもしれないので runtime が必要」という意味ではなく、静的に見て universal final sink owner ではない ということです。


UITextSkinTranslationPatch を ownership point と見なすのは誤りです。


C. Risk points


後続 SetText() が前回値を普通に上書きする。


direct .text + Apply() が patch を bypass。


console 系 sink は完全に bypass。


patch 本体が translation ではなく observation/marker-strip である。


D. Evidence needed


owner claim をするなら L3 必須。


逆に「owner ではない」ことを示す negative test は簡単です。


UITextSkin.SetText() 後に別 SetText() を呼んで上書きできること


.text + Apply() が patch を通らないこと


classic popup / sidebar が UITextSkin を使わないこと




この 3 本で universal sink ではないことは十分立証できます。



まとめ
強 owner と見てよいのは、次の 4 系統です。


ConversationDisplayTextPatch


GetDisplayNamePatch / GetDisplayNameProcessPatch（item name 系）


FactionsLineTranslationPatch


JournalEntryDisplayTextPatch / JournalMapNoteDisplayTextPatch


逆に、owner と見てはいけないのは次です。


UITextSkinTranslationPatch


MessageLogPatch


PopupTranslationPatch の route-side translator


LookTooltipContentPatch


BaseLineWithTooltipStartTooltipPatch


SinkPrereqUiMethodTranslationPatch / SinkPrereqSetDataTranslationPatch


optional
改修優先度を付けるなら、穴が大きいのは SkillsAndPowersLine.setData / SPNode.ModernUIText、CharacterStatusScreen の classText / levelText、AbilityBar.Update() の button row 本体です。