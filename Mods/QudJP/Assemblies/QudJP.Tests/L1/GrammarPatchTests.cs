using System.Collections.Generic;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class GrammarPatchTests
{
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

        var skipped = GrammarMakeAndListPatch.Prefix(input, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("A、B、とC"));
        });
    }

    [Test]
    public void MakeAndListPatch_ReturnsEmptyString_WhenListIsEmpty()
    {
        var result = "placeholder";
        var input = new List<string>();

        var skipped = GrammarMakeAndListPatch.Prefix(input, ref result);

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

        var skipped = GrammarMakeAndListPatch.Prefix(input, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("{{W|刀}}と&G盾^k"));
        });
    }

    [Test]
    public void MakeOrListPatch_JoinsThreeItemsInJapaneseStyle()
    {
        var result = string.Empty;
        var input = new List<string> { "A", "B", "C" };

        var skipped = GrammarMakeOrListPatch.Prefix(input, ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("A、B、またはC"));
        });
    }

    [Test]
    public void MakeOrListPatch_ReturnsEmptyString_WhenListIsEmpty()
    {
        var result = "placeholder";
        var input = new List<string>();

        var skipped = GrammarMakeOrListPatch.Prefix(input, ref result);

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

        var skipped = GrammarMakeOrListPatch.Prefix(input, ref result);

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
    public void InitCapsPatch_ReturnsInputUnchanged_ForNormalText()
    {
        var result = string.Empty;

        var skipped = GrammarInitCapsPatch.Prefix("hello", ref result);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.False);
            Assert.That(result, Is.EqualTo("hello"));
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
