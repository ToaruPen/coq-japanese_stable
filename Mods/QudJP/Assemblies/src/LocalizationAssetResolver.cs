using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace QudJP;

public static class LocalizationAssetResolver
{
    private static string? localizationRootOverride;

    public static string GetLocalizationPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Path must not be empty.", nameof(relativePath));
        }

        var localizationRoot = GetLocalizationRoot();
        var normalizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var resolvedPath = Path.GetFullPath(Path.Combine(localizationRoot, normalizedRelativePath));
        if (!IsSubPathOf(resolvedPath, localizationRoot))
        {
            throw new InvalidOperationException($"Path escapes Localization root: {relativePath}");
        }

        return resolvedPath;
    }

    internal static void SetLocalizationRootForTests(string? rootPath)
    {
        localizationRootOverride = rootPath;
    }

    private static string GetLocalizationRoot()
    {
        if (!string.IsNullOrWhiteSpace(localizationRootOverride))
        {
            return Path.GetFullPath(localizationRootOverride);
        }

        var modRoot = ResolveModRootDirectory();
        return Path.Combine(modRoot, "Localization");
    }

    private static string ResolveModRootDirectory()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        string assemblyDirectory;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            assemblyDirectory = AppContext.BaseDirectory;
        }
        else
        {
            var dirName = Path.GetDirectoryName(assemblyPath);
            if (dirName is null)
            {
                Trace.TraceWarning("QudJP: Assembly directory name is null for '{0}', falling back to AppContext.BaseDirectory.", assemblyPath);
                dirName = AppContext.BaseDirectory;
            }

            assemblyDirectory = dirName;
        }

        var modRoot = Directory.GetParent(assemblyDirectory);
        if (modRoot is null)
        {
            Trace.TraceWarning("QudJP: Mod root directory is null (parent of '{0}'), using assembly directory as root.", assemblyDirectory);
            return assemblyDirectory;
        }

        return modRoot.FullName;
    }

    private static bool IsSubPathOf(string candidatePath, string rootPath)
    {
        var rootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
        var candidateWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
        return candidateWithSeparator.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
