using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GolemQuestMoundDisplayOptionsTranslationPatchTests
{
    [Test]
    public void TranslateLiteral_LeavesUnknownEmptyAndMarkedValuesSafe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests("{{W|[Backspace]}} {{y|Build}}"),
                Is.EqualTo("{{W|[Backspace]}} {{y|建造}}"));
            Assert.That(
                GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests("{{K|Build}}"),
                Is.EqualTo("{{K|建造}}"));
            Assert.That(GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests("option:-2"), Is.EqualTo("option:-2"));
            Assert.That(GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests(string.Empty), Is.Empty);
            Assert.That(
                GolemQuestMoundDisplayOptionsTranslationPatch.TranslateLiteralForTests("\u0001{{K|Build}}"),
                Is.EqualTo("{{K|Build}}"));
        });
    }
}
