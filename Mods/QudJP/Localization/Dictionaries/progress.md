# 辞書進捗メモ
- 詳細な手順は `Docs/pipelines/<target>.md` を参照。
- Localization を更新したら `Localization.zip` を再生成し、Player.log の `Loaded '<file>.ja.json'` を確認する。

## 対応状況
| 範囲 / 機能 | 主な ContextID / Hook | 辞書 | 進捗 | メモ |
| --- | --- | --- | --- | --- |
| **Message Log** | `MessageQueue.AddPlayerMessage` / `Messaging.HandleMessage` / `MessageQueueWorldLocalizer` | `ui-messagelog-world.ja.json`, `ui-messagelog.ja.json` | 対応中 | explode/disappear/submerge/cohere/appear/stasis field・PsychicCombat などテンプレ完了。`tmp/out-messagelog-game.json` / Player.log で確認。XDidYToZ 汎用は ExtraMessagingPatches で verb/prep 和訳済み。 |
| Message / Autoact / Harvest | `Harvestable.AttemptHarvest` / `LiquidVolume` / `Shrine` / `AutoAct` / `Physics` / `ExtraMessagingPatches` | `ui-messagelog-world.ja.json`, `ui-popup.ja.json` | 対応中 | 自動行動・Harvest メッセージは Harmony 側でローカライズ済み。 |
| **Skills** | `SkillsAndPowers*` | `ui-skillsandpowers.ja.json` | 翻訳済 | `SkillsAndPowersLocalizer` で TMP 表示も保護。 |
| Attributes & Powers | `CharacterAttributeLine*` / `CharacterStatusScreen*` / `Statistic.GetHelpText` | `ui-attributes.ja.json` | 翻訳済 | `SafeStringTranslator` 経由。 |
| Equipment | `InventoryLine*`, `BodyPart.GetCardinalDescription()` | `ui-inventory.ja.json` | 対応中 | Slot 表示など要確認。 |
| Popup / 汎用 UI | `Popup.ShowBlock`, `Qud.UI.PopupMessage.ShowPopup` | `ui-popup.ja.json`, `ui-quit-hotfix.ja.json` | 翻訳済 | Quit 文言 Hotfix 済み。 |
| World Generation | `WorldCreationProgress.*` | `ui-worldgen.ja.json` | 翻訳済 | Console / Unity 両方で確認。 |
| Options / Help / Keybinds | `Qud.UI.OptionsScreen` / `HelpScreen` / `KeybindsScreen` | `ui-options.ja.json`, `ui-help.ja.json`, `ui-keybinds.ja.json` | 翻訳済 | 主要画面完了。 |
| UI 共通テキスト | （共通ラベル） | `ui-default.ja.json` | 翻訳済 | Back / OK / Loading など。 |
| Inventory / Trade | `InventoryLine*`, `Trade*` | `ui-inventory.ja.json`, `ui-trade.ja.json` | 対応中 | 取引メニューは要追加確認。 |
| Journal / Book | `BookUI`, `JournalScreen` | `ui-journal.ja.json` | 翻訳済 | 書籍 UI。 |
| Mutation 説明 | `Chargen.Mutation.LongDescription` | `mutation-descriptions.ja.json` | 翻訳済 | 82 件対応。 |
| 自動抽出 UI | ILSpy 抽出経由 | `ui-auto-generated.ja.json` | 翻訳済 | 自動生成テキスト。 |
| 置換文字列 | `GameText.VariableReplace` / `XRL.World.Text.*` / `HistoricStringExpander.ExpandString` | `ui-messagelog.ja.json` | 翻訳済 | `=...=` / `<spice.*>` 系。 |
| Name 生成 | `NameStyles.Generate` / `NameStyle.Generate` / `Naming.xml` | `Naming.jp.xml` + Harmony(NameStylePatch) | 対応中 | TwoName や Hyphenation を Harmony で制御。 |
| Conversation Pronoun | `ConversationScript.PronounExchangeDescription` (`Speaker.t()`/`Speaker.its`) | Harmony | 翻訳済 | `ConversationPronounExchangeTranslationPatch.cs` で代名詞交換の文面を差し替え。 |

## メモ
- NameStyle/NameStyles: TwoName のスペース／ハイフン出力を Harmony で調整。`Naming.jp.xml` では HyphenationChance=0 で運用。
- Conversation Pronoun: `"you and ... exchange pronouns"` の文面は `ConversationPronounExchangeTranslationPatch.cs` で差し替え済み。

## ガイド
- 翻訳方針・パイプラインは `Docs/pipelines/<target>.md` を必ず参照。
- UTF-8 / LF を維持。`py -3 scripts/validate_dict.py` と `py -3 scripts/check_encoding.py --path <path>` で検証する。
- Player.log の `[JP][TR][MISS]` をチェックし、`tmp/out-*.json` と突合。

## 最近の更新
- 2026-03-20: `ui-displayname-adjectives.ja.json` に display name adjective と Mod 接頭辞（painted/engraved/nulling/recycling/radio-powered/phase-conjugate など）を反映済み。
- 2025-11-26: HistorySpice 主要プレースホルダを `world-gospels.ja.json` に反映（digits 系は key=text のまま）。
- 2025-11-25: ExtraMessagingPatches に XDidYToZ 汎用の verb/prep 和訳テーブルを追加（firefighting / bandage / dismiss / convince / intercept / space-time vortex）。`ui-messagelog-world.ja.json` の関連テンプレートも更新。
- 2025-11-24: `Some / some` の記事や `GetDisplayName.StackCount`（`x{n}`）を調整。`ui-displayname-adjectives.ja.json` に `Some` を追加。
- 2025-11-24: `scripts/verify_regex.py` で CombatLog/Messaging/ExtraMessaging の正規化・パターンテストを追加し、全ケース PASS を確認。
- 2025-11-23: `MessageQueueWorldLocalizer` に explode/disappear/submerge/cohere/appear/stasis field/focus/are sucked into を追加。`ui-messagelog-world.ja.json` を反映。

## TODO
- Runtime miss 追跡: display name adjective / Mod 接頭辞は既存の補完済み項目を前提に、Player.log の `[JP][MISS]` で未収録の新規語だけを確認する。
- XDidYToZ 汎用で新しい verb/prep が見付かったら訳テーブルに追記（Player.log の `[JP][MISS]` を確認）。
