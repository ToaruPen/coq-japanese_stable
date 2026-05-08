namespace QudJP.Tests.L1;

using QudJP.UI;

[TestFixture]
[Category("L1")]
public sealed class JapaneseBlockWrapTests
{
    [Test]
    public void TryWrapForCjkBlock_BreaksJapaneseRunsAtVisibleWidth()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "あいうえおかきくけこ",
            width: 5,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Is.EqualTo("あいうえお\nかきくけこ"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void TryWrapForCjkBlock_ReturnsFalseForNullOrEmptyInput(string? source)
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            source!,
            width: 10,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(wrapped, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_ReopensActiveQudColorAfterInsertedBreak()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "&Yあいうえおかき",
            width: 4,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Is.EqualTo("&Yあいうえ\n&Yおかき"));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_PreservesQudMarkupBoundaries()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "{{y|あいうえおかき}}",
            width: 4,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Is.EqualTo("{{y|あいうえ\nおかき}}"));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_PreservesRootContractTokens()
    {
        const string source = "あいうえお\u0001=variable.name={0}{12:format}<sprite name=key>{{TMP|かきくけこ}}";

        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            source,
            width: 8,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Does.Contain("\u0001"));
            Assert.That(wrapped, Does.Contain("=variable.name="));
            Assert.That(wrapped, Does.Contain("{0}"));
            Assert.That(wrapped, Does.Contain("{12:format}"));
            Assert.That(wrapped, Does.Contain("<sprite name=key>"));
            Assert.That(wrapped, Does.Contain("{{TMP|"));
            Assert.That(wrapped, Does.Contain("}}"));
        });
    }

    [TestCase("ああ{0}いう", "{0}")]
    [TestCase("ああ{12:format}いう", "{12:format}")]
    [TestCase("ああ=variable.name=いう", "=variable.name=")]
    public void TryWrapForCjkBlock_PreservesRootContractTokenBoundariesAcrossForcedBreaks(
        string source,
        string token)
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            source,
            width: 3,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Does.Contain(token));
            Assert.That(wrapped, Does.Not.Contain(token[..^1] + "\n" + token[^1]));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_RejectsPreferredBreakPastWidth()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "あいうえおかきくけこさし。すせそ",
            width: 12,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped.Split('\n'), Has.All.Length.LessThanOrEqualTo(12));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_LeavesNonCjkTextForVanillaWrapper()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "The quick brown fox",
            width: 5,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(wrapped, Is.EqualTo("The quick brown fox"));
        });
    }

    [Test]
    public void TryWrapTooltipLongDescription_UsesNarrowWidthForJapaneseInjectorRules()
    {
        const string source = "持続：41-50ラウンド　熱耐性 +100／冷気耐性 -50／クイックネス +10（真系：+20）。凍結しない。あなたが与える熱ダメージ +25%。外部の熱源で体温は上昇しない。発火して効果を失わないよう、移動し続けなければならない。";

        var changed = JapaneseBlockWrap.TryWrapTooltipLongDescription(source, out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped.Split('\n'), Has.All.Length.LessThanOrEqualTo(34));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_PrefersRecentJapanesePunctuation()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "あいうえおかきくけこ。さしすせそたちつてと",
            width: 12,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Is.EqualTo("あいうえおかきくけこ。\nさしすせそたちつてと"));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_DoesNotPreferStatDelimitersAsLineBreaks()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "熱耐性 +100／冷気耐性 -50／クイックネス +10。",
            width: 12,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Does.Not.Contain("／\n"));
            Assert.That(wrapped, Does.Not.Contain("：\n"));
            Assert.That(wrapped, Does.Not.Contain(";\n"));
        });
    }

    [Test]
    public void TryWrapForCjkBlock_PrefersRecentHalfWidthSpace()
    {
        var changed = JapaneseBlockWrap.TryWrapForCjkBlock(
            "古い遺物 調査記録が残っている",
            width: 8,
            maxLines: 5000,
            out var wrapped);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(wrapped, Is.EqualTo("古い遺物 \n調査記録が残って\nいる"));
        });
    }
}
