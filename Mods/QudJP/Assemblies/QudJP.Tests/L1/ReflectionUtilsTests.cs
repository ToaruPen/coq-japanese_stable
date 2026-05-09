namespace QudJP.Tests.L1;

#pragma warning disable CA1823, CS0414, S1144, S2094, S2325

[TestFixture]
[Category("L1")]
public sealed class ReflectionUtilsTests
{
    [Test]
    public void GetPropertyOrFieldValue_ReadsPrivateProperty()
    {
        Assert.That(
            ReflectionUtils.GetPropertyOrFieldValue(new PrivatePropertyTarget(), "Secret"),
            Is.EqualTo("private-property"));
    }

    [Test]
    public void GetPropertyOrFieldValue_ReadsInternalField()
    {
        Assert.That(
            ReflectionUtils.GetPropertyOrFieldValue(new InternalFieldTarget(), "State"),
            Is.EqualTo("internal-field"));
    }

    [Test]
    public void GetPropertyOrFieldValue_WalksBaseTypes()
    {
        var target = new DerivedTarget();

        Assert.Multiple(() =>
        {
            Assert.That(
                ReflectionUtils.GetPropertyOrFieldValue(target, "BaseState"),
                Is.EqualTo("inherited-private-field"));
            Assert.That(
                ReflectionUtils.GetPropertyOrFieldValue(target, "InheritedProperty"),
                Is.EqualTo("inherited-protected-property"));
        });
    }

    [Test]
    public void GetPropertyOrFieldValue_IgnoresIndexedProperties()
    {
        Assert.That(
            ReflectionUtils.GetPropertyOrFieldValue(new IndexedPropertyTarget(), "Item"),
            Is.Null);
    }

    [Test]
    public void GetPropertyOrFieldValue_ReturnsNullForNullInstance()
    {
        Assert.That(
            ReflectionUtils.GetPropertyOrFieldValue(null, "Anything"),
            Is.Null);
    }

    [Test]
    public void GetPropertyOrFieldValue_ReturnsNullForMissingMember()
    {
        Assert.That(
            ReflectionUtils.GetPropertyOrFieldValue(new PrivatePropertyTarget(), "Missing"),
            Is.Null);
    }

    private sealed class PrivatePropertyTarget
    {
        private string Secret => "private-property";
    }

    private sealed class InternalFieldTarget
    {
        internal readonly string State = "internal-field";
    }

    private class BaseTarget
    {
        private readonly string BaseState = "inherited-private-field";

        protected string InheritedProperty => "inherited-protected-property";
    }

    private sealed class DerivedTarget : BaseTarget
    {
    }

    private sealed class IndexedPropertyTarget
    {
        public string this[int index] => "indexed-property";
    }
}

#pragma warning restore CA1823, CS0414, S1144, S2094, S2325
