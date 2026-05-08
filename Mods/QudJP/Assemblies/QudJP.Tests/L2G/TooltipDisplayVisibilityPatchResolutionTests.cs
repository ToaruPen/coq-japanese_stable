#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.Loader;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class TooltipDisplayVisibilityPatchResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureManagedAssemblyLoaded("Assembly-CSharp");
    }

    [Test]
    public void TargetMethod_ResolvesModelSharkTooltipDisplay()
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.TooltipDisplayVisibilityPatch");
        Assert.That(patchType, Is.Not.Null, "TooltipDisplayVisibilityPatch is missing.");

        var targetMethodAccessor = patchType!.GetMethod("TargetMethod", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodAccessor, Is.Not.Null, "TargetMethod accessor is missing.");

        var targetMethod = targetMethodAccessor!.Invoke(null, null) as MethodBase;

        Assert.Multiple(() =>
        {
            Assert.That(targetMethod, Is.Not.Null);
            Assert.That(targetMethod!.DeclaringType?.FullName, Is.EqualTo("ModelShark.Tooltip"));
            Assert.That(targetMethod.Name, Is.EqualTo("Display"));
            Assert.That(targetMethod.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(targetMethod.GetParameters()[0].ParameterType, Is.EqualTo(typeof(float)));
        });
    }

    private static Assembly? EnsureManagedAssemblyLoaded(string assemblyName)
    {
        var alreadyLoaded = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal));
        if (alreadyLoaded is not null)
        {
            return alreadyLoaded;
        }

        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
        }
        catch
        {
            return null;
        }
    }
}
#endif
