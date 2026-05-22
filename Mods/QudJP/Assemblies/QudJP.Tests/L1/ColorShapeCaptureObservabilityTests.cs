namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ColorShapeCaptureObservabilityTests
{
    [SetUp]
    public void SetUp()
    {
        ColorShapeCaptureObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ColorShapeCaptureObservability.ResetForTests();
    }

    [Test]
    public void Record_EmitsStructuredColorShapeArtifact()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            ColorShapeCaptureObservability.Record(
                "InventoryLineTranslationPatch > field=text",
                "InventoryLine.GameObjectDisplayName",
                "{{c|chem cell}} {{y|[{{g|fresh water}}]}}",
                "{{c|ケムセル}} {{y|[{{g|清水}}]}}"));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("ColorShapeProbe/v1"));
            Assert.That(output, Does.Contain("route='InventoryLineTranslationPatch'"));
            Assert.That(output, Does.Contain("producer='InventoryLine.GameObjectDisplayName'"));
            Assert.That(output, Does.Contain("source_visible='chem cell [fresh water]'"));
            Assert.That(output, Does.Contain("final_visible='ケムセル [清水]'"));
            Assert.That(output, Does.Contain("; source_color_spans=0:{{c|"));
            Assert.That(output, Does.Contain("; final_color_spans=0:{{c|"));
            Assert.That(output, Does.Contain("; markup_semantic_status=clean;"));
            Assert.That(
                ColorShapeCaptureObservability.GetRouteProducerHitCountForTests(
                    "InventoryLineTranslationPatch",
                    "InventoryLine.GameObjectDisplayName"),
                Is.EqualTo(1));
        });
    }
}
