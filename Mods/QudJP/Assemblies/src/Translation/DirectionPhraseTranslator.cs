namespace QudJP;

internal static class DirectionPhraseTranslator
{
    internal static bool TryTranslateAdverbPhrase(string source, out string translated)
    {
        translated = source switch
        {
            "to the north" => "北に",
            "to the south" => "南に",
            "to the east" => "東に",
            "to the west" => "西に",
            "to the northeast" => "北東に",
            "to the northwest" => "北西に",
            "to the southeast" => "南東に",
            "to the southwest" => "南西に",
            "nearby" => "近く",
            "above" => "上方",
            "below" => "下方",
            "here" => "ここ",
            "somewhere" => "どこか",
            _ => string.Empty,
        };

        return translated.Length > 0;
    }

    internal static bool TryTranslateNounStem(string source, out string translated)
    {
        translated = source switch
        {
            "north" => "北",
            "south" => "南",
            "east" => "東",
            "west" => "西",
            "northeast" => "北東",
            "northwest" => "北西",
            "southeast" => "南東",
            "southwest" => "南西",
            "to the north" => "北側",
            "to the south" => "南側",
            "to the east" => "東側",
            "to the west" => "西側",
            "to the northeast" => "北東側",
            "to the northwest" => "北西側",
            "to the southeast" => "南東側",
            "to the southwest" => "南西側",
            "nearby" => "近く",
            "above" => "上方",
            "below" => "下方",
            "here" => "ここ",
            "somewhere" => "どこか",
            _ => string.Empty,
        };

        return translated.Length > 0;
    }
}
