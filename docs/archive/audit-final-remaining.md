# Final audit of the 4 remaining localization issues

Scope: only the four requested issues. No PR review comments or unrelated findings were investigated.

## Common source note

- The task referenced `Base/Dictionaries/world-gospels.json` and `Base/Dictionaries/templates-variable.json`, but those files do **not** exist in the installed 2.0.4 base assets I checked.
- For Issue 1 I used `Base/HistorySpice.json` plus the decompiled `XRL.Annals/*.cs` event builders as the real English source.
- For Issue 2 I used the generated JP dictionary itself plus `Base/Commands.xml` and the repo-localized `Options.jp.xml` / `Commands.jp.xml` for terminology consistency.
- For Issue 3 I used `templates-variable.ja.json` together with `StartReplaceTranslationPatch.cs` and its tests, because the English originals are already present in the dictionary keys.

## Issue 1 — `world-gospels.ja.json` malformed spice/tag region

### Findings

- The late Annals-oriented block contained procedural tag fragments that had been turned into translator notes/stubs such as `スルタン史の語句`, `（乗り物）`, and `[時代]`.
- Those stubs were not valid runtime spice tags. The safest deterministic repair was to restore the live spice fragments, and only translate the connective Japanese around them when the source split made that unambiguous.
- `HistoricStringExpanderPatch` is currently disabled in code, so this dictionary is not actively translated at runtime today; however, the data itself was still malformed and worth fixing.

| Lines | English original / source fragment | JP text found during audit | What was wrong | Class | Action |
| --- | --- | --- | --- | --- | --- |
| 6223 / 6308 / 6318 / 6403 | `<spice.history.gospels.CommittedWrongAgainstSultan.` | `スルタンへの罪` | Partial spice prefix was replaced by a descriptive stub, so any later era/random suffix would be lost. | (a) fixable now | Restored the live spice prefix unchanged. |
| 6228 / 6323 | `<spice.history.gospels.EnemyHostName.` | `敵の呼び名` | Same problem: a live prefix was collapsed into a note-like label. | (a) fixable now | Restored the live spice prefix unchanged. |
| 6233 / 6328 | `<spice.history.gospels.HumblePractice.` | `慎ましい慣習` | Same placeholder-stub corruption. | (a) fixable now | Restored the live spice prefix unchanged. |
| 6238 / 6283 / 6333 / 6378 | `<spice.history.gospels.` | `スルタン史の語句` | Generic procedural tag prefix was replaced by a translator note, making the fragment unusable. | (a) fixable now | Restored the raw prefix. |
| 6243 / 6338 | ` <spice.history.gospels.` | ` <spice.history.gospels.>（乗り物）` | Malformed tag plus translator note `（乗り物）` inserted inside a live fragment. | (a) fixable now | Removed the note and restored the raw fragment. |
| 6248 / 6343 | ` <spice.history.gospels.VehicularSabotage.` | ` <spice.history.gospels.VehicularSabotage.>（破壊工作）` | Malformed tag plus translator note `（破壊工作）`. | (a) fixable now | Removed the note and restored the raw fragment. |
| 6253 / 6348 | `.vehicle.!random> and <spice.history.gospels.CrashedVehicle.` | `<spice.history.gospels.[時代].vehicle.!random>が<spice.history.gospels.CrashedVehicle.[時代].!random>` | Translator note `[時代]` was shipped inside spice tags; the English conjunction fragment was also rewritten in a way that hard-coded non-existent tags. | (a) fixable now | Rebuilt the fragment as `...を制御できなくなり、...` while preserving both live spice paths. |
| 6258 / 6353 | `<entity.name> lost control of <entity.possessivePronoun> <spice.history.gospels.` | `<entity.name>は<entity.possessivePronoun><spice.history.gospels.[時代].vehicle.!random>の制御を失い、` | Translator note `[時代]` was embedded in the tag, and the fragment no longer matched the source split. | (a) fixable now | Rewrote the fragment as `<entity.name>は<entity.possessivePronoun>の<spice.history.gospels.` so the downstream suffix can expand normally. |
| 6263 / 6358 | `<entity.subjectPronoun> lost control of <entity.possessivePronoun> <spice.history.gospels.` | `<entity.subjectPronoun>は<entity.possessivePronoun><spice.history.gospels.[時代].vehicle.!random>の制御を失い、` | Same `[時代]` placeholder leak and fragment mismatch as the previous line. | (a) fixable now | Rewrote the fragment in the same tag-preserving form. |
| 6268 / 6363 | `<spice.history.gospels.VehicularSabotageResult.` | `車両破壊の結果` | A live spice prefix was reduced to a descriptive label. | (a) fixable now | Restored the raw prefix. |
| 6273 / 6368 | `<spice.history.gospels.ImmoralPractice.` | `不道徳な慣習` | Live prefix collapsed into stub text. | (a) fixable now | Restored the raw prefix. |
| 6278 / 6373 | `.adjective.!random> <spice.history.gospels.` | `形容詞付きの<spice.history.gospels.>` | Translator note text (`形容詞付き`) was inserted instead of preserving the adjective-to-location bridge. | (a) fixable now | Rebuilt the fragment as `.adjective.!random>の<spice.history.gospels.`. |
| 6288 / 6383 | `<spice.history.gospels.CivilizationActivity.` | `文明活動` | Live prefix collapsed into stub text. | (a) fixable now | Restored the raw prefix. |
| 6293 / 6388 | `<spice.history.gospels.Celebration.` | `祝宴の行い` | Live prefix collapsed into stub text. | (a) fixable now | Restored the raw prefix. |
| 6298 / 6393 | `<spice.history.gospels.LostItem.` | `紛失した品` | Live prefix collapsed into stub text. | (a) fixable now | Restored the raw prefix. |
| 6303 / 6398 | `<spice.history.gospels.MarriageAllianceResult.` | `婚姻同盟の結果` | Live prefix collapsed into stub text. | (a) fixable now | Restored the raw prefix. |
| 6313 / 6408 | `<spice.history.gospels.RitualName.` | `儀式名` | Live prefix collapsed into stub text. | (a) fixable now | Restored the raw prefix. |
| 6423 | `<spice.history.gospels.EarlySultanate.location.!random>` | `初期スルタン朝の場所` | This is a complete procedural token, so translating it into a note would always break lookup. | (a) fixable now | Restored the full live token unchanged. |

### Result

- Fixed all deterministic Issue 1 entries in the requested region.
- No per-entry `(b)` human-decision items remained once the approach was narrowed to “restore valid procedural fragments first.”
- One environment-level `(c)` note remains: the task reference path is stale for 2.0.4 (`Base/Dictionaries/world-gospels.json` is not present; the real source is split across `HistorySpice.json` and Annals code).

## Issue 2 — `ui-auto-generated.ja.json` untranslated UI labels

### Findings

- I scanned the entire file for ASCII-only / clearly untranslated player-facing values.
- That yielded **47** candidate entries.
- **40** were deterministically translated now.
- **7** were retained intentionally because they are already-established acronyms / brands / technical terms elsewhere in the repo (`UI`, `Mod`, `KBM`, `PS`, `XBox`, `VSync`).

| Line | Context | Key | Audited text | Disposition |
| --- | --- | --- | --- | --- |
| 90 | `CommandBinding.Category.Adventuring.Label` | `Adventuring` | `Adventuring` | translated to `冒険` |
| 95 | `CommandBinding.Category.UI.Label` | `UI` | `UI` | kept (standard acronym/brand) |
| 815 | `CommandBinding.CmdWaitN.Display` | `Wait a number of turns` | `Wait a number of turns` | translated to `指定ターン待機` |
| 825 | `CommandBinding.CmdWaitUntilMorning.Display` | `Rest until morning` | `Rest until morning` | translated to `夜明けまで休む` |
| 830 | `CommandBinding.CmdWaitUntilPartyHealed.Display` | `Rest until party is healed` | `Rest until party is healed` | translated to `仲間が全快するまで休む` |
| 930 | `CommandBinding.LookDirection/down.Display` | `Look south` | `Look south` | translated to `南を見る` |
| 935 | `CommandBinding.LookDirection/left.Display` | `Look west` | `Look west` | translated to `西を見る` |
| 940 | `CommandBinding.LookDirection/right.Display` | `Look east` | `Look east` | translated to `東を見る` |
| 945 | `CommandBinding.LookDirection/up.Display` | `Look north` | `Look north` | translated to `北を見る` |
| 1080 | `Options.Category.Mod.Label` | `Mod` | `Mod` | kept (standard acronym/brand) |
| 1085 | `Options.Category.UI.Label` | `UI` | `UI` | kept (standard acronym/brand) |
| 1165 | `Options.Option.OptionAbilityBarMode.Value.Compact` | `Compact` | `Compact` | translated to `コンパクト` |
| 1250 | `Options.Option.OptionAutoSipLevel.Value.Dehydrated` | `Dehydrated` | `Dehydrated` | translated to `脱水` |
| 1255 | `Options.Option.OptionAutoSipLevel.Value.Parched` | `Parched` | `Parched` | translated to `乾き` |
| 1265 | `Options.Option.OptionAutoSipLevel.Value.Thirsty` | `Thirsty` | `Thirsty` | translated to `渇き` |
| 1270 | `Options.Option.OptionAutoSipLevel.Value.Tumescent` | `Tumescent` | `Tumescent` | translated to `膨満` |
| 1345 | `Options.Option.OptionAutoexploreIgnoreDistantEnemies.Value.None` | `None` | `None` | translated to `なし` |
| 1355 | `Options.Option.OptionAutoexploreIgnoreEasyEnemies.Value.Average` | `Average` | `Average` | translated to `普通` |
| 1360 | `Options.Option.OptionAutoexploreIgnoreEasyEnemies.Value.Easy` | `Easy` | `Easy` | translated to `容易` |
| 1365 | `Options.Option.OptionAutoexploreIgnoreEasyEnemies.Value.Impossible` | `Impossible` | `Impossible` | translated to `不可能` |
| 1370 | `Options.Option.OptionAutoexploreIgnoreEasyEnemies.Value.None` | `None` | `None` | translated to `なし` |
| 1375 | `Options.Option.OptionAutoexploreIgnoreEasyEnemies.Value.Tough` | `Tough` | `Tough` | translated to `困難` |
| 1380 | `Options.Option.OptionAutoexploreIgnoreEasyEnemies.Value.Very Tough` | `Very Tough` | `Very Tough` | translated to `非常に困難` |
| 1520 | `Options.Option.OptionAutosaveInterval.Value.0|None` | `0|None` | `0|None` | translated to `0|なし` |
| 1625 | `Options.Option.OptionControllerFont.Value.Auto` | `Auto` | `Auto` | translated to `自動` |
| 1630 | `Options.Option.OptionControllerFont.Value.KBM` | `KBM` | `KBM` | kept (standard acronym/brand) |
| 1635 | `Options.Option.OptionControllerFont.Value.PS` | `PS` | `PS` | kept (standard acronym/brand) |
| 1640 | `Options.Option.OptionControllerFont.Value.PS Filled` | `PS Filled` | `PS Filled` | translated to `PS 塗りつぶし` |
| 1645 | `Options.Option.OptionControllerFont.Value.XBox` | `XBox` | `XBox` | kept (standard acronym/brand) |
| 1650 | `Options.Option.OptionControllerFont.Value.XBox Filled` | `XBox Filled` | `XBox Filled` | translated to `XBox 塗りつぶし` |
| 1890 | `Options.Option.OptionDisplayFramerate.Value.Unlimited` | `Unlimited` | `Unlimited` | translated to `無制限` |
| 1895 | `Options.Option.OptionDisplayFramerate.Value.VSync` | `VSync` | `VSync` | kept (standard acronym/brand) |
| 1935 | `Options.Option.OptionDisplayHPWarning.Value.No Warning` | `No Warning` | `No Warning` | translated to `警告なし` |
| 1955 | `Options.Option.OptionDisplayResolution.Value.*Resolution` | `*Resolution` | `*Resolution` | translated to `*解像度` |
| 1975 | `Options.Option.OptionDockMovable.Value.Flip` | `Flip` | `Flip` | translated to `反転` |
| 1980 | `Options.Option.OptionDockMovable.Value.Left` | `Left` | `Left` | translated to `左` |
| 1985 | `Options.Option.OptionDockMovable.Value.Right` | `Right` | `Right` | translated to `右` |
| 1990 | `Options.Option.OptionDockMovable.Value.Unset` | `Unset` | `Unset` | translated to `未設定` |
| 2155 | `Options.Option.OptionMainMenuBackground.Value.Classic` | `Classic` | `Classic` | translated to `クラシック` |
| 2160 | `Options.Option.OptionMainMenuBackground.Value.Modern` | `Modern` | `Modern` | translated to `モダン` |
| 2205 | `Options.Option.OptionMouseCursor.Value.Alternate` | `Alternate` | `Alternate` | translated to `代替` |
| 2210 | `Options.Option.OptionMouseCursor.Value.Default` | `Default` | `Default` | translated to `デフォルト` |
| 2215 | `Options.Option.OptionMouseCursor.Value.System` | `System` | `System` | translated to `システム` |
| 2300 | `Options.Option.OptionPlayScale.Value.Cover` | `Cover` | `Cover` | translated to `カバー` |
| 2305 | `Options.Option.OptionPlayScale.Value.Fit` | `Fit` | `Fit` | translated to `フィット` |
| 2310 | `Options.Option.OptionPlayScale.Value.Pixel Perfect` | `Pixel Perfect` | `Pixel Perfect` | translated to `ピクセルパーフェクト` |
| 2435 | `Options.Option.OptionSessionBackups.Value.None` | `None` | `None` | translated to `なし` |

### Result

- Fixed all clear English leftovers that should be localized in the generated UI dictionary.
- Left the seven standard acronyms/brands untouched because the repo already uses those forms elsewhere (`Options.jp.xml`, `ui-options.ja.json`, `ui-default.ja.json`).

## Issue 3 — `templates-variable.ja.json` dynamic verb templates

### Findings

- There are **6** entries whose English key contains `=verb:...=` tokens while the Japanese text hardcodes the verb.
- After checking `StartReplaceTranslationPatch.cs` and `StartReplaceTranslationPatchTests.cs`, these are **acceptable**, not broken.
- The patch replaces the **entire** English template string before the game engine resolves remaining variables, so Japanese does not need to preserve English subject-verb agreement markers.

| Line | English key | Context | Current Japanese | Verdict | Reason |
| --- | --- | --- | --- | --- | --- |
| 14 | `=subject.T= =verb:bask= in the sunlight and =verb:absorb= the nourishing rays.` | `XRL.World.Parts.Mutation.PhotosyntheticSkin` | `=subject.T=は日光の中で日光浴し、滋養に満ちた光を吸収した。` | Acceptable | `StartReplaceTranslationPatch` replaces the whole source string before variable expansion; Japanese does not need English-style subject-verb agreement, so hardcoding the verb is fine. |
| 29 | `{{K|=subject.T= =verb:slip= on the ink!}}` | `XRL.Liquids.LiquidInk` | `{{K|=subject.T=はインクで滑った！}}` | Acceptable | Explicitly covered by `StartReplaceTranslationPatchTests`. |
| 34 | `{{slimy|=subject.T= =verb:slip= on the slime!}}` | `XRL.Liquids.LiquidSlime` | `{{slimy|=subject.T=はスライムで滑った！}}` | Acceptable | Explicitly covered by `StartReplaceTranslationPatchTests`. |
| 39 | `{{C|=subject.T= =verb:slip= on the oil!}}` | `XRL.Liquids.LiquidOil` | `{{C|=subject.T=は油で滑った！}}` | Acceptable | Explicitly covered by `StartReplaceTranslationPatchTests`. |
| 44 | `{{C|=subject.T= =verb:slip= on the ice!}}` | `XRL.Liquids.LiquidWater` | `{{C|=subject.T=は氷で滑った！}}` | Acceptable | Explicitly covered by `StartReplaceTranslationPatchTests`. |
| 49 | `{{Y|=subject.T= =verb:slip= on the gel!}}` | `XRL.Liquids.LiquidGel` | `{{Y|=subject.T=はゲルで滑った！}}` | Acceptable | Explicitly covered by `StartReplaceTranslationPatchTests`. |

### Result

- No file change needed for Issue 3.
- The earlier audit note that treated these as placeholder breakage should be reclassified as a conservative false positive.

## Issue 4 — `Conversations.jp.xml` MT-quality scan

### Findings

- The most obvious MT artifacts clustered in two places:
  - the `Tzedech` chime dialogue, where several metaphoric lines were translated too literally;
  - the duplicated Klanq/golem help conversations, where intentionally broken English source lines had been mirrored into too-broken Japanese.
- I did **not** re-audit If/Then/Else terminology or ManyEyes, per scope.

| Line | Audited text | What was wrong | Disposition | Rewrite / note |
| --- | --- | --- | --- | --- |
| 206 | `嘲りか？ 挑発か？ よくもこれだけのことをしておいて、通信を試みるなどという残酷さが持てるものだ。` | Literal MT of “What cruelty drives you…” produced unidiomatic Japanese. | fixed | `嘲りか？ 挑発か？ これだけのことをしておいて、よくも交信を試みられるものだ。` |
| 212 | `その嘲笑が吐き気を催す。君が吐き気を催す。落ち着きなどしない、ティクヴァ！` | Repetition was mechanically translated and sounded broken. | fixed | `君の嘲りには吐き気がする。君自身にも吐き気がする。私は落ち着きなどしない、ティクヴァ！` |
| 218 | `おお、身振りをする手よ。` | Accurate word-by-word, but still highly opaque in Japanese; likely needs a human voice decision. | subjective / left |  |
| 241 | `君か。見ると煮え立つ。迎えもする。渦巻く。君はこの重みを知るか？ 知れるのか？` | Fragmented MT preserves the alien cadence but still reads rough enough to merit a human pass. | subjective / left |  |
| 246 | `私は引き裂かれている！ この結果、この結果、剪断が私を割く。...` | “the shear splits me” was rendered as the opaque noun `剪断`. | fixed | `私は引き裂かれている！ この結末が、この結末が私を裂く。...` |
| 254 | `存在よ！ 君は持続する意志と行為能力を与えられ、二度刻まれた肉体の建築と引く力の強い繊維を有する。...` | Several metaphor chunks were translated too literally (`肉体の建築`, `織り糸`). | fixed | `存在よ！ 君には持続する意志と行為の力、二度刻まれた肉体の構造、そして強く引く繊維が与えられている。...` |
| 274 | `聞け！ 君が操縦者であると知るのは今まで以上に重要だ。君は自らを鍛え直し、選択的な残酷さを行使する道具とする。...` | The argument remained understandable, but the Japanese was still rigidly literal. | fixed | `聞け！ 今こそ、君が操縦者だと知ることが何より重要だ。君は鍛え直され、研ぎ澄まされ、選び取った残酷さを行使するための道具となる。...` |
| 8962 | `アツムスは存在の超越写し。生き物パーツの特性を型取り、本体と交差させる。` | Telegraphic source voice is intentional, but the Japanese lost readability. | fixed | `アツムスは存在の超越的な写し。クリーチャーの部位の特性を型取りし、本体と交差させる。` |
| 8967 | `ゼータクローム武器。クリーチャーを武芸に同調させ、メタクロームの拳形（こぶしかた）をそのまま型取る。` | Word choice (`拳形`) read like raw MT. | fixed | `ゼータクローム武器。クリーチャーを武芸に同調させ、メタクロームの拳の型をなぞる。` |
| 8972 | `君の経験から話して書き付け、存在のきずなを形成。クリーチャーの夢脳が最初に聞く音。` | Missing particles and literal phrasing made the line feel broken. | fixed | `君の経験から生まれた言葉を書き付け、存在のきずなを形作る。クリーチャーの幼い夢脳が最初に聞く音。` |
| 8977 | `掌にはんだ付けする選んだトークン。重さ5ポンド以下。選択次第でクリーチャーの性質と行動が変わる。` | The first sentence read like an untranslated note fragment. | fixed | `掌にはんだ付けし、物の意味に向きを合わせる選んだトークン。重さ5ポンド以下。他と同じく、選択次第でクリーチャーの性質と行動が変わる。` |
| 8982 | `スピンドル上昇クリーチャーには強い電池と珍しい電磁シールド必要。紫トゲでフラックス3ドラム使って中性子セル作る、または別電源探す。` | Particles were dropped and the second sentence read like raw gloss text. | fixed | `スピンドルを昇るクリーチャーには強力な電池と希少な電磁遮蔽が要る。紫トゲならフラックス3ドラムで中性子セルを作れる。さもなくば別の電源を探す。` |
| 8987 | `スクラップ粘土の盛り土と対話する。それからクランクと話して回路ハンダ付け、スープかき混ぜる。` | Telegraphic voice is fine, but the Japanese was still malformed. | fixed | `スクラップ粘土の塚とやり取りする。それからクランクと話し、回路をはんだ付けしてスープをかき混ぜる。` |
| 8992 | `何聞く？ クランク話す。` | The broken-English voice can be kept without dropping all Japanese particles. | fixed | `何を聞く？ クランク、答える。` |
| 9004 | `クリーチャーは友だちみたいに君についてくる。それから中に入って操作。友だちも一人なら一緒に入れる。` | Missing verb/object details made the line read like a draft note. | fixed | `クリーチャーは友だちみたいについてくる。それに、中に入って操縦もできる。友だち一人なら一緒に乗れる。` |
| 9009 | `クリーチャーは巨大クリーチャー。超大型ギアしか装備できない。クラーケンとかソルトバックみたい。` | Literal repetition (`巨大クリーチャー`) and raw loanword `ギア` felt machine-translated. | fixed | `クリーチャーは大きな生き物。超大型装備しか身につけられない。クラーケンやソルトバックみたい。` |
| 9014 | `トゲトゲがオモンポーチのスルタン法廷に修復の小部屋を作った。そこでサンスラグ1ドラムでクリーチャー塗り直す。失くしたら呼び戻す。` | The line lost helper verbs and read like memo text. | fixed | `トゲトゲがオモンポーチのスルタン法廷に作り直しの小部屋をこしらえた。そこでサンスラグ1ドラムでクリーチャーを塗り直せる。失くしても呼び戻せる。` |
| 9019 | `そう！ でもそれまでにもクリーチャーと歩ける。話して、戦って、眠って……普通。友だち。` | “the normal. Friend.” was copied too literally. | fixed | `そう！ でもその前でもクリーチャーと一緒に歩き回れる。話す、戦う、眠る……ふつうのこと。友だち。` |
| 9024 | `クランク、生きてる！ ジャロピー悲劇がクランクを薄暗いろうそく部屋に招いた。石床をぬいぐるみで柔らかくして、退屈な瓶棚を喜びと胞子で磨いた。トゲトゲ、見て。` | Readable in outline, but several noun compounds were still raw MT. | fixed | `クランク、生きる！ ジャロピーの悲劇がクランクを薄暗いろうそく部屋へ招いた。クランクは石床をぬいぐるみで柔らかくして、退屈な瓶棚に喜びと胞子をまぶした。トゲトゲ、見て。` |
| 13366 | `クランク、生きてる！ ジャロピー悲劇がクランクを薄暗いろうそく部屋に招いた。...` | The duplicated late-file golem block repeated the same MT artifact. | fixed | `9024` と同じ修正を反映。` |

### Result

- Fixed the clear, source-backed MT problems above.
- Left the more interpretive chime-poetry lines at **218** and **241** for human judgment; they are understandable, but still stylistically debatable enough that I did not want to over-normalize them.
- A post-write fast scan surfaced four more deterministic conversation fixes, which are already applied in `Conversations.jp.xml`:
  - **43**: `私は菌類の友だ。私を植民させてくれ。` → `私は菌類の友だ。私の体に定着させてくれ。`
  - **3089 / 3104 / 3117 / 3130 / 3145 / 3156, plus 3097**: `液体修理工` → `液体職人`
  - **5631**: `手のコートありますよ！` → `手袋ありますよ！`
  - **10264**: `肉の修理工` → `生身の職人`

## Files changed

- `Mods/QudJP/Localization/Dictionaries/world-gospels.ja.json`
- `Mods/QudJP/Localization/Dictionaries/ui-auto-generated.ja.json`
- `Mods/QudJP/Localization/Conversations.jp.xml`
- `docs/audit-final-remaining.md`
