# Workshop Release vX.Y.Z

## Identity

- Version:
- Git commit:
- Git tag:
- Previous release tag/range:
- Version source:
- GitHub Release URL, if created:
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

- [ ] `git status --short --branch`
- [ ] Release range established from previous tag, release report, changelog/GitHub release, or explicit user range
- [ ] `Mods/QudJP/manifest.json` version, `vX.Y.Z` tag, release ZIP name, changenote first line, and report version match
- [ ] `git rev-list -n1 vX.Y.Z` matches `git rev-parse HEAD`
- [ ] `just workshop-preflight X.Y.Z`
- [ ] `just release-zip-check dist/QudJP-vX.Y.Z.zip`
- [ ] `just build-workshop-upload dist/QudJP-vX.Y.Z.zip /tmp/qudjp-workshop-changenote.txt`

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
