using System;
using System.IO;

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

    [Test]
    public void GetPropertyOrFieldValue_CachesResolvedMemberAccessor()
    {
        ReflectionUtils.ClearAccessorCacheForTests();
        var target = new ReflectionTarget { Name = "alpha" };

        Assert.That(ReflectionUtils.GetPropertyOrFieldValue(target, "Name"), Is.EqualTo("alpha"));
        Assert.That(ReflectionUtils.GetPropertyOrFieldValue(target, "Name"), Is.EqualTo("alpha"));

        Assert.That(ReflectionUtils.GetAccessorCacheCountForTests(), Is.EqualTo(1));
    }

    [Test]
    public void InventoryLineTranslationPatch_UsesSharedReflectionUtilsForHotMembers()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryLineTranslationPatch.cs");
        var source = File.ReadAllText(sourcePath);
        var method = ExtractMethodBody(source, "private static object? GetMemberValue");

        Assert.That(method, Does.Contain("ReflectionUtils.GetPropertyOrFieldValue(instance, memberName)"));
        Assert.That(method, Does.Not.Contain("AccessTools.Property(type, memberName)"));
        Assert.That(method, Does.Not.Contain("AccessTools.Field(type, memberName)"));
    }

    [Test]
    public void UITextSkinReflectionAccessor_CachesTextWriteStrategiesByType()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "UITextSkinReflectionAccessor.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("ConcurrentDictionary<Type,"));
        Assert.That(source, Does.Contain("GetOrAdd"));
        Assert.That(source, Does.Contain("SetText"));
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "method signature not found: " + signature);
        var braceStart = source.IndexOf('{', start);
        Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), "method body not found: " + signature);

        var depth = 0;
        for (var index = braceStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(braceStart, index - braceStart + 1);
                }
            }
        }

        Assert.Fail("method body did not terminate: " + signature);
        return string.Empty;
    }

    private sealed class PrivatePropertyTarget
    {
        private string Secret => "private-property";
    }

    private sealed class InternalFieldTarget
    {
        internal readonly string State = "internal-field";
    }

    private sealed class ReflectionTarget
    {
        public string Name { get; set; } = string.Empty;
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
