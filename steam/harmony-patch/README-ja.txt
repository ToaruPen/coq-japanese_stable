QudJP Harmony 2.4.2 更新パッチ（Windows）
==========================================

対象
----

このパッケージは Caves of Qud 1.0.5 の Windows 版専用です。
QudJP 本体とは別配布の任意導入パッチであり、展開しただけではゲームを変更しません。
Harmony 2.4.2 への更新を希望する場合にだけ、同梱の
「Install Harmony 2.4.2.cmd」を明示的に実行してください。

導入前の注意
------------

1. Caves of Qud を完全に終了してください。起動中は導入しないでください。
2. インストーラーはゲーム本体の 0Harmony.dll の SHA-256 を検証し、
   Caves of Qud 1.0.5 で確認済みのファイルだけを対象にします。
3. 置換前の DLL は同じフォルダーに
   0Harmony.dll.qudjp-backup-before-2.4.2 としてバックアップされます。
   既存のバックアップを上書きする用途ではありません。
4. 同梱 payload/net48/0Harmony.dll の SHA-256 も検証されます。
   SHA256SUMS.txt と一致しない場合は使用を中止し、QudJP v0.5.02 の GitHub Release
   から更新パッチ ZIP を再取得してください。
5. このスクリプトは、展開先にあるファイルを自動的に管理者権限で再実行しません。
   ゲームフォルダーへ書き込めないというエラーが出た場合は、この画面を閉じ、
   エクスプローラーで「Install Harmony 2.4.2.cmd」を右クリックして
   「管理者として実行」を選んでください。

元に戻す方法
------------

Caves of Qud を完全に終了してから「Restore Game Harmony.cmd」を実行してください。
保存済みバックアップの SHA-256 と、現在の DLL がこのパッケージの Harmony 2.4.2
であることを検証したうえで復元します。バックアップは削除しないでください。
書き込み権限のエラーが出た場合は、この画面を閉じ、エクスプローラーで
「Restore Game Harmony.cmd」を右クリックして「管理者として実行」を選んでください。

Steam の更新・整合性確認
------------------------

Steam のゲーム更新、ファイルの整合性確認、再インストールによって、ゲーム本体の
0Harmony.dll が元に戻ることがあります。ゲーム更新後は互換性が再確認されるまで
このパッチを再適用しないでください。対象外のゲーム版やハッシュ不明の DLL には
使用できません。

このパッチは Harmony パッチ適用開始時の待ち時間への対処を目的としています。
ゲーム全般の FPS 向上や CPU 使用率低下を保証・主張するものではありません。

配布元とソース
--------------

Harmony 公式ソース:
https://github.com/pardeike/Harmony

Harmony 2.4.2 公式リリース:
https://github.com/pardeike/Harmony/releases/tag/v2.4.2.0

QudJP ソース:
https://github.com/ToaruPen/coq-japanese_stable

ライセンスは LICENSE-QudJP.txt、LICENSE-Harmony.txt、
THIRD-PARTY-NOTICES.txt を参照してください。
