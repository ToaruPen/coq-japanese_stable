"""Tests for the scanner.rule_classifier module."""

from __future__ import annotations

import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from textwrap import dedent

import pytest  # pyright: ignore[reportMissingImports]

from scripts.legacies.scanner.inventory import (
    Confidence,
    DestinationDictionary,
    FixedLeafRejectionReason,
    HitKind,
    OwnershipClass,
    RawHit,
    SiteType,
    read_inventory_draft_json,
    write_raw_hits_jsonl,
)
from scripts.legacies.scanner.rule_classifier import _rejection_reason, classify_raw_hit, classify_raw_hits_file

REPO_ROOT = Path(__file__).resolve().parents[2]


@dataclass(frozen=True, slots=True)
class Case:
    """One classifier test case backed by a source snippet."""

    id: str
    family: str
    matched_code: str
    source_text: str


def _source(*lines: str) -> str:
    """Wrap a few copied decompiled lines in a minimal compilable-ish C# shell."""
    body = "\n".join(f"        {line}" for line in lines)
    return dedent(
        f"""
        using System.Text;

        namespace Example;

        public sealed class Demo
        {{
            public void Run()
            {{
        {body}
            }}
        }}
        """
    ).lstrip()


def _make_raw_hit(tmp_path: Path, case: Case) -> tuple[Path, RawHit]:
    """Write the source file for a case and build the matching RawHit."""
    file_path = tmp_path / f"{case.id}.cs"
    file_path.parent.mkdir(parents=True, exist_ok=True)
    file_path.write_text(case.source_text, encoding="utf-8")
    lines = case.source_text.splitlines()
    for index, line in enumerate(lines, start=1):
        column = line.find(case.matched_code)
        if column >= 0:
            return (
                tmp_path,
                RawHit(
                    hit_kind=HitKind.SINK,
                    family=case.family,
                    pattern=f"{case.family}-pattern",
                    file=file_path.relative_to(tmp_path).as_posix(),
                    line=index,
                    column=column + 1,
                    matched_code=case.matched_code,
                ),
            )
    msg = f"Matched code not found for case {case.id!r}."
    raise AssertionError(msg)


LEAF_CASES = [
    Case(
        id="leaf-flight",
        family="AddPlayerMessage",
        matched_code='MessageQueue.AddPlayerMessage("You begin flying!")',
        source_text=_source('MessageQueue.AddPlayerMessage("You begin flying!");'),
    ),
    Case(
        id="leaf-precognition",
        family="Popup",
        matched_code='Popup.Show("You cannot access someone else\'s precognitive vision.")',
        source_text=_source('Popup.Show("You cannot access someone else\'s precognitive vision.");'),
    ),
    Case(
        id="leaf-frozen",
        family="Popup",
        matched_code='Popup.ShowFail("You are frozen solid!")',
        source_text=_source('Popup.ShowFail("You are frozen solid!");'),
    ),
]

TEMPLATE_CASES = [
    Case(
        id="template-character-status",
        family="SetText",
        matched_code='levelText.SetText(string.Format("Level: {0} \\u00af HP: {1}/{2} \\u00af XP: {3}/{4} \\u00af Weight: {5}#", GO.Level, GO.Stat("Hitpoints"), GO.GetStat("Hitpoints").BaseValue, GO.Stat("XP"), Leveler.GetXPForLevel(GO.Stat("Level") + 1), GO.Weight))',
        source_text=_source(
            'levelText.SetText(string.Format("Level: {0} \\u00af HP: {1}/{2} \\u00af XP: {3}/{4} \\u00af Weight: {5}#", GO.Level, GO.Stat("Hitpoints"), GO.GetStat("Hitpoints").BaseValue, GO.Stat("XP"), Leveler.GetXPForLevel(GO.Stat("Level") + 1), GO.Weight));'
        ),
    ),
    Case(
        id="template-combat-block",
        family="AddPlayerMessage",
        matched_code="IComponent<GameObject>.AddPlayerMessage($\"You block with {arg}! (+{part.AV} AV)\", 'g')",
        source_text=_source(
            "IComponent<GameObject>.AddPlayerMessage($\"You block with {arg}! (+{part.AV} AV)\", 'g');"
        ),
    ),
    Case(
        id="template-exodus",
        family="EmitMessage",
        matched_code="EmitMessage($\"Exodus launch in {Timer}...\", ' ', FromDialog: false, UsePopup: false, AlwaysVisible: true)",
        source_text=_source(
            "EmitMessage($\"Exodus launch in {Timer}...\", ' ', FromDialog: false, UsePopup: false, AlwaysVisible: true);"
        ),
    ),
]

BUILDER_CASES = [
    Case(
        id="builder-status-screen",
        family="SetText",
        matched_code='mutationNameText.SetText("{{B|" + characterMutationLineData.mutation.GetDisplayName() + "}}")',
        source_text=_source(
            'mutationNameText.SetText("{{B|" + characterMutationLineData.mutation.GetDisplayName() + "}}");'
        ),
    ),
    Case(
        id="builder-journal-line",
        family="SetText",
        matched_code="headerText.SetText(journalRecipeNote.Recipe.GetDisplayName())",
        source_text=_source("headerText.SetText(journalRecipeNote.Recipe.GetDisplayName());"),
    ),
    Case(
        id="builder-mutation-b-gone",
        family="Popup",
        matched_code='Popup.Show("Om nom nom! " + mutation.GetDisplayName() + " is gone! {{w|*belch*}}")',
        source_text=_source('Popup.Show("Om nom nom! " + mutation.GetDisplayName() + " is gone! {{w|*belch*}}");'),
    ),
]

MESSAGE_FRAME_CASES = [
    Case(
        id="message-frame-tier1",
        family="DidX",
        matched_code='ParentObject.DidX("charge")',
        source_text=_source('ParentObject.DidX("charge");'),
    ),
    Case(
        id="message-frame-tier2",
        family="DidX",
        matched_code='gameObject.Physics.DidX("lock", "in place")',
        source_text=_source('gameObject.Physics.DidX("lock", "in place");'),
    ),
    Case(
        id="message-frame-tier3",
        family="DidX",
        matched_code='IComponent<GameObject>.XDidY(mutation.ParentObject, "emit", "a freezing ray" + ((registeredSlot != null) ? (" from " + mutation.ParentObject.its + " " + registeredSlot.GetOrdinalName()) : ""), "!", null, null, mutation.ParentObject)',
        source_text=_source(
            'IComponent<GameObject>.XDidY(mutation.ParentObject, "emit", "a freezing ray" + ((registeredSlot != null) ? (" from " + mutation.ParentObject.its + " " + registeredSlot.GetOrdinalName()) : ""), "!", null, null, mutation.ParentObject);'
        ),
    ),
]

VERB_COMPOSITION_CASES = [
    Case(
        id="verb-flight",
        family="Does",
        matched_code='Object.Does("begin")',
        source_text=_source('MessageQueue.AddPlayerMessage(Object.Does("begin") + " flying.");'),
    ),
    Case(
        id="verb-combat",
        family="Does",
        matched_code='Attacker.Does("miss")',
        source_text=_source('IComponent<GameObject>.AddPlayerMessage(Attacker.Does("miss") + " you!");'),
    ),
    Case(
        id="verb-carapace",
        family="Does",
        matched_code='CarapaceObject.Does("loosen")',
        source_text=_source(
            'Popup.Show(CarapaceObject.Does("loosen") + ". Your AV decreases by {{R|" + ACModifier + "}}.");'
        ),
    ),
]

VERB_FALSE_POSITIVE_CASES = [
    Case(
        id="verb-false-gametext",
        family="Does",
        matched_code='who.Does("were", int.MaxValue, null, null, null, AsIfKnown: false, Single: false, NoConfusion: false, NoColor: false, Stripped: false, WithoutTitles: true, Short: true, BaseOnly: false, WithIndefiniteArticle: false, null, IndicateHidden: false, Pronoun: true)',
        source_text=_source(
            'text = ((!text.StartsWith("You")) ? (who.Does("were", int.MaxValue, null, null, null, AsIfKnown: false, Single: false, NoConfusion: false, NoColor: false, Stripped: false, WithoutTitles: true, Short: true, BaseOnly: false, WithIndefiniteArticle: false, null, IndicateHidden: false, Pronoun: true) + " " + Grammar.InitLower(text)) : text.Replace("You", who.It));'
        ),
    ),
    Case(
        id="verb-false-domination",
        family="Does",
        matched_code='Target.Does("do")',
        source_text=_source(
            'FailureMessage = Target.Does("do") + " not have a consciousness you can make psychic contact with.";'
        ),
    ),
    Case(
        id="verb-false-cybernetics",
        family="Does",
        matched_code='base.Terminal.Subject.Does("are", int.MaxValue, null, null, null, AsIfKnown: false, Single: false, NoConfusion: false, NoColor: false, Stripped: false, WithoutTitles: true, Short: true, BaseOnly: false, WithIndefiniteArticle: false, null, IndicateHidden: false, Pronoun: false, SecondPerson: true, null, AsPossessed: true, null, Reference: true)',
        source_text=_source(
            'MainText = "Welcome, Aristocrat, to a becoming nook. " + base.Terminal.Subject.Does("are", int.MaxValue, null, null, null, AsIfKnown: false, Single: false, NoConfusion: false, NoColor: false, Stripped: false, WithoutTitles: true, Short: true, BaseOnly: false, WithIndefiniteArticle: false, null, IndicateHidden: false, Pronoun: false, SecondPerson: true, null, AsPossessed: true, null, Reference: true) + " one step closer to the Grand Unification. Please choose from the following options.";'
        ),
    ),
]

VARIABLE_TEMPLATE_CASES = [
    Case(
        id="variable-template-photosynthetic",
        family="ReplaceBuilder",
        matched_code='"=subject.T= =verb:bask= in the sunlight and =verb:absorb= the nourishing rays.".StartReplace()',
        source_text=_source(
            '"=subject.T= =verb:bask= in the sunlight and =verb:absorb= the nourishing rays.".StartReplace().AddObject(ParentObject).EmitMessage(ParentObject);'
        ),
    ),
    Case(
        id="variable-template-gametext",
        family="ReplaceBuilder",
        matched_code="Message.StartReplace()",
        source_text=_source(
            "return Message.StartReplace().AddArgument(Subject).AddArgument(Object).StripColors(StripColors).ToString();"
        ),
    ),
    Case(
        id="variable-template-village-coda",
        family="ReplaceBuilder",
        matched_code="text.StartReplace()",
        source_text=_source(
            'string text = "=sultan.term= blessed =village.name= in =village.region=.";',
            'text.StartReplace().AddObject(System.Sultan).AddReplacer("village.name", villageName).AddReplacer("village.region", regionName).ToString();',
        ),
    ),
]

PROCEDURAL_TEXT_CASES = [
    Case(
        id="procedural-village-warden",
        family="HistoricStringExpander",
        matched_code='HistoricStringExpander.ExpandString("<spice.villages.warden.introDialog.!random>")',
        source_text=_source(
            'string text = HistoricStringExpander.ExpandString("<spice.villages.warden.introDialog.!random>");'
        ),
    ),
    Case(
        id="procedural-item-naming",
        family="HistoricStringExpander",
        matched_code='HistoricStringExpander.ExpandString("<spice.elements." + Element + ".adjectives.!random> " + HistoricStringExpander.ExpandString("<spice.itemTypes." + Type + ".!random>") + " of " + text)',
        source_text=_source(
            'string displayName = HistoricStringExpander.ExpandString("<spice.elements." + Element + ".adjectives.!random> " + HistoricStringExpander.ExpandString("<spice.itemTypes." + Type + ".!random>") + " of " + text);'
        ),
    ),
    Case(
        id="procedural-village-coda-base",
        family="HistoricStringExpander",
        matched_code='HistoricStringExpander.ExpandString("<spice.instancesOf.tar.!random>")',
        source_text=_source(
            'string description = string.Format("Walling of {0}.", HistoricStringExpander.ExpandString("<spice.instancesOf.tar.!random>"));'
        ),
    ),
]

NARRATIVE_TEMPLATE_CASES = [
    Case(
        id="narrative-cloning",
        family="JournalAPI",
        matched_code='JournalAPI.AddAccomplishment("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", you multiplied.", "In the month of " + Calendar.GetMonth() + " of " + Calendar.GetYear() + " AR, =name= immaculately birthed " + The.Player.GetPronounProvider().Reflexive + ".", "In =year=, while traveling near " + JournalAPI.GetLandmarkNearestPlayer().Text + ", =name= created a simulacrum of " + The.Player.GetPronounProvider().Reflexive + " for the purpose of faking chariot accidents.", null, "general", MuralCategory.WeirdThingHappens, MuralWeight.High, null, -1L)',
        source_text=_source(
            'JournalAPI.AddAccomplishment("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", you multiplied.", "In the month of " + Calendar.GetMonth() + " of " + Calendar.GetYear() + " AR, =name= immaculately birthed " + The.Player.GetPronounProvider().Reflexive + ".", "In =year=, while traveling near " + JournalAPI.GetLandmarkNearestPlayer().Text + ", =name= created a simulacrum of " + The.Player.GetPronounProvider().Reflexive + " for the purpose of faking chariot accidents.", null, "general", MuralCategory.WeirdThingHappens, MuralWeight.High, null, -1L);'
        ),
    ),
    Case(
        id="narrative-opening-story",
        family="JournalAPI",
        matched_code='JournalAPI.AddAccomplishment("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", you arrived at " + VillageName + ".", "On the auspicious " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", =name= arrived in " + VillageName + " and began " + The.Player.GetPronounProvider().PossessiveAdjective + " prodigious odyssey through Qud.", "At <spice.time.partsOfDay.!random> under <spice.commonPhrases.strange.!random.article> and " + text2 + " sky, the people of " + VillageName + " saw an image on the horizon that looked like a " + text + " bathed in " + text2 + ". It was =name=, and after " + The.Player.GetPronounProvider().Subjective + " came and left, the people of " + VillageName + " built a monument to =name= and thenceforth called " + The.Player.GetPronounProvider().Objective + " " + Grammar.MakeTitleCase(text) + "-in-" + Grammar.MakeTitleCase(text2) + ".", null, "general", MuralCategory.IsBorn, MuralWeight.Medium, null, -1L)',
        source_text=_source(
            'JournalAPI.AddAccomplishment("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", you arrived at " + VillageName + ".", "On the auspicious " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", =name= arrived in " + VillageName + " and began " + The.Player.GetPronounProvider().PossessiveAdjective + " prodigious odyssey through Qud.", "At <spice.time.partsOfDay.!random> under <spice.commonPhrases.strange.!random.article> and " + text2 + " sky, the people of " + VillageName + " saw an image on the horizon that looked like a " + text + " bathed in " + text2 + ". It was =name=, and after " + The.Player.GetPronounProvider().Subjective + " came and left, the people of " + VillageName + " built a monument to =name= and thenceforth called " + The.Player.GetPronounProvider().Objective + " " + Grammar.MakeTitleCase(text) + "-in-" + Grammar.MakeTitleCase(text2) + ".", null, "general", MuralCategory.IsBorn, MuralWeight.Medium, null, -1L);'
        ),
    ),
    Case(
        id="narrative-death",
        family="JournalAPI",
        matched_code='JournalAPI.AddAccomplishment("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", " + text.Replace("!", "."), null, null, null, "general", MuralCategory.Generic, MuralWeight.Nil, null, -1L)',
        source_text=_source(
            'JournalAPI.AddAccomplishment("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", " + text.Replace("!", "."), null, null, null, "general", MuralCategory.Generic, MuralWeight.Nil, null, -1L);'
        ),
    ),
]

STRING_BUILDER_TEMPLATE_CASES = [
    Case(
        id="string-builder-sound-manager",
        family="Popup",
        matched_code="Popup.Show(stringBuilder.ToString())",
        source_text=_source(
            "StringBuilder stringBuilder = new();",
            "stringBuilder.Append(soundRequestLog.ToString()).Append('\\n');",
            "Popup.Show(stringBuilder.ToString());",
        ),
    ),
    Case(
        id="string-builder-skills-status",
        family="SetText",
        matched_code="requirementsText.SetText(stringBuilder.ToString())",
        source_text=_source(
            "StringBuilder stringBuilder = new();",
            'stringBuilder.Append(" ::\\n");',
            "requirementsText.SetText(stringBuilder.ToString());",
        ),
    ),
    Case(
        id="string-builder-item-naming",
        family="Popup",
        matched_code="Popup.Show(stringBuilder.ToString())",
        source_text=_source(
            "StringBuilder stringBuilder = new();",
            'stringBuilder.Append("[Debug: Created " + gameObject2.DebugName + " as InfluencedBy.]\\n");',
            "Popup.Show(stringBuilder.ToString());",
        ),
    ),
]

UNRESOLVED_CASES = [
    Case(
        id="unresolved-exception",
        family="Popup",
        matched_code='Popup.ShowFail("Could not generate turret from blueprint \\"" + value + "\\"\\n\\n" + ex.ToString())',
        source_text=_source(
            'Popup.ShowFail("Could not generate turret from blueprint \\"" + value + "\\"\\n\\n" + ex.ToString());'
        ),
    ),
    Case(
        id="unresolved-wish-list-entry",
        family="Popup",
        matched_code="Popup.Show(list2[num].ToString())",
        source_text=_source("Popup.Show(list2[num].ToString());"),
    ),
    Case(
        id="unresolved-int-game-state",
        family="AddPlayerMessage",
        matched_code='MessageQueue.AddPlayerMessage(The.Game.GetIntGameState("zoomnodes").ToString())',
        source_text=_source('MessageQueue.AddPlayerMessage(The.Game.GetIntGameState("zoomnodes").ToString());'),
    ),
]


@pytest.mark.parametrize("case", LEAF_CASES, ids=lambda case: case.id)
def test_classifies_literal_string_args_as_leaf(tmp_path: Path, case: Case) -> None:
    """Literal-string sink arguments become Leaf sites with high confidence."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.LEAF
    assert site.confidence is Confidence.HIGH
    assert site.key is not None
    assert site.key in case.matched_code
    assert site.pattern == case.matched_code
    assert site.source_route == case.family
    assert site.ownership_class is OwnershipClass.MID_PIPELINE_OWNED
    expected_destination = (
        DestinationDictionary.SCOPED
        if case.family in {"Popup", "AddPlayerMessage"}
        else DestinationDictionary.GLOBAL_FLAT
    )
    assert site.destination_dictionary is expected_destination
    assert site.rejection_reason is None


@pytest.mark.parametrize("case", TEMPLATE_CASES, ids=lambda case: case.id)
def test_classifies_format_and_interpolation_as_high_confidence_templates(tmp_path: Path, case: Case) -> None:
    """string.Format and interpolation sinks become Template sites."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.TEMPLATE
    assert site.confidence is Confidence.HIGH
    assert site.needs_review is False


@pytest.mark.parametrize("case", BUILDER_CASES, ids=lambda case: case.id)
def test_classifies_get_display_name_routes_as_builders(tmp_path: Path, case: Case) -> None:
    """GetDisplayName-fed sink sites become Builder sites."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.BUILDER
    assert site.confidence is Confidence.HIGH
    assert site.ownership_class is OwnershipClass.PRODUCER_OWNED
    assert site.destination_dictionary is None
    assert site.rejection_reason is FixedLeafRejectionReason.BUILDER_DISPLAY_NAME


@pytest.mark.parametrize(
    ("case", "verb", "extra", "frame", "lookup_tier"),
    [
        (MESSAGE_FRAME_CASES[0], "charge", None, "DidX", 1),
        (MESSAGE_FRAME_CASES[1], "lock", "in place", "DidX", 2),
        (
            MESSAGE_FRAME_CASES[2],
            "emit",
            '"a freezing ray" + ((registeredSlot != null) ? (" from " + mutation.ParentObject.its + " " + registeredSlot.GetOrdinalName()) : "")',
            "XDidY",
            3,
        ),
    ],
    ids=lambda row: row.id if isinstance(row, Case) else str(row),
)
def test_classifies_didx_family_as_message_frames(
    tmp_path: Path,
    case: Case,
    verb: str,
    extra: str | None,
    frame: str,
    lookup_tier: int,
) -> None:
    """DidX-family call sites expose verb/extra/frame metadata."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.MESSAGE_FRAME
    assert site.confidence is Confidence.HIGH
    assert site.verb == verb
    assert site.extra == extra
    assert site.frame == frame
    assert site.lookup_tier == lookup_tier


@pytest.mark.parametrize(
    ("case", "verb", "source_context"),
    [
        (VERB_COMPOSITION_CASES[0], "begin", 'MessageQueue.AddPlayerMessage(Object.Does("begin") + " flying.");'),
        (
            VERB_COMPOSITION_CASES[1],
            "miss",
            'IComponent<GameObject>.AddPlayerMessage(Attacker.Does("miss") + " you!");',
        ),
        (
            VERB_COMPOSITION_CASES[2],
            "loosen",
            'Popup.Show(CarapaceObject.Does("loosen") + ". Your AV decreases by {{R|" + ACModifier + "}}.");',
        ),
    ],
    ids=lambda row: row.id if isinstance(row, Case) else str(row),
)
def test_classifies_real_does_sites_as_verb_composition(
    tmp_path: Path,
    case: Case,
    verb: str,
    source_context: str,
) -> None:
    """True-positive Does() sites become VerbComposition with statement context."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.VERB_COMPOSITION
    assert site.confidence is Confidence.HIGH
    assert site.verb == verb
    assert site.source_context == source_context


@pytest.mark.parametrize("case", VERB_FALSE_POSITIVE_CASES, ids=lambda case: case.id)
def test_filters_known_does_false_positives(tmp_path: Path, case: Case) -> None:
    """Known non-content Does() sites are removed from the inventory draft."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is None


@pytest.mark.parametrize("case", VARIABLE_TEMPLATE_CASES, ids=lambda case: case.id)
def test_classifies_start_replace_sites_as_variable_templates(tmp_path: Path, case: Case) -> None:
    """StartReplace sites become VariableTemplate with high confidence."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.VARIABLE_TEMPLATE
    assert site.confidence is Confidence.HIGH


@pytest.mark.parametrize("case", PROCEDURAL_TEXT_CASES, ids=lambda case: case.id)
def test_classifies_historic_string_expander_as_procedural_text(tmp_path: Path, case: Case) -> None:
    """HistoricStringExpander sites require runtime verification."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.PROCEDURAL_TEXT
    assert site.confidence is Confidence.LOW
    assert site.needs_runtime is True
    assert site.ownership_class is OwnershipClass.PRODUCER_OWNED
    assert site.rejection_reason is FixedLeafRejectionReason.PROCEDURAL


@pytest.mark.parametrize("case", NARRATIVE_TEMPLATE_CASES, ids=lambda case: case.id)
def test_classifies_journal_api_as_narrative_templates(tmp_path: Path, case: Case) -> None:
    """JournalAPI call sites become review-needed NarrativeTemplate sites."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.NARRATIVE_TEMPLATE
    assert site.confidence is Confidence.MEDIUM
    assert site.needs_review is True
    assert site.ownership_class is OwnershipClass.PRODUCER_OWNED
    assert site.rejection_reason is FixedLeafRejectionReason.NARRATIVE_TEMPLATE


@pytest.mark.parametrize("case", STRING_BUILDER_TEMPLATE_CASES, ids=lambda case: case.id)
def test_classifies_string_builder_to_string_as_medium_templates(tmp_path: Path, case: Case) -> None:
    """StringBuilder.ToString sink args become medium-confidence templates."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.TEMPLATE
    assert site.confidence is Confidence.MEDIUM
    assert site.needs_review is True
    assert site.ownership_class is OwnershipClass.PRODUCER_OWNED
    assert site.rejection_reason is FixedLeafRejectionReason.TEMPLATE


@pytest.mark.parametrize("case", UNRESOLVED_CASES, ids=lambda case: case.id)
def test_leaves_non_string_builder_to_string_and_complex_calls_unresolved(tmp_path: Path, case: Case) -> None:
    """Non-StringBuilder ToString calls stay Unresolved."""
    source_root, raw_hit = _make_raw_hit(tmp_path, case)

    site = classify_raw_hit(raw_hit, source_root)

    assert site is not None
    assert site.type is SiteType.UNRESOLVED
    assert site.confidence is Confidence.LOW
    assert site.needs_runtime is True
    assert site.ownership_class is OwnershipClass.SINK
    assert site.rejection_reason is FixedLeafRejectionReason.UNRESOLVED


def test_classify_raw_hits_file_writes_inventory_draft_and_stats(tmp_path: Path) -> None:
    """File-level classification writes deterministic JSON and excludes filtered Does hits."""
    cases = [
        LEAF_CASES[0],
        VERB_COMPOSITION_CASES[0],
        VERB_FALSE_POSITIVE_CASES[0],
        STRING_BUILDER_TEMPLATE_CASES[0],
    ]
    raw_hits: list[RawHit] = []
    for case in cases:
        _source_root, raw_hit = _make_raw_hit(tmp_path / "source", case)
        raw_hits.append(raw_hit)

    raw_hits_path = tmp_path / "raw_hits.jsonl"
    write_raw_hits_jsonl(raw_hits_path, raw_hits)

    output_path = tmp_path / "inventory_draft.json"
    draft = classify_raw_hits_file(raw_hits_path, tmp_path / "source", output_path=output_path)

    assert output_path.exists()
    persisted = read_inventory_draft_json(output_path)
    assert persisted == draft
    assert draft.stats.input_hits == 4
    assert draft.stats.filtered_hits == 1
    assert draft.stats.proven_fixed_leaf == 1
    assert draft.stats.rejected_fixed_leaf == 2
    assert [site.type for site in draft.sites] == [
        SiteType.LEAF,
        SiteType.VERB_COMPOSITION,
        SiteType.TEMPLATE,
    ]


def test_rejection_reason_defensively_handles_non_proven_leaf_sites() -> None:
    """Leaf sites that are not yet proven should produce a stable rejection reason instead of raising."""
    assert _rejection_reason(SiteType.LEAF, needs_runtime=False) is FixedLeafRejectionReason.NEEDS_REVIEW


def test_direct_script_help_runs_without_module_bootstrap_errors() -> None:
    """Direct script execution should show help instead of import-path failures."""
    completed = subprocess.run(  # noqa: S603 -- test invokes a repo-local fixed script path via the active interpreter.
        [sys.executable, str(REPO_ROOT / "scripts" / "legacies" / "scanner" / "rule_classifier.py"), "--help"],
        capture_output=True,
        text=True,
        cwd=REPO_ROOT,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    assert "Run Phase 1b rule-based source classification." in completed.stdout
