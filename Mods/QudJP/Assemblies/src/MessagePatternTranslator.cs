using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace QudJP;

internal static class MessagePatternTranslator
{
    private static readonly object SyncRoot = new object();
    private static readonly ConcurrentDictionary<string, Regex> RegexCache =
        new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> MissingPatternLog =
        new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

    private static List<MessagePatternDefinition>? loadedPatterns;
    private static string? patternFileOverride;
    private static int loadInvocationCount;

    internal static int LoadInvocationCount => Volatile.Read(ref loadInvocationCount);

    internal static string Translate(string? source, string? context = null)
    {
        using var _ = Translator.PushLogContext(context);

        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var (stripped, spans) = ColorCodePreserver.Strip(source);
        if (stripped.Length == 0)
        {
            return source!;
        }

        var translated = TranslateStripped(stripped);
        return ColorCodePreserver.Restore(translated, spans);
    }

    internal static void SetPatternFileForTests(string? filePath)
    {
        lock (SyncRoot)
        {
            patternFileOverride = filePath;
            loadedPatterns = null;
            RegexCache.Clear();
            MissingPatternLog.Clear();
            Interlocked.Exchange(ref loadInvocationCount, 0);
        }
    }

    internal static void ResetForTests()
    {
        SetPatternFileForTests(null);
    }

    private static string TranslateStripped(string source)
    {
        var patterns = GetLoadedPatterns();
        for (var index = 0; index < patterns.Count; index++)
        {
            var definition = patterns[index];
            var regex = GetCompiledRegex(definition.Pattern);
            var match = regex.Match(source);
            if (!match.Success)
            {
                continue;
            }

            return ApplyTemplate(definition.Template, match);
        }

        if (MissingPatternLog.TryAdd(source, 0))
        {
            Trace.TraceInformation($"QudJP MessagePatternTranslator: no pattern for '{source}'.{Translator.GetCurrentLogContextSuffix()}");
        }

        return source;
    }

    private static List<MessagePatternDefinition> GetLoadedPatterns()
    {
        var cached = Volatile.Read(ref loadedPatterns);
        if (cached is not null)
        {
            return cached;
        }

        lock (SyncRoot)
        {
            if (loadedPatterns is null)
            {
                loadedPatterns = LoadPatterns();
            }

            return loadedPatterns;
        }
    }

    private static List<MessagePatternDefinition> LoadPatterns()
    {
        Interlocked.Increment(ref loadInvocationCount);

        var patternFilePath = ResolvePatternFilePath();
        if (!File.Exists(patternFilePath))
        {
            throw new FileNotFoundException(
                $"QudJP: message pattern dictionary file not found: {patternFilePath}",
                patternFilePath);
        }

        using var stream = File.OpenRead(patternFilePath);
        var serializer = new DataContractJsonSerializer(typeof(MessagePatternDocument));
        var document = serializer.ReadObject(stream) as MessagePatternDocument;
        if (document?.Patterns is null)
        {
            throw new InvalidDataException($"QudJP: message pattern file has no patterns array: {patternFilePath}");
        }

        var definitions = new List<MessagePatternDefinition>(document.Patterns.Count);
        for (var index = 0; index < document.Patterns.Count; index++)
        {
            var patternEntry = document.Patterns[index];
            var pattern = patternEntry?.Pattern;
            var template = patternEntry?.Template;
            if (pattern is null || pattern.Length == 0 || template is null)
            {
                throw new InvalidDataException(
                    $"QudJP: malformed message pattern entry at index {index} in '{patternFilePath}'.");
            }

            _ = GetCompiledRegex(pattern);
            definitions.Add(new MessagePatternDefinition(pattern, template));
        }

        return definitions;
    }

    private static string ResolvePatternFilePath()
    {
        if (!string.IsNullOrWhiteSpace(patternFileOverride))
        {
            return Path.GetFullPath(patternFileOverride);
        }

        return LocalizationAssetResolver.GetLocalizationPath("Dictionaries/messages.ja.json");
    }

    private static Regex GetCompiledRegex(string pattern)
    {
        return RegexCache.GetOrAdd(pattern, CreateRegex);
    }

    private static Regex CreateRegex(string pattern)
    {
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private static string ApplyTemplate(string template, Match match)
    {
        var capturedCount = match.Groups.Count - 1;
        if (capturedCount <= 0)
        {
            return template;
        }

        var placeholders = new object[capturedCount];
        for (var index = 0; index < capturedCount; index++)
        {
            placeholders[index] = match.Groups[index + 1].Value;
        }

        return string.Format(CultureInfo.InvariantCulture, template, placeholders);
    }

    private sealed class MessagePatternDefinition
    {
        internal MessagePatternDefinition(string pattern, string template)
        {
            Pattern = pattern;
            Template = template;
        }

        internal string Pattern { get; }

        internal string Template { get; }
    }

    [DataContract]
    private sealed class MessagePatternDocument
    {
        [DataMember(Name = "patterns")]
        public List<MessagePatternEntry>? Patterns { get; set; }
    }

    [DataContract]
    private sealed class MessagePatternEntry
    {
        [DataMember(Name = "pattern")]
        public string? Pattern { get; set; }

        [DataMember(Name = "template")]
        public string? Template { get; set; }
    }
}
