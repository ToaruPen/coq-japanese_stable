using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class UrchinBelcherTranslationPatchTests
{
    [Test]
    public void Postfix_TranslatesCtorDescriptionAndCommandText()
    {
        var target = new DummyUrchinBelcher
        {
            EventKey = "CommandBelchUrchins",
            Description = "You belch forth various urchins.",
            BelchTable = "UrchinsToBelch",
            CommandName = "Belch Urchins",
            CommandDescription = "You belch forth various urchins.",
        };

        UrchinBelcherTranslationPatch.TranslateForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(target.EventKey, Is.EqualTo("CommandBelchUrchins"));
            Assert.That(target.Description, Is.EqualTo("さまざまなウニを吐き出す。"));
            Assert.That(target.BelchTable, Is.EqualTo("UrchinsToBelch"));
            Assert.That(target.CommandName, Is.EqualTo("ウニを吐く"));
            Assert.That(target.CommandDescription, Is.EqualTo("さまざまなウニを吐き出す。"));
        });
    }

    [Test]
    public void Postfix_PreservesUnknownAndEmptyValuesAndStripsMarkers()
    {
        var target = new DummyUrchinBelcher
        {
            Description = "Unknown belcher description.",
            CommandName = string.Empty,
            CommandDescription = "\u0001You belch forth various urchins.",
        };

        UrchinBelcherTranslationPatch.TranslateForTests(target);

        Assert.Multiple(() =>
        {
            Assert.That(target.Description, Is.EqualTo("Unknown belcher description."));
            Assert.That(target.CommandName, Is.EqualTo(string.Empty));
            Assert.That(target.CommandDescription, Is.EqualTo("さまざまなウニを吐き出す。"));
        });
    }

    private sealed class DummyUrchinBelcher
    {
        public string? EventKey { get; set; }

        public string? Description { get; set; }

        public string? BelchTable { get; set; }

        public string? CommandName { get; set; }

        public string? CommandDescription { get; set; }
    }
}
