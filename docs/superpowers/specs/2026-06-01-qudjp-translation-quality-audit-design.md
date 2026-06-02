# QudJP Translation Quality Audit Skill Design

## Why

QudJP の翻訳改善には、未翻訳やルート所有権の判定だけではなく、既存訳や候補訳を「文脈に対して正しいか」「日本語として自然か」「用語・文体が一貫しているか」で監査する作業が必要になる。既存の `qudjp-localization-triage` は runtime text と owner route の判断に強いが、訳文品質そのものを複数観点でレビューする入口は分離した方がよい。

## What

repo-local skill として `.codex/skills/qudjp-translation-quality-audit/` を作る。

このスキルは次の入力を扱う。

- `diff-review`: 変更済みの翻訳差分をレビューする。
- `single-candidate`: 1つまたは少数の英語/日本語表現について、誤訳・珍訳・改善案を判断する。
- `inventory-sweep`: 辞書、XML、コーパス、用語表から怪しい訳や表記揺れを広く探す。

必要に応じて複数エージェントを派遣する。派遣の基本役割は以下。

- `Context Investigator`: 実装、decompiled source、既存 asset、必要なら wiki から意味と文脈を調べる。
- `Japanese Quality Reviewer`: 日本語としての自然さ、文体、過剰な直訳、珍訳をレビューする。
- `Consistency Auditor`: `docs/glossary.csv`、`docs/glossary-policy.md`、既存 XML/JSON から表記揺れと context split を確認する。
- `Coordinator`: 各観点を統合し、採用訳、却下訳、保留、追加調査、検証コマンドを決める。

## Boundaries

このスキルは翻訳品質の判断手順を定義する。動的/generated text の owner route 判定や sink vs producer の実装修正判断が主目的になった場合は、既存の `qudjp-localization-triage` に接続する。

初期版では deterministic な表記揺れ検出スクリプトは作らない。まず人間/エージェント監査の手順、証拠順、出力形式を固める。繰り返し発生する検査だけを将来スクリプト化する。

## Workflow

1. 入力を `diff-review`、`single-candidate`、`inventory-sweep` に分類する。
2. 対象範囲と証拠源を宣言する。
3. 小さい単発候補は単独で監査し、差分や棚卸しでは独立観点ごとに複数エージェントを派遣する。
4. 文脈正確性、自然さ、一貫性、markup/placeholder 保持を別々に判定する。
5. Coordinator が採用案と根拠を出す。
6. 実装する場合は、翻訳 asset 変更の通常検証と、必要に応じて owner route の既存スキルへ引き継ぐ。

## Validation

スキル作成後に以下を行う。

- skill folder validator を通す。
- repo の dotfiles/static check を通す。
- empirical prompt tuning で realistic scenario を最低3つ評価する。
  - 単発の誤訳/珍訳候補。
  - 翻訳差分レビュー。
  - 用語・表記揺れ棚卸し。
- 曖昧さが出た場合はスキル文面を最小修正し、再検証する。

## Risks

- ルート所有権判断と訳文品質判断が混ざると、安易な dictionary leaf 追加につながる。
- 「自然な日本語」だけを優先すると、固有名詞・用語・Caves of Qud 固有の文体が崩れる。
- 複数エージェントの結果をそのまま採用すると、証拠レベルの違いが混ざる。

このため、最終判断は Coordinator が evidence order、glossary status、既存 asset、route safety を明示して統合する。
