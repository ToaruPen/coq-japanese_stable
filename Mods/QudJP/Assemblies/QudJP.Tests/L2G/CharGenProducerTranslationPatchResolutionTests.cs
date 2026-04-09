#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.Loader;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class CharGenProducerTranslationPatchResolutionTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureGameAssemblyLoaded();
    }

    [Test]
    public void BreadcrumbPatch_TargetMethods_ResolveExpectedOverrides()
    {
        AssertTargetMethods(
            "QudJP.Patches.CharGenBreadcrumbTranslationPatch",
            new[]
            {
                "XRL.CharacterBuilds.Qud.UI.QudAttributesModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudBuildLibraryModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudBuildSummaryModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudChartypeModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudChooseStartingLocationModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudCustomizeCharacterModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudCyberneticsModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudGamemodeModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudGenotypeModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudPregenModuleWindow|GetBreadcrumb|",
                "XRL.CharacterBuilds.Qud.UI.QudSubtypeModuleWindow|GetBreadcrumb|",
            });
    }

    [Test]
    public void MenuOptionPatch_TargetMethods_ResolveExpectedOverrides()
    {
        AssertTargetMethods(
            "QudJP.Patches.CharGenMenuOptionTranslationPatch",
            new[]
            {
                "XRL.CharacterBuilds.Qud.UI.QudAttributesModuleWindow|GetKeyMenuBar|",
                "XRL.CharacterBuilds.Qud.UI.QudBuildLibraryModuleWindow|GetKeyMenuBar|",
                "XRL.CharacterBuilds.Qud.UI.QudBuildSummaryModuleWindow|GetKeyMenuBar|",
                "XRL.CharacterBuilds.Qud.UI.QudGamemodeModuleWindow|GetKeyMenuBar|",
                "XRL.CharacterBuilds.Qud.UI.QudMutationsModuleWindow|GetKeyMenuBar|",
            });
    }

    [Test]
    public void SubtypeSelectionPatch_TargetMethods_ResolveExpectedOverrides()
    {
        AssertTargetMethods(
            "QudJP.Patches.CharGenSubtypeSelectionTranslationPatch",
            new[]
            {
                "XRL.CharacterBuilds.Qud.QudSubtypeModule|GetSelections|",
            });
    }

    [Test]
    public void ChromePatch_TargetMethods_ResolveFrameworkHooks()
    {
        AssertTargetMethods(
            "QudJP.Patches.CharGenChromeTranslationPatch",
            new[]
            {
                "XRL.UI.Framework.FrameworkScroller|BeforeShow|XRL.CharacterBuilds.EmbarkBuilderModuleWindowDescriptor|System.Collections.Generic.IEnumerable`1[[XRL.UI.Framework.FrameworkDataElement]]",
                "XRL.UI.Framework.CategoryMenuController|setData|XRL.UI.Framework.FrameworkDataElement",
            });
    }

    [Test]
    public void CustomizePatch_TargetMethods_ResolveExpectedStateMachines()
    {
        var patchType = typeof(Translator).Assembly.GetType("QudJP.Patches.CharGenCustomizeTranslationPatch", throwOnError: false);
        Assert.That(patchType, Is.Not.Null, "Patch type not found: QudJP.Patches.CharGenCustomizeTranslationPatch");

        var targetMethodsMethod = patchType!.GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, $"TargetMethods not found for {patchType.FullName}");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, $"TargetMethods returned null for {patchType.FullName}");

        var methodMarkers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            Assert.That(methodInfo.Name, Is.EqualTo("MoveNext"));
            var declaringTypeName = methodInfo.DeclaringType?.FullName ?? string.Empty;
            if (declaringTypeName.Contains("<GetSelections>", StringComparison.Ordinal))
            {
                methodMarkers.Add("GetSelections");
            }
            else if (declaringTypeName.Contains("<SelectMenuOption>", StringComparison.Ordinal))
            {
                methodMarkers.Add("SelectMenuOption");
            }
            else if (declaringTypeName.Contains("<OnChooseGenderAsync>", StringComparison.Ordinal))
            {
                methodMarkers.Add("OnChooseGenderAsync");
            }
            else if (declaringTypeName.Contains("<OnChoosePronounSetAsync>", StringComparison.Ordinal))
            {
                methodMarkers.Add("OnChoosePronounSetAsync");
            }
            else if (declaringTypeName.Contains("<OnChoosePet>", StringComparison.Ordinal))
            {
                methodMarkers.Add("OnChoosePet");
            }
        }

        Assert.That(
            methodMarkers,
            Is.EquivalentTo(new[]
            {
                "GetSelections",
                "SelectMenuOption",
                "OnChooseGenderAsync",
                "OnChoosePronounSetAsync",
                "OnChoosePet",
            }));
    }

    private static void AssertTargetMethods(string patchTypeName, string[] expectedSignatures)
    {
        var patchType = typeof(Translator).Assembly.GetType(patchTypeName, throwOnError: false);
        Assert.That(patchType, Is.Not.Null, $"Patch type not found: {patchTypeName}");

        var targetMethodsMethod = patchType!.GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethodsMethod, Is.Not.Null, $"TargetMethods not found for {patchType.FullName}");

        var result = targetMethodsMethod!.Invoke(null, null) as System.Collections.IEnumerable;
        Assert.That(result, Is.Not.Null, $"TargetMethods returned null for {patchType.FullName}");

        var actualSignatures = new List<string>();
        var resolvedMethods = new List<MethodInfo>();
        foreach (var item in result!)
        {
            if (item is not MethodInfo methodInfo)
            {
                continue;
            }

            var signature = methodInfo.DeclaringType!.FullName + "|" + methodInfo.Name + "|" + string.Join(
                "|",
                Array.ConvertAll(methodInfo.GetParameters(), static parameter => NormalizeTypeName(parameter.ParameterType.FullName)));
            actualSignatures.Add(signature);
            resolvedMethods.Add(methodInfo);
        }

        Assert.That(actualSignatures, Is.EquivalentTo(expectedSignatures));
        AssertStringMembersRemainResolvable(patchTypeName, resolvedMethods);
    }

    private static string NormalizeTypeName(string? typeName)
    {
        if (typeName is null)
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(typeName, @",\s*[^\[\],]+,\s*Version=[^\]]+", string.Empty);
    }

    private static void AssertStringMembersRemainResolvable(string patchTypeName, IReadOnlyList<MethodInfo> resolvedMethods)
    {
        switch (patchTypeName)
        {
            case "QudJP.Patches.CharGenBreadcrumbTranslationPatch":
                foreach (var methodInfo in resolvedMethods)
                {
                    AssertStringMemberExists(
                        methodInfo.ReturnType,
                        "Title",
                        $"{patchTypeName} return type for {methodInfo.DeclaringType!.FullName}.{methodInfo.Name}");
                }

                return;

            case "QudJP.Patches.CharGenMenuOptionTranslationPatch":
                foreach (var methodInfo in resolvedMethods)
                {
                    var elementType = ResolveEnumerableElementType(methodInfo.ReturnType);
                    Assert.That(
                        elementType,
                        Is.Not.Null,
                        $"{patchTypeName} return type should expose an enumerable element type: {methodInfo.ReturnType.FullName}");
                    AssertStringMemberExists(
                        elementType!,
                        "Description",
                        $"{patchTypeName} enumerable element type for {methodInfo.DeclaringType!.FullName}.{methodInfo.Name}");
                }

                return;

            case "QudJP.Patches.CharGenSubtypeSelectionTranslationPatch":
                foreach (var methodInfo in resolvedMethods)
                {
                    var elementType = ResolveEnumerableElementType(methodInfo.ReturnType);
                    Assert.That(
                        elementType,
                        Is.Not.Null,
                        $"{patchTypeName} return type should expose an enumerable element type: {methodInfo.ReturnType.FullName}");
                    AssertStringMemberExists(
                        elementType!,
                        "Description",
                        $"{patchTypeName} enumerable element type for {methodInfo.DeclaringType!.FullName}.{methodInfo.Name}");
                }

                return;

            case "QudJP.Patches.CharGenChromeTranslationPatch":
                foreach (var methodInfo in resolvedMethods)
                {
                    var parameters = methodInfo.GetParameters();
                    Assert.That(parameters.Length, Is.GreaterThan(0), $"{patchTypeName} expected at least one parameter on {methodInfo.Name}");

                    string? memberName = null;
                    if (string.Equals(methodInfo.Name, "BeforeShow", StringComparison.Ordinal))
                    {
                        memberName = "title";
                    }
                    else if (string.Equals(methodInfo.Name, "setData", StringComparison.Ordinal))
                    {
                        memberName = "Title";
                    }

                    Assert.That(memberName, Is.Not.Null, $"{patchTypeName} encountered unexpected target method {methodInfo.Name}");

                    var runtimeType = parameters[0].ParameterType;
                    if (string.Equals(methodInfo.Name, "setData", StringComparison.Ordinal))
                    {
                        runtimeType = parameters[0].ParameterType.Assembly.GetType(
                            "XRL.UI.Framework.CategoryMenuData",
                            throwOnError: false)
                            ?? runtimeType;
                    }

                    AssertStringMemberExists(
                        runtimeType,
                        memberName!,
                        $"{patchTypeName} first parameter type for {methodInfo.DeclaringType!.FullName}.{methodInfo.Name}");
                }

                return;
        }
    }

    private static void AssertStringMemberExists(Type runtimeType, string memberName, string context)
    {
        var field = runtimeType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field is not null && field.FieldType == typeof(string))
        {
            return;
        }

        var property = runtimeType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is not null && property.PropertyType == typeof(string) && property.CanRead)
        {
            return;
        }

        Assert.Fail($"{context} is missing readable string member '{memberName}' on runtime type '{runtimeType.FullName}'.");
    }

    private static Type? ResolveEnumerableElementType(Type sequenceType)
    {
        if (sequenceType.IsArray)
        {
            return sequenceType.GetElementType();
        }

        if (sequenceType.IsGenericType
            && string.Equals(sequenceType.GetGenericTypeDefinition().FullName, "System.Collections.Generic.IEnumerable`1", StringComparison.Ordinal))
        {
            return sequenceType.GetGenericArguments()[0];
        }

        foreach (var current in sequenceType.GetInterfaces())
        {
            if (current.IsGenericType
                && string.Equals(current.GetGenericTypeDefinition().FullName, "System.Collections.Generic.IEnumerable`1", StringComparison.Ordinal))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
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
