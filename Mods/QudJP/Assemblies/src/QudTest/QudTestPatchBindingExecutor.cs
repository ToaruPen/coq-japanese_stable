using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace QudJP.QudTest;

#pragma warning disable S3011
internal static class QudTestPatchBindingExecutor
{
    private const BindingFlags TargetBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly IReadOnlyDictionary<string, string> AllowedZeroTargetPatchTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["QudJP.Patches.HistoricStringExpanderPatch"] =
                "intentionally disabled to avoid corrupting HistorySpice/world generation output",
            ["QudJP.Patches.SteamWorkshopUploaderViewTranslationPatch"] =
                "optional Steam Workshop uploader UI type is absent from the stable game assembly",
        };

    public static IReadOnlyList<QudTestCaseResult> ExecuteAll()
    {
        EnsureGameAssemblyLoaded();
        return EnumeratePatchTypes()
            .Select(ExecutePatchType)
            .OrderBy(static result => result.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static string Execute(QudTestCase testCase)
    {
        if (string.IsNullOrWhiteSpace(testCase.Patch))
        {
            throw new InvalidOperationException("patch-binding case requires patch");
        }

        EnsureGameAssemblyLoaded();
        var patchType = ResolvePatchType(testCase.Patch);
        if (patchType is null)
        {
            throw new InvalidOperationException("patch type not found: " + testCase.Patch);
        }

        var targets = ResolveTargets(patchType);
        if (targets.Count == 0)
        {
            throw new InvalidOperationException("patch target set was empty: " + patchType.FullName);
        }

        targets.Sort(StringComparer.Ordinal);
        return string.Join("\n", targets);
    }

    public static string Expected(QudTestCase testCase)
    {
        return testCase.ExpectedTargets.Count == 0
            ? testCase.Expected
            : string.Join("\n", testCase.ExpectedTargets.OrderBy(static target => target, StringComparer.Ordinal));
    }

    private static IEnumerable<Type> EnumeratePatchTypes()
    {
        return typeof(QudTestPatchBindingExecutor).Assembly.GetTypes()
            .Where(static type => type.IsClass)
            .Where(static type => string.Equals(type.Namespace, "QudJP.Patches", StringComparison.Ordinal))
            .Where(static type =>
                type.GetMethod("TargetMethod", TargetBindingFlags) is not null
                || type.GetMethod("TargetMethods", TargetBindingFlags) is not null)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);
    }

    private static QudTestCaseResult ExecutePatchType(Type patchType)
    {
        var expected = "one or more resolved target signatures";
        try
        {
            var targets = ResolveTargets(patchType);
            targets.Sort(StringComparer.Ordinal);
            var actual = string.Join("\n", targets);
            var zeroTargetReason = AllowedZeroTargetPatchTypes.TryGetValue(patchType.FullName ?? string.Empty, out var reason)
                ? reason
                : string.Empty;
            var passed = targets.Count > 0 || zeroTargetReason.Length > 0;
            var diagnostic = string.Empty;
            if (targets.Count == 0)
            {
                diagnostic = zeroTargetReason.Length == 0
                    ? "patch target set was empty"
                    : zeroTargetReason;
            }

            return new QudTestCaseResult
            {
                Id = "binding-all." + patchType.Name,
                Route = "patch-binding-all",
                Input = patchType.FullName ?? patchType.Name,
                Expected = zeroTargetReason.Length == 0 ? expected : "zero targets are explicitly allowed",
                Actual = actual,
                Passed = passed,
                Diagnostic = diagnostic,
            };
        }
        catch (Exception ex)
        {
            return new QudTestCaseResult
            {
                Id = "binding-all." + patchType.Name,
                Route = "patch-binding-all",
                Input = patchType.FullName ?? patchType.Name,
                Expected = expected,
                Actual = string.Empty,
                Passed = false,
                Diagnostic = ex.GetType().Name + ": " + ex.Message,
            };
        }
    }

    private static Type? ResolvePatchType(string patch)
    {
        var assembly = typeof(QudTestPatchBindingExecutor).Assembly;
        var patchType = assembly.GetType(patch, throwOnError: false);
        if (patchType is not null || patch.Contains('.'))
        {
            return patchType;
        }

        return assembly.GetType("QudJP.Patches." + patch, throwOnError: false);
    }

    private static void EnsureGameAssemblyLoaded()
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(static assembly => assembly.GetName().Name == "Assembly-CSharp"))
        {
            return;
        }

        EnsureManagedAssemblyLoaded("UnityEngine.CoreModule");
        EnsureManagedAssemblyLoaded("UnityEngine.UI");
        EnsureManagedAssemblyLoaded("UnityEngine.UIModule");
        EnsureManagedAssemblyLoaded("Unity.TextMeshPro");
        EnsureManagedAssemblyLoaded("UniTask");
        EnsureManagedAssemblyLoaded("ZString");

        var localPath = ResolveManagedAssemblyPath("Assembly-CSharp");
        try
        {
            _ = Assembly.Load("Assembly-CSharp");
        }
        catch (FileNotFoundException) when (localPath is not null)
        {
#pragma warning disable S3885
            _ = Assembly.LoadFrom(localPath!);
#pragma warning restore S3885
        }
    }

    private static void EnsureManagedAssemblyLoaded(string assemblyName)
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == assemblyName))
        {
            return;
        }

        var localPath = ResolveManagedAssemblyPath(assemblyName);
        try
        {
            _ = Assembly.Load(assemblyName);
        }
        catch (FileNotFoundException) when (localPath is not null)
        {
#pragma warning disable S3885
            _ = Assembly.LoadFrom(localPath!);
#pragma warning restore S3885
        }
    }

    private static string? ResolveManagedAssemblyPath(string assemblyName)
    {
        foreach (var directory in ManagedAssemblyDirectories())
        {
            var path = Path.Combine(directory, assemblyName + ".dll");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> ManagedAssemblyDirectories()
    {
        yield return AppDomain.CurrentDomain.BaseDirectory;

        var envDir = Environment.GetEnvironmentVariable("COQ_MANAGED_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            yield return envDir;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(
                home,
                "Games/CavesOfQud-stable-ref/CoQ.app/Contents/Resources/Data/Managed");
        }
    }

    private static List<string> ResolveTargets(Type patchType)
    {
        var signatures = new List<string>();
#pragma warning disable S3011
        var targetMethod = patchType.GetMethod("TargetMethod", TargetBindingFlags);
#pragma warning restore S3011
        if (targetMethod is not null)
        {
            var methodBase = InvokeTargetMethod(targetMethod, patchType);
            if (methodBase is not null)
            {
                signatures.Add(FullMethodSignature(methodBase));
            }
        }

#pragma warning disable S3011
        var targetMethods = patchType.GetMethod("TargetMethods", TargetBindingFlags);
#pragma warning restore S3011
        if (targetMethods is not null)
        {
            foreach (var methodBase in InvokeTargetMethods(targetMethods, patchType))
            {
                signatures.Add(FullMethodSignature(methodBase));
            }
        }

        if (targetMethod is null && targetMethods is null)
        {
            throw new InvalidOperationException("patch target entrypoint not found: " + patchType.FullName);
        }

        return signatures.Distinct(StringComparer.Ordinal).ToList();
    }

    private static MethodBase? InvokeTargetMethod(MethodInfo targetMethod, Type patchType)
    {
        try
        {
#pragma warning disable S3011
            return targetMethod.Invoke(null, null) as MethodBase;
#pragma warning restore S3011
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(
                "patch target invocation failed: " + patchType.FullName + ": " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message,
                ex.InnerException);
        }
    }

    private static IEnumerable<MethodBase> InvokeTargetMethods(MethodInfo targetMethods, Type patchType)
    {
        object? result;
        try
        {
#pragma warning disable S3011
            result = targetMethods.Invoke(null, null);
#pragma warning restore S3011
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(
                "patch targets invocation failed: " + patchType.FullName + ": " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message,
                ex.InnerException);
        }

        if (result is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (var item in enumerable)
        {
            if (item is MethodBase methodBase)
            {
                yield return methodBase;
            }
        }
    }

    private static string FullMethodSignature(MethodBase methodBase)
    {
        var returnType = methodBase is MethodInfo methodInfo
            ? NormalizeTypeName(methodInfo.ReturnType.FullName)
            : "System.Void";

        return string.Join(
            "|",
            new[]
            {
                methodBase.DeclaringType?.FullName ?? string.Empty,
                methodBase.Name,
                returnType,
            }.Concat(Array.ConvertAll(
                methodBase.GetParameters(),
                static parameter => NormalizeTypeName(parameter.ParameterType.FullName))));
    }

    private static string NormalizeTypeName(string? typeName)
    {
        return typeName is null
            ? string.Empty
            : Regex.Replace(typeName, @",\s*[^\[\],]+,\s*Version=[^\]]+", string.Empty);
    }
}
#pragma warning restore S3011
