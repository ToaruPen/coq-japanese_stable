# Caves of Qud Japanese Localization (QudJP) — Legacy

> **このリポジトリはアーカイブ済みです。**
> 開発は後継リポジトリに移行しました。

## 移行先

### [ToaruPen/CoQ-Japanese_v2](https://github.com/ToaruPen/CoQ-Japanese_v2)

v2 はベータ版ローカライゼーションパイプラインに対応したグリーンフィールド再設計です。

主な改善点:
- ベータ版の `_S/_T`・`GameText`・`ReplaceBuilder`・`[LanguageProvider]` パイプラインに最適化
- テスト駆動開発 (TDD): デコンパイルソースを忠実に再現する DummyTargets パターン
- 434+ L1 テスト、64+ Python テスト
- Harmony パッチは最終手段 — ベータネイティブ拡張ポイントを優先

---

## このリポジトリについて

v1 は Caves of Qud の安定版 (v2.0.4) を対象とした日本語化 Mod でした。
ベータ版でのローカライゼーション API の大幅な変更に伴い、v2 で再設計を行いました。

v1 のコード・アセット・テストパターンは v2 の設計時に参照資料として使用されましたが、
直接のポートではありません。

---

## License

TBD
