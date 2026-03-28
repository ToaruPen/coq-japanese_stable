# Inventory Verification Runbook

`scripts/verify_inventory.py` を使って、Rosetta 起動した Caves of Qud の既知セーブを読み込み、
インベントリ/装備画面の表示確認を自動化するための手順です。

---

## 目的

- インベントリ表示の日本語化確認を毎回同じ手順で再現する
- `Player.log` の probe と実画面スクリーンショットを同時に証跡として残す
- 修正作業の前後で inventory rendering の回帰確認を素早く行う

---

## 前提条件

- macOS
- Apple Silicon では Rosetta 2 が入っていること
- 画面がロックされていないこと
- 通常の `screencapture` が黒画面を返さないこと
- `scripts/sync_mod.py` で配備できる状態であること
- `Continue` -> `LOAD GAME` の先頭項目から検証用セーブを開けること
- Hammerspoon がインストール済みであること

> `verify_inventory.py` は Hammerspoon を前面化補助として使いますが、
> `~/.hammerspoon/init.lua` は変更しません。実行時に Console へ一時 Lua を流すだけです。

---

## 実行コマンド

### 既に配備済みの場合

```bash
python3 scripts/verify_inventory.py \
  --skip-sync \
  --screenshot-path artifacts/verify_inventory/verified-inventory.png
```

### 配備込みで実行する場合

```bash
python3 scripts/verify_inventory.py \
  --screenshot-path artifacts/verify_inventory/verified-inventory.png
```

---

## 既定フロー

1. ロック解除済みセッションか確認する
2. 必要なら `scripts/sync_mod.py` で mod を配備する
3. `scripts/launch_rosetta.sh` で CoQ を Rosetta 起動する
4. Hammerspoon で CoQ を前面化する
5. タイトル画面で `Continue` へ 1 つ下移動する
6. `space` を 2 回送って `LOAD GAME` の先頭セーブを読む
7. `Player.log` で world-ready probe を待つ
8. `i` を送って inventory / equipment 画面を開く
9. スクリーンショットを保存する
10. ゲームを終了する

---

## 成功判定

標準出力に次の JSON が出れば成功です。

```json
{
  "build_marker_found": true,
  "load_ready_found": true,
  "inventory_probe_found": true
}
```

実際の成功 run では、`inventory_probe_matches` に少なくとも次のいずれかが入ります。

- `[QudJP] InventoryLineReplacementStateNextFrame/v1:`
- `[QudJP] EquipmentLineProbe/v1:`

成功 run の実例:

```json
{
  "build_marker_found": true,
  "load_ready_found": true,
  "load_ready_matches": [
    "[QudJP] Translator:<non-main-menu>"
  ],
  "inventory_probe_found": true,
  "inventory_probe_matches": [
    "[QudJP] InventoryLineReplacementStateNextFrame/v1:",
    "[QudJP] EquipmentLineProbe/v1:"
  ],
  "screenshot_path": "artifacts/verify_inventory/operational-fullpath.png"
}
```

---

## 保存先

運用上は `artifacts/verify_inventory/` に保存しておくのが分かりやすいです。

例:

- `artifacts/verify_inventory/verified-inventory.png`
- `artifacts/verify_inventory/operational-fullpath.png`

---

## 失敗時の見方

### `build_marker_found` が `false`

- Rosetta 起動に失敗している
- `Player.log` に QudJP の bootstrap 痕跡が出ていない

確認先:

- `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- `scripts/launch_rosetta.sh`

### `load_ready_found` が `false`

- タイトル操作が通っていない
- save 読み込み前にフォーカスが外れている
- 画面が lock / sleep 状態に戻っている

### `inventory_probe_found` が `false`

- world には入れたが inventory を開けていない
- inventory 表示 probe まで到達していない

### スクリーンショットが黒い

- macOS が lock / sleep 状態
- 実行時の screen capture 前提が崩れている

次を先に確認してください。

```bash
python3 - <<'PY'
import plistlib, subprocess
root = plistlib.loads(subprocess.run(['/usr/sbin/ioreg', '-a', '-n', 'Root'], capture_output=True, check=True).stdout)
print(root.get('IOConsoleLocked'))
PY
```

`True` なら、ロック解除後に再実行してください。

---

## 補足

- Hammerspoon は前面化補助としてのみ使います
- 既存の `~/.hammerspoon/init.lua` や既存ホットキー設定は変更しません
- 自動化は unlocked なローカル GUI セッション前提です
- CI 向きではなく、L3 のローカル回帰確認用です
