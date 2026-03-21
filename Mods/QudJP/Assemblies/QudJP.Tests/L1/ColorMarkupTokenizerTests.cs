namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorMarkupTokenizerTests
{
    [Test]
    public void StripRestore_PreservesTmpColorTags()
    {
        var input = "<color=#44ff88>Hello</color>";
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(input);

        var restored = ColorAwareTranslationComposer.Restore("こんにちは", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("Hello"));
            Assert.That(restored, Is.EqualTo("<color=#44ff88>こんにちは</color>"));
        });
    }

    [Test]
    public void StripRestore_PreservesMixedQudAndTmpMarkup()
    {
        var input = "{{W|<color=#44ff88>Hello</color>}}";
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(input);

        var restored = ColorAwareTranslationComposer.Restore("こんにちは", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("Hello"));
            Assert.That(restored, Is.EqualTo("{{W|<color=#44ff88>こんにちは</color>}}"));
        });
    }
}
