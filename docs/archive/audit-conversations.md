# Conversation translation audit

Date: 2026-03-31

Scope: read-only audit of `Mods/QudJP/Localization/Conversations.jp.xml` and `Mods/QudJP/Localization/HiddenConversations.jp.xml` only.

Reference: `docs/glossary.csv`.

Method: `xmllint --noout` on both XML files, regex/lxml scan for English-bearing text and markup anomalies, then manual verification of every line cited below. I did **not** promote purely poetic/opaque prose unless there was a concrete untranslated term, glossary mismatch, or structural problem.

## 1. Summary statistics

| Metric | Result |
| --- | --- |
| Files audited | 2 |
| Lines audited | 15,559 total (`13,391 + 2,168`) |
| Conversation definitions | 213 total (`204 + 9`) |
| XML well-formedness | Both files pass `xmllint --noout` |
| Top 40 breakdown | 33 untranslated/partially untranslated, 4 glossary mismatches, 2 markup/tag issues, 1 structural XML risk |
| Confirmed glossary mismatch types | 4 types / 31 occurrences |
| Repeated English or romanized residuals | At least 92 repeated token hits in `Conversations.jp.xml` (`elseing`, `kicksoft`, `Argyve`, `NON MOLOCH`, etc.) |
| Confirmed broken pronoun-style tokens | 0 found |
| Confirmed `{{...}}` balance issues | 0 found |
| Confirmed visible tag corruption | 1 confirmed (`Crystals` intro) |

## 2. Top 40 critical findings

| # | Location | Category | Finding | Why it matters |
| --- | --- | --- | --- | --- |
| 1 | `Conversations.jp.xml:11768` — `Crystals / Start` | プレースホルダー・タグ破損 | `{{emote||...}}` has an extra pipe and does not match normal emote markup shape. | Only confirmed markup break in the audited scope; likely affects rendering. |
| 2 | `HiddenConversations.jp.xml:873` — `EndAccedeResigned` | 未翻訳テキスト | Choice text is `I must think on this further.` | Fully English in an endgame branch. |
| 3 | `HiddenConversations.jp.xml:874` — `EndAccedeResigned` | 未翻訳テキスト | Same untranslated choice appears again in the resigned-state branch copy. | Duplicate untranslated branch. |
| 4 | `Conversations.jp.xml:896` — `TauNoLonger / Suborned` | 未翻訳テキスト | `You` remains English inside a visible color tag. | Player sees a mixed-language accusation line. |
| 5 | `Conversations.jp.xml:231` — `Tzedech / KilledCompanion` | 未翻訳テキスト | Player choice keeps `-elseing`. | Quest jargon is left raw at a player-facing choice point. |
| 6 | `Conversations.jp.xml:246` — `Tzedech / Else2` | 未翻訳テキスト | The line keeps `-else` and `-elseing`. | Secret/lore response becomes hard to understand. |
| 7 | `Conversations.jp.xml:284` — `Tzedech / Deny` | 未翻訳テキスト | The line keeps `-else` and `-then`. | Core contrast in the dialogue remains English. |
| 8 | `Conversations.jp.xml:338` — `ChavvahPrime / WelcomeNoPhysiology` | 未翻訳テキスト | Quest-critical choice says `-else の儀`. | Objective prompt is not fully localized. |
| 9 | `Conversations.jp.xml:349` — `ChavvahPrime / Work` | 未翻訳テキスト | Quest briefing repeats `-else`. | First explanation of the quest remains mixed-language. |
| 10 | `Conversations.jp.xml:356,360` — `ChavvahPrime / Work2, ActiveQuest` | 未翻訳テキスト | Active instructions still use `-else`. | Repeats during in-progress quest reminder. |
| 11 | `Conversations.jp.xml:362` — `ChavvahPrime / DoneQuest` | 未翻訳テキスト | Completion line says `-else の儀は完了した`. | Reward/turn-in line still reads unfinished. |
| 12 | `Conversations.jp.xml:370,373` — `ChavvahPrime / Physiology, Physiology2` | 未翻訳テキスト | Lore explanation retains `-then` / `-else`. | Core worldbuilding terminology is not localized. |
| 13 | `Conversations.jp.xml:375,381` — `ChavvahPrime / BarathrumChavvahDone2, WillYouHelp2` | 未翻訳テキスト | Agreement and follow-up nodes still use `-else`. | Important acceptance/dependency dialogue remains rough. |
| 14 | `Conversations.jp.xml:388,389` — `ChavvahPrime / SlynthArrived, SlynthSettled` | 未翻訳テキスト | Settlement follow-up keeps `-then` / `-else`. | Post-quest flavor text still leaks English. |
| 15 | `Conversations.jp.xml:404,406,430,432` — `Tikva/Miryam` starts | 未翻訳テキスト | Vocative `-elser` is left raw. | Repeated across multiple greeting states. |
| 16 | `Conversations.jp.xml:442` — `Miryam / TauChime` | 未翻訳テキスト | `-else の営み` remains raw. | Emotional mourning line loses clarity. |
| 17 | `Conversations.jp.xml:582,601,620,662` — `Thicksalt` cluster | 未翻訳テキスト | `kicksoft` is left as-is four times. | Repeated alien term is not localized or glossed anywhere. |
| 18 | `Conversations.jp.xml:591,610` — `Thicksalt` cluster | 未翻訳テキスト | `crungle` remains in English gloss. | Reads like an unfinished localization pass. |
| 19 | `Conversations.jp.xml:729` — `Tammuz / KilledCompanion` | 未翻訳テキスト | `Elsefolder` is left raw. | Player-facing emotional line contains unexplained English jargon. |
| 20 | `Conversations.jp.xml:748` — `Tammuz / ElseWelcome` | 未翻訳テキスト | `-elseing` remains raw. | Greeting branch still looks unfinished. |
| 21 | `Conversations.jp.xml:903,975,993,1004` — `TauNoLonger` cluster | 未翻訳テキスト | Multiple reward/farewell nodes retain `-elseing`. | End-state dialogue repeatedly leaks English. |
| 22 | `Conversations.jp.xml:1045,1071` — `WanderingTau` cluster | 未翻訳テキスト | Follow-up nodes retain `-elseing`. | Alternative outcome branch is unfinished too. |
| 23 | `Conversations.jp.xml:1857` — `ManyEyes / Start` | 未翻訳テキスト | `NON MOLOCH` and `BRIGHTSHEOL` remain English. | Hidden-lore opening becomes mixed language immediately. |
| 24 | `Conversations.jp.xml:1865,1879` — `ManyEyes / Start, MaqqomScramble` | 未翻訳テキスト | `*READOUT*` is left raw. | Two system-text nodes remain untranslated. |
| 25 | `Conversations.jp.xml:1871,1874,1885,1911` — `ManyEyes` cluster | 未翻訳テキスト | `MAQQOM YD` remains English in explanation and player choice. | Key lore term is never localized or glossed in Japanese. |
| 26 | `Conversations.jp.xml:1893-1895` — `ManyEyes / MaqqomTell` | 未翻訳テキスト | `SALUM`, `SAAD`, `PIPE`, `SLIPSTREAM`, `STEL-CRISTAL`, and `CASKE` remain raw. | Dense lore exposition becomes very hard to parse. |
| 27 | `Conversations.jp.xml:1903` — `ManyEyes / Mean` | 未翻訳テキスト | `GALGALLIM` remains English. | Short lore line is still untranslated. |
| 28 | `Conversations.jp.xml:2564,2581,2585,2605` — `Argyve` emote lines | 未翻訳テキスト | NPC name `Argyve` is left in English inside Japanese emote prose. | Repeats across multiple quest states. |
| 29 | `Conversations.jp.xml:2574,2590` — `Argyve` goodbye choices | 未翻訳テキスト | Player choice says `さらばだ、Argyve。`. | Visible choice text should not mix English name forms. |
| 30 | `Conversations.jp.xml:7211` — `Otho / Signal` | 未翻訳テキスト | `baetyl` is raw while the same quest chain already uses `ベテル`. | One concept appears both translated and untranslated. |
| 31 | `Conversations.jp.xml:7244` — `Otho / PresentTheDisk` | 未翻訳テキスト | Choice text is `[Otho にディスクを渡す]`. | UI-style action line still uses an English NPC name. |
| 32 | `Conversations.jp.xml:3261` — `Shem / Start` | 未翻訳テキスト | Favorite title `Immaculate Chrome` is fully English. | Feels like a missed title localization. |
| 33 | `Conversations.jp.xml:4740` — `Neek / GoingOn` | 未翻訳テキスト / 機械翻訳痕跡 | `mush room` remains as an English pun in parentheses. | Mixed-language joke reads like a leftover note rather than polished JP. |
| 34 | `Conversations.jp.xml:6302,6316,6328` — `Vivira` cluster | 未翻訳テキスト | `BEEP`, `crackle`, and `crackle-sigh` remain English SFX in visible dialogue. | Repeated English SFX stand out in otherwise localized NPC speech. |
| 35 | `Conversations.jp.xml:5935` — `GenericTradeOption / Start` | プレースホルダー・タグ破損 | Green color code opens with `&g` but never resets with `&y`. | Only confirmed unclosed inline color span in scope. |
| 36 | `Conversations.jp.xml:10499` — `Eskhind is dead.` | 構造リスク | Element is `<Node>` instead of lowercase `<node>`. | Only case-variant node tag in file; may fail if parser is case-sensitive. |
| 37 | `Conversations.jp.xml:2066,2109` — `Slynth candidate lines` | 誤訳 / 用語不一致 | `メカニミスト` conflicts with glossary forms `メカニマス教徒` / `メカニマス教団`. | Faction/religion naming drifts from project terminology. |
| 38 | `Conversations.jp.xml:2690` and 24 more occurrences (e.g. `7211`, `7800`) | 誤訳 / 用語不一致 | `ベテスダ・スーサ` conflicts with glossary `ベセスダ・スーサ`. | High-frequency place-name drift across quest dialogue. |
| 39 | `Conversations.jp.xml:6352,6613` — `Vivira/Agyra` | 誤訳 / 用語不一致 | `食らう者たちの墓所` conflicts with glossary `イーターの墓所`. | Major dungeon name is inconsistent in Mopango dialogue. |
| 40 | `HiddenConversations.jp.xml:658,793` — `EndCovenant*` | 誤訳 / 用語不一致 | `グリットゲート` conflicts with glossary `グリット・ゲート`. | Same proper noun is normalized elsewhere but broken here. |

## 3. Systematic pattern issues

### A. `If, Then, Else` quest family is the largest concentration of untranslated jargon

The `Tzedech` / `ChavvahPrime` / `Tikva` / `Miryam` / `Thicksalt` / `Tammuz` / `TauNoLonger` / `WanderingTau` conversation family repeatedly leaks source-side English terminology: `-else`, `-then`, `-elseing`, `-elser`, `Elsefolder`, `kicksoft`, and `crungle`.

This is the single biggest player-facing quality problem in the audited scope because it appears in:

- quest acceptance choices,
- active objective reminders,
- completion/turn-in lines,
- emotional companion aftermath,
- post-quest settlement flavor.

In other words, this is not isolated fluff; it hits critical comprehension points all along the route.

### B. `ManyEyes` hidden-lore dialogue is still partially in source-language form

`ManyEyes` retains dense all-caps English tokens (`NON MOLOCH`, `BRIGHTSHEOL`, `MAQQOM YD`, `SALUM`, `GALGALLIM`, etc.) and raw system text (`*READOUT*`).

Some of these may be intended alien proper nouns, but the current Japanese file does not provide consistent in-line glossing. As written, the scene reads more like a partially localized lore dump than deliberate bilingual stylization.

### C. Grit Gate / Joppa chains still contain isolated English fragments

Beyond the top 40, several otherwise localized conversations still have isolated English leftovers that feel like partial pass-through from source assets rather than intentional style. Examples include raw English NPC-name usage (`Argyve`, `Otho`), technical terms (`baetyl`), title fragments (`Immaculate Chrome`), and lower-priority leftovers such as `make-me`, `pest`, and the `{{W|trade}}` / `{{W|craft}}` / `{{W|illness}}` / `{{W|violence}}` quote at `Conversations.jp.xml:9596-9599`.

Those latter `{{W|...}}` words may be engine evidence keys rather than ordinary prose, so I did not elevate them into the top 40. They should still be checked against runtime behavior before closing the audit.

### D. Structural health is otherwise good

The audited XML is mostly structurally sound:

- both files are well-formed XML,
- `{{` / `}}` pairs are balanced,
- no confirmed `=pronouns.subjective=`-style token corruption was found,
- `HiddenConversations.jp.xml` is materially cleaner than `Conversations.jp.xml`.

The severe issues are concentrated in `Conversations.jp.xml`, with `HiddenConversations.jp.xml` mostly limited to one untranslated endgame choice pair and one glossary-normalization issue.

## 4. Glossary proper noun mismatches

| Glossary entry | Glossary form | Observed form in audited files | Occurrences | Example lines |
| --- | --- | --- | ---: | --- |
| `Mechanimist` / `Mechanimists` | `メカニマス教徒` / `メカニマス教団` | `メカニミスト` | 2 | `Conversations.jp.xml:2066,2109` |
| `Bethesda Susa` | `ベセスダ・スーサ` | `ベテスダ・スーサ` | 25 | `Conversations.jp.xml:2690,7211,7800` |
| `Tomb of the Eaters` | `イーターの墓所` | `食らう者たちの墓所` | 2 | `Conversations.jp.xml:6352,6613` |
| `Grit Gate` | `グリット・ゲート` | `グリットゲート` | 2 | `HiddenConversations.jp.xml:658,793` |

## Closing note

I found **no evidence of large-scale placeholder corruption**, but I did find one concrete emote-tag break, one node-element casing risk, repeated glossary drift, and a heavy concentration of untranslated English in the `If, Then, Else` route plus `ManyEyes` lore dialogue.
