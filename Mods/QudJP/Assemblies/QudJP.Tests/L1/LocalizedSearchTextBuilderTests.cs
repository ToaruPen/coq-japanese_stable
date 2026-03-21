using QudJP;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class LocalizedSearchTextBuilderTests
{
    [Test]
    public void Build_ConcatenatesEnglishAndLocalizedFragments()
    {
        var result = LocalizedSearchTextBuilder.Build(
            new[] { "The villagers of Abal", "Reputation: 0" },
            new[] { "Abalの村人たち", "評判: 0" });

        Assert.That(result, Is.EqualTo("the villagers of abal reputation: 0 Abalの村人たち 評判: 0"));
    }

    [Test]
    public void Build_DeduplicatesAndSkipsEmptyFragments()
    {
        var result = LocalizedSearchTextBuilder.Build(
            new[] { "Reputation: 0", "Reputation: 0", null, "  " },
            new[] { "評判: 0", "評判: 0" });

        Assert.That(result, Is.EqualTo("reputation: 0 評判: 0"));
    }
}
