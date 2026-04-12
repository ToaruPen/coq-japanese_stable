# Fixed-leaf Workflow

この文書は、issue-357 の Roslyn static SoT pilot で fixed-leaf candidate を集め、レビューし、検証し、昇格するための手順です。runtime proof や issue-358 の observability とは別です。

## 前提

- `~/dev/coq-decompiled_stable/` がローカルに存在していること
- それは tracing 用の decompiled source であり、外部の read-only input で、shipped artifact ではないこと
- `scripts/legacies/scan_text_producers.py` の `--validate-fixed-leaf` を使うこと。別の top-level validator はありません
- レビュー対象は `docs/candidate-inventory.json` と stdout の validation report です。`docs/candidate-inventory.json` は current static consumers 向けの bridge/view-only artifact で、source of truth ではありません

decompiled source は入力です。commit 対象ではありません。game binaries も同じ扱いです。

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

## Destination choice

destination dictionary は、validator が見る tier です。

- `global_flat`: repo-wide で共有できる exact leaf
- `scoped`: 1 つの screen, family, producer route に閉じる exact leaf

迷ったら、より狭い home を選びます。`Popup` と `AddPlayerMessage` は、現行 validator では scoped route として扱われます。

## Promotion rules

accepted fixed leaves は、少しずつ昇格します。1 回の変更で広げすぎないでください。

昇格とは、選んだ destination dictionary に安全な候補を追加することです。`Translator` の runtime semantics を変えることではありません。`Translator` は今も flat key-only の exact lookup のままです。

validation は重複や広すぎる追加を upstream で落とします。runtime 側で吸収しません。

昇格したあとも、同じ scanner コマンドを再実行して、`--validate-fixed-leaf` の report が通ることを確認します。

## 何を残すか

accepted しなかった候補は、rejection reason を残したままにします。将来の再判定で理由が追えることが重要です。

candidate inventory は、実装メモではなく review artifact です。今の判断をそのまま読める形で残してください。
