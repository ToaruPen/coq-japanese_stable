from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

# ruff: noqa: E501, RUF001

REPO_ROOT = Path(__file__).resolve().parents[2]
ITEMS_XML = REPO_ROOT / "Mods" / "QudJP" / "Localization" / "ObjectBlueprints" / "Items.jp.xml"

EXPECTED_BEHAVIOR_DESCRIPTIONS = {
    "BiologicalIndexer": "生物クリーチャーの正確なHP・AV・DVを参照できる。",
    "TechnologicalIndexer": "ロボットの正確なHP・AV・DVを参照し、遺物を自動識別する。",
    "ForceModulator": "力場の壁を通り抜けられる。",
    "CherubicVisage": "+1 自我",
    "ElectromagneticSensor": "視認していなくても近くのロボットを探知できる。",
    "NavigationSystem": (
        "廃墟で迷子になる確率を30%軽減する。\n"
        "その他の地形では迷子になる確率を10%軽減する。\n"
        "これらの効果はコンパスブレスレットとは重複しない。"
    ),
    "TibularHydrojets": "泳いでいる間、移動速度 +200",
    "SkinGlitter1": "光属性攻撃を屈折させる確率 +8%",
    "SingleSkillsoft1": "コスト 0～50 SP のスキルを1つ習得する。",
    "DermalInsulation": "+6 熱耐性\n+6 冷気耐性\n+6 電撃耐性\n+6 酸耐性",
    "NightVision": "暗視能力を付与する。",
    "HyperElasticAnkleTendons": "+6 移動速度",
    "ParabolicMuscularSubroutine": "投擲距離が2マス伸びる。\n投擲距離内の目標へ投げると、命中率が100%になる。",
    "TranslucentSkin": "+2 DV",
    "StabilizerArmLocks": "遠隔攻撃の命中判定では、敏捷が6高いものとして扱われる。",
    "RapidReleaseFingerFlexors": "拳銃を25%速く発射できる。",
    "CarbideHandBones": "拳のダメージが2d3になる。",
    "Pentaceps": "突撃の射程が4マス伸びる。",
    "CommunicationsInterlock": "ロボットを5レベル高いものとして叱責する。",
    "InflatableAxons": "発動。クールダウン100。\n10ラウンドのあいだ俊敏 +40、その後10ラウンド鈍重になる（俊敏 -10）。",
    "NocturnalApex": "昼間の自然回復率 +10%。\n夜ごとに1回《徘徊》でき、100ラウンドのあいだ敏捷 +6、移動速度 +10。",
    "AirCurrentMicrosensor": "階段などの上下マップ遷移が常に明らかになる。",
    "PneumaticPistons": "ジャンプ距離が4マス伸びる。",
    "ReactiveCranialPlating": "運動エネルギーによる朦朧・気絶を受けない。",
    "PhaseHarmonicModulator": "同位相・異位相どちらの物体やクリーチャーとも干渉できる。",
    "FireSuppressionSystem": "炎上している間、毎ターン自動的に難燃ジェルを放出する。",
    "AnchorSpikes": "物理的な手段で強制移動させられない。",
    "SkinGlitter2": "光属性攻撃を屈折させる確率 +16%",
    "SingleSkillsoft2": "コスト 51～150 SP のスキルを1つ習得する。",
    "Schemasoft2": "単一の遺物分類の全設計図にアクセスできる。",
    "GroundingShunts": "+50 電撃耐性",
    "DermalPlating": "+1 AV",
    "TransparentSkin": "+3 DV",
    "BionicHands": "+2 敏捷",
    "BionicArm": "+2 筋力",
    "UltraElasticAnkleTendons": "+10 移動速度",
    "BeautifulVisage": "+2 自我",
    "DopamineSynth": "+2 意志力",
    "BionicHeart": "+2 頑健",
    "GiantHands": "両手武器を片手で扱える。",
    "MedassistModule": (
        "インジェクターの使用アクションコストを80%軽減する。\n"
        "最大8本のインジェクターを装填でき、メドアシストモジュールAIが適切と判断したときに（AIの裁量で）アクションコストなしに自動使用する。"
    ),
    "PalladiumElectrodeposits": "局所格子に20ユニットの演算力を供給する。\n頭部に埋め込むと知力 +2。",
    "SecurityInterlock": "どんな扉でも解錠できる。",
    "IntravenousPort": "トニックの効果時間が2倍になる。",
    "HighGradeDermalInsulation": "+9 熱耐性\n+9 冷気耐性\n+9 電撃耐性\n+9 酸耐性",
    "MatterRecompositer": "発動。クールダウン100。\nマップ上の探索済みランダム地点へテレポートする。",
    "CustomVisage": "インストール時に選んだ派閥との評判 +300",
    "EquipmentRack": "背中装備スロットが1つ増える。",
    "BionicLiver": "毒と病気に対して免疫を得る。",
    "AnomalyFumigator": "毎ターン、通常性ガスの雲を放出する。",
    "SingleSkillsoft3": "コストが151 SPより高いスキルを1つ習得する。",
    "Schemasoft3": "単一の遺物分類の全設計図にアクセスできる。",
    "FulleriteHandBones": "拳のダメージが2d4+1になる。",
    "MotorizedTreads": "+150 移動速度\n強制移動・転倒・拘束に対するセーヴ +4\n靴を装備できない。\nこのインプラントは取り外せない。",
    "ReactiveTraumaPlate": "致死量のダメージ、または最大HPの50%以上のダメージを与えるダメージ源を1回無効化する。このインプラントはその過程で破壊される。",
    "GunRack": "左右の遠隔武器スロットが1つずつ増える。\n背中に装備を着用できない。",
    "OnboardRecoiler": "このインプラントに場所を刻印（クールダウン100）し、その場所へリコイル（クールダウン50）できる。",
    "MagneticCore": "浮遊物装備スロットが1つ増える。",
    "Phase-Adaptive Scope": "遠隔武器を発射するか物を投げると、その投射物は位相をずらして障害物を通り抜け、目標に到達する。",
    "Schemasoft4": "単一の遺物分類の全設計図にアクセスできる。",
    "PrecisionForceLathe": (
        "空いている手持ちまたは投擲武器スロットにフォースナイフを生成する。\n"
        "フォースナイフを投げると、別のフォースナイフが即座に投擲武器スロットに実体化する。\n"
        "フォースナイフは1d8ダメージを与え、貫通値は対象のAVと等しく、投擲距離 +6。\n"
        "フォースナイフはあなたとの接触を失うか装備から外れると消える。\n"
        "フォース・モジュレーターを埋め込んでいる場合、フォースナイフを力場越しに投げられる。"
    ),
    "GraftedMirrorArm": "投擲武器スロットが1つ増える。\n投擲を行うと、すべての投擲武器スロットから同時に投げる。",
    "MicromanipulatorArray": (
        "戦闘中でも工作と修理を1ターンのアクションコストで行える。\n"
        "その他の工作スキルのアクションコストを40%軽減する。\n"
        "遺物識別時、知力が8高いものとして扱われる。\n"
        "遺物の解体でビットを回収する確率 +10%。"
    ),
    "PenetratingRadar": "半径10マス以内を完全に視認できる。ただし力場と時空異常には遮られる。",
    "BiodynamicPowerPlant": "ジャック改造を持つアイテムなど、オンボード電源システムを利用できる装備品に、毎ターン5000ユニットのチャージを継続供給する。",
    "StasisProjector": "発動。クールダウン100。\n約5ラウンド持続する隣接したステイシス場を最大6マスまで生成する。",
    "HolographicVisage": "インストール時に選んだ派閥との評判 +200\n発動すると選択した派閥を変更できる。",
    "OpticalMultiscanner": (
        "ロボット・生物・構造物の正確なHP・AV・DVを参照できる。\n"
        "遺物を自動識別する。\n"
        "階段などの上下マップ遷移が常に明らかになる。"
    ),
    "SocialCoprocessor": (
        "新しいクリーチャーと水儀を行うたび、評判を追加で25得る。初めての水儀後にこのインプラントを埋め込んだ場合、その相手と次に水儀を行うと評判を25得る。\n"
        "水儀での評判コストが20%減少する。\n"
        "《布教》できるクリーチャーが1体増える。\n"
        "ローカルラティス上の演算力が高いほど効果が増す。"
    ),
    "StasisEntangler": "発動。クールダウン200。\n視線内のクリーチャー1体を選ぶ。他の全クリーチャーを15ラウンドのステイシス場に入れる。",
    "CrysteelHandBones": "拳のダメージが3d4になる。",
    "HighFidelityMatterRecompositer": "発動。クールダウン50。\nマップ上の探索済み任意の場所へテレポートする。",
    "TreeSkillsoft": "スキルツリー全体にアクセスできる。",
    "BaseCathedra": (
        "+100 移動速度\n+50 HP\n+100 所持重量\n+3 自我\n飛行能力を得る。\n\n"
        "発動（CT 100）: 宝石ごとの効果を引き起こす。\n\n"
        "背中に装備を着用できない。\nこのインプラントは取り外せない。"
    ),
    "CathedraSapphire": (
        "+100 移動速度\n+50 HP\n+100 所持重量\n+3 自我\n飛行能力を得る。\n\n"
        "発動（CT 100）: 周囲に衝撃波を放ち、敵を吹き飛ばして気絶させる。\n\n"
        "背中に装備を着用できない。\nこのインプラントは取り外せない。"
    ),
    "CathedraRuby": (
        "+100 移動速度\n+50 HP\n+100 所持重量\n+3 自我\n飛行能力を得る。\n\n"
        "発動（CT 100）: 自身を中心に熱念動フィールドを展開する。\n\n"
        "背中に装備を着用できない。\nこのインプラントは取り外せない。"
    ),
    "CathedraWhiteOpal": (
        "+100 移動速度\n+50 HP\n+100 所持重量\n+3 自我\n飛行能力を得る。\n\n"
        "発動（CT 100）: 周囲に煌めく粉塵を撒き散らす。\n\n"
        "背中に装備を着用できない。\nこのインプラントは取り外せない。"
    ),
    "CathedraBlackOpal": (
        "+100 移動速度\n+50 HP\n+100 所持重量\n+3 自我\n飛行能力を得る。\n\n"
        "発動（CT 100）: 時空を裂き、クァドのランダムな場所へ跳躍する。\n\n"
        "背中に装備を着用できない。\nこのインプラントは取り外せない。"
    ),
}


def test_cybernetics_behavior_descriptions_match_game_1_0_4_audit() -> None:
    """Cybernetics BehaviorDescription text follows the audited 1.0.4 game data."""
    document = ET.parse(ITEMS_XML)  # noqa: S314 -- local repository XML
    actual: dict[str, str] = {}

    for obj in document.getroot().findall("object"):
        name = obj.get("Name")
        if name is None:
            continue
        for part in obj.findall("part"):
            if part.get("Name") == "CyberneticsBaseItem" and part.get("BehaviorDescription") is not None:
                actual[name] = part.get("BehaviorDescription", "")
                break

    missing = sorted(set(EXPECTED_BEHAVIOR_DESCRIPTIONS) - set(actual))
    unexpected = sorted(set(actual) - set(EXPECTED_BEHAVIOR_DESCRIPTIONS))
    mismatched = {
        name: (actual[name], expected)
        for name, expected in EXPECTED_BEHAVIOR_DESCRIPTIONS.items()
        if name in actual and actual[name] != expected
    }

    assert missing == []
    assert unexpected == []
    assert mismatched == {}
