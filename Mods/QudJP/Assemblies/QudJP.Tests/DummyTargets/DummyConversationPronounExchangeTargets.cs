namespace QudJP.Tests.DummyTargets;

internal sealed class DummyConversationPronounSet
{
    public string ShortName { get; set; } = "he/him/his";

    public string GetShortName()
    {
        return ShortName;
    }
}

internal sealed class DummyConversationSpeaker
{
    public string Name { get; set; } = "Mehmet";

    public string its = "his";

    public string GiveVerb { get; set; } = " gives";

    public DummyConversationPronounSet PronounSet { get; set; } = new();

    public string t()
    {
        return Name;
    }

    public string GetVerb(string verb)
    {
        _ = verb;
        return GiveVerb;
    }

    public DummyConversationPronounSet GetPronounSet()
    {
        return PronounSet;
    }
}

internal static class DummyConversationPronounExchangeTarget
{
    public static string PronounExchangeDescription(
        object player,
        DummyConversationSpeaker speaker,
        bool speakerGivePronouns,
        bool speakerGetPronouns,
        bool speakerGetNewPronouns)
    {
        _ = player;
        if (speakerGivePronouns && speakerGetPronouns)
        {
            return "you and " + speaker.t() + " exchange pronouns; " + speaker.its + " are " + speaker.GetPronounSet().GetShortName();
        }

        if (speakerGivePronouns)
        {
            return speaker.t() + speaker.GetVerb("give") + " you " + speaker.its + " pronouns, which are " + speaker.GetPronounSet().GetShortName();
        }

        if (speakerGetNewPronouns)
        {
            return "you give " + speaker.t() + " your new pronouns";
        }

        if (speakerGetPronouns)
        {
            return "you give " + speaker.t() + " your pronouns";
        }

        return null!;
    }

    /// <summary>Returns null to exercise the Postfix null guard.</summary>
    public static string PronounExchangeDescriptionNull(
        object player,
        DummyConversationSpeaker speaker,
        bool speakerGivePronouns,
        bool speakerGetPronouns,
        bool speakerGetNewPronouns)
    {
        _ = player;
        _ = speaker;
        _ = speakerGivePronouns;
        _ = speakerGetPronouns;
        _ = speakerGetNewPronouns;
        return null!;
    }

    /// <summary>Returns a fixed unmatched string to exercise the Postfix no-op path.</summary>
    public static string PronounExchangeDescriptionFixed(
        object player,
        DummyConversationSpeaker speaker,
        bool speakerGivePronouns,
        bool speakerGetPronouns,
        bool speakerGetNewPronouns)
    {
        _ = player;
        _ = speaker;
        _ = speakerGivePronouns;
        _ = speakerGetPronouns;
        _ = speakerGetNewPronouns;
        return "unmatched pronoun text";
    }
}
