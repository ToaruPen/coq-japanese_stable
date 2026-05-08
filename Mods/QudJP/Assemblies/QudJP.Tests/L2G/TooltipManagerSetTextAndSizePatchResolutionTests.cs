#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.Loader;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class TooltipManagerSetTextAndSizePatchResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureManagedAssemblyLoaded("Assembly-CSharp");
#if HAS_TMP
        _ = EnsureManagedAssemblyLoaded("UnityEngine.CoreModule");
        _ = EnsureManagedAssemblyLoaded("Unity.TextMeshPro");
#endif
    }

    [Test]
    public void TargetMethod_ResolvesModelSharkSetTextAndSize()
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.TooltipManagerSetTextAndSizePatch");
        Assert.That(patchType, Is.Not.Null, "TooltipManagerSetTextAndSizePatch is missing.");

        var targetMethodAccessor = patchType!.GetMethod("TargetMethod", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodAccessor, Is.Not.Null, "TargetMethod accessor is missing.");

        var targetMethod = targetMethodAccessor!.Invoke(null, null) as MethodBase;

        Assert.Multiple(() =>
        {
            Assert.That(targetMethod, Is.Not.Null);
            Assert.That(targetMethod!.DeclaringType?.FullName, Is.EqualTo("ModelShark.TooltipManager"));
            Assert.That(targetMethod.Name, Is.EqualTo("SetTextAndSize"));
            Assert.That(targetMethod.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(targetMethod.GetParameters()[0].ParameterType.FullName, Is.EqualTo("ModelShark.TooltipTrigger"));
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
