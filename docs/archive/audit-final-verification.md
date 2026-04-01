# Final localization verification audit

Date: 2026-03-31

Scope: audit only `Mods/QudJP/Localization/` against `docs/glossary.csv` (research only, no source-file edits).

## Status by check category

| Check category | Status | Notes |
| --- | --- | --- |
| Glossary consistency | **FAIL** | Multiple glossary regressions remain across XML and JSON localization assets. |
| Structural integrity | **PASS** | All 35 XML files passed `xmllint --noout`; all 56 JSON files parsed successfully; no `DisplayName={{Y|}}` found under `ObjectBlueprints/`. |
| Remaining English residual | **PASS** | No definite untranslated user-facing entries remained after excluding intentional English/UI tokens, acronyms, engine identifiers, and product-title cases. |
| Tag / placeholder integrity | **PASS** | No confirmed broken `{{...}}` pairs or translator-note leftovers. Raw scans mostly surfaced intentional split-tag dictionary fragments and intentional starred emotes/action prompts. |

## Remaining glossary issues found

Counts below are grouped by wrong-form family and based on literal occurrences in localization assets.

| Wrong / inconsistent form | Expected glossary form | Count | Sample evidence | Notes |
| --- | --- | ---: | --- | --- |
| `イーター` | `喰らう者` | 23 | `Books.jp.xml:877`, `Conversations.jp.xml:10116`, `ObjectBlueprints/Creatures.jp.xml:7065` | Includes ordinary noun use and tomb-related phrasing. |
| `食らう者` | `喰らう者` | 8 | `Conversations.jp.xml:5816`, `HiddenConversations.jp.xml:257`, `Quests.jp.xml:350` | Kanji variant drift. |
| `喰らう者の墓` | `喰らう者の墓所` | 1 | `Dictionaries/achievements.ja.json:901` | Exact place-name truncation. |
| `メカニミスト` / bare `メカニマス教` | `メカニマス教徒` / `メカニマス教団` | 18 | `Dictionaries/achievements.ja.json:445`, `Conversations.jp.xml:3407`, `ObjectBlueprints/Creatures.jp.xml:1864` | Short-form faction/member naming still appears in several routes. |
| `修理工` | `工匠` | 49 | `Conversations.jp.xml:2538`, `HiddenConversations.jp.xml:149`, `ObjectBlueprints/Items.jp.xml:282` | Highest-volume glossary regression in current sweep. |
| `神社` | `神殿` | 11 | `ObjectBlueprints/Furniture.jp.xml:407`, `ObjectBlueprints/Furniture.jp.xml:471`, `ObjectBlueprints/Furniture.jp.xml:1100` | Includes display names and descriptive prose. |
| `ゼタクロム` | `ゼータクローム` | 3 | `ObjectBlueprints/HiddenObjects.jp.xml:437`, `:692`, `:1234` | Material-name drift. |
| `ドロマド商人` | `ドロマド商団` | 5 | `Books.jp.xml:975`, `Conversations.jp.xml:10680`, `Dictionaries/ui-default.ja.json:271` | Merchant/caravan naming drift. |
| `ガーシュ...` family | `ギルシュ...` family | 17 | `ObjectBlueprints/Creatures.jp.xml:2230`, `:3122`, `ObjectBlueprints/HiddenObjects.jp.xml:1783` | Includes `ガーシュリング`, `ガーシュワーム`, `ガーシュの小神`. |
| `パックス・クラング` | `パクス・クランク` | 1 | `Books.jp.xml:1120` | Proper name still wrong in book credit line. |
| `ヴィヴィラ` | `ビヴィラ` | 6 | `Conversations.jp.xml:6308`, `:6336`, `:10175` | NPC name drift. |
| `スヴァルディム` | `スヴァーディム` | 9 | `Conversations.jp.xml:1385`, `:12757`, `Dictionaries/ui-messagelog-world.ja.json:1467` | Creature/faction naming drift. |
| `ブライトシオル` | `ブライトシェオル` | 4 | `Dictionaries/ui-default.ja.json:1017`, `:1021`, `Dictionaries/achievements.ja.json:633` | Proper noun typo variant. |
| `生命の樹` / `チャヴァー` | `生命の木チャヴァ` / `チャヴァ` | 5 | `Books.jp.xml:809`, `:1485`, `Conversations.jp.xml:2057` | Tree-name and proper-name variants both remain. |
| `モグライー` / `モグラ=イ` | `モグラヤイ` | 14 | `Books.jp.xml:975`, `:872`, `ObjectBlueprints/WorldTerrain.jp.xml:87` | Great Salt Desert naming drift beyond the originally listed wrong forms. |
| `クゥド` | `クッド` | 11 | `Books.jp.xml:98`, `:1491`, `Dictionaries/mutation-descriptions.ja.json:595` | Additional glossary inconsistency found during sweep. |
| `クダー` | `クッド` | 1 | `Conversations.jp.xml:3831` | Inflected/variant form still diverges from glossary spelling. |
| Raw `Qud` | `クッド` | 2 | `Dictionaries/journal-patterns.ja.json:51`, `:56` | Untranslated proper noun remained in pattern text. |

## Notes on non-issues / filtered false positives

- XML / JSON structure is clean. No parsing failures occurred.
- The empty-display-name check found no remaining `DisplayName={{Y|}}` entries under `ObjectBlueprints/`.
- Exact `text == key` JSON matches that remained were overwhelmingly intentional: product title (`Caves of Qud`), input labels (`Esc`, `Enter`, `Tab`), acronyms (`UI`, `KBM`, `PS`, `HUD`), identifiers, file paths, or engine/template tokens.
- Raw `{{...}}` mismatch scans produced many false positives from intentionally split dictionary fragments such as `ui-modpage.ja.json`, `world-mods.ja.json`, and `world-parts.ja.json`; spot checks showed these are fragment-based constructions rather than fresh corruption.
- Raw `*...*` scans found many Japanese starred segments, but they were concentrated in intentional `{{emote|*...*}}` lines, explicit action choices like `*うなずく*`, and `xtagTextFragments` noises. No suspicious exact tokens such as `*スルタン*`, `*時代*`, `*乗り物*`, or `*クッド*` were found.
- A translator-note scan returned one false positive at `Dictionaries/world-parts.ja.json:560` (`[注ぎ元の容器を選択]`), which is a legitimate UI label rather than a leftover note.

## Summary

Structural integrity is good and the audit did **not** uncover new XML/JSON breakage or obvious placeholder corruption.

However, glossary consistency still fails in multiple high-visibility areas, especially `修理工`/`工匠`, `イーター`/`喰らう者`, `メカニマス教*`, and several proper nouns (`ゼタクロム`, `ヴィヴィラ`, `スヴァルディム`, `ブライトシェオル`, `モグラヤイ`, `クッド`).

**Conclusion: the localization is not yet ready for L3 testing.** Resolve the remaining glossary regressions first, then rerun this final sweep.
