namespace QudJP;

internal static class DirectionPhraseTranslator
{
    internal static bool TryTranslateAdverbPhrase(string source, out string translated)
    {
        var normalized = source.ToUpperInvariant();
        translated = normalized switch
        {
            "NORTH" => "北側に",
            "SOUTH" => "南側に",
            "EAST" => "東側に",
            "WEST" => "西側に",
            "NORTHEAST" => "北東側に",
            "NORTHWEST" => "北西側に",
            "SOUTHEAST" => "南東側に",
            "SOUTHWEST" => "南西側に",
            "TO THE NORTH" => "北側に",
            "TO THE SOUTH" => "南側に",
            "TO THE EAST" => "東側に",
            "TO THE WEST" => "西側に",
            "TO THE NORTHEAST" => "北東側に",
            "TO THE NORTHWEST" => "北西側に",
            "TO THE SOUTHEAST" => "南東側に",
            "TO THE SOUTHWEST" => "南西側に",
            "NEARBY" => "近く",
            "ABOVE" => "上方",
            "BELOW" => "下方",
            "HERE" => "ここ",
            "SOMEWHERE" => "どこか",
            _ => string.Empty,
        };

        return translated.Length > 0;
    }

    internal static bool TryTranslateNounStem(string source, out string translated)
    {
        translated = source.ToUpperInvariant() switch
        {
            "NORTH" => "北",
            "SOUTH" => "南",
            "EAST" => "東",
            "WEST" => "西",
            "NORTHEAST" => "北東",
            "NORTHWEST" => "北西",
            "SOUTHEAST" => "南東",
            "SOUTHWEST" => "南西",
            "TO THE NORTH" => "北側",
            "TO THE SOUTH" => "南側",
            "TO THE EAST" => "東側",
            "TO THE WEST" => "西側",
            "TO THE NORTHEAST" => "北東側",
            "TO THE NORTHWEST" => "北西側",
            "TO THE SOUTHEAST" => "南東側",
            "TO THE SOUTHWEST" => "南西側",
            "NEARBY" => "近く",
            "ABOVE" => "上方",
            "BELOW" => "下方",
            "HERE" => "ここ",
            "SOMEWHERE" => "どこか",
            _ => string.Empty,
        };

        return translated.Length > 0;
    }
}
