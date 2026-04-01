using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class NameStyleGenerateSeparatorHelpersTests
{
    [TestCase('-', '・')]
    [TestCase(' ', '・')]
    [TestCase('x', 'x')]
    public void TranslateSeparator_ReturnsExpectedCharacter(char source, char expected)
    {
        Assert.That(NameStyleGenerateSeparatorHelpers.TranslateSeparator(source), Is.EqualTo(expected));
    }
}
