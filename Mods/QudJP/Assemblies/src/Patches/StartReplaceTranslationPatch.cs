using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using HarmonyLib;

namespace QudJP.Patches;

/// <summary>
/// Prefix patch on the string.StartReplace() extension method in GameTextExtensions.
/// Replaces English =variable= template strings with Japanese equivalents
/// before the ReplaceBuilder processes them.
/// </summary>
[HarmonyPatch]
public static class StartReplaceTranslationPatch
{
    private static Dictionary<string, string>? templateDictionary;
    private static string? dictionaryPathOverride;

    public static void SetDictionaryPathForTests(string path)
    {
        dictionaryPathOverride = path;
        templateDictionary = null;
    }

    public static void ResetForTests()
    {
        dictionaryPathOverride = null;
        templateDictionary = null;
    }

    /// <summary>Resolve the extension method target at patch time.</summary>
    public static MethodBase TargetMethod()
    {
        // GameTextExtensions is the compiler-generated static class for the extension methods
        var type = AccessTools.TypeByName("GameTextExtensions");
        if (type is not null)
        {
            return AccessTools.Method(type, "StartReplace", new[] { typeof(string) });
        }

        // Fallback: search all types for the extension method
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var t in types)
            {
                if (!t.IsDefined(typeof(ExtensionAttribute), false))
                {
                    continue;
                }

                var m = AccessTools.Method(t, "StartReplace", new[] { typeof(string) });
                if (m is not null && m.IsStatic)
                {
                    return m;
                }
            }
        }

        System.Diagnostics.Trace.TraceError(
            "QudJP: StartReplaceTranslationPatch.TargetMethod failed to resolve GameTextExtensions.StartReplace(string).");
        throw new MissingMethodException("GameTextExtensions", "StartReplace");
    }

    [HarmonyPrefix]
    public static void Prefix(ref string Text)
    {
        try
        {
            if (string.IsNullOrEmpty(Text))
            {
                return;
            }

            EnsureLoaded();

            if (templateDictionary is not null && templateDictionary.TryGetValue(Text, out var translated))
            {
                Text = translated;
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.TraceError("QudJP: StartReplaceTranslationPatch.Prefix failed: {0}", ex);
        }
    }

    private static void EnsureLoaded()
    {
        if (templateDictionary is not null)
        {
            return;
        }

        var path = ResolveDictionaryPath();
        if (!File.Exists(path))
        {
            System.Diagnostics.Trace.TraceError(
                "QudJP: Variable template dictionary not found: {0}", path);
            templateDictionary = new Dictionary<string, string>();
            return;
        }

        try
        {
            var serializer = new DataContractJsonSerializer(typeof(VariableTemplateDictionary));
            using var stream = File.OpenRead(path);
            if (serializer.ReadObject(stream) is VariableTemplateDictionary doc && doc.Entries is not null)
            {
                var dict = new Dictionary<string, string>();
                foreach (var entry in doc.Entries)
                {
                    if (entry.Key is not null && entry.Text is not null && !entry.Key.StartsWith("__", StringComparison.Ordinal))
                    {
                        dict[entry.Key] = entry.Text;
                    }
                }

                templateDictionary = dict;
            }
            else
            {
                templateDictionary = new Dictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                "QudJP: Failed to load variable template dictionary '{0}': {1}", path, ex);
            templateDictionary = new Dictionary<string, string>();
        }
    }

    private static string ResolveDictionaryPath()
    {
        if (!string.IsNullOrWhiteSpace(dictionaryPathOverride))
        {
            return Path.GetFullPath(dictionaryPathOverride);
        }

        return LocalizationAssetResolver.GetLocalizationPath("Dictionaries/templates-variable.ja.json");
    }

    [DataContract]
    private sealed class VariableTemplateDictionary
    {
        [DataMember(Name = "entries")]
        public List<VariableTemplateEntry>? Entries { get; set; }
    }

    [DataContract]
    private sealed class VariableTemplateEntry
    {
        [DataMember(Name = "key")]
        public string? Key { get; set; }

        [DataMember(Name = "text")]
        public string? Text { get; set; }
    }
}
