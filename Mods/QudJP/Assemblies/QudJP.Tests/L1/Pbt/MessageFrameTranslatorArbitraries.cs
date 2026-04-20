using FsCheck;
using FsCheck.Fluent;

namespace QudJP.Tests.L1.Pbt;

public sealed record DirectMarkerText(string Value);

public static class MessageFrameTranslatorArbitraries
{
    public static Arbitrary<DirectMarkerText> DirectMarkerTexts()
    {
        var characters = Gen.Elements('a', 'b', 'c', 'x', 'y', 'z', '熊', '防', 'ぐ', '刀', '光');

        return Gen.Choose(1, 12)
            .SelectMany(length => Gen.ArrayOf(characters, length))
            .Select(chars => new DirectMarkerText(new string(chars)))
            .ToArbitrary();
    }
}
