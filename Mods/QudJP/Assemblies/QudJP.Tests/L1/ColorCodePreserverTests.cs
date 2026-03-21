namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorCodePreserverTests
{
    [Test]
    public void StripRestore_PreservesMarkupFormat()
    {
        var input = "{{W|a sword}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("刀", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("a sword"));
            Assert.That(restored, Is.EqualTo("{{W|刀}}"));
        });
    }

    [Test]
    public void StripRestore_PreservesForegroundCodes()
    {
        var input = "&Ggreen&y";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("緑", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("green"));
            Assert.That(restored, Is.EqualTo("&G緑&y"));
        });
    }

    [Test]
    public void StripRestore_PreservesBackgroundCodes()
    {
        var input = "^rdanger^k";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("危険", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("danger"));
            Assert.That(restored, Is.EqualTo("^r危険^k"));
        });
    }

    [Test]
    public void Strip_KeepsEscapedAmpersandLiteral()
    {
        var input = "AT&&T";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore(stripped, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("AT&&T"));
            Assert.That(spans, Is.Empty);
            Assert.That(restored, Is.EqualTo("AT&&T"));
        });
    }

    [Test]
    public void Strip_KeepsEscapedCaretLiteral()
    {
        var input = "^^caret";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore(stripped, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("^^caret"));
            Assert.That(spans, Is.Empty);
            Assert.That(restored, Is.EqualTo("^^caret"));
        });
    }

    [Test]
    public void StripRestore_HandlesMixedFormatsInSingleString()
    {
        var input = "{{W|&GGo^r!}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("行け!", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("Go!"));
            Assert.That(restored, Is.EqualTo("{{W|&G行け^r!}}"));
        });
    }

    [Test]
    public void SliceSpans_ReturnsRangeRelativeMarkup()
    {
        var input = "{{W|[Esc]}} {{y|Cancel}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var hotkey = ColorCodePreserver.Restore("[Esc]", ColorCodePreserver.SliceSpans(spans, 0, 5));
        var label = ColorCodePreserver.Restore("キャンセル", ColorCodePreserver.SliceSpans(spans, 6, 6));

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("[Esc] Cancel"));
            Assert.That(hotkey, Is.EqualTo("{{W|[Esc]}}"));
            Assert.That(label, Is.EqualTo("{{y|キャンセル}}"));
        });
    }

    [Test]
    public void Strip_ReturnsEmptyValue_ForNullOrEmptyInput()
    {
        var nullResult = ColorCodePreserver.Strip(null);
        var emptyResult = ColorCodePreserver.Strip(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(nullResult.stripped, Is.EqualTo(string.Empty));
            Assert.That(nullResult.spans, Is.Empty);
            Assert.That(emptyResult.stripped, Is.EqualTo(string.Empty));
            Assert.That(emptyResult.spans, Is.Empty);
        });
    }
}
