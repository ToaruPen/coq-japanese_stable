namespace QudJP;

internal static class DirectionPhraseTranslator
{
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
