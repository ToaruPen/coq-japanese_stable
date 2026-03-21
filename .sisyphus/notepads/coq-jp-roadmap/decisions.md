# Decisions

## 2026-03-11 Task 0 PoC
- ローカルPoC検証の一次ターゲットは `net10.0`（実行可能性を優先）。
- ゲーム投入DLLは Unity/Mono 互換を優先して `net48` でビルド。
- Part B は手動ゲーム起動 + Player.log 検証をゲート条件として継続採用。

## 2026-03-11 Task 0: Target Framework Decision
- Test project: net10.0 (primary runner, dotnet SDK 10.0.100)
- Mod DLL: net48 (matches game's Mono runtime)
- Assembly-CSharp.dll reference: `<Private>false</Private>` (reference-only, not copied)
- Harmony: NuGet Lib.Harmony 2.4.2 for tests, game-bundled 0Harmony 2.2.2.0 for runtime
- 3-Layer test strategy: CONFIRMED viable after Part A success

## 2026-03-11 Task 1 スキャフォールディング（再実行）
- `.sln` ファイルは手動作成（dotnet CLI が `.slnx` に変換する問題を回避）。
- `.editorconfig` の C# スタイルルールは severity=error で統一（`dotnet_style_qualification` 系）。
- CI は `hashFiles()` ガードで条件付き実行（テストプロジェクト・Pythonファイル未存在時にスキップ）。
- pyproject.toml の Ruff ignore は `D100`, `D104`, `COM812`, `ISC001` の4ルールのみ。

## 2026-03-13 Task 18-20 Harmony runtime / Rosetta decision
- Apple Silicon ネイティブ ARM64 + Unity Mono + game-bundled `0Harmony 2.2.2.0` では、Harmony patch 適用が広範囲に失敗する前提で扱う。
- macOS Apple Silicon 上の実ゲーム確認は、以後 `scripts/launch_rosetta.sh` または `Launch CavesOfQud (Rosetta).command` を使った Rosetta 起動を標準手順とする。
- 根拠: ネイティブ起動では `HarmonySharedState` / `mprotect returned EACCES` 系の障害が出た一方、Rosetta 起動では `Harmony patching complete: 22 method(s) patched.` まで進んだ。
- したがって、Apple Silicon 上で native ARM64 ログだけを見て個別 patch を掘るのは後回しにし、まず Rosetta での再現・比較を優先する。

## 2026-03-13 Task 20 world generation safety decision
- `HistoricStringExpanderPatch` は当面 runtime で無効化する。
- 根拠: Rosetta 起動後、`HistorySpice` 生成中に `spice reference 時間/ガラスの/遊牧民 ... wasn't a node` が大量発生し、その後 `CherubimSpawner.ReplaceDescription` の `ArgumentOutOfRangeException` と `Worships.Generate` の `NullReferenceException` でワールド生成が停止した。
- これは `HistoricStringExpanderPatch` が表示文だけでなく history generation 用の symbolic key まで翻訳して破壊している仮説と最も整合する。
- 当面は playability を coverage より優先し、world generation が通ることを先に確保する。歴史文ローカライズは後で表示専用ケースに限定して再導入する。

## 2026-03-20 Route-first localization decisions
- `Player.log` は backlog の証拠として使い、翻訳対象の優先順位は route 単位で決める。raw diff 件数だけでは作業順を決めない。
- `scripts/verify_inventory.py` は inventory/equipment 表示検証専用とし、一般的な未訳観測には使わない。一般観測の自動化は char-gen から inventory 各タブまで辿れる場合に限る。
- `Colors.xml` display name は player-facing surface が未確認のため active scope から外す。
- stateful liquid container (`waterskin`, `canteen`, wine/honey/slime variants) は static XML `DisplayName` で解かず、`logic-required` として template/route 側で扱う。
- shortcut / checkbox / pseudo-graphic (`[Space]`, `[Esc]`, `[R]`, `[Delete]`, `[ ][n]`, `[■][n]`) は観測ノイズとして扱う。
- already-localized direct-route text は翻訳自体は通しつつ、missing log だけ suppress する。対象 route は `MainMenuLocalizationPatch`, `CharGenLocalizationPatch`, `GetDisplayNamePatch`, `GetDisplayNameProcessPatch`, `InventoryLocalizationPatch`, `FactionsStatusScreenTranslationPatch`。
- `GetDisplayName*` の mixed display name (`lacquered サンダル` など) は exact-match 辞書の量産ではなく、前置 modifier だけを route 側で翻訳して base noun へ連結する。

## 2026-03-20 Popup / UITextSkin fixed-label batch decision
- `PopupTranslationPatch` の `Do you really want to attack the {0}?` は exact key の量産ではなく template 化で吸う。
- `PopupTranslationPatch` の攻撃確認は `Do you really want to attack {0}?` と `Do you really want to attack the {0}?` の両方を template で受ける。
- `PopupTranslationPatch` の pure Japanese target names は already-localized text として missing にしない。
- `UITextSkinTranslationPatch` の `Save Tombstone File`, `Exit`, `Armor`, `Books`, `Building zone...` は fixed residual とみなし、辞書で処理する。
- `InventoryLocalizationPatch` の `水袋 [empty]`, `水袋 [32 drams of fresh water]`, `たいまつ x11 (unburnt)` など数量/状態 readout は引き続き `logic-required` に留める。

## 2026-03-20 Factions status noise decision
- `FactionsStatusScreenTranslationPatch` では template 適用前後とは別に、日本語化済みの faction label そのものを direct-route text として扱い、missing key にしない。
- したがって faction status の残差は英語 faction 名本体と reputation/village template に絞って扱う。

## 2026-03-20 Generated faction and title-prefix decision
- `Cult of Oroyumed` など `FactionsStatusScreen` に残る英語 faction 名は `references/Base/Factions.xml` に存在しないため、static faction asset ではなく generated proper name 側として扱い、現時点では static XML で増やさない。
- `Amrodの村人たち` のような mixed village labels も generated proper name を含むため、translation backlog ではなく generated-name/defer 側へ寄せる。
- 一方 `Warden イラメ`, `Barathrumite 工匠`, `Barathrumite アルコノート` のような `英語 prefix + 既存日本語 base` は mixed display-name route で吸えるので、`ui-displayname-adjectives.ja.json` に prefix を足して処理する。

## 2026-03-20 Popup refusal template decision
- `The {0} refuses to speak to you.` は popup 固定文型で、target 名だけが可変なので exact key 量産ではなく `PopupTranslationPatch` + `ui-popup.ja.json` の template で処理する。
- target がすでに日本語のときは popup route で再翻訳せず、そのままテンプレートへ差し込む。

## 2026-03-20 Category-label lookup correction
- latest `Player.log` で `Scrap`, `Data Disks`, `Clothes` が `UITextSkinTranslationPatch` から missing になっていたため実ファイルを確認したところ、`ui-default.ja.json` に未登録だった。
- これらは inventory/category 見出しの fixed label なので `ui-default.ja.json` に追加して処理する。
- `Do you really want to attack the ウォーターヴァイン農家?` の残差は template 追加後の再観測で確認する。今回は brittle な個別キー追加をせず保留する。

## 2026-03-20 Fixed UITextSkin residual decision
- `You don't have any schematics.` は tinkering 系の空状態だが、現在 observable な route では fixed sink text として届いており、既存用語 `設計図` に合わせて `ui-default.ja.json` で処理する。
- `You have no active quests.` は quest screen の固定空状態なので `ui-default.ja.json` で処理する。
- `TARGET: [none]` は HUD/targeting の固定 readout とみなし、動的 template 化を待たず `ui-default.ja.json` に `対象: [なし]` として入れる。

## 2026-03-20 Weight-unit decision
- inventory / status readout に出る `{n} lbs.` は、当面デフォルト表記のまま維持する。
- 理由: 重量単位の全面ローカライズは fixed-label ではなく UI/数値 readout 全体の仕様判断を伴うため、現在の route-first backlog からは切り離す。
- したがって `|1 lbs.|`, `|4 lbs.|`, `[{weight} lbs.]`, `Total weight: ... lbs.` の系統は今回以降も即時翻訳対象にせず、必要なら別の unit-policy issue として扱う。

## 2026-03-20 Phase shift decision
- 最新 `Player.log` の残差は、`Popup` / `Options` / `Conversation` の fixed residual がかなり整理され、残りは `GetDisplayName*` の generated/composed names、`InventoryLocalizationPatch` の quantity/state readout、`UITextSkin` の dynamic sink、`CharGen` の counters/noise に寄っている。
- したがって今後の優先順位は `already-localized` suppress の追加よりも、未訳の本体を翻訳する asset batch と、必要最小限の route-level root-cause fix を優先する。
- suppress work は、新しい route を触るときに明らかなノイズや既訳再検出が見えた場合だけ追加する。

## 2026-03-20 Options and popup residual decision
- `OptionsLocalizationPatch` に残っている `[■] 効果音`, `移動` などの日本語行は already-localized text なので missing を suppress する。
- 同 route に残る `Sound`, `Display`, `Controls`, `Accessibility`, `Legacy UI`, `Automation`, `Autoget`, `Prompts`, `Performance`, `App Settings`, `Debug`, `MOD`, `Screen` は fixed category title とみなし `ui-options.ja.json` に追加する。
- `That is not owned by you. Are you sure you want to open it?` は fixed popup prompt として `ui-popup.ja.json` に追加する。
- 上記の対応は fixed residual 解消であり、重量表記 `n lbs.` を翻訳しない方針には影響しない。

## 2026-03-20 Conversation marker decision
- `ConversationDisplayTextPatch` に残る `生きて飲め。 [End]` や `取引しよう。 [begin trade]` は、未翻訳会話本文ではなく action marker 付きの表示文字列なので、route 側で trailing marker を除去する。
- `スティルトとは？`, `なぜそんなことを聞く？`, `仕事を探している。`, `この辺りで仕事は見つかるだろうか？` のような既に日本語化された choice 文は direct-route text として missing を suppress する。
- これにより conversation residual は marker/route ノイズを除去し、本当に未訳の会話本文だけを残す方針とする。

## 2026-03-20 Popup / inventory-trade fixed residual decision
- `How long would you like to sleep?` と `There's nothing on that. Would you like to store an item?` は fixed popup prompt とみなし `ui-popup.ja.json` に入れる。
- `Close Menu`, `sort: a-z/by class`, `vendor actions`, `add one`, `remove one`, `toggle all` は inventory/trade 固定操作ラベルとして `ui-inventory-actions.ja.json` に入れる。
- `Tools`, `Trade Goods`, `Grenades` は `Food` / `Meds` / `Light Sources` と同じカテゴリ見出しとして `ui-default.ja.json` に入れる。
- `Until Waxing Salt Sun` は時間帯/日付系の派生が増える可能性が高いため、このバッチでは exact key 追加を見送り、後続の source 調査対象にする。

## 2026-03-20 Trade UI wording adjustment
- `vendor actions` は店主の行動説明ではなく、取引画面で `add one` / `remove one` / `toggle all` と並ぶ操作グループ見出しとして出ているため、最終的な訳語は簡潔に `取引` とする。

## 2026-03-20 Save-delete popup template decision
- latest `Player.log` では `PopupTranslationPatch` の残差が `Are you sure you want to delete the save game for Yashur?` と `Delete Yashur` の 2 件に絞られていた (`Player.log:2167`, `Player.log:2168`)。
- これらは save name だけが可変の fixed popup/title なので、exact key 追加ではなく `Are you sure you want to delete the save game for {0}?` と `Delete {0}` の template で処理する。
- `{0}` に入る save name は player/generator 由来の proper name なので popup route では再翻訳せず、そのまま差し込む。

## 2026-03-20 Popup sink template routing decision
- 再起動後の `Player.log` でも `Are you sure you want to delete the save game for Symoshum?` と `Delete Symoshum` が `PopupTranslationPatch` context で missing になったが、配備済み DLL には save-delete template 分岐が入っていた。
- `UITextSkinTranslationPatch` は stack に `Qud.UI.Popup` / `XRL.UI.Popup` があると context を `PopupTranslationPatch` に再分類するため、popup residual の一部は prefix ではなく popup sink 側から観測される。
- したがって popup template 処理は `PopupTranslationPatch` 専用に閉じず、`UITextSkinTranslationPatch` の popup sink でも同じ template helper を通す。

## 2026-03-20 Additional popup residual decision
- save-delete と attack-confirm を閉じた後の popup residual は、`You sense only hostility from the ...`, marker 付きの conversation choice (`[1] 取引しよう。 [begin trade]` など), および line-wrap された `Type 'ABANDON' to confirm.` 系に寄った。
- `hostility` 文は target だけが可変の popup fixed template とみなし、`You sense only hostility from {0}.` で処理する。
- popup sink に残る marker 付き conversation choice は exact key を増やさず、番号 prefix を保持したまま `[begin trade]` / `[End]` と末尾空白を除去して正規化する。
- line-wrap された `ABANDON` 確認文は rendered popup 文面と canonical dictionary key の差分が空白/改行だけなので、popup route 側で正規化して既存の canonical key へ寄せる。

## 2026-03-20 Generated display-name title decision
- latest `Player.log` の `GetDisplayName*` 上位残差には `Naruur, village apothecary`, `Uukat, the village tinker`, `bloody Naruur, village apothecary`, `bloody Naruur` がまとまって出ていた。
- `references/Base/Naming.xml:4443-4454` に `village tinker` / `the village tinker` / `village apothecary` / `the village apothecary` の template があるため、proper name 本体ではなく generated title suffix 側は route で安全に翻訳できる。
- したがって `GetDisplayName*` では generated proper name 本体を辞書に追加せず、`,` 以後の title suffix と、先頭の英語 modifier (`bloody` など) だけを route 変換する。
- `{{B}}|濡れた豚農家` のような color/markup 付き日本語 display name は already-localized direct-route text とみなし、brace/pipeline を理由に missing へ落とさない。

## 2026-03-20 Tinker terminology decision
- これまで UI / chargen / subtitle / 一部ローカライズ本文で `Tinker` を `ティンカー` としていたが、今後の既定訳語は `修理工` とする。
- ただし `tinkering` の動作・技能説明のように職能名ではなく作業内容を指す箇所は、機械的に `修理工` へ寄せず、既存文脈に合わせて `工作` 系の自然な語に直す。
- 今回の変更では display-name suffix (`the village tinker` など) と chargen/subtype/UI の直接表示、および本文内の明示的な役職名を `修理工` に寄せた。

## 2026-03-20 Source-vs-dictionary audit decision
- `Mods/QudJP/Localization/Dictionaries/README.md` のとおり、`Dictionaries/*.json` は ILSpy 抽出の UI / メッセージ系 runtime text 用であり、`Mods/QudJP/Localization/*.jp.xml` は別の merge 経路として扱う。
- したがって「元ソースにあるが mod 辞書にないか」の監査は、`references/Base/*.xml` 全体を辞書へ機械比較するのではなく、まず `Translator.Translate(...)` に流れる runtime route と `Naming.xml` のような generated title/template source を優先して見る。
- 初回の `Naming.xml` 監査では、辞書未登録 template が `82` 件あり、その内訳は fixed `18` 件、pattern/macro 断片 `64` 件だった。
- `*.jp.xml` 側で concrete 名が別経路訳済みのものと、`*Name*` などの命名マクロ断片を除くと、静的な有力候補は `Warmonger amongst the True`, `Wraith-Knight Templar of the Binary Honorum`, `High Mecha`, `village merchant`, `the village merchant`, `Pipe Milker`, `Deep`, `Bridesmaid of Qas`, `Bridesmaid of Qon` の 9 件に絞られる。
- このうち `village merchant` / `the village merchant` は `tinker` / `apothecary` と同じ generated title suffix family なので、優先度を最上位にして先に辞書追加と route 回帰テストで閉じる。
- `Wraith-Knight Templar of the Binary Honorum` は `Naming.xml` template でありつつ `ObjectBlueprints/Creatures.jp.xml` に既訳 `バイナリー・オノルムの亡霊聖堂騎士` があるため、訳語の新規判断を増やさず UI 辞書へ同文言を再利用して閉じる。

## 2026-03-20 Gameplay message-pattern priority decision
- 現行 `Player.log` の gameplay 直結残差は、`MessageLogPatch` 側の no-pattern が最も密で、`You pass by a ...`, `The way is blocked by some ...`, `You stop moving because the ... is in the way.`, combat hit/miss/crit with weapon and roll brackets, bleeding/confusion/venom/dazed/lost-sight がまとまって残っていた。
- これらは exact key を増やす対象ではなく、`messages.ja.json` の regex pattern でまとめて処理するのが正しい。
- 第一弾として、移動/遮蔽、武器付き combat log、bleeding 派生、confusion/venom/dazed/lost-sight を追加して gameplay log の土台を先に埋める。

## 2026-03-20 Route characterization-first decision
- `QudJP.Tests.csproj` は production patch 群を test project へ直接取り込み、`Assembly-CSharp.dll` があれば参照できる構成なので、L1/L2 で route / template family の characterisation test を先に固めるのは現実的である。
- ただし「全文シミュレータ」で全 runtime 文を先回り再現するのはやりすぎで、先に固定すべきなのは `GetDisplayName*`, `Grammar`, `MessageLog`, `Popup`, `Conversation`, `HUD/template tooltip` の family ごとの構文である。
- 最新 `Player.log` では `GetDisplayNamePatch=1013`, `GetDisplayNameProcessPatch=488` が支配的で、残差も `legendary グロウフィッシュ`, `タム, dromad merchant`, `水たまり of salty water`, `レシェフの神殿, the Last Sultan` のような「文字列個別」ではなく「slot 合成」の問題に寄っていた。
- したがって以後の優先順は、1) `Grammar` の count boundary, 2) `GetDisplayName*` の slot composition boundary, 3) `MessageLog` / `Popup` / `Conversation` の family 拡張, 4) gameplay L3 で未発見 family を拾う、の順にする。

## 2026-03-20 Route boundary decision for counts and display-name slots
- `GrammarPatchHelpers.BuildJapaneseList` は 0/1/2/3+ を汎用処理できるため、列挙は「最大件数を推測する」のではなく `0`, `1`, `2`, `3+` の境界ケースを test fixture で固定する。
- `Naming.xml` の slot は `Honorific`, `Epithet`, `Title`, `ExtraTitle` の family に分かれており、実運用上は `modifier + properName + title` のような合成が起こる。現行 route ではまず `modifier 1個`, `title 1個`, `modifier + title` の組み合わせ境界を優先して固定する。
- `, title` suffix family は `dromad merchant` と `the Last Sultan` も exact dictionary key で閉じられるため、既存訳のある `ドロマド商人` / `最後のスルタン` を UI 辞書へ追加して route 側の suffix 合成に流す。
- `水たまり of salty water` のような `JP head + of + ASCII phrase` は display-name route の独立 family とみなし、ASCII phrase 全体の直訳があればそれを使い、なければ token 単位で全要素を訳せた場合のみ `{translatedTail}の{head}` に再構成する。

## 2026-03-20 HUD sink and shrine/death follow-up decision
- `Player.log` 2026-03-20 16:42:23 run の主残差は `UITextSkinTranslationPatch` の HUD/sink 合成文字列と、shrine / death / journal 系 popup-message family に寄っていた。
- `UITextSkinTranslationPatch` では exact key を増やす前に route を優先し、`LVL: n Exp: a / b`, `HP: a / b`, 空白区切り状態列 (`Sated Quenched`), カンマ区切り評価列 (`Perfect, Hostile, Average`), command bar (`Look | ESC | (F1) lock | ...`) を sink template family として処理する。
- command bar の hotkey-only segment (`ESC` など) は翻訳対象ではないため、template 失敗ではなく pass-through success として扱う。
- `Quenched` の既定訳は `潤い` ではなく `潤っている` に変更する。HUD での理解容易性を優先し、`Sated Quenched` は `満腹 潤っている` とする。
- popup / message の death 文は exact key を増やすより `You died.\n\nYou were killed by {0}.` の fixed template として route 化する。
- shrine 系 popup の `[D] Desecrate` / `[y] pray` は hotkey label route で処理しつつ、label 本体 `desecrate` / `pray` も runtime UI 辞書へ持たせる。
- gameplay log の遮蔽文は `some` だけでなく `a` も実ログに出るため、`The way is blocked by a ...` も message pattern family に追加する。

## 2026-03-20 Dynamic text observability decision
- `Player.log` は現状、主に `missing key` / `no pattern` / 個別 probe しか出しておらず、「翻訳はされたが不自然」な dynamic text は追えない。
- そのため dynamic text は exact-key 監査だけでは不十分で、route-family 単位の `source -> translated` 観測を追加する。
- ただし全文を常時ログするとノイズが多すぎるため、`DynamicTextProbe/v1` を導入し、`route + family` ごとに power-of-two hit だけを sample 記録する。
- この observability の対象は少なくとも `GrammarPatch`, `MessagePatternTranslator`, `PopupTranslationPatch`, `UITextSkinTranslationPatch`, `FactionsStatusScreenTranslationPatch`, `UITextSkinTemplateTranslator` とする。
- grammar 系は `A`, `Pluralize`, `MakePossessive`, `MakeAndList`, `MakeOrList`, `SplitOfSentenceList`, `InitCaps`, `CardinalNumber` の family を出し、列挙系は件数境界 (`count=0/1/2/3+`) を family 名に含める。
- display-name / HUD / popup / message は template family 名をログに持たせる。これにより、次回以降の L3 では `missing` だけでなく「どう組み立てられたか」を `Player.log` から追えるようにする。

## 2026-03-20 Upstream display-name state-tag decision
- `Assembly-CSharp.dll` の decompile で、`GameObject.GetDisplayName(...)` は `GetDisplayNameEvent.GetFor(...)` を呼び、その内部 `DescriptionBuilder` が `PrimaryBase` / `LastAdded` / `Titles` / `WithClauses` を保持して最終表示名を組み立てていることを確認した。
- bracketed state suffix は popup 固有の後付けではなく、effect / part の `HandleEvent(GetDisplayNameEvent E)` から `E.AddTag("[...]")` で付与される upstream family である。
- 確認できた concrete source には `Sitting` (`[sitting]` / `[sitting on ...]`), `Flying` (`[flying]`), `Wading` (`[wading]`), `Prone` (`[prone]` / `[lying on ...]`), `Enclosed` (`[enclosed in ...]`), `Engulfed` (`[engulfed by ...]`), `LiquidVolume` (`[empty]`, `[sealed]`, `[auto-collecting ...]`) がある。
- したがって `タム, dromad merchant [sitting]` のような文字列は popup route だけで個別分解するのではなく、`GetDisplayName*` で `base + [state]` を family として扱うのが正しい。
- この family では `base` は既存の display-name route (`title suffix`, `mixed modifier`, `of phrase`, `proper name modifier`) に再投入し、`state` は exact key または prepositional template (`sitting on {0}`, `lying on {0}`, `enclosed in {0}`, `engulfed by {0}`, `auto-collecting {0}`) で処理する。

## 2026-03-20 Upstream consolidation candidates decision
- decompile 監査の結果、downstream exact-key を増やす前に upstream family へ寄せるべき候補は少なくとも次の 4 群に整理できる。
- 1) effect / part の `AddTag("[...]")` family: state suffix, liquid-container suffix, ammo/energy loader の `empty/no cell/no cells`, timer/status tag。
- 2) `Naming.xml` と `Titles` / `Honorifics` / `Epithets` part family: proper name + title / epithet / honorific。
- 3) `DescriptionBuilder.AddClause(...)` family: `with ...`, liquid preposition, `of ...` 句。
- 4) HUD / sink family: `UITextSkin` で個別 exact key に見えているが、実際には slot 合成である `HP`, `LVL/Exp`, status line, threat line, command bar。
- 当面の優先順は、A) `GetDisplayNameEvent.AddTag("[...]")` family の一般化、B) liquid / inventory suffix family の一般化、C) title / epithet family の継続監査、D) sink family の exact-key 削減、の順とする。

## 2026-03-20 Logic-required policy decision
- QudJP では、動的に組み立てられる文を `logic required` として明示的に扱う。これは display-name, grammar, message-log, popup, inventory suffix, liquid description, HUD sink などを含む。
- `logic required` な文は、runtime log の表層文字列から broad regex/template を広げる前に、decompile もしくは upstream text asset から生成元と slot 構造を確認する。
- 方針文書は `docs/logic-required-policy.md` に置き、`AGENTS.md` から参照させる。以後の route/template work はこの文書を基本方針とする。
- test は「最終文字列が合うか」だけでなく、slot 境界・件数境界・optional segment を押さえる。特に `0/1/2/3+`、single/plural、base/state/title の境界を fixture 化する。
- `L3 Player.log` は upstream 分析の代替ではなく、route hit の確認・未発見 family の発見・レンダリング回帰の確認に限定する。
- subagent 監査でも、この方針に反する broad family は violation 候補として扱い、必要なら upstream deep dive に戻す。

## 2026-03-20 Liquid `of` family narrowing decision
- `of` という語自体は `Grammar` の前置詞表に含まれるが、それだけでは display-name の `JP head + of + ASCII tail` family を general grammar とみなす根拠にはならない。
- 実際に `[* dram(s) of *]` や `水たまり of salty water` に関わる upstream は `LiquidVolume` であり、`PhysicalPhenomena.jp.xml` の `Water` object も `LiquidVolume NamePreposition="of"` を持つ。
- `LiquidVolume.AppendLiquidDescription(...)` は `UsePreposition + amount/unit + liquid name` を連結して display-name tag/ clause を組み立てる。
- したがって `UITextSkinTranslationPatch` の broad `TryTranslateOfPhraseDisplayName` は撤去し、`water/puddle/pool` に対応する localized liquid-head family (`...水たまり`, `...池`) にだけ反応する liquid-specific route へ narrowing する。

## 2026-03-20 Sink exact-lookup precedence decision
- `UITextSkin` の sink では `SpaceSequence` / `CommaSequence` の generic route が exact key より先に走ると、`take all -> 取る すべて`, `Display Options -> 表示 オプション` のような回帰が起きる。
- ただし exact key probe を無条件で `Translator.Translate(...)` に寄せると、`LVL: 1 Exp: 0 / 220` や `Sated Quenched` のような dynamic familyに先回り missing が立つ。
- そのため sink exact lookup は `Translator.TryGetTranslation(...)` の non-logging probe を新設し、「辞書に存在する時だけ」先に exact を適用する。
- この exact-first は `UITextSkin` sink に限定し、display-name / popup / message の logic-required route には持ち込まない。

## 2026-03-20 Combat color finding
- 戦闘文が赤系で表示される件は、現時点では mod 側の回帰ではなく upstream の色指定である可能性が高い。
- `MessageLogPatch` は `Message` だけを翻訳し、`Color` は変更していない。
- upstream では player hit が `MessageQueue.AddPlayerMessage(..., Stat.GetResultColor(num11))` を使い、`GetResultColor` は penetration 結果に応じて `&r`, `&R`, `&M`, `&w`, `&W`, `&y` を返す。
- したがって「常に赤にしている」わけではなく、penetration 結果や consequential color の upstream 仕様をそのまま保持していると判断する。
