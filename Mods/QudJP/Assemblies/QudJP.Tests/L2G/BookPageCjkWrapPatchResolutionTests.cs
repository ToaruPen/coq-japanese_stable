#if HAS_GAME_DLL
using System.Reflection;
using QudJP.Patches;
using XRL.UI;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class BookPageCjkWrapPatchResolutionTests
{
    [Test]
    public void TargetMethod_ResolvesBookPageStringConstructor()
    {
        _ = typeof(BookPage);
        var targetMethodAccessor = typeof(BookPageCjkWrapPatch).GetMethod(
            "TargetMethod",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodAccessor, Is.Not.Null, "BookPageCjkWrapPatch.TargetMethod is missing.");

        var target = targetMethodAccessor!.Invoke(null, null);

        Assert.Multiple(() =>
        {
            Assert.That(target, Is.InstanceOf<ConstructorInfo>());
            var constructor = (ConstructorInfo)target!;
            Assert.That(constructor.DeclaringType?.FullName, Is.EqualTo("XRL.UI.BookPage"));
            var parameterTypes = Array.ConvertAll(constructor.GetParameters(), static parameter => parameter.ParameterType.FullName);
            Assert.That(parameterTypes, Is.EqualTo(new[] { "System.String", "System.String" }));
        });
    }
}
#endif
