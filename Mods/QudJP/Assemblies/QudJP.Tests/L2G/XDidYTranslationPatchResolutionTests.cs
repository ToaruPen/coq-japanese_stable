#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

#pragma warning disable S3011

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
[NonParallelizable]
public sealed class XDidYTranslationPatchResolutionTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryPath = null!;
    private string? lastMessage;
    private object? originalXrlCore;
    private bool restoreXrlCore;
    private static bool? patchedInvokeContinueOriginal;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _ = EnsureGameAssemblyLoaded();
    }

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-xdidy-l2g", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryPath = Path.Combine(tempDirectory, "verbs.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "ui-test.ja.json"), "{\"entries\":[]}\n", Utf8WithoutBom);
        MessageFrameTranslator.ResetForTests();
        MessageFrameTranslator.SetDictionaryPathForTests(dictionaryPath);
        XDidYTranslationPatch.SetMessageDispatcherForTests((_, message, _, _) => lastMessage = message);
        lastMessage = null;
        StubXrlCoreWhenMissing();
    }

    [TearDown]
    public void TearDown()
    {
        XDidYTranslationPatch.SetMessageDispatcherForTests(null);
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }

        if (restoreXrlCore)
        {
            ResolveXrlCoreField().SetValue(null, originalXrlCore);
            restoreXrlCore = false;
        }
    }

    [TestCase("XDidY")]
    [TestCase("XDidYToZ")]
    [TestCase("WDidXToYWithZ")]
    public void TargetMethods_ResolveMessagingOwnerMethods(string methodName)
    {
        var targetMethods = typeof(XDidYTranslationPatch)
            .GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethods, Is.Not.Null);

        var methods = ((IEnumerable<MethodBase>)targetMethods!.Invoke(null, null)!).ToArray();

        Assert.That(
            methods.Any(method =>
                string.Equals(method.Name, methodName, StringComparison.Ordinal)
                && string.Equals(method.DeclaringType?.FullName, "XRL.World.Capabilities.Messaging", StringComparison.Ordinal)),
            Is.True,
            $"Messaging.{methodName} was not resolved by XDidYTranslationPatch.TargetMethods().");
    }

    [TestCase("block", "熊", "\u0001熊は防いだ。", false)]
    [TestCase("teleport", "熊", null, true)]
    [TestCase("", "熊", null, true)]
    [TestCase("block", "{{W|熊}}", "\u0001{{W|熊}}は防いだ。", false)]
    [TestCase("block", "\u0001熊", "\u0001熊は防いだ。", false)]
    public void PrefixXDidY_ResolvedMessagingMethod_ProducesExpectedJapaneseOutput(
        string verb,
        string subjectOverride,
        string? expectedMessage,
        bool expectedContinueOriginal)
    {
        WriteDictionary(tier1: new[] { ("block", "防いだ") });
        var messagingMethod = ResolveMessagingMethod("XDidY");
        var invokeArgs = BuildMessagingArgs(
            messagingMethod,
            new Dictionary<string, object?>
            {
                ["Actor"] = null,
                ["Verb"] = verb,
                ["SubjectOverride"] = subjectOverride,
                ["AlwaysVisible"] = true,
            });

        RunWithMessagingPrefix(messagingMethod, () => messagingMethod.Invoke(null, invokeArgs));

        Assert.Multiple(() =>
        {
            Assert.That(patchedInvokeContinueOriginal, Is.EqualTo(expectedContinueOriginal));
            Assert.That(lastMessage, Is.EqualTo(expectedMessage));
        });
    }

    [Test]
    public void PrefixXDidY_ResolvedMessagingMethod_UsesRealGameObjectOneForSubject()
    {
        WriteUiDictionary(("CanvasWall", "帆布壁"));
        WriteDictionary(tier1: new[] { ("collapse", "崩れた") });

        var actor = CreateGameObjectWithDisplayName("CanvasWall");
        var messagingMethod = ResolveMessagingMethod("XDidY");
        var oneMethod = actor.GetType().GetMethod("One", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(oneMethod, Is.Not.Null);
        Assert.That(
            oneMethod!.Invoke(actor, BuildDisplayNameMethodArgs(oneMethod, useFullNames: false, indefiniteArticle: false)),
            Is.EqualTo("CanvasWall"));
        var invokeArgs = BuildMessagingArgs(
            messagingMethod,
            new Dictionary<string, object?>
            {
                ["Actor"] = actor,
                ["Verb"] = "collapse",
                ["AlwaysVisible"] = true,
            });

        RunWithMessagingPrefix(messagingMethod, () => messagingMethod.Invoke(null, invokeArgs));

        Assert.Multiple(() =>
        {
            Assert.That(patchedInvokeContinueOriginal, Is.False);
            Assert.That(lastMessage, Is.EqualTo("\u0001帆布壁は崩れた。"));
        });
    }

    [TestCase("One", true, false)]
    [TestCase("one", false, true)]
    public void TryBuildDisplayNameMethodArgs_AcceptsRealGameObjectDisplayNameSignatures(
        string methodName,
        bool useFullNames,
        bool indefiniteArticle)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var gameObjectType = assembly.GetType("XRL.World.GameObject", throwOnError: false);
        Assert.That(gameObjectType, Is.Not.Null);

        var displayNameMethod = gameObjectType!
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(method =>
                string.Equals(method.Name, methodName, StringComparison.Ordinal)
                && method.ReturnType == typeof(string)
                && method.GetParameters().Length >= 13);

        var buildArgsMethod = typeof(XDidYTranslationPatch)
            .GetMethod("TryBuildDisplayNameMethodArgs", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(buildArgsMethod, Is.Not.Null);

        var invokeArgs = new object?[] { displayNameMethod, useFullNames, indefiniteArticle, null };
        var accepted = (bool)buildArgsMethod!.Invoke(null, invokeArgs)!;
        Assert.That(accepted, Is.True);

        var builtArgs = invokeArgs[3] as object?[];
        Assert.That(builtArgs, Is.Not.Null);

        var displayNameArgs = builtArgs!;
        var parameters = displayNameMethod.GetParameters();

        Assert.Multiple(() =>
        {
            Assert.That(displayNameArgs, Has.Length.EqualTo(parameters.Length));
            Assert.That(GetArg(parameters, displayNameArgs, "WithoutTitles"), Is.EqualTo(!useFullNames));
            Assert.That(GetArg(parameters, displayNameArgs, "Short"), Is.EqualTo(!useFullNames));
            Assert.That(GetArg(parameters, displayNameArgs, "WithIndefiniteArticle"), Is.EqualTo(indefiniteArticle));
        });
    }

    private void WriteDictionary(IEnumerable<(string verb, string text)>? tier1 = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"entries\": [],");
        builder.AppendLine("  \"tier1\": [");
        WriteTier1(builder, tier1);
        builder.AppendLine("  ],");
        builder.AppendLine("  \"tier2\": [],");
        builder.AppendLine("  \"tier3\": []");
        builder.AppendLine("}");

        File.WriteAllText(dictionaryPath, builder.ToString(), Utf8WithoutBom);
    }

    private void WriteUiDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"")
                .Append(EscapeJson(entries[index].key))
                .Append("\",\"text\":\"")
                .Append(EscapeJson(entries[index].text))
                .Append("\"}");
        }

        builder.AppendLine("]}");
        File.WriteAllText(Path.Combine(tempDirectory, "ui-test.ja.json"), builder.ToString(), Utf8WithoutBom);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private static void WriteTier1(StringBuilder builder, IEnumerable<(string verb, string text)>? entries)
    {
        if (entries is null)
        {
            return;
        }

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
            {
                builder.AppendLine(",");
            }

            first = false;
            builder.Append("    { \"verb\": \"")
                .Append(EscapeJson(entry.verb))
                .Append("\", \"text\": \"")
                .Append(EscapeJson(entry.text))
                .Append("\" }");
        }

        if (!first)
        {
            builder.AppendLine();
        }
    }

    private static MethodInfo ResolveMessagingMethod(string methodName)
    {
        var targetMethods = typeof(XDidYTranslationPatch)
            .GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(targetMethods, Is.Not.Null);

        var methods = ((IEnumerable<MethodBase>)targetMethods!.Invoke(null, null)!).ToArray();
        var method = methods.OfType<MethodInfo>().Single(candidate =>
            string.Equals(candidate.Name, methodName, StringComparison.Ordinal)
            && string.Equals(candidate.DeclaringType?.FullName, "XRL.World.Capabilities.Messaging", StringComparison.Ordinal));
        return method;
    }

    private static object?[] BuildMessagingArgs(MethodInfo method, IReadOnlyDictionary<string, object?> overrides)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            if (parameter.Name is not null && overrides.TryGetValue(parameter.Name, out var value))
            {
                args[index] = value;
                continue;
            }

            args[index] = GetDefaultParameterValue(parameter);
        }

        return args;
    }

    private void StubXrlCoreWhenMissing()
    {
        var coreField = ResolveXrlCoreField();
        originalXrlCore = coreField.GetValue(null);
        if (originalXrlCore is not null)
        {
            return;
        }

        var assembly = EnsureGameAssemblyLoaded();
        var coreType = assembly.GetType("XRL.Core.XRLCore", throwOnError: true)!;
        coreField.SetValue(null, Activator.CreateInstance(coreType));
        restoreXrlCore = true;
    }

    private static FieldInfo ResolveXrlCoreField()
    {
        var assembly = EnsureGameAssemblyLoaded();
        var coreType = assembly.GetType("XRL.Core.XRLCore", throwOnError: true)!;
        var coreField = coreType.GetField("Core", BindingFlags.Public | BindingFlags.Static);
        Assert.That(coreField, Is.Not.Null);
        return coreField!;
    }

    private static object CreateGameObjectWithDisplayName(string displayName)
    {
        var assembly = EnsureGameAssemblyLoaded();
        var gameObjectType = assembly.GetType("XRL.World.GameObject", throwOnError: true)!;
        var renderType = assembly.GetType("XRL.World.Parts.Render", throwOnError: true)!;
        var actor = Activator.CreateInstance(gameObjectType)!;
        var render = Activator.CreateInstance(renderType)!;

        var displayNameField = renderType.GetField("DisplayName", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(displayNameField, Is.Not.Null);
        displayNameField!.SetValue(render, displayName);

        var renderField = gameObjectType.GetField("Render", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(renderField, Is.Not.Null);
        renderField!.SetValue(actor, render);

        var setIntPropertyMethod = gameObjectType.GetMethod(
            "SetIntProperty",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            new[] { typeof(string), typeof(int), typeof(bool) },
            modifiers: null);
        Assert.That(setIntPropertyMethod, Is.Not.Null);
        setIntPropertyMethod!.Invoke(actor, new object?[] { "ProperNoun", 1, false });

        var setStringPropertyMethod = gameObjectType.GetMethod(
            "SetStringProperty",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            new[] { typeof(string), typeof(string), typeof(bool) },
            modifiers: null);
        Assert.That(setStringPropertyMethod, Is.Not.Null);
        setStringPropertyMethod!.Invoke(actor, new object?[] { "DefiniteArticle", string.Empty, false });
        setStringPropertyMethod.Invoke(actor, new object?[] { "IndefiniteArticle", string.Empty, false });

        return actor;
    }

    private static object?[] BuildDisplayNameMethodArgs(
        MethodInfo displayNameMethod,
        bool useFullNames,
        bool indefiniteArticle)
    {
        var buildArgsMethod = typeof(XDidYTranslationPatch)
            .GetMethod("TryBuildDisplayNameMethodArgs", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(buildArgsMethod, Is.Not.Null);

        var invokeArgs = new object?[] { displayNameMethod, useFullNames, indefiniteArticle, null };
        var accepted = (bool)buildArgsMethod!.Invoke(null, invokeArgs)!;
        Assert.That(accepted, Is.True);

        var builtArgs = invokeArgs[3] as object?[];
        Assert.That(builtArgs, Is.Not.Null);
        return builtArgs!;
    }

    private static void RunWithMessagingPrefix(MethodBase original, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        patchedInvokeContinueOriginal = null;

        try
        {
            harmony.Patch(
                original,
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(XDidYTranslationPatchResolutionTests),
                    nameof(PrefixAndStopOriginalForTests))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static bool PrefixAndStopOriginalForTests(MethodBase __originalMethod, object[] __args)
    {
        patchedInvokeContinueOriginal = XDidYTranslationPatch.Prefix(__originalMethod, __args);
        return false;
    }

    private static object? GetArg(ParameterInfo[] parameters, object?[] args, string name)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (string.Equals(parameters[index].Name, name, StringComparison.Ordinal))
            {
                return args[index];
            }
        }

        Assert.Fail($"Parameter not found: {name}");
        return null;
    }

    private static object? GetDefaultParameterValue(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue is DBNull ? null : parameter.DefaultValue;
        }

        return parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : null;
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.xdidy.l2g.{Guid.NewGuid():N}";
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
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
