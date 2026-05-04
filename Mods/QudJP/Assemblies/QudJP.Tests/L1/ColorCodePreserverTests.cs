using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorCodePreserverTests
{
    [Test]
    public void StripRestore_PreservesMarkupFormat()
    {
        var input = "{{W|a sword}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("刀", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("a sword"));
            Assert.That(restored, Is.EqualTo("{{W|刀}}"));
        });
    }

    [Test]
    public void StripRestore_PreservesWholeWrapperOnShorterSentence()
    {
        var input = "{{r|Your irritable genome acts up.}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("あなたの過敏ゲノムが暴走した", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("Your irritable genome acts up."));
            Assert.That(restored, Is.EqualTo("{{r|あなたの過敏ゲノムが暴走した}}"));
        });
    }

    [Test]
    public void StripRestore_PreservesForegroundCodes()
    {
        var input = "&Ggreen&y";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("緑", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("green"));
            Assert.That(restored, Is.EqualTo("&G緑&y"));
        });
    }

    [Test]
    public void StripRestore_PreservesBackgroundCodes()
    {
        var input = "^rdanger^k";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("危険", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("danger"));
            Assert.That(restored, Is.EqualTo("^r危険^k"));
        });
    }

    [Test]
    public void Strip_KeepsEscapedAmpersandLiteral()
    {
        var input = "AT&&T";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore(stripped, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("AT&&T"));
            Assert.That(spans, Is.Empty);
            Assert.That(restored, Is.EqualTo("AT&&T"));
        });
    }

    [Test]
    public void Strip_KeepsEscapedCaretLiteral()
    {
        var input = "^^caret";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore(stripped, spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("^^caret"));
            Assert.That(spans, Is.Empty);
            Assert.That(restored, Is.EqualTo("^^caret"));
        });
    }

    [Test]
    public void StripRestore_HandlesMixedFormatsInSingleString()
    {
        var input = "{{W|&GGo^r!}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var restored = ColorCodePreserver.Restore("行け!", spans);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("Go!"));
            Assert.That(restored, Is.EqualTo("{{W|&G行け^r!}}"));
        });
    }

    [Test]
    public void SliceBoundarySpans_PreservesPrefixWrapperOwnership_WhenTemplateTextShrinks()
    {
        var source = "{{r|You miss!}} [12 vs 14]";
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(source);
        var match = Regex.Match(stripped, "^You miss! \\[(.+?) vs (.+?)\\]$");
        var translated = "攻撃は外れた！ [12 vs 14]";
        var firstCaptureStart = translated.IndexOf("12", StringComparison.Ordinal);
        var lastCaptureEnd = translated.IndexOf("14", StringComparison.Ordinal) + 2;

        var restored = ColorAwareTranslationComposer.RestoreMatchBoundaries(
            translated,
            spans,
            match,
            stripped.Length,
            firstCaptureStart,
            lastCaptureEnd,
            skipAdjacentClosingBoundary: false);

        Assert.That(restored, Is.EqualTo("{{r|攻撃は外れた！}} [12 vs 14]"));
    }

    [Test]
    public void SliceSpans_ReturnsRangeRelativeMarkup()
    {
        var input = "{{W|[Esc]}} {{y|Cancel}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var hotkey = ColorCodePreserver.Restore("[Esc]", ColorCodePreserver.SliceSpans(spans, 0, 5));
        var label = ColorCodePreserver.Restore("キャンセル", ColorCodePreserver.SliceSpans(spans, 6, 6));

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("[Esc] Cancel"));
            Assert.That(hotkey, Is.EqualTo("{{W|[Esc]}}"));
            Assert.That(label, Is.EqualTo("{{y|キャンセル}}"));
        });
    }

    [Test]
    public void SliceSpans_DoesNotCaptureNextWrapperOpeningToken()
    {
        var input = "{{R|lead}}{{G|slug}}";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var first = ColorCodePreserver.Restore("鉛", ColorCodePreserver.SliceSpans(spans, 0, 4));
        var second = ColorCodePreserver.Restore("弾", ColorCodePreserver.SliceSpans(spans, 4, 4));

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("leadslug"));
            Assert.That(first, Is.EqualTo("{{R|鉛}}"));
            Assert.That(second, Is.EqualTo("{{G|弾}}"));
        });
    }

    [Test]
    public void SliceSpans_DoesNotTreatAdjacentColorCodeOpenersAsCaptureClosers()
    {
        var input = "{{R|lead}}^rslug^k";
        var (stripped, spans) = ColorCodePreserver.Strip(input);

        var first = ColorCodePreserver.Restore("鉛", ColorCodePreserver.SliceSpans(spans, 0, 4));
        var second = ColorCodePreserver.Restore("弾", ColorCodePreserver.SliceSpans(spans, 4, 4));

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo("leadslug"));
            Assert.That(first, Is.EqualTo("{{R|鉛}}"));
            Assert.That(second, Does.StartWith("^r"));
            Assert.That(second, Does.Not.Contain("{{R|"));
        });
    }

    [Test]
    public void RestoreCapture_PreservesEmptyWrapperAtCaptureEnd()
    {
        var input = "{{C|TARGET: タム、ドロマド商団 [座っている]{{B|}}}}";
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(input);
        var match = Regex.Match(stripped, "^(?<label>TARGET:) (?<name>.+)$");

        Assert.That(match.Success, Is.True);

        var restored = ColorAwareTranslationComposer.RestoreCapture("タム、ドロマド商団 [座っている]", spans, match.Groups["name"]);

        Assert.That(restored, Is.EqualTo("タム、ドロマド商団 [座っている]{{B|}}}}"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesTerminalSingleCharacterWrapper()
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            "{{W|foo}}{{R|!}}",
            _ => "訳!");

        Assert.That(translated, Is.EqualTo("{{W|訳}}{{R|!}}"));
    }

    [Test]
    public void TranslatePreservingColors_ExpandsWholeWrapperToTranslatedLabel()
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            "{{y|No}}",
            _ => "いいえ");

        Assert.That(translated, Is.EqualTo("{{y|いいえ}}"));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotDuplicateTranslatedOwnedWholeWrapper()
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            "{{B|{{B|wet}}}}",
            _ => "{{B|濡れた}}");

        Assert.That(translated, Is.EqualTo("{{B|濡れた}}"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesDifferentWholeSourceWrapperAroundTranslatedOwnedMarkup()
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            "{{R|No}}",
            _ => "{{y|いいえ}}");

        Assert.That(translated, Is.EqualTo("{{R|{{y|いいえ}}}}"));
    }

    [Test]
    public void TranslatePreservingColors_RestoresInlineSourceWrapperByVisibleText_WhenTranslatedOwnsOtherMarkup()
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            "{{W|[n]}} {{y|No}}",
            _ => "{{c|[n]}} いいえ");

        Assert.That(translated, Is.EqualTo("{{W|{{c|[n]}}}} {{y|いいえ}}"));
    }

    [Test]
    public void TranslatePreservingColors_RestoresPartialSourceWrapperAfterWholeSourceWrapper_WhenTranslatedOwnsMarkup()
    {
        var translated = ColorAwareTranslationComposer.TranslatePreservingColors(
            "{{B|left {{R|target}} right}}",
            _ => "{{Y|left target right}}");

        Assert.That(translated, Is.EqualTo("{{B|{{Y|left {{R|target}} right}}}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_RestoresWholeSourceWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{y|No}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "いいえ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{y|いいえ}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_RestoresOnlyOuterNestedSourceWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{R|{{y|No}}}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "いいえ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{R|いいえ}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_PreservesNestedTranslatedOwnedMarkup()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{R|{{y|No}}}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "{{y|いいえ}}",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{R|{{y|いいえ}}}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_PreservesNestedQudAndTmpColorWrappers()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{W|<color=#44ff88>No</color>}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "いいえ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{W|<color=#44ff88>いいえ</color>}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_PreservesNestedTmpColorAndQudWrappers()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("<color=#44ff88>{{y|No}}</color>");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "いいえ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("<color=#44ff88>{{y|いいえ}}</color>"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_PreservesWholeForegroundWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("&GNo&y");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "いいえ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("&Gいいえ&y"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_PreservesWholeBackgroundWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("^rNo^k");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "いいえ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("^rいいえ^k"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_DoesNotProjectInlineSourceWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("Take {{y|No}} now");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "今はいいえを選ぶ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("今はいいえを選ぶ"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_DoesNotPairAdjacentWrappers()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{W|[n]}} {{y|No}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "[n] いいえ",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("[n] いいえ"));
    }

    [Test]
    public void RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership_RestoresAdjacentWrappers()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{W|[n]}} {{y|No}}");

        var restored = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            "[n] いいえ",
            spans,
            stripped);

        Assert.That(restored, Is.EqualTo("{{W|[n]}} {{y|いいえ}}"));
    }

    [Test]
    public void RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership_PreservesTranslatedOwnedMarkup()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{R|No}}");

        var restored = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            "{{y|いいえ}}",
            spans,
            stripped);

        Assert.That(restored, Is.EqualTo("{{R|{{y|いいえ}}}}"));
    }

    [Test]
    public void RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership_DoesNotDuplicateTranslatedOwnedSameWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{y|No}}");

        var restored = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            "{{y|いいえ}}",
            spans,
            stripped);

        Assert.That(restored, Is.EqualTo("{{y|いいえ}}"));
    }

    [Test]
    public void RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership_DoesNotOverProjectMiddleSegmentWithOneAnchor()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("A {{y|No}} B");

        var restored = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            "A いいえ 後続文",
            spans,
            stripped);

        Assert.That(restored, Is.EqualTo("A いいえ 後続文"));
    }

    [Test]
    public void RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership_UsesAnchorsForRepeatedVisibleSegment()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("A [n] B {{y|[n]}}");

        var restored = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            "A [n] B [n]",
            spans,
            stripped);

        Assert.That(restored, Is.EqualTo("A [n] B {{y|[n]}}"));
    }

    [Test]
    public void RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership_ClosesTranslatedMarkupBeforeLaterSourceOpening()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("A {{y|No}}");

        var restored = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            "{{g|A }}いいえ",
            spans,
            stripped);

        Assert.That(restored, Is.EqualTo("{{g|A }}{{y|いいえ}}"));
    }

    [Test]
    public void RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership_PreservesNestedMixedFamilyCloseOrder()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{W|<color=#44ff88>No</color>}}");

        var restored = ColorAwareTranslationComposer.RestoreSourceBoundaryWrappersByVisibleTextPreservingTranslatedOwnership(
            "いいえ",
            spans,
            stripped);

        Assert.That(restored, Is.EqualTo("{{W|<color=#44ff88>いいえ</color>}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_PreservesTranslatedOwnedMarkup()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{y|No}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "{{W|いいえ}}",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{y|{{W|いいえ}}}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_DoesNotDuplicateTranslatedOwnedWholeWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{y|No}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "{{y|いいえ}}",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{y|いいえ}}"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_DoesNotDuplicateLeadingSameFamilyTranslatedMarkup()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{B|wet グロウフィッシュ [swimming]}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "{{B|濡れた}}グロウフィッシュ [swimming]",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{B|濡れた}}グロウフィッシュ [swimming]"));
    }

    [Test]
    public void RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership_PreservesSourceNestingWhenTranslatedWrapperDuplicatesOuterWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{W|<color=#44ff88>No</color>}}");

        var restored = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            "{{W|いいえ}}",
            spans,
            stripped.Length);

        Assert.That(restored, Is.EqualTo("{{W|<color=#44ff88>いいえ</color>}}"));
    }

    [Test]
    public void RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership_DoesNotProjectNestedSameFamilySourceWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{R|{{y|No}}}}");
        var match = Regex.Match(stripped, "^(No)$");

        var restored = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            "{{y|いいえ}}",
            spans,
            match.Groups[1]);

        Assert.That(restored, Is.EqualTo("{{R|{{y|いいえ}}}}"));
    }

    [Test]
    public void RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership_DoesNotDuplicateTranslatedOwnedWholeWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{y|No}}");
        var match = Regex.Match(stripped, "^(No)$");

        var restored = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            "{{y|いいえ}}",
            spans,
            match.Groups[1]);

        Assert.That(restored, Is.EqualTo("{{y|いいえ}}"));
    }

    [Test]
    public void RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership_PreservesSourceNestingWhenTranslatedWrapperDuplicatesOuterWrapper()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{W|<color=#44ff88>No</color>}}");
        var match = Regex.Match(stripped, "^(No)$");

        var restored = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            "{{W|いいえ}}",
            spans,
            match.Groups[1]);

        Assert.That(restored, Is.EqualTo("{{W|<color=#44ff88>いいえ</color>}}"));
    }

    [Test]
    public void RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership_DoesNotPairAdjacentWrappers()
    {
        var (stripped, spans) = ColorAwareTranslationComposer.Strip("{{W|[n]}} {{y|No}}");
        var match = Regex.Match(stripped, @"^(\[n\]) (No)$");

        var restored = ColorAwareTranslationComposer.RestoreCaptureWholeBoundaryWrappersPreservingTranslatedOwnership(
            "{{y|いいえ}}",
            spans,
            match.Groups[2]);

        Assert.That(restored, Is.EqualTo("{{y|いいえ}}"));
    }

    [Test]
    public void Strip_ReturnsEmptyValue_ForNullOrEmptyInput()
    {
        var nullResult = ColorCodePreserver.Strip(null);
        var emptyResult = ColorCodePreserver.Strip(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(nullResult.stripped, Is.EqualTo(string.Empty));
            Assert.That(nullResult.spans, Is.Empty);
            Assert.That(emptyResult.stripped, Is.EqualTo(string.Empty));
            Assert.That(emptyResult.spans, Is.Empty);
        });
    }
}
