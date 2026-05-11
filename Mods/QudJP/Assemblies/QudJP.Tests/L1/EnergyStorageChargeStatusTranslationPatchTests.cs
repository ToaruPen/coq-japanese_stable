using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class EnergyStorageChargeStatusTranslationPatchTests
{
    [Test]
    public void TryTranslateChargeStatus_TranslatesFullWithoutUsingUiFullLeaf()
    {
        var changed = EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(
            "{{G|Full}}",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("{{G|満充電}}"));
            Assert.That(translated, Does.Not.Contain("全画面"));
        });
    }

    [TestCase("{{K|Drained}}", "{{K|空}}")]
    [TestCase("{{r|Very Low}}", "{{r|残量ごく少}}")]
    [TestCase("{{R|Low}}", "{{R|残量少}}")]
    [TestCase("{{W|Used}}", "{{W|使用済み}}")]
    [TestCase("{{g|Fresh}}", "{{g|残量多}}")]
    [TestCase("{{G|Full}}", "{{G|満充電}}")]
    [TestCase("{{G|Fully Wound}}", "{{G|完全に巻かれている}}")]
    [TestCase("{{G|Full Speed}}", "{{G|最高速}}")]
    [TestCase("{{G|Fully Tensed}}", "{{G|完全に張っている}}")]
    [TestCase("{{G|Bright}}", "{{G|明るい}}")]
    [TestCase("{{G|Pure Black}}", "{{G|純黒}}")]
    [TestCase("{{G|Vigorous}}", "{{G|活力十分}}")]
    public void TryTranslateChargeStatus_TranslatesKnownStatusFamilies(string source, string expected)
    {
        var changed = EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("{{G|~100%}}")]
    [TestCase("{{G|100%}}")]
    [TestCase("{{G|全画面}}")]
    [TestCase("")]
    public void TryTranslateChargeStatus_LeavesNonOwnedValuesUnchanged(string source)
    {
        var changed = EnergyStorageChargeStatusTranslationPatch.TryTranslateChargeStatus(source, out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }
}
