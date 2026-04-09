#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.Loader;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class AbilityManagerScreenTranslationPatchResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureGameAssemblyLoaded();
    }

    [Test]
    public void TargetMethods_ResolveExpectedHooks()
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.AbilityManagerScreenTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "Patch type not found: QudJP.Patches.AbilityManagerScreenTranslationPatch");

        var targetMethodsMethod = patchType!.GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, $"TargetMethods not found for {patchType.FullName}");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, $"TargetMethods returned null for {patchType.FullName}");

        var actualSignatures = new List<string>();
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            actualSignatures.Add(
                methodInfo.DeclaringType!.FullName + "|" + methodInfo.Name + "|" + string.Join(
                    "|",
                    Array.ConvertAll(methodInfo.GetParameters(), static parameter => NormalizeTypeName(parameter.ParameterType.FullName))));
        }

        Assert.That(
            actualSignatures,
            Is.EquivalentTo(
                new[]
                {
                    "Qud.UI.AbilityManagerScreen|FilterItems|",
                    "Qud.UI.AbilityManagerScreen|UpdateMenuBars|",
                    "Qud.UI.AbilityManagerScreen|HandleHighlightLeft|XRL.UI.Framework.FrameworkDataElement",
                }));
    }

    private static string NormalizeTypeName(string? typeName)
    {
        if (typeName is null)
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(typeName, @",\s*[^\[\],]+,\s*Version=[^\]]+", string.Empty);
    }

    private static Assembly EnsureGameAssemblyLoaded()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(static assembly => string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
        if (assembly is not null)
        {
            return assembly;
        }

        var managedDir = ResolveManagedDirectory();
        var assemblyPath = Path.Combine(managedDir, "Assembly-CSharp.dll");
        if (!File.Exists(assemblyPath))
        {
            Assert.Ignore($"Assembly-CSharp.dll not found at '{assemblyPath}'. Set COQ_MANAGED_DIR to run game-DLL-backed tests.");
            return null!;
        }

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
    }

    private static string ResolveManagedDirectory()
    {
        var envDir = Environment.GetEnvironmentVariable("COQ_MANAGED_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            return envDir;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultDir = Path.Combine(
            home,
            "Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed");

        if (Directory.Exists(defaultDir))
        {
            return defaultDir;
        }

        Assert.Ignore("Game managed directory not found. Set COQ_MANAGED_DIR to run game-DLL-backed tests.");
        return string.Empty;
    }
}
#endif
