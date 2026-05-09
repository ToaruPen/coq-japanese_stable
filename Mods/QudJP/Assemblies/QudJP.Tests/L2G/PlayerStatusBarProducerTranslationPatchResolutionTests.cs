#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class PlayerStatusBarProducerTranslationPatchResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureGameAssemblyLoaded();
    }

    [Test]
    public void TargetMethods_ResolvePlayerStatusBarProducerMethods()
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.PlayerStatusBarProducerTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "PlayerStatusBarProducerTranslationPatch type not found.");

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

            var signature = methodInfo.Name + "|" + string.Join(
                "|",
                Array.ConvertAll(methodInfo.GetParameters(), static parameter => parameter.ParameterType.FullName));
            actualSignatures.Add(signature);
        }

        Assert.That(actualSignatures, Is.EquivalentTo(new[]
        {
            "BeginEndTurn|XRL.Core.XRLCore",
            "Update|",
        }));
    }

    private static string ResolveManagedDirectory()
    {
        foreach (var candidate in EnumerateDefaultManagedDirectories())
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        var envDir = Environment.GetEnvironmentVariable("COQ_MANAGED_DIR");
        if (!string.IsNullOrWhiteSpace(envDir) && Directory.Exists(envDir))
        {
            return envDir;
        }

        Assert.Ignore(
            "Game managed directory not found in OS defaults, and COQ_MANAGED_DIR was unset or missing. " +
            "Set COQ_MANAGED_DIR to your Caves of Qud Managed directory to run game-DLL-backed tests.");
        return string.Empty;
    }

    private static IEnumerable<string> EnumerateDefaultManagedDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                yield return Path.Combine(programFilesX86, "Steam", "steamapps", "common", "Caves of Qud", "CoQ_Data", "Managed");
            }

            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                yield return Path.Combine(programFiles, "Steam", "steamapps", "common", "Caves of Qud", "CoQ_Data", "Managed");
            }

            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            yield return Path.Combine(home, ".steam", "steam", "steamapps", "common", "Caves of Qud", "CoQ_Data", "Managed");
            yield return Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Caves of Qud", "CoQ_Data", "Managed");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam", "steamapps", "common", "Caves of Qud", "CoQ_Data", "Managed");
            yield break;
        }

        yield return Path.Combine(
            home,
            "Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/Managed");
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
