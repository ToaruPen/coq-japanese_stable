using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace QudJP.Patches;

internal static class DoesVerbRouteTranslator
{
    private const char MarkerPrefix = '\x02';
    private const char MarkerTerminator = '\x03';
    private const char FieldSeparator = '\u001f';

    internal static string MarkDoesFragment(string fragment, string verb, int subjectLength, string? adverb)
    {
        if (string.IsNullOrEmpty(fragment) || string.IsNullOrWhiteSpace(verb))
        {
            return fragment ?? string.Empty;
        }

        return MarkerPrefix.ToString()
            + verb
            + FieldSeparator
            + subjectLength.ToString(CultureInfo.InvariantCulture)
            + FieldSeparator
            + fragment.Length.ToString(CultureInfo.InvariantCulture)
            + FieldSeparator
            + (adverb ?? string.Empty)
            + MarkerTerminator
            + fragment;
    }

    internal static bool TryTranslateMarkedMessage(string source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryParseMarkedMessage(stripped, out var marker, out var visible))
        {
            translated = source;
            return false;
        }

        if (TryTranslateVisibleMessage(visible, marker.Verb, marker.SubjectLength, marker.FragmentLength, marker.Adverb, out var visibleTranslated))
        {
            translated = ColorAwareTranslationComposer.Restore(visibleTranslated, spans);
            return true;
        }

        translated = ColorAwareTranslationComposer.Restore(visible, spans);
        return false;
    }

    internal static bool TryTranslatePlainSentence(string source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source ?? string.Empty;
            return false;
        }

        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        if (!TryTranslatePlainVisibleSentence(stripped, out var visibleTranslated))
        {
            translated = source;
            return false;
        }

        translated = ColorAwareTranslationComposer.Restore(visibleTranslated, spans);
        return true;
    }

    internal static bool TryTranslatePlainSentenceForTests(string source, out string translated)
    {
        return TryTranslatePlainSentence(source, out translated);
    }

    private static bool TryTranslatePlainVisibleSentence(string source, out string translated)
    {
        translated = string.Empty;
        var text = source.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var endMark = ExtractTerminalEndMark(ref text);
        var candidates = new List<PlainSentenceCandidate>();
        foreach (var baseVerb in MessageFrameTranslator.GetKnownVerbsForTests())
        {
            foreach (var form in GetVerbForms(baseVerb))
            {
                foreach (var index in FindVerbForms(text, form))
                {
                    var subject = text.Substring(0, index).TrimEnd();
                    if (subject.Length == 0)
                    {
                        continue;
                    }

                    candidates.Add(new PlainSentenceCandidate(
                        index,
                        form.Length,
                        baseVerb,
                        subject,
                        NormalizeFragment(text.Substring(index + form.Length))));
                }
            }
        }

        candidates.Sort(static (left, right) =>
        {
            var indexComparison = left.Index.CompareTo(right.Index);
            return indexComparison != 0
                ? indexComparison
                : right.FormLength.CompareTo(left.FormLength);
        });

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (TryTranslateFromParts(candidate.Subject, candidate.Verb, candidate.Extra, endMark, out translated))
            {
                return true;
            }

            if (TrySplitTrailingAdverb(candidate.Subject, out var trimmedSubject, out var adverb)
                && TryTranslateFromParts(trimmedSubject, candidate.Verb, CombineExtra(adverb, candidate.Extra), endMark, out translated))
            {
                return true;
            }
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryTranslateFromParts(
        string subject,
        string verb,
        string? extra,
        string? endMark,
        out string translated)
    {
        var normalizedSubject = NormalizeSubject(subject);
        return MessageFrameTranslator.TryTranslateXDidY(normalizedSubject, verb, extra, endMark, out translated);
    }

    private static string NormalizeSubject(string subject)
    {
        var trimmed = subject.Trim().TrimEnd(',', ' ');
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        if (string.Equals(trimmed, "You", StringComparison.Ordinal)
            || string.Equals(trimmed, "you", StringComparison.Ordinal))
        {
            return "あなた";
        }

        if (trimmed.StartsWith("The ", StringComparison.Ordinal)
            || trimmed.StartsWith("the ", StringComparison.Ordinal)
            || trimmed.StartsWith("A ", StringComparison.Ordinal)
            || trimmed.StartsWith("a ", StringComparison.Ordinal)
            || trimmed.StartsWith("An ", StringComparison.Ordinal)
            || trimmed.StartsWith("an ", StringComparison.Ordinal))
        {
            var separator = trimmed.IndexOf(' ');
            return trimmed.Substring(separator + 1);
        }

        return trimmed;
    }

    private static bool TryTranslateVisibleMessage(
        string visible,
        string verb,
        int subjectLength,
        int fragmentLength,
        string? adverb,
        out string translated)
    {
        if (fragmentLength < 0
            || subjectLength < 0
            || fragmentLength > visible.Length
            || subjectLength > fragmentLength)
        {
            translated = string.Empty;
            return false;
        }

        var subject = visible.Substring(0, subjectLength);
        var tail = visible.Substring(fragmentLength);
        return TryTranslateFromParts(subject, verb, BuildExtra(adverb, tail, out var endMark), endMark, out translated);
    }

    private static string? BuildExtra(string? adverb, string tail, out string? endMark)
    {
        var trimmedTail = tail.TrimStart();
        endMark = null;
        if (trimmedTail.Length > 0)
        {
            var last = trimmedTail[trimmedTail.Length - 1];
            if (last is '.' or '!' or '?')
            {
                endMark = last.ToString();
                trimmedTail = trimmedTail.Substring(0, trimmedTail.Length - 1).TrimEnd();
            }
        }

        return CombineExtra(adverb, trimmedTail);
    }

    private static string? CombineExtra(string? left, string? right)
    {
        var leftValue = NormalizeFragment(left);
        var rightValue = NormalizeFragment(right);
        if (leftValue is null)
        {
            return rightValue;
        }

        if (rightValue is null)
        {
            return leftValue;
        }

        return leftValue + " " + rightValue;
    }

    private static string? NormalizeFragment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value!.Trim();
    }

    private static string? ExtractTerminalEndMark(ref string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var last = text[text.Length - 1];
        if (last is not '.' and not '!' and not '?')
        {
            return null;
        }

        text = text.Substring(0, text.Length - 1);
        return last.ToString();
    }

    private static bool TryParseMarkedMessage(string source, out MarkerInfo marker, out string visible)
    {
        marker = default;
        visible = source;
        if (string.IsNullOrEmpty(source) || source[0] != MarkerPrefix)
        {
            return false;
        }

        var terminator = source.IndexOf(MarkerTerminator);
        if (terminator <= 1)
        {
            return false;
        }

        var payload = source.Substring(1, terminator - 1).Split(FieldSeparator);
        if (payload.Length != 4)
        {
            return false;
        }

        if (!int.TryParse(payload[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var subjectLength)
            || !int.TryParse(payload[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fragmentLength))
        {
            return false;
        }

        marker = new MarkerInfo(payload[0], subjectLength, fragmentLength, payload[3]);
        visible = source.Substring(terminator + 1);
        return true;
    }

    private static IEnumerable<int> FindVerbForms(string text, string form)
    {
        var searchStart = 0;
        while (searchStart < text.Length)
        {
            var index = text.IndexOf(form, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            var beforeOk = index > 0 && text[index - 1] == ' ';
            var afterIndex = index + form.Length;
            var afterOk = afterIndex == text.Length
                || text[afterIndex] == ' '
                || text[afterIndex] == ',';
            if (beforeOk && afterOk)
            {
                yield return index;
            }

            searchStart = index + 1;
        }
    }

    private static bool TrySplitTrailingAdverb(string subject, out string trimmedSubject, out string adverb)
    {
        trimmedSubject = string.Empty;
        adverb = string.Empty;

        var lastSpace = subject.LastIndexOf(' ');
        if (lastSpace <= 0 || lastSpace >= subject.Length - 1)
        {
            return false;
        }

        var candidate = subject.Substring(lastSpace + 1);
        for (var index = 0; index < candidate.Length; index++)
        {
            if (!char.IsLetter(candidate[index]) || !char.IsLower(candidate[index]))
            {
                return false;
            }
        }

        trimmedSubject = subject.Substring(0, lastSpace).TrimEnd();
        adverb = candidate;
        return trimmedSubject.Length > 0;
    }

    private static IEnumerable<string> GetVerbForms(string baseVerb)
    {
        yield return baseVerb;

        switch (baseVerb)
        {
            case "are":
                yield return "is";
                yield break;
            case "were":
                yield return "was";
                yield break;
            case "have":
                yield return "has";
                yield break;
            case "do":
                yield return "does";
                yield break;
            case "go":
                yield return "goes";
                yield break;
        }

        if (baseVerb.EndsWith("y", StringComparison.Ordinal)
            && baseVerb.Length > 1
            && !IsVowel(baseVerb[baseVerb.Length - 2]))
        {
            yield return baseVerb.Remove(baseVerb.Length - 1) + "ies";
            yield break;
        }

        if (baseVerb.EndsWith("s", StringComparison.Ordinal)
            || baseVerb.EndsWith("x", StringComparison.Ordinal)
            || baseVerb.EndsWith("z", StringComparison.Ordinal)
            || baseVerb.EndsWith("ch", StringComparison.Ordinal)
            || baseVerb.EndsWith("sh", StringComparison.Ordinal)
            || baseVerb.EndsWith("o", StringComparison.Ordinal))
        {
            yield return baseVerb + "es";
            yield break;
        }

        yield return baseVerb + "s";
    }

    private static bool IsVowel(char value)
    {
        var lowered = char.ToLowerInvariant(value);
        return lowered is 'a' or 'e' or 'i' or 'o' or 'u';
    }

    private readonly struct MarkerInfo
    {
        internal MarkerInfo(string verb, int subjectLength, int fragmentLength, string? adverb)
        {
            Verb = verb;
            SubjectLength = subjectLength;
            FragmentLength = fragmentLength;
            Adverb = string.IsNullOrWhiteSpace(adverb) ? null : adverb;
        }

        internal string Verb { get; }

        internal int SubjectLength { get; }

        internal int FragmentLength { get; }

        internal string? Adverb { get; }
    }

    private readonly struct PlainSentenceCandidate
    {
        internal PlainSentenceCandidate(int index, int formLength, string verb, string subject, string? extra)
        {
            Index = index;
            FormLength = formLength;
            Verb = verb;
            Subject = subject;
            Extra = extra;
        }

        internal int Index { get; }

        internal int FormLength { get; }

        internal string Verb { get; }

        internal string Subject { get; }

        internal string? Extra { get; }
    }
}
