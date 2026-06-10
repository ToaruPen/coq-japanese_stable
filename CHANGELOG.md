# Changelog

All notable changes to QudJP will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

---

## [0.5.01] - 2026-06-11

### Fixed

- inventory / equipment 画面で、日本語化後の表示名更新によって並び順や選択状態が崩れる場合を修正しました。
- inventory action menu の更新時に、表示名・行テキスト・cursor sound の状態が安定して保たれるようにしました。

---

## [0.5.00] - 2026-06-06

### Added

- blank/ruined mural slate など、壁画生成で出る追加表示名を日本語化しました。
- 変異表示名と metamorphed effect label の日本語化範囲を広げました。

### Changed

- map note、reputation secret、object label に出る becoming nook の訳語を `変容の僻隅` に統一しました。
- 歴史的地点の訪問・探索・回収・依頼者・map pin など、生成クエスト tab の表示文を追加で日本語化しました。
- baetyl の報酬 wish / 要求文に出るアイテム名を日本語化し、folded carbide axe などの報酬表示にも対応しました。
- 歴史文に出る配偶者・家族関係表現の日本語化範囲を広げました。

### Fixed

- AutoAct 停止メッセージで、すでに日本語化された action label が崩れる場合を修正し、chem-cell inventory action の recharge hotkey を保つようにしました。
- cybernetics replacement option の日本語表現を見直しました。
- recoiler destination、生成 miner/bomber 名、10-pointed asterisk debug object など、world-part 表示名に残っていた未翻訳箇所を修正しました。
- grip selection、recoiler use、no-recoiler failure、item-picking WishCommand などの popup prompt を日本語化しました。
- Spaser weapon 表示名、food-status 表現、stun/daze/Quickness 用語、quest step 表示名、dynamic quest site-intro frame、Annals gospel fallback、Tinkering detail label、会話・クエスト文に残っていた英語表現を調整しました。
- Otho の Grit Gate 初回報酬会話、inspired / plasma-coated active effect detail、procedural yuckwheat 料理効果文、Baetyl relic item name、AutoAct hostile-sighting popup、StairsDown long-fall prompt、molting basilisk description など、runtime 生成テキストに残っていた未翻訳箇所を修正しました。

---

## [0.4.10] - 2026-05-31

### Fixed

- Player.log で確認された ability label、mutation rank-up popup、liquid pour prompt/option、skill status label、generated item description、advanced toolkit の loaded-cell color tag などの未翻訳 UI・説明文を修正しました。
- mutation popup、active effect、item mod name、popup/log 内の generated display name など、追加で見つかった runtime 生成テキストの未翻訳表示を日本語化しました。
- color markup 付きの item name や generated display name で、日本語化後も色タグが保たれるようにしました。
- tutorial Mehmet の POI highlight が正しく日本語化経路に乗るようにしました。
- inventory line、menu option、bottom context 表示の処理を見直し、inventory/menu の表示更新を軽くしました。

---

## [0.4.00] - 2026-05-29

### Fixed

- runtime 生成テキストの翻訳範囲を大きく広げ、ability label、説明文、popup option、message frame、生成 UI fragment に残っていた英語表示を追加で日本語化しました。
- mutation 選択と rapid advancement popup option、ability bar label、direction prompt、targeting footer、fire-mode prompt、ability manager、trade、tinkering、inventory、main menu などの UI 表示を追加で日本語化しました。
- inventory action、campfire ingredient row、liquid collect/fill、firefighting、sleeping target wake-up、companion dismissal、auto-collect など、条件で変わる操作ラベルと失敗メッセージを改善しました。
- active-effect adjective chain、stuck-in effect target、複数形の combat miss、ForceProjector access failure、liquid slip、NPC wild-shot、lost-travel failure、Water Ritual reputation article residue など、Player.log で観測された未翻訳 runtime text を修正しました。
- Player.log で追加確認された combat log、mutation prompt、inventory action、energy-cell UI、auto-disassembly message、graffiti/readout description、powered-off stat bonus、lost-chance description、liquid-covered effect name、`water-stained` item prefix などの未翻訳表示を修正しました。
- schemasoft chip、Cyclopean Prism、PitMaterial、Evil Twin、Cherubim/Hexacherubim element text、brackish liquid puddle、water-bond、compound stained、solar pumping station、hydraulic liquid/flywheel、Oboroqoru worshipper title suffix などの生成表示名・説明文を日本語化しました。
- generated journal location discovery、painted/engraved Sultan challenge tooltip、Jewel-Encrusted short description、stat adjustment description で、英語残りや color markup の崩れが出る経路を修正しました。
- ray mutation message log、ability bar hotkey suffix、generated liquid color tag、attack-confirmation target name などで、日本語化済みの body-part/source/target と色タグが保たれるようにしました。
- translated item name、charge cell、recipe name、tinkering bit code などで、日本語化後も color markup が保たれるようにしました。

---

## [0.3.21] - 2026-05-23

### Fixed

- turret tinkering、long blade stance、schemasoft chip、Cyclopean Prism、PitMaterial、Evil Twin、Cherubim/Hexacherubim element text などの生成・表示名テキストを追加で日本語化しました。
- gigantic item mod description、sparking baetyl reward description、bandaging、multi-horns charge message などの説明文・報酬文・戦闘ログに残っていた英語表示を改善しました。
- アイテム表示名の翻訳時に、angle suffix より前にある色 markup が失われる場合がある問題を修正しました。
- エネルギーセルの充電状態ラベルを見直し、インベントリや詳細表示に残っていた不自然な表現を改善しました。

---

## [0.3.20] - 2026-05-22

### Fixed

- item modifier の prefix、with-clause、flexiweaved の段階表示など、アイテム名に残っていた英語断片を追加で日本語化しました。
- インベントリ操作名と、歴史的な場面説明文の一部に残っていた英語表示を日本語化しました。
- 固定アイテム、effect、ability、option、object description の訳文を見直し、不自然な表現や残っていた英語用語を改善しました。

---

## [0.3.01] - 2026-05-22

### Fixed

- metabolized effect や long blade stance など、active effects 画面に残っていた英語表示の日本語カバレッジを改善しました。
- Charge、Death From Above、Juke、Hook and Drag、Proselytize、Slam、Make Camp、field amputation、Tinkering recharge、repair outcome など、スキル由来の失敗ログ・確認ポップアップ・メッセージ表現を追加で日本語化しました。
- 生成クエスト、journal text、message frame で日本語化される表示文の範囲を広げました。
- 料理効果名と食事効果ポップアップで使われる `metabolized` 系の日本語表現を統一しました。
- 終了確認ポップアップの長押し承認文とセーブせず終了するボタン表示を日本語化しました。

---

## [0.3.00] - 2026-05-20

### Changed

- 確認ポップアップの質問文を、丁寧な `しますか？` 形式へ揃えました。
- macOS Apple Silicon 向け Harmony 2.4.2 native ARM64 回避策に合わせて、ビルド時の `Lib.Harmony` fallback package を更新しました。
- 既存のメッセージパターン翻訳が正しい owner surface で追跡されるよう、確認済み経路の分類を整理しました。

### Fixed

- アビリティ詳細、アビリティ画面のホットキー、ステータス画面タブ、飛行メッセージ、レーザー光線ダメージログに残っていた英語表示を日本語化しました。
- 生成される歴史、墓碑文、村の伝承、クエスト会話、料理本名、レリック文、地下聖堂の銘文、墓石、サイキックハンター称号、集落名の日本語カバレッジを拡充しました。
- 方角や近隣ロケーションを含む動的クエスト標識が、英語 prefix 断片ではなく自然な日本語の目標文として表示されるようにしました。
- 生成される形容詞、身体特徴、地形、活動、称号の断片を、色 markup、placeholder、固有名を保ったまま自然な日本語へ置き換えました。
- 生成アイテム説明、active effect 名、インベントリの武器状態ラベルに残っていた英語表示を日本語化しました。
- code redemption、色選択、マウス入力、save error などの固定ポップアップ文を日本語化しました。
- Exodus countdown、ミサイル命中出力、automatic action stop、料理 trigger、normality-lattice contest などのログ・ポップアップ文を追加で日本語化しました。
- QudJP 有効時に、一部の Carbide Chef と手続き生成料理レシピから効果が失われる問題を修正しました。
- サイバネティック implant の説明文を Caves of Qud 1.0.4 の効果に合わせて修正しました。
- ゴーレム素材選択ポップアップの翻訳 patch が起動時に失敗する問題を修正しました。
- 日本語 UI 翻訳を有効にしたとき、journal entries が消える問題を修正しました。
- Unstable Genome、Beak、Beguiling、Domination、Psychometry、Dystechnia の mutation 説明訳を修正しました。
- pass-by、XDidY、戦闘メッセージ、所有表現、Does subject、message/journal capture、mutation self-target prompt、belcher output、conk confirmation、生成像ラベルなどに残る英語冠詞・所有代名詞を除去しました。
- `[swimming]`、`bloody wet`、rusted、broken、cracked、flying、wading、raised、timer/cell/chapter/ammo suffix などの状態・suffix 表示を日本語化しました。
- bleeding、leaking、oozing、fluxing などの message frame、damage source、wound stop log 表現を日本語化しました。
- Tzedech/Welcome 会話の空 override を削除し、localization validation warning が出ないようにしました。
- Water Ritual の message log、summary、conversation choice に残っていた英語表示を日本語化しました。
- campfire cooking menu、ingredient selection、recipe cooking、meal creation message、生成 journal text、HistorySpice 系の歴史名に残っていた英語表示を日本語化しました。
- masterwork weapon mod の説明、表示名、maker mark、examine-identification message を日本語化しました。
- schematic や data disk の bit cost など、display name の angle suffix に含まれる色付き tinkering bit tag を保持するようにしました。
- ミサイル武器の複数 ammo・複数 projectile の runtime tooltip 行を日本語化しました。
- cracked scrap component の item-name 表現を、より分かりやすい `ひび割れた` に修正しました。

---

## [0.2.52] - 2026-05-13

### Added

- macOS Apple Silicon 環境で Harmony 2.4.2 の native ARM64 回避策を試すための上級者向け補助スクリプトと、QudJP が保持したバックアップからゲーム DLL を戻す復元スクリプトを追加しました。

### Fixed

- 戦闘スキル、火災鎮圧、ゴーレム選択、ロケーション検索、変異の自己対象確認、古いセーブデータの継続確認、Water Ritual、Trade UI などの自動生成ポップアップ・メッセージに日本語訳を追加しました。
- 追加の静的生成テキストについて、実行時の文脈に応じた日本語訳が使われる経路を拡充しました。
- 取引、充電状態、料理、ステータス画面、キャンプ保存、アビリティ管理画面の一部ポップアップ訳を修正しました。
- 攻撃が対象の装甲を貫通しなかったときの戦闘ログ表示について、ロール値が表示されない経路でも日本語化されるようにしました。
- アイテム名を日本語化したときに、AV/DV や武器性能の記号色が失われる場合がある問題を修正しました。
- ミサイル武器 UI の射撃・リロード表示を日本語化する際、Unity のレイアウト更新例外が起きることがある問題を修正しました。
- ローカル確認用の dev 同期で、ゲーム側が無効なバージョン文字列として扱う Version が生成されないようにしました。

---

## [0.2.51] - 2026-05-10

### Changed

- UI コマンドの日本語ラベルについて、look 系の操作と examine 系の操作を区別しやすくしました。

### Fixed

- 複数の自動生成ポップアップ・メッセージ経路に不足していた日本語 owner-route 翻訳を追加しました。
- 歴史的な墓碑銘の冒頭文を、UI の look コマンドではなく物語文として訳すようにしました。
- インベントリ UI 更新後に InventoryLine の TMP 表示が崩れる問題を修復しました。
- インベントリの追加操作コンテキストメニューラベルを日本語化しました。
- Mod Manager 下部コンテキストのホットキー表示で、括弧付きキー表記を保持し、不明な color-code 警告が出ないようにしました。
- 実行時辞書の重複キー診断を通常起動ログへ出しすぎないようにし、テキストを変える重複だけを override として扱うようにしました。
- `あなたは a ...` のような翻訳済み動的メッセージに残る英語冠詞を除去しました。
- スナップジョー関連の日本語表記を `スナップジョー` に統一しました。

---

## [0.2.50] - 2026-05-09

### Changed

- 内部リリースメタデータとドキュメントの対象ゲーム表記を Caves of Qud 1.0.4 に合わせました。
- チュートリアルに対応しました。具体的には、ポップアップ、ガイド付きハイライト、look-mode コマンド、キャラクタープリセット案内を日本語化しました。
- 固有タイトル `Caves of Qud` は許容しつつ、単独の `Qud` 表記を glossary check で検出する挙動を文書化しました。

### Fixed

- `L` キーの look 表示経路で使われる TMP ツールチップに対して、日本語説明文が途中で欠けたり非表示になったりする問題を修復する処理を追加しました。
- 長い日本語説明文を TMP / Qud markup / 変数 placeholder を壊さずに意味的な区切りで折り返すようにし、look tooltip の長文表示を読みやすくしました。
- インベントリ項目名の TMP replacement が折り畳み・更新・非アクティブ行の影響で不安定になる経路を固めました。
- verbose probe と runtime diagnostics の gate を整理し、通常リリースビルドでの診断ログ生成や観測処理の負荷を抑えるようにしました。
- 実行時 JSON アセットの読み込みを `Newtonsoft.Json` ベースに統一し、Linux Unity/Mono 環境での辞書・パターン読み込み互換性を改善しました。
- Workshop staging で release DLL に必須の runtime marker が含まれることを検証し、dev-only probe marker を含む DLL を弾くようにしました。
- 改行エスケープ表記だけが異なる重複 JSON 辞書キーを検出できるようにしました。

---

## [0.2.46] - 2026-05-07

### Fixed

- Steam Workshop 版でインベントリのアイテム名修復処理が `UnityEngine.Vector2.get_x()` 例外により止まり、アイテム名が消える問題を修正しました。

---

## [0.2.45] - 2026-05-07

### Fixed

- Windows 環境でインベントリ表示の診断処理が Unity API のビルド差異により失敗し、アイテム名の修復処理が止まる問題を修正しました。

---

## [0.2.44] - 2026-05-07

### Fixed

- TextMeshPro のフォント warmup API がゲーム本体のビルド差異で異なる場合でも、
  QudJP の起動が失敗しないようにしました。
- アイテム操作ポップアップで `load` / `unload` が日本語化されない問題を修正しました。

---

## [0.2.43] - 2026-05-07

### Fixed

- インベントリ画面のアイテム名が消える問題について、ローカル検証では
  `0.2.42` 時点で解決していた修正が、CI/CD の release artifact 生成不備により
  Steam Workshop 版 DLL へ反映されていなかった問題を修正しました。
- インベントリ項目の折り畳みや更新時に、非アクティブな行が表示中の
  アイテム名 replacement を誤って無効化する経路を修正しました。
- インベントリ項目の折り畳み時に、保持済みアイテム名 replacement の位置・
  色・透明度・表示状態が元の行に追従するようにしました。
- Steam Workshop へ出荷する DLL に TextMeshPro / InventoryLine 表示修正が
  含まれていない場合、release 検証で失敗するガードを追加しました。
- インベントリ操作の `remove`、メモ、重要マーク関連ラベルの日本語訳を
  実際のゲーム内表示キーに合わせて修正しました。
- 「You embark for the caves of Qud.」の日本語訳を
  `クッドの洞窟へ旅立つ。` に統一しました。

---

## [0.2.42] - 2026-05-07

### Fixed

- インベントリ画面でアイテム名が表示されなくなる問題を追加修正しました。

---

## [0.2.41] - 2026-05-07

### Changed

- README、NOTICE、Steam Workshop 説明文の対応ゲーム表記を Caves of Qud 1.0.4 対応として明確化しました。

### Fixed

- 生成された村の帆布テント名とサイバネティクス端末の身体部位サフィックスを日本語化しました。
- インベントリ画面で一部アイテム名が表示されなくなる問題を修正しました。

---

## [0.2.4] - 2026-05-06

### Changed

- Standardize the shipped mod display name as `Caves of Qud Japanese Mod`.

### Fixed

- Improve Japanese localization for generated hookah, mutation, liquid
  container, statue, and combat messages.
- Improve Japanese coverage for combat logs, popups, world mod text, and
  missile targeting UI labels such as Fire and Reload.
- Improve Japanese localization for generated runtime text, including generated
  display names, quest titles, shrine prayer messages, targeting commands,
  effect details, and combat log messages.
- Improve Japanese localization for generated status and display-name text,
  including seated, prone, engulfed, enclosed, piloting, marked, cleaved,
  dominated, and time-dilated states.

---

## [0.2.3] - 2026-05-06

### Changed

- Added the tag-triggered GitHub Release ZIP workflow as the source artifact
  for Steam Workshop staging.
- Marked newly created GitHub Releases as `Latest` so the repository release
  page follows the newest shipped version.
- Documented and verified the release artifact download path used before
  Workshop upload.

### Notes

- This is a release-pipeline validation release. Game localization content is
  unchanged from v0.2.2 except for the manifest version.

---

## [0.2.2] - 2026-05-05

### Fixed

- Fixed Control Mapping category grouping and preserved upstream directional key bindings.
- Fixed several generated runtime localization routes, including trade-water popup text, pet mutation death messages, zone wind changes, and fresh-water terminology.
- Improved Japanese runtime text coverage for combat log, popups, death summary, and deployable object messages.
- Repaired invisible text in game summary and journal-style TextMeshPro screens.

---

## [0.2.1] - 2026-05-05

### Fixed

- Improve Japanese coverage for generated ability names, active effects,
  cooking effects, message log text, and village history descriptions.
- Improve Japanese translation coverage for multiline item descriptions, popup
  messages, and slimy/statue messages.

---

## [0.2.0] - 2026-05-04

### Added

- Broad runtime translation coverage for ability bars, status screens, trade,
  tinkering, quests, journals, mod management, world map, save/load, death,
  score, achievement, popup, and conversation UI routes.
- Japanese procedural text coverage for Markov corpus text, procedural names,
  titles, body-part variants, world parts, world mods, cooking/status effects,
  death reasons, historical narratives, and Sultan Resheph annals.
- CJK font packaging and runtime fallback setup for Workshop/mod-manager use.
- Release, Steam Workshop, translation-token, glossary-consistency, XML source
  markup, runtime smoke, and agent workflow validation tooling.
- Sultan Resheph history translation in `Mods/QudJP/Localization/Dictionaries/annals-patterns.ja.json` (#420 PR1).
- Build-time pipeline at `scripts/{extract,validate,translate,merge}_annals_patterns.py` and `scripts/tools/AnnalsPatternExtractor/` for extracting and translating regex/template pairs from `XRL.Annals/*.cs` (#420 PR1).
- `JournalPatternTranslator` now supports ordered multi-file pattern dictionary load (`SetPatternFilesForTests(params string[])`).

### Changed

- Moved many dynamic translation routes from sink-side fallback toward
  producer/owner-side translation so rendered text keeps markup and route
  context more reliably.
- Normalized terminology and glossary usage across item names, factions,
  mutations, books, cooking text, UI labels, and static XML/JSON dictionaries.
- Expanded C# L1/L2/L2G and Python test coverage for runtime routes,
  placeholder parity, markup preservation, scanner output, release packaging,
  and Workshop staging.

### Fixed

- Preserved color markup, placeholder tokens, literal escape markers, and
  information-bearing variables across translated XML and JSON assets.
- Repaired remaining untranslated or unstable UI routes found through runtime
  log audits, including popup handoffs, player status bar refresh, chargen
  summaries, display-name tails, journal/lair text, and ability HUD text.
- Cleaned XML validation false positives and stale generated-artifact scanning
  behavior.

---

## [0.1.0] — 2026-03-11

### Added

**Project scaffolding**
- Repository structure: `Mods/QudJP/`, `scripts/`, `docs/`, `.github/`
- `manifest.json` with mod metadata (Id, Title, Author, Version, Tags)
- `pyproject.toml` with Ruff `select = ["ALL"]` and pytest configuration
- `.editorconfig` for consistent formatting across editors
- GitHub Actions CI pipeline (Ubuntu 24.04, .NET 8.0.x + 10.0.x, Python 3.12)

**C# translation infrastructure**
- `Translator` — JSON dictionary loader with lazy initialization and `ConcurrentDictionary` cache (net48 compatible)
- `ColorCodePreserver` — Preserves and restores `{{W|...}}`, `&X`, `^Y` color codes around translation
- `QudJPMod` — Harmony patch entry point with fail-fast initialization

**Harmony patches (11 patches, 8 Grammar methods)**
- `OptionsLocalizationPatch` — Translates options screen labels via Postfix on `Show()`
- `MainMenuLocalizationPatch` — Translates main menu button labels via Postfix on `Show()`
- `PopupTranslationPatch` — Translates popup titles, body text, and button labels via `HarmonyTargetMethods` Prefix
- `UITextSkinTranslationPatch` — Common UI text translation via `UITextSkin`
- `GrammarPatch` — Neutralizes English grammar for Japanese: removes articles, disables pluralization, appends `の`, and adapts list formatting. Covers 8 methods: `A`, `Pluralize`, `MakePossessive`, `MakeAndList`, `MakeOrList`, `SplitOfSentenceList`, `InitCaps`, `CardinalNumber`
- `ConversationDisplayTextPatch` — Translates conversation node display text via Postfix
- `GetDisplayNamePatch` — Translates item/creature display names
- `CharGenLocalizationPatch` — Translates character generation UI
- `InventoryLocalizationPatch` — Translates inventory screen
- `MessageLogPatch` — Translates message log entries using regex pattern matching
- `ProceduralTextPatch` — Translates `HistoricStringExpander` output

**Roslyn analyzers**
- `EnableNETAnalyzers`, `AnalysisLevel=latest-all` — full .NET analyzer suite enabled
- SonarAnalyzer.CSharp — additional static analysis rules
- Custom analyzers: QJ001 (catch-all suppression), QJ002 (null-coalescing in TargetMethod), QJ003 (empty catch block)

**Python tooling**
- `check_encoding.py` — UTF-8 BOM detection, CRLF detection, mojibake character detection
- `validate_xml.py` — XML parse validation, color code balance check, duplicate ID/Name detection, empty `<text>` detection
- `diff_localization.py` — Translation coverage comparison between base game XML and localized XML, with `--summary` and `--missing-only` modes
- `extract_base.py` — Copies game `StreamingAssets/Base/` XML to local `references/Base/`
- `sync_mod.py` — Include-first deployment via rsync (87 files, excludes source code)
- `build_release.py` — Builds Release DLL and creates `dist/QudJP-v{version}.zip`

**Test suite**
- 101 C# NUnit tests (L1 pure logic + L2 Harmony integration)
- 83 Python pytest tests across all scripts
- 3-layer test architecture: L1 (no HarmonyLib), L2 (no UnityEngine), L3 (manual game smoke)
- DummyTarget pattern for L2 tests — no direct instantiation of `Assembly-CSharp.dll` types

**Legacy XML migration**
- 35 XML translation files (66,306 lines) migrated from legacy project
- 35 JSON dictionary files (32,836 lines) migrated from legacy project
- All files normalized to UTF-8 without BOM, LF line endings, zero mojibake characters

**Fail-fast error handling**
- Initialization failures (`LoadTranslations`, `ApplyHarmonyPatches`) raise exceptions immediately
- `TargetMethod()` resolution failures log `Trace.TraceError` and return `null`
- Runtime translation failures log `Trace.TraceError` and return original text (no exception thrown)

**Documentation**
- `docs/glossary.csv` — 84-entry terminology glossary with English, Japanese, Short, Notes, Status columns
- `docs/game-data-analysis.md` — Analysis of base game XML structure and translatable string counts
- `docs/ilspy-analysis.md` — ILSpy decompilation findings for key game types
- `docs/poc-results.md` — Proof-of-concept results for Harmony + NUnit on macOS ARM64
- `docs/migration-plan.md` — Legacy XML migration plan and execution record
- `docs/translation-coverage-report.md` — Translation coverage by category (ObjectBlueprints 77%, Conversations 97.5%)
- `docs/deployment.md` — Deployment guide

---

[Unreleased]: https://github.com/ToaruPen/coq-japanese_stable/compare/v0.5.01...HEAD
[0.5.01]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.5.01
[0.5.00]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.5.00
[0.4.10]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.4.10
[0.4.00]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.4.00
[0.3.21]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.3.21
[0.3.20]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.3.20
[0.3.01]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.3.01
[0.3.00]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.3.00
[0.2.52]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.52
[0.2.51]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.51
[0.2.50]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.50
[0.2.46]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.46
[0.2.45]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.45
[0.2.44]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.44
[0.2.43]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.43
[0.2.42]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.42
[0.2.41]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.41
[0.2.4]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.4
[0.2.3]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.3
[0.2.2]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.2
[0.2.1]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.1
[0.2.0]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.2.0
[0.1.0]: https://github.com/ToaruPen/coq-japanese_stable/releases/tag/v0.1.0
