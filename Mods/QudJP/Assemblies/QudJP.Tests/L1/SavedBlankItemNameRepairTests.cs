using System.Xml.Linq;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class SavedBlankItemNameRepairTests
{
    // Historical markup-only DisplayName changes in 2d9826fc (#841).
    private static readonly (string Blueprint, string Legacy, string Repaired)[] Repairs =
    [
        ("SalthopperMandible", "{{G|}}", "{{G|ソルトホッパーの大顎}}"),
        ("Red Security Card", "{{r|}}", "{{r|労働者用セキュリティカード}}"),
        ("Green Security Card", "{{G|}}", "{{G|緊急サービス用セキュリティカード}}"),
        ("Blue Security Card", "{{B|}}", "{{B|法執行機関用セキュリティカード}}"),
        ("Purple Security Card", "{{M|}}", "{{M|軍用セキュリティカード}}"),
        ("Copper Trollking Key", "{{w|}}", "{{w|青銅}}の鍵"),
        ("Silver Trollking Key", "{{silvery|}}", "{{silvery|銀}}の鍵"),
        ("BarathrumKey", "{{c|}}", "{{c|クローム}}の鍵"),
        ("GritGateGridKey", "{{c|}}", "{{c|クローム}}のセキュリティカード"),
        ("CrystalKey", "{{m|}}", "{{m|水晶}}の鍵"),
    ];

    [Test]
    public void Repair_RestoresEveryHistoricalMarkupOnlyName()
    {
        Assert.That(Repairs, Has.Length.EqualTo(10));
        Assert.Multiple(() =>
        {
            foreach (var (blueprint, legacy, repaired) in Repairs)
            {
                Assert.That(SavedBlankItemNameRepair.IsKnownBlueprint(blueprint), Is.True, blueprint);
                Assert.That(SavedBlankItemNameRepair.Repair(blueprint, legacy), Is.EqualTo(repaired), blueprint);
            }
        });
    }

    [Test]
    public void Repair_AllReplacementsMatchCurrentItemsXml()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "../../../../../Localization/ObjectBlueprints/Items.jp.xml"));
        var document = XDocument.Load(path);

        Assert.Multiple(() =>
        {
            foreach (var (blueprint, legacy, repaired) in Repairs)
            {
                var item = document.Root!.Elements("object").Single(item => (string?)item.Attribute("Name") == blueprint);
                var render = item.Elements("part").Single(part => (string?)part.Attribute("Name") == "Render");
                Assert.That((string?)render.Attribute("DisplayName"), Is.EqualTo(repaired), blueprint);
                Assert.That(SavedBlankItemNameRepair.Repair(blueprint, legacy), Is.EqualTo(repaired), blueprint);
            }
        });
    }

    [Test]
    public void Repair_IsIdempotentForEveryReplacement()
    {
        Assert.Multiple(() =>
        {
            foreach (var (blueprint, legacy, repaired) in Repairs)
            {
                var first = SavedBlankItemNameRepair.Repair(blueprint, legacy);
                var second = SavedBlankItemNameRepair.Repair(blueprint, first);
                Assert.That(second, Is.EqualTo(repaired), blueprint);
                Assert.That(second, Is.SameAs(first), blueprint);
            }
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Unknown")]
    [TestCase("red security card")]
    [TestCase("Red Security Card ")]
    [TestCase("Yellow Security Card")]
    [TestCase("Gold Trollking Key")]
    public void Repair_LeavesUnknownAndExcludedBlueprintsUnchanged(string? blueprint)
    {
        const string source = "{{r|}}";
        Assert.Multiple(() =>
        {
            Assert.That(SavedBlankItemNameRepair.IsKnownBlueprint(blueprint), Is.False);
            Assert.That(SavedBlankItemNameRepair.Repair(blueprint, source), Is.SameAs(source));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("red security card")]
    [TestCase("{{r|労働者用セキュリティカード}}")]
    [TestCase("{{Y|私の宝物}}")]
    [TestCase("{{y|}}")]
    [TestCase("{{R|}}")]
    [TestCase("{{G|}}")]
    [TestCase("{{Y| }}")]
    [TestCase("{{r|}} ")]
    [TestCase("\u0001{{r|}}")]
    [TestCase("&Y")]
    public void Repair_LeavesEveryNonexactDisplayNameUnchanged(string? source)
    {
        Assert.That(SavedBlankItemNameRepair.Repair("Red Security Card", source), Is.SameAs(source));
    }
}
