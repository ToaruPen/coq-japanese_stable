namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class GameTypeResolverTests
{
    [SetUp]
    public void SetUp()
    {
        GameTypeResolver.ResetCacheForTests();
    }

    [TearDown]
    public void TearDown()
    {
        GameTypeResolver.ResetCacheForTests();
    }

    [Test]
    public void FindType_ReturnsFullNameMatch_WhenAvailable()
    {
        var resolved = GameTypeResolver.FindType(typeof(GameTypeResolverTests).FullName!, "DefinitelyNotTheSimpleName");

        Assert.That(resolved, Is.EqualTo(typeof(GameTypeResolverTests)));
    }

    [Test]
    public void FindType_FallsBackToSimpleName_WhenFullNameIsMissing()
    {
        var resolved = GameTypeResolver.FindType("QudJP.Tests.L1.Does.Not.Exist", nameof(GameTypeResolverTests));

        Assert.That(resolved, Is.EqualTo(typeof(GameTypeResolverTests)));
    }

    [Test]
    public void FindType_ReturnsNull_WhenNeitherNameResolves()
    {
        var resolved = GameTypeResolver.FindType("QudJP.Tests.L1.Does.Not.Exist", "NoSuchSimpleTypeName");

        Assert.That(resolved, Is.Null);
    }

    [Test]
    public void FindType_LogsWarning_WhenNeitherNameResolves()
    {
        const string fullTypeName = "QudJP.Tests.L1.Does.Not.Exist";
        const string simpleTypeName = "NoSuchSimpleTypeName";

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(GameTypeResolver.FindType(fullTypeName, simpleTypeName), Is.Null));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("GameTypeResolver failed to resolve type"));
            Assert.That(output, Does.Contain(fullTypeName));
            Assert.That(output, Does.Contain(simpleTypeName));
        });
    }

    [Test]
    public void FindType_CachesFallbackSimpleNameResolution()
    {
        var first = GameTypeResolver.FindType("QudJP.Tests.L1.Does.Not.Exist", nameof(GameTypeResolverTests));
        var second = GameTypeResolver.FindType("QudJP.Tests.L1.Does.Not.Exist", nameof(GameTypeResolverTests));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(typeof(GameTypeResolverTests)));
            Assert.That(second, Is.EqualTo(typeof(GameTypeResolverTests)));
            Assert.That(GameTypeResolver.FallbackScanCountForDiagnostics, Is.EqualTo(1));
        });
    }

    [Test]
    public void FindType_DoesNotCacheMisses_BecauseLaterAssemblyLoadsCanResolveThem()
    {
        const string fullTypeName = "QudJP.Tests.L1.Does.Not.Exist";
        const string simpleTypeName = "NoSuchSimpleTypeName";

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            Assert.That(GameTypeResolver.FindType(fullTypeName, simpleTypeName), Is.Null);
            Assert.That(GameTypeResolver.FindType(fullTypeName, simpleTypeName), Is.Null);
        });

        Assert.Multiple(() =>
        {
            Assert.That(GameTypeResolver.FallbackScanCountForDiagnostics, Is.EqualTo(2));
            Assert.That(
                System.Text.RegularExpressions.Regex.Count(output, "GameTypeResolver failed to resolve type"),
                Is.EqualTo(2));
        });
    }
}
