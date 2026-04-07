#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.Loader;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class GameManagerUpdateSelectedAbilityPatchResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureGameAssemblyLoaded();
    }

    [Test]
    public void TargetMethod_ResolvesGameManagerUpdateSelectedAbility()
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.GameManagerUpdateSelectedAbilityPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "GameManagerUpdateSelectedAbilityPatch type not found.");

        var targetMethodMethod = patchType!.GetMethod("TargetMethod", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodMethod, Is.Not.Null, $"TargetMethod not found for {patchType.FullName}");

        var result = targetMethodMethod!.Invoke(null, null) as MethodInfo;
        Assert.That(result, Is.Not.Null, $"TargetMethod returned null for {patchType.FullName}");

        Assert.Multiple(() =>
        {
            Assert.That(result!.DeclaringType?.FullName, Is.EqualTo("GameManager"));
            Assert.That(result.Name, Is.EqualTo("UpdateSelectedAbility"));
            Assert.That(result.GetParameters(), Is.Empty);
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
            "Library/Application Support/Steam/steamapps/common/Caves of Qud/CoQ.app/Contents/Resources/Data/Managed");

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
