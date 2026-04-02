using System.Collections.Generic;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class GrammarPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
    }

    [Test]
    public void APatch_ReturnsInputUnchanged_ForNormalWord()
    {
        var result = string.Empty;

        var skipped = GrammarAPatch.Prefix("sword", Capitalize: false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("sword"));
        });
    }

    [Test]
    public void APatch_ReturnsEmptyString_WhenInputIsEmpty()
    {
        var result = "placeholder";

        var skipped = GrammarAPatch.Prefix(string.Empty, Capitalize: true, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void APatch_PreservesColorCodes()
    {
        var result = string.Empty;

        var skipped = GrammarAPatch.Prefix("{{W|sword}}", Capitalize: false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("{{W|sword}}"));
        });
    }

    [Test]
    public void APatch_StripsLeadingIndefiniteArticle()
    {
        var result = string.Empty;

        var skipped = GrammarAPatch.Prefix("a ドア", Capitalize: false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("ドア"));
        });
    }

    [Test]
    public void APatch_StripsLeadingSomePrefix()
    {
        var result = string.Empty;

        var skipped = GrammarAPatch.Prefix("some ブラインストーク", Capitalize: false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("ブラインストーク"));
        });
    }

    [Test]
    public void PluralizePatch_ReturnsInputUnchanged_ForNormalWord()
    {
        var result = string.Empty;

        var skipped = GrammarPluralizePatch.Prefix("child", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("child"));
        });
    }

    [Test]
    public void PluralizePatch_ReturnsEmptyString_WhenInputIsEmpty()
    {
        var result = "placeholder";

        var skipped = GrammarPluralizePatch.Prefix(string.Empty, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void PluralizePatch_PreservesColorCodes()
    {
        var result = string.Empty;

        var skipped = GrammarPluralizePatch.Prefix("&Gknife^k", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("&Gknife^k"));
        });
    }

    [Test]
    public void MakePossessivePatch_AppendsNoParticle_ForNormalWord()
    {
        var result = string.Empty;

        var skipped = GrammarMakePossessivePatch.Prefix("sword", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("swordの"));
        });
    }

    [Test]
    public void MakePossessivePatch_HandlesEmptyString()
    {
        var result = "placeholder";

        var skipped = GrammarMakePossessivePatch.Prefix(string.Empty, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("の"));
        });
    }

    [Test]
    public void MakePossessivePatch_PreservesColorCodes()
    {
        var result = string.Empty;

        var skipped = GrammarMakePossessivePatch.Prefix("{{W|sword}}", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("{{W|sword}}の"));
        });
    }

    [Test]
    public void MakePossessivePatch_DoesNotDuplicateNoParticle()
    {
        var result = string.Empty;

        var skipped = GrammarMakePossessivePatch.Prefix("猫の", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("猫の"));
        });
    }

    [Test]
    public void MakeAndListPatch_JoinsThreeItemsInJapaneseStyle()
    {
        var result = string.Empty;
        var input = new List<string> { "A", "B", "C" };

        var skipped = GrammarMakeAndListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("A、B、とC"));
        });
    }

    [TestCase(new[] { "A" }, "A")]
    [TestCase(new[] { "A", "B" }, "AとB")]
    [TestCase(new[] { "A", "B", "C", "D" }, "A、B、C、とD")]
    public void MakeAndListPatch_CharacterizesBoundaryCounts(string[] input, string expected)
    {
        var result = string.Empty;

        var skipped = GrammarMakeAndListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void MakeAndListPatch_ReturnsEmptyString_WhenListIsEmpty()
    {
        var result = "placeholder";
        var input = new List<string>();

        var skipped = GrammarMakeAndListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void MakeAndListPatch_PreservesColorCodes()
    {
        var result = string.Empty;
        var input = new List<string> { "{{W|刀}}", "&G盾^k" };

        var skipped = GrammarMakeAndListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("{{W|刀}}と&G盾^k"));
        });
    }

    [Test]
    public void MakeAndListPatch_StripsLeadingIndefiniteArticlesFromItems()
    {
        var result = string.Empty;
        var input = new List<string> { "a ドア", "a 薄めの塩の水たまり" };

        var skipped = GrammarMakeAndListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("ドアと薄めの塩の水たまり"));
        });
    }

    [Test]
    public void MakeAndListPatch_StripsLeadingSomePrefixFromItems()
    {
        var result = string.Empty;
        var input = new List<string> { "a ウォーターヴァイン", "some ブラインストーク", "some ブラインストーク" };

        var skipped = GrammarMakeAndListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("ウォーターヴァイン、ブラインストーク、とブラインストーク"));
        });
    }

    [Test]
    public void MakeAndListPatch_LogsDynamicTransformProbe()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            var result = string.Empty;
            var input = new List<string> { "A", "B", "C" };
            _ = GrammarMakeAndListPatch.Prefix(input, false, ref result);
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("DynamicTextProbe/v1"));
            Assert.That(output, Does.Contain("route='GrammarPatch'"));
            Assert.That(output, Does.Contain("family='MakeAndList/count=3+'"));
            Assert.That(output, Does.Contain("source='A | B | C'"));
            Assert.That(output, Does.Contain("translated='A、B、とC'"));
        });
    }

    [Test]
    public void MakeOrListPatch_JoinsThreeItemsInJapaneseStyle()
    {
        var result = string.Empty;
        var input = new List<string> { "A", "B", "C" };

        var skipped = GrammarMakeOrListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("A、B、またはC"));
        });
    }

    [TestCase(new[] { "A" }, "A")]
    [TestCase(new[] { "A", "B" }, "AまたはB")]
    [TestCase(new[] { "A", "B", "C", "D" }, "A、B、C、またはD")]
    public void MakeOrListPatch_CharacterizesBoundaryCounts(string[] input, string expected)
    {
        var result = string.Empty;

        var skipped = GrammarMakeOrListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void MakeOrListPatch_ReturnsEmptyString_WhenListIsEmpty()
    {
        var result = "placeholder";
        var input = new List<string>();

        var skipped = GrammarMakeOrListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void MakeOrListPatch_PreservesColorCodes()
    {
        var result = string.Empty;
        var input = new List<string> { "{{W|刀}}", "&G盾^k" };

        var skipped = GrammarMakeOrListPatch.Prefix(input, false, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("{{W|刀}}または&G盾^k"));
        });
    }

    [Test]
    public void SplitOfSentenceListPatch_SplitsEnglishList()
    {
        List<string> result = new();

        var skipped = GrammarSplitOfSentenceListPatch.Prefix("A, B, and C", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(new[] { "A", "B", "C" }));
        });
    }

    [TestCase("A", new[] { "A" })]
    [TestCase("A and B", new[] { "A", "B" })]
    [TestCase("A, B, C, and D", new[] { "A", "B", "C", "D" })]
    [TestCase("A、B、C", new[] { "A", "B", "C" })]
    public void SplitOfSentenceListPatch_CharacterizesBoundaryCounts(string input, string[] expected)
    {
        List<string> result = new();

        var skipped = GrammarSplitOfSentenceListPatch.Prefix(input, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void SplitOfSentenceListPatch_ReturnsEmptyList_ForEmptyInput()
    {
        List<string> result = new() { "placeholder" };

        var skipped = GrammarSplitOfSentenceListPatch.Prefix(string.Empty, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public void SplitOfSentenceListPatch_SplitsJapaneseCommaAndPreservesColorCodes()
    {
        List<string> result = new();

        var skipped = GrammarSplitOfSentenceListPatch.Prefix("{{W|A}}、&GB^k and ^rC^k", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(new[] { "{{W|A}}", "&GB^k", "^rC^k" }));
        });
    }

    [Test]
    public void SplitOfSentenceListPatch_IsDisabledForGrammarInGameVersion_2_0_4()
    {
        var isHarmonyPatch = false;
        foreach (var attribute in typeof(GrammarSplitOfSentenceListPatch).GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == "HarmonyLib.HarmonyPatch")
            {
                isHarmonyPatch = true;
                break;
            }
        }

        Assert.That(isHarmonyPatch, Is.False);
    }

    [Test]
    public void InitCapsPatch_CapitalizesAsciiFirstChar_ForNormalText()
    {
        var result = string.Empty;

        var skipped = GrammarInitCapsPatch.Prefix("hello", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("Hello"));
        });
    }

    [Test]
    public void InitCapsPatch_LeavesNonAsciiFirstCharUnchanged_ForJapaneseText()
    {
        var result = string.Empty;

        var skipped = GrammarInitCapsPatch.Prefix("こんにちは", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("こんにちは"));
        });
    }

    [Test]
    public void InitCapsPatch_ReturnsEmptyString_WhenInputIsEmpty()
    {
        var result = "placeholder";

        var skipped = GrammarInitCapsPatch.Prefix(string.Empty, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void InitCapsPatch_PreservesColorCodes()
    {
        var result = string.Empty;

        var skipped = GrammarInitCapsPatch.Prefix("{{W|hello}}", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("{{W|hello}}"));
        });
    }

    [Test]
    public void CardinalNumberPatch_FormatsPositiveNumber()
    {
        var result = string.Empty;

        var skipped = GrammarCardinalNumberPatch.Prefix(42, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("42"));
        });
    }

    [Test]
    public void CardinalNumberPatch_FormatsZero()
    {
        var result = string.Empty;

        var skipped = GrammarCardinalNumberPatch.Prefix(0, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("0"));
        });
    }

    [Test]
    public void CardinalNumberPatch_FormatsNegativeNumberWithInvariantCulture()
    {
        var result = string.Empty;

        var skipped = GrammarCardinalNumberPatch.Prefix(-12, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("-12"));
        });
    }
}
