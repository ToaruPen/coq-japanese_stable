using System;
using NUnit.Framework;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class StringUtilityTests
{
    [Test]
    public void ReplaceVisibleToken_KeepsMarkupDelimiters()
    {
        var input = "{{W|a sword}}";

        var result = ReplaceVisibleToken(input, "sword", "blade");

        Assert.That(result, Is.EqualTo("{{W|a blade}}"));
    }

    [Test]
    public void RemoveIndefiniteArticle_StripsEnglishPrefix()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RemoveIndefiniteArticle("a relic"), Is.EqualTo("relic"));
            Assert.That(RemoveIndefiniteArticle("an amulet"), Is.EqualTo("amulet"));
            Assert.That(RemoveIndefiniteArticle("relic"), Is.EqualTo("relic"));
        });
    }

    private static string ReplaceVisibleToken(string source, string oldToken, string newToken)
    {
        return source.Replace(oldToken, newToken);
    }

    private static string RemoveIndefiniteArticle(string source)
    {
        if (source.StartsWith("an ", System.StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(3);
        }

        if (source.StartsWith("a ", System.StringComparison.OrdinalIgnoreCase))
        {
            return source.Substring(2);
        }

        return source;
    }
}
