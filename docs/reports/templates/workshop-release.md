# Workshop Release vX.Y.Z

## Identity

- Version:
- Git commit:
- Git tag:
- Workshop item: https://steamcommunity.com/sharedfiles/filedetails/?id=3718988020
- Steam app ID: `333640`
- Published file ID: `3718988020`

## Changenote

```text
vX.Y.Z / <short-git-hash>

更新内容:
- 

翻訳追加・改善:
- 

修正:
- 

既知の問題:
- 一部の自動生成テキスト、チュートリアル、キャラクター生成画面には未翻訳または不自然な訳が残る場合があります。
- ゲーム本体のアップデートにより、一部表示やパッチが壊れる可能性があります。
```

## Artifacts

- Release ZIP:
- Release ZIP SHA256:
- Workshop content folder: `dist/workshop/QudJP/`
- Workshop VDF: `dist/workshop/workshop_item.vdf`

## Preflight

- [ ] `dotnet build Mods/QudJP/Assemblies/QudJP.csproj`
- [ ] `ruff check scripts/`
- [ ] `uv run pytest scripts/tests/test_build_release.py scripts/tests/test_build_workshop_upload.py scripts/tests/test_sync_mod.py scripts/tests/test_tokenize_corpus.py -q`
- [ ] `python3.12 scripts/check_encoding.py Mods/QudJP/Localization scripts`
- [ ] `python3.12 scripts/check_glossary_consistency.py Mods/QudJP/Localization`
- [ ] `python3.12 scripts/check_translation_tokens.py Mods/QudJP/Localization`
- [ ] `python3.12 scripts/validate_xml.py Mods/QudJP/Localization --strict --warning-baseline scripts/validate_xml_warning_baseline.json`
- [ ] `python3.12 scripts/build_release.py`
- [ ] Release ZIP spot-check
- [ ] `python3.12 scripts/build_workshop_upload.py --release-zip dist/QudJP-vX.Y.Z.zip --changenote-file /tmp/qudjp-workshop-changenote.txt`

## Upload

- steamcmd command:
- Upload completed at:
- Steam output summary:

## Post-Publish Smoke

- [ ] Workshop page title, description, preview image, visibility, file size, and changenote checked
- [ ] Subscribe/resubscribe checked
- [ ] Mod Manager lists QudJP with expected version and preview
- [ ] Options screen renders Japanese text and CJK glyphs
- [ ] One short conversation renders Japanese text and CJK glyphs
- [ ] Fresh Player.log checked for QudJP build markers, missing glyph warnings, compile errors, and `MODWARN`

## Decision

- Result: GO / NO-GO
- Notes:
- Rollback tag, if needed:
