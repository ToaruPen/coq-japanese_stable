using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class PopupTextFieldTranslatorTests
{
    [Test]
    public void TryTranslateTextField_UpdatesMutableTextField()
    {
        var item = new DummyTextItem("Cancel");

        var changed = PopupTextFieldTranslator.TryTranslateTextField(
            item,
            static text => text == "Cancel" ? "キャンセル" : text);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(item.text, Is.EqualTo("キャンセル"));
        });
    }

    [Test]
    public void TryTranslateTextField_LeavesMissingTextFieldUnchanged()
    {
        var item = new DummyItemWithoutText();

        var changed = PopupTextFieldTranslator.TryTranslateTextField(
            item,
            static text => text + " translated");

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(item.label, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslateTextField_SkipsEmptyText()
    {
        var item = new DummyTextItem(string.Empty);

        var changed = PopupTextFieldTranslator.TryTranslateTextField(
            item,
            static _ => "translated");

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(item.text, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslateTextField_CanNormalizeNullTextToEmpty()
    {
        var item = new DummyNullableTextItem(null);

        var changed = PopupTextFieldTranslator.TryTranslateTextField(
            item,
            static text => text,
            translateNullAsEmpty: true);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(item.text, Is.EqualTo(string.Empty));
        });
    }

    private sealed class DummyTextItem
    {
        internal DummyTextItem(string text)
        {
            this.text = text;
        }

        public string text;
    }

    private sealed class DummyNullableTextItem
    {
        internal DummyNullableTextItem(string? text)
        {
            this.text = text;
        }

        public string? text;
    }

    private sealed class DummyItemWithoutText
    {
        public string label { get; } = string.Empty;
    }
}
