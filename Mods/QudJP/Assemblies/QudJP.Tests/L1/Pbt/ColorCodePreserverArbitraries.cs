using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record ColorizedCase(string Source, string VisibleText, string TranslatedVisibleText, string ExpectedRestored);

public sealed record ForegroundColorCodeCase(string Source, string VisibleText, string TranslatedVisibleText, string ExpectedRestored);

public sealed record BackgroundColorCodeCase(string Source, string VisibleText, string TranslatedVisibleText, string ExpectedRestored);

public sealed record TmpColorTagCase(string Source, string VisibleText, string TranslatedVisibleText, string ExpectedRestored);

public sealed record ColorWrapper(string Open, string Close);

public static class ColorCodePreserverArbitraries
{
    private static Gen<string> SafeText()
    {
        var characters = Gen.Elements('a', 'b', 'c', 'x', 'y', 'z', '刀', '緑', '危', '険', '光');
        return Gen.Choose(1, 8)
            .SelectMany(length => Gen.ArrayOf(characters, length))
            .Select(chars => new string(chars));
    }

    private static Gen<string> SafeTextIncludingEmpty()
    {
        var characters = Gen.Elements('a', 'b', 'c', 'x', 'y', 'z', '刀', '緑', '危', '険', '光');
        return Gen.Choose(0, 8)
            .SelectMany(length => Gen.ArrayOf(characters, length))
            .Select(chars => new string(chars));
    }

    public static Arbitrary<ColorizedCase> ColorizedCases()
    {
        var wrappers = Gen.OneOf(
            Gen.Constant(new ColorWrapper("{{W|", "}}")),
            Gen.Constant(new ColorWrapper("{{r|", "}}")));

        return (from rawVisible in SafeText()
            from wrapper in wrappers
            let translated = new string('訳', rawVisible.Length)
            select new ColorizedCase(
                wrapper.Open + rawVisible + wrapper.Close,
                rawVisible,
                translated,
                wrapper.Open + translated + wrapper.Close))
            .ToArbitrary();
    }

    public static Arbitrary<ForegroundColorCodeCase> ForegroundColorCodeCases()
    {
        return (from rawVisible in SafeText()
                let translated = new string('訳', rawVisible.Length)
                select new ForegroundColorCodeCase(
                    "&G" + rawVisible + "&y",
                    rawVisible,
                    translated,
                    "&G" + translated + "&y"))
            .ToArbitrary();
    }

    public static Arbitrary<BackgroundColorCodeCase> BackgroundColorCodeCases()
    {
        return (from rawVisible in SafeText()
                let translated = new string('訳', rawVisible.Length)
                select new BackgroundColorCodeCase(
                    "^r" + rawVisible + "^k",
                    rawVisible,
                    translated,
                    "^r" + translated + "^k"))
            .ToArbitrary();
    }

    public static Arbitrary<TmpColorTagCase> TmpColorTagCases()
    {
        var colors = Gen.OneOf(
            Gen.Constant("red"),
            Gen.Constant("cyan"),
            Gen.Constant("#00ff00"));

        return (from rawVisible in SafeTextIncludingEmpty()
                from color in colors
                let translated = new string('訳', rawVisible.Length)
                select new TmpColorTagCase(
                    $"<color={color}>{rawVisible}</color>",
                    rawVisible,
                    translated,
                    $"<color={color}>{translated}</color>"))
            .ToArbitrary();
    }
}
