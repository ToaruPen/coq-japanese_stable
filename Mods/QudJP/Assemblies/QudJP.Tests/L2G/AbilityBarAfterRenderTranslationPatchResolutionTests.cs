#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.Loader;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class AbilityBarAfterRenderTranslationPatchResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureGameAssemblyLoaded();
    }

    [Test]
    public void TargetMethod_ResolvesAbilityBarAfterRender()
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.AbilityBarAfterRenderTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "AbilityBarAfterRenderTranslationPatch type not found.");

        var targetMethodMethod = patchType!.GetMethod("TargetMethod", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodMethod, Is.Not.Null, $"TargetMethod not found for {patchType.FullName}");

        var result = targetMethodMethod!.Invoke(null, null) as MethodInfo;
        Assert.That(result, Is.Not.Null, $"TargetMethod returned null for {patchType.FullName}");

        Assert.Multiple(() =>
        {
            Assert.That(result!.DeclaringType?.FullName, Is.EqualTo("Qud.UI.AbilityBar"));
            Assert.That(result.Name, Is.EqualTo("AfterRender"));
            Assert.That(
                Array.ConvertAll(result.GetParameters(), static parameter => parameter.ParameterType.FullName),
                Is.EqualTo(new[]
                {
                    "XRL.Core.XRLCore",
                    "ConsoleLib.Console.ScreenBuffer",
                }));
        });
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
            "Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/Managed");

        if (Directory.Exists(defaultDir))
        {
            return defaultDir;
        }

        Assert.Ignore("Game managed directory not found. Set COQ_MANAGED_DIR to run game-DLL-backed tests.");
        return string.Empty;
    }

    private static Assembly EnsureGameAssemblyLoaded()
    {
        var loadedAssembly = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            static assembly => string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        var managedDir = ResolveManagedDirectory();
        var assemblyPath = Path.Combine(managedDir, "Assembly-CSharp.dll");

        Assert.That(File.Exists(assemblyPath), Is.True, $"Assembly-CSharp.dll not found at {assemblyPath}");
        loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        Assert.That(loadedAssembly.GetType("XRL.World.GameObject", throwOnError: false), Is.Not.Null);
        return loadedAssembly;
    }
}
#endif
