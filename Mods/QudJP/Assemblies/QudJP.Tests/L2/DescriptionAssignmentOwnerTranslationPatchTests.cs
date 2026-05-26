using QudJP.Patches;
using System.Text;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionAssignmentOwnerTranslationPatchTests
{
    [Test]
    public void TranslateBiocapacitor_TranslatesFixedDescription()
    {
        var target = new DummyDescriptionTarget { Description = "biocapacitor" };

        DescriptionAssignmentOwnerTranslationPatch.TranslateBiocapacitorForTests(target);

        Assert.That(target.Description, Is.EqualTo("生体コンデンサー"));
    }

    [Test]
    public void TranslateBiocapacitor_PreservesUnknownAndStripsMarkerPrefixedDescription()
    {
        var unknown = new DummyDescriptionTarget { Description = "capacitor" };
        var marker = new DummyDescriptionTarget { Description = "\u0001biocapacitor" };

        DescriptionAssignmentOwnerTranslationPatch.TranslateBiocapacitorForTests(unknown);
        DescriptionAssignmentOwnerTranslationPatch.TranslateBiocapacitorForTests(marker);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Description, Is.EqualTo("capacitor"));
            Assert.That(marker.Description, Is.EqualTo("biocapacitor"));
        });
    }

    [Test]
    public void TranslateMovementCapabilities_TranslatesGeneratedStatusSuffixes()
    {
        var target = new DummyMovementCapabilitiesEvent
        {
            Descriptions =
            [
                "Charge {{W|[attack]}}",
                "Sprint [toggled off]",
                "Fly {{g|[toggled on]}}",
                "{{K|Phase [attack] [toggled on]}}",
            ],
        };

        DescriptionAssignmentOwnerTranslationPatch.TranslateMovementCapabilityDescriptionsForTests(target);

        Assert.That(
            target.Descriptions,
            Is.EqualTo(new[]
            {
                "Charge {{W|[攻撃]}}",
                "Sprint [オフ]",
                "Fly {{g|[オン]}}",
                "{{K|Phase [攻撃] [オン]}}",
            }));
    }

    [Test]
    public void TranslateMovementCapabilities_PreservesUnknownAndMarkerPrefixedDescriptions()
    {
        var target = new DummyMovementCapabilitiesEvent
        {
            Descriptions =
            [
                "Charge [disabled]",
                "\u0001Sprint [toggled off]",
            ],
        };

        DescriptionAssignmentOwnerTranslationPatch.TranslateMovementCapabilityDescriptionsForTests(target);

        Assert.That(target.Descriptions, Is.EqualTo(new[] { "Charge [disabled]", "Sprint [toggled off]" }));
    }

    [TestCase(
        "Foliage camouflage: This item grants the wearer +=level= DV in foliage.",
        "植生迷彩: 着用者は植生の中で+=level= DVを得る。")]
    [TestCase(
        "Urban camouflage: This item grants the wearer +=level= DV near trash and furniture.",
        "都市迷彩: 着用者はゴミや家具の近くで+=level= DVを得る。")]
    public void TranslateCamouflageDescription_TranslatesFixedDescription(string source, string expected)
    {
        var target = new DummyDescriptionTarget { Description = source };

        DescriptionAssignmentOwnerTranslationPatch.TranslateCamouflageForTests(target);

        Assert.That(target.Description, Is.EqualTo(expected));
    }

    [Test]
    public void TranslateCamouflageDescription_PreservesUnknownAndStripsMarkerPrefixedDescription()
    {
        var unknown = new DummyDescriptionTarget { Description = "Camouflage: unchanged." };
        var marker = new DummyDescriptionTarget { Description = "\u0001Urban camouflage: This item grants the wearer +=level= DV near trash and furniture." };

        DescriptionAssignmentOwnerTranslationPatch.TranslateCamouflageForTests(unknown);
        DescriptionAssignmentOwnerTranslationPatch.TranslateCamouflageForTests(marker);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Description, Is.EqualTo("Camouflage: unchanged."));
            Assert.That(marker.Description, Is.EqualTo("Urban camouflage: This item grants the wearer +=level= DV near trash and furniture."));
        });
    }

    [Test]
    public void TranslateMechanimistLibrarian_TranslatesNameTitleAndShortDescription()
    {
        var target = new DummyMechanimistLibrarianTarget
        {
            DisplayName = "Sheba Hagadias",
            Titles = new DummyTitlesPart
            {
                TitleList = "snapjaw;;librarian of the Stilt",
            },
            DescriptionPart = new DummyDescriptionPart
            {
                Short = MechanimistLibrarianShortDescriptionSource,
            },
        };

        DescriptionAssignmentOwnerTranslationPatch.TranslateMechanimistLibrarianForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(target.DisplayName, Is.EqualTo("シェバ・ハガディアス"));
            Assert.That(target.Titles.TitleList, Is.EqualTo("snapjaw;;大寺院の司書"));
            Assert.That(target.DescriptionPart.Short, Does.Contain("聖堂の前廊"));
            Assert.That(target.DescriptionPart.Short, Does.Contain("カサフェセンス"));
            Assert.That(target.DescriptionPart.Short, Does.Not.Contain("Argent Fathers"));
        });
    }

    [Test]
    public void TranslateWingsPart_TranslatesGeneratedBodyPartDescription()
    {
        var target = new DummyDescriptionTarget { Description = "Worn around Wings" };

        DescriptionAssignmentOwnerTranslationPatch.TranslateWingsPartForTests(target);

        Assert.That(target.Description, Is.EqualTo("翼の周囲に着用する"));
    }

    [Test]
    public void TranslateBannerDescription_TranslatesGeneratedRulesText()
    {
        var banner = new DummyBannerTarget
        {
            Description = "Bestows the {{|inspired}} effect to {{w|Mechanimists}} who can see this item.",
        };
        var evt = new DummyShortDescriptionEvent
        {
            Postfix = new StringBuilder(banner.Description),
        };

        DescriptionAssignmentOwnerTranslationPatch.TranslateBannerDescriptionForTests(banner, evt);

        Assert.Multiple(() =>
        {
            Assert.That(banner.Description, Is.EqualTo("このアイテムを見ることができる{{w|Mechanimists}}に{{|inspired}}効果を与える。"));
            Assert.That(evt.Postfix.ToString(), Is.EqualTo("このアイテムを見ることができる{{w|Mechanimists}}に{{|inspired}}効果を与える。"));
        });
    }

    [Test]
    public void TranslateRemainingDescriptionAssignments_PreserveUnknownAndStripMarkerPrefixedValues()
    {
        var wingsUnknown = new DummyDescriptionTarget { Description = "Worn around Tail" };
        var wingsMarker = new DummyDescriptionTarget { Description = "\u0001Worn around Wings" };
        var banner = new DummyBannerTarget { Description = "\u0001Bestows the {{|inspired}} effect to {{w|Mechanimists}} who can see this item." };
        var evt = new DummyShortDescriptionEvent
        {
            Postfix = new StringBuilder("Unknown banner rule."),
        };

        DescriptionAssignmentOwnerTranslationPatch.TranslateWingsPartForTests(wingsUnknown);
        DescriptionAssignmentOwnerTranslationPatch.TranslateWingsPartForTests(wingsMarker);
        DescriptionAssignmentOwnerTranslationPatch.TranslateBannerDescriptionForTests(banner, evt);

        Assert.Multiple(() =>
        {
            Assert.That(wingsUnknown.Description, Is.EqualTo("Worn around Tail"));
            Assert.That(wingsMarker.Description, Is.EqualTo("Worn around Wings"));
            Assert.That(
                banner.Description,
                Is.EqualTo("Bestows the {{|inspired}} effect to {{w|Mechanimists}} who can see this item."));
            Assert.That(evt.Postfix.ToString(), Is.EqualTo("Unknown banner rule."));
        });
    }

    private const string MechanimistLibrarianShortDescriptionSource =
        "In the narthex of the Stilt, cloistered beneath a marble arch and close to =pronouns.possessive= Argent Fathers, =pronouns.subjective= =verb:muse:afterpronoun= over a tattered codex. =pronouns.Subjective==verb:'re:afterpronoun= safe here, but it wasn't always that way. As a youngling, =pronouns.possessive= own kind understood =pronouns.objective= little. Only when =pronouns.subjective= =verb:were:afterpronoun= gifted a copy of the Canticles Chromaic did =pronouns.subjective= learn comfort, or mirth, or reason. =pronouns.Possessive= journey to the Stilt took several years, but now that =pronouns.subjective==verb:'re:afterpronoun= here, Sheba =verb:seek= to consolidate all the learning of the ages tucked away in Qud's innumerable chrome nooks. Here, =pronouns.subjective= =verb:prepare:afterpronoun= a residence where pilgrims can study the wisdom of others and bring themselves nearer to the divinity of the Kasaphescence.";

    private sealed class DummyDescriptionTarget
    {
        public string? Description { get; set; }
    }

    private sealed class DummyDescriptionPart
    {
        public string? Short { get; set; }
    }

    private sealed class DummyTitlesPart
    {
        public string? TitleList { get; set; }
    }

    private sealed class DummyMechanimistLibrarianTarget
    {
        public string? DisplayName { get; set; }

        public DummyTitlesPart Titles { get; set; } = new();

        public DummyDescriptionPart DescriptionPart { get; set; } = new();
    }

    private sealed class DummyBannerTarget
    {
        public string? Description { get; set; }
    }

    private sealed class DummyShortDescriptionEvent
    {
        public StringBuilder? Postfix { get; set; }
    }

    private sealed class DummyMovementCapabilitiesEvent
    {
        public List<string>? Descriptions { get; set; }
    }
}
