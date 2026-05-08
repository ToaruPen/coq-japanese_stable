using System.Reflection;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class TooltipTextRepairerTests
{
    [Test]
    public void NormalizeVisibilityLimits_ReopensHiddenTmpPages()
    {
        var method = RequireTooltipTextRepairerMethod("NormalizeVisibilityLimits");

        var result = method.Invoke(null, new object[] { 0, 0, 0 });

        Assert.That(result, Is.EqualTo((int.MaxValue, int.MaxValue, 1)));
    }

    [Test]
    public void CanRepairTextForTests_SkipsInactiveAndEmptyText()
    {
        var method = RequireTooltipTextRepairerMethod("CanRepairText");

        Assert.Multiple(() =>
        {
            Assert.That(method.Invoke(null, new object?[] { true, true, "日本語", "LongDescription" }), Is.EqualTo(true));
            Assert.That(method.Invoke(null, new object?[] { true, false, "日本語", "LongDescription" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { true, true, string.Empty, "LongDescription" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { true, true, "日本語", "QudJPReplacementText" }), Is.EqualTo(false));
        });
    }

    [Test]
    public void IsLookerTooltipNameForTests_MatchesOnlyPolatLooker()
    {
        var method = RequireTooltipTextRepairerMethod("IsLookerTooltipName");

        Assert.Multiple(() =>
        {
            Assert.That(method.Invoke(null, new object?[] { "PolatLooker" }), Is.EqualTo(true));
            Assert.That(method.Invoke(null, new object?[] { "UI Manager/Tooltip Container/PolatLooker" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { "DualPolatLooker" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { "PolatLooker:Debug" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { "TileTooltip" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { null }), Is.EqualTo(false));
        });
    }

    [Test]
    public void ShouldRepairTooltipNameForTests_LimitsRepairToPolatLooker()
    {
        var method = RequireTooltipTextRepairerMethod("ShouldRepairTooltipName");

        Assert.Multiple(() =>
        {
            Assert.That(method.Invoke(null, new object?[] { "PolatLooker" }), Is.EqualTo(true));
            Assert.That(method.Invoke(null, new object?[] { "TileTooltip" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { "GenericModelSharkTooltip" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { "DualPolatLooker" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { "PolatLooker:Debug" }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object?[] { null }), Is.EqualTo(false));
        });
    }

    private static MethodInfo RequireTooltipTextRepairerMethod(string methodName)
    {
        var type = typeof(Translator).Assembly.GetType("QudJP.TooltipTextRepairer");
        Assert.That(type, Is.Not.Null, "QudJP.TooltipTextRepairer is missing.");

        var method = type!.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, methodName + " is missing.");
        return method!;
    }
}
