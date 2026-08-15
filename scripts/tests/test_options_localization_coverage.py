from __future__ import annotations

from pathlib import Path
from xml.etree import ElementTree as ET

REPO_ROOT = Path(__file__).resolve().parents[2]
OPTIONS_PATH = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "Options.jp.xml"

EXPECTED_OPTIONS = {
    "OptionDisableAllIdleTileAnimations": ("タイルの待機アニメーションをすべて無効化", "アクセシビリティ"),
    "OptionTextBuilderDebug": ("テキストビルダーのリーク解析を有効化", "Mod"),
    "OptionDrawStepImmediately": ("移動直後にフレームを描画（低CPU環境向け）", "パフォーマンス"),  # noqa: RUF001
    "OptionEnableSeed": ("ワールドシードを有効化（実験的）", "デバッグ"),  # noqa: RUF001
    "OptionDisableZoneThawRestrict": ("ゾーン解凍の制限を無効化", "デバッグ"),
    "OptionDrawFloodExplore": ("自動探索のフラッドフィルを描画", "デバッグ"),
}

EXPECTED_HELP = {
    "OptionTextBuilderDebug": (
        "多くのテキストビルダーは、プールされたリソースを正しく解放するために、"
        "通常はusing文またはusing宣言で破棄する必要があります。 "
        "テキストビルダーの破棄を忘れるとエラーが出力されます。"
        "このオプションを有効にすると、そのエラーに破棄漏れの位置を示すスタックトレースが含まれます。"
    ),
    "OptionDisableZoneThawRestrict": (
        "現在のゲーム内での状態にかかわらず、データベースからゾーンの解凍を試みます。 "
        "たとえば、予知を巻き戻した後でも、その予知中に訪れたゾーンを解凍できます。"
    ),
}

EXPECTED_RUNTIME_ATTRIBUTES = {
    "OptionDisableAllIdleTileAnimations": {
        "Requires": None,
        "RequiresCapability": None,
        "Type": "Checkbox",
        "Default": "No",
    },
    "OptionTextBuilderDebug": {
        "Requires": "OptionEnableMods==Yes,OptionAllowCSMods==Yes,OptionShowAdvancedOptions==Yes",
        "RequiresCapability": None,
        "Type": "Checkbox",
        "Default": "No",
    },
    "OptionDrawStepImmediately": {
        "Requires": "OptionShowAdvancedOptions==Yes",
        "RequiresCapability": None,
        "Type": "Checkbox",
        "Default": "Yes",
    },
    "OptionEnableSeed": {
        "Requires": "OptionShowAdvancedOptions==Yes",
        "RequiresCapability": None,
        "Type": "Checkbox",
        "Default": "No",
    },
    "OptionDisableZoneThawRestrict": {
        "Requires": "OptionShowAdvancedOptions==Yes",
        "RequiresCapability": None,
        "Type": "Checkbox",
        "Default": "Yes",
    },
    "OptionDrawFloodExplore": {
        "Requires": "OptionShowAdvancedOptions==Yes",
        "RequiresCapability": "IsDesktop",
        "Type": "Checkbox",
        "Default": "No",
    },
}


def _normalized_text(element: ET.Element) -> str:
    return " ".join("".join(element.itertext()).split())


def test_current_1_0_5_options_have_reviewed_japanese_rows() -> None:
    """Every reviewed option added or changed in 1.0.5 must have its Japanese row."""
    root = ET.parse(OPTIONS_PATH).getroot()  # noqa: S314 - repository-owned localization fixture
    option_rows = root.findall("option")
    by_id = {option.get("ID"): option for option in option_rows}

    for option_id, (display_text, category) in EXPECTED_OPTIONS.items():
        assert sum(option.get("ID") == option_id for option in option_rows) == 1
        option = by_id.get(option_id)
        assert option is not None, f"missing 1.0.5 option localization: {option_id}"
        assert option.get("DisplayText") == display_text
        assert option.get("Category") == category
        assert {
            attribute: option.get(attribute)
            for attribute in ("Requires", "RequiresCapability", "Type", "Default")
        } == EXPECTED_RUNTIME_ATTRIBUTES[option_id]

    for option_id, expected_help in EXPECTED_HELP.items():
        helptext = by_id[option_id].find("helptext")
        assert helptext is not None, f"missing 1.0.5 option help text: {option_id}"
        assert _normalized_text(helptext) == expected_help
