using QudJP.Patches;

namespace QudJP.Tests.L1;

#pragma warning disable S1144, S2094, S2325

[TestFixture]
[Category("L1")]
public sealed class FrameworkDataElementSetDataTargetResolverTests
{
    [Test]
    public void Resolve_ReturnsExactSetDataMethod()
    {
        var method = FrameworkDataElementSetDataTargetResolver.Resolve(
            "DummyPatch",
            typeof(DummyLine),
            typeof(DummyFrameworkDataElement));

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.GetParameters()[0].ParameterType, Is.EqualTo(typeof(DummyFrameworkDataElement)));
    }

    [Test]
    public void Resolve_LogsMissingExactSetDataMethod()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            var method = FrameworkDataElementSetDataTargetResolver.Resolve(
                "DummyPatch",
                typeof(DummyWithoutExactSetData),
                typeof(DummyFrameworkDataElement));

            Assert.That(method, Is.Null);
        });

        Assert.That(output, Does.Contain("QudJP: DummyPatch.setData(FrameworkDataElement) not found."));
    }

    private sealed class DummyFrameworkDataElement
    {
        public string? Description { get; set; }
    }

    private sealed class DummyLine
    {
        public void setData(DummyFrameworkDataElement data)
        {
            _ = data;
        }

        public void setData(object data)
        {
            _ = data;
        }
    }

    private sealed class DummyWithoutExactSetData
    {
        public void setData(string data)
        {
            _ = data;
        }
    }
}

#pragma warning restore S1144, S2094, S2325
