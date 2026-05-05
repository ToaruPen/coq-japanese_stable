# Fixed-leaf Workflow

Archived note: this workflow is tied to the legacy bridge candidate inventory.
It is preserved as historical context only and is not source of truth for
issue #493 static producer work.

この文書は、issue-357 の Roslyn static SoT pilot で fixed-leaf candidate を集め、レビューし、検証し、昇格するための手順です。runtime proof や issue-358 の observability とは別です。

## 前提

- `~/dev/coq-decompiled_stable/` がローカルに存在していること
- それは tracing 用の decompiled source であり、外部の read-only input で、shipped artifact ではないこと
- `scripts/legacies/scan_text_producers.py` の `--validate-fixed-leaf` を使うこと。別の top-level validator はありません
- レビュー対象は `docs/candidate-inventory.json` と stdout の validation report です。`docs/candidate-inventory.json` は current static consumers 向けの bridge/view-only artifact で、source of truth ではありません
- fixed-leaf work は static coverage / asset promotion 専用です。runtime proof、Phase F observability、owner-route audit とは混ぜません

decompiled source は入力です。commit 対象ではありません。game binaries も同じ扱いです。

## first batches が実証したこと

最初の実運用で不足していたのは「手順」ではなく、queue の中身でした。最終 wording は次の 3 点に合わせます。

1. **最初の safe batch は空で正しかった**
   - 27-row の pending fixed-leaf queue は、すべて pseudo-leaf noise でした
   - 実体は `""`, `" "`, `BodyText`, `SelectedModLabel` の placeholder / spacing / widget-channel 識別子です
   - したがって first batch は import-first ではなく **prune-first** です
2. **duplicate-sensitive な queue なので、既存カバレッジは addition set から外して読む**
   - `translated` / `excluded` rows は fixed-leaf addition set に含めません
   - 既に狭い安全な home に入っている duplicate family は、新規 promotion ではなく existing coverage として defer します
3. **stale bridge bookkeeping は fixed-leaf promotion に混ぜない**
   - `Prone` と `HolographicBleeding` は既存 message-frame seam 上の stale bridge bookkeeping で、新しい fixed-leaf route work ではありません
   - こうした rows は reconcile / owner audit で片付け、fixed-leaf import の理由にしません

## 使うコマンド

### Happy path, scan から validation まで一気に回す

```bash
python scripts/legacies/scan_text_producers.py \
  --source-root ~/dev/coq-decompiled_stable \
  --cache-dir .scanner-cache \
  --output docs/candidate-inventory.json \
  --phase all \
  --validate-fixed-leaf
```

### 既存 cache を使って Phase 1d だけ再実行する

```bash
python scripts/legacies/scan_text_producers.py \
  --source-root ~/dev/coq-decompiled_stable \
  --cache-dir .scanner-cache \
  --output docs/candidate-inventory.json \
  --phase 1d \
  --validate-fixed-leaf
```

## issue-357 first PR verification

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Analyzers.Tests/QudJP.Analyzers.Tests.csproj --filter StaticSotPilot
pytest scripts/tests/test_scan_text_producers.py
pytest scripts/tests/test_scanner_inventory.py
pytest scripts/tests/test_scanner_ast_grep_runner.py
pytest scripts/tests/test_scanner_rule_classifier.py
pytest scripts/tests/test_scanner_cross_reference.py
pytest scripts/tests/test_reconcile_inventory_status.py
```

## Review checklist

候補を読むときは、各 site に次の情報が明示されているかを見ます。

- source route
- ownership class
- confidence
- destination dictionary
- rejection reason

候補の値は省略せず、`null` でも明示されているほうがレビューしやすいです。

レビュー順は固定です。

1. pseudo-leaf noise を先に除外する
   - `""`
   - `" "`
   - `BodyText`
   - `SelectedModLabel`
2. `translated` / `excluded` rows を current addition set から外す
3. 残った candidate だけを fixed-leaf gate に照らして読む
4. promoted / deferred / rejected を、source route・ownership class・destination dictionary・rejection reason 付きで記録する

次の条件を満たすものだけを accepted fixed leaf とします。

- source が stable である
- owner-safe である
- markup-preserving である
- `needs_runtime` ではない

次の種類は fixed-leaf ではありません。

- `message-frame`
- builder / display-name
- procedural text
- unresolved site
- `needs_runtime` site
- markup を壊す候補
- `AddPlayerMessage` のような sink-observed route
- 既存 seam で既に covered なのに bridge artifact だけ stale な bookkeeping row

## Destination choice

destination dictionary は、validator が見る tier です。

- `global_flat`: repo-wide で共有できる exact leaf
- `scoped`: 1 つの screen, family, producer route に閉じる exact leaf

迷ったら、より狭い home を選びます。`Popup` の exact leaf は narrow owner が明確なので scoped home を選べます。`AddPlayerMessage` は sink-observed umbrella なので fixed-leaf owner ではなく、fallback destination にもしません。

## Promotion rules

accepted fixed leaves は、少しずつ昇格します。1 回の変更で広げすぎないでください。

昇格とは、選んだ destination dictionary に安全な候補を追加することです。`Translator` の runtime semantics を変えることではありません。`Translator` は今も flat key-only の exact lookup のままです。

validation は重複や広すぎる追加を upstream で落とします。runtime 側で吸収しません。

最初の batch と同じく、safe survivor が 0 件なら **promotion しない** のが正解です。queue が pseudo-leaf noise か duplicate-sensitive existing coverage しか残していないなら、empty batch のまま report を残します。

duplicate family が既に narrow home で covered されている場合は、新規 import せず existing coverage として defer します。

昇格したあとも、同じ scanner コマンドを再実行して、`--validate-fixed-leaf` の report が通ることを確認します。

## Command flow

1. happy path で fresh inventory と validation report を出す
2. 既存 cache を使う再レビューでは `--phase 1d --validate-fixed-leaf` を使う
3. `docs/candidate-inventory.json` を review artifact として読み、pseudo-leaf noise → translated/excluded rows → 残りの survivors の順で絞る
4. 既存 seam にもう載っている stale bridge rows は fixed-leaf work から外す
5. 昇格があってもなくても、promoted / deferred / rejected を理由付きで記録する

## Non-goals

- `Translator` の runtime semantics を変えない
- route-aware runtime registration を作らない
- `AddPlayerMessage` を fixed-leaf owner や sink-side fallback にしない
- `message-frame`, builder/display-name, procedural, unresolved, `needs_runtime` を fixed-leaf 辞書へ押し込まない
- stale bridge bookkeeping を新しい fixed-leaf route work と言い換えない

## 何を残すか

accepted しなかった候補は、rejection reason を残したままにします。将来の再判定で理由が追えることが重要です。

candidate inventory は、実装メモではなく review artifact です。今の判断をそのまま読める形で残してください。
