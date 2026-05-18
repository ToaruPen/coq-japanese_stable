using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QudJP.Patches;

internal static class DynamicQuestConstructorConversationTextTranslator
{
    private static readonly IReadOnlyDictionary<string, string> ExactFrames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Will you do it?"] = "引き受けてくれるか？",
            ["Will you?"] = "引き受けてくれるか？",
            ["What say you?"] = "どうだ？",
            ["What do you think?"] = "どう思う？",
            ["Will you find it for us?"] = "われらのために見つけてくれるか？",
            ["Would you be willing to locate it?"] = "それを探し出してくれないか？",
            ["Will you"] = "引き受けてくれるか",
            ["Would you"] = "引き受けてくれるか",
            ["What do you say"] = "どうだ",
        };

    private static readonly Regex RewardTailPattern = new(
        "^(?<speaker>We|I) will (?<verb>reward|pay you for|compensate you for) your (?<service>services|service|assistance|work|labor)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TaskForYouPattern = new(
        "^I have a (?<task>task|errand|job|project|charge|stint) for you$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TaskNeedsDoingPattern = new(
        "^I have a (?<task>task|errand|job|project|charge|stint) that needs doing$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CouldUseYouPattern = new(
        "^I could use you for a (?<task>task|errand|job|project|charge|stint)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CouldDoForMePattern = new(
        "^(?:There's|there's) a (?<task>task|errand|job|project|charge|stint) you could do for me$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex GreatBoonPattern = new(
        "^It would be a great (?<gift>gift|favor|grant|dower|boon) to the (?<prospect>trade|artistic|monetary|spiritual|prayer|festive|safety|musical|architectural|technological) prospects of our village$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex WhatTreasuresPattern = new(
        "^What (?<treasure>treasures|secrets|riches|pearls|mysteries) might this place (?<verb>hold|hide|contain)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ComeCloseOnlyPattern = new(
        "^(?:Come, close!|(?<friend>Friend|Adventurer|Wanderer|Traveler|Drifter|Nomad)!|Live and drink, (?<friend2>friend|adventurer|wanderer|traveler|drifter|nomad)\\.|(?<listen>Listen|Hark|Hear me|Mind what I say), (?<friend3>friend|adventurer|wanderer|traveler|drifter|nomad)\\.|By the .+ .+, (?<friend4>friend|adventurer|wanderer|traveler|drifter|nomad), (?<listen2>listen|hark|hear me|mind what I say)\\.)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ThankArrivalPattern = new(
        "^(?:Thank|Bless|Praise|I'm grateful to|I bow down to|I smile on|Kiss) the .+ .+ that you are here, (?<friend>friend|adventurer|wanderer|traveler|drifter|nomad)\\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SacredPastPattern = new(
        "^(?:.+?[.!] |[^,]+, )?(?:(?<there>There was a time where )?my|(?<once>You know, once )my) (?<kin>kin|people|kind|folk|kinsfolk|tribe|clan) (?<habit>used to spend our days|would spend all our time|would be|would spend our days) \\*Activity\\*(?: all day)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SacredAfterLearningPattern = new(
        "^But after (?<learning>learning|discovering|hearing of|becoming acquainted with) the \\*sanctityOfSacredThing\\*, we changed our (?<ways>ways|customs|culture|habits|priorities|manner) and (?<composed>composed|invented|fashioned|imagined|consecrated) new (?<rituals>rituals|rites|rites of passage|customs|practices|ceremonies)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PersonalItemRumorPattern = new(
        "^(?:(?<recent>Recently|Just a while ago|A short while ago|The other day) )?(?:(?<speaker>I|\\*villagerName\\*) (?<learned>learned|determined|found out|ascertained|gathered) that (?<itemAt>there's \\*itemName\\.an\\* nearby in \\*itemLocation\\*|\\*itemLocation\\* is home to \\*itemName\\.an\\*|\\*itemName\\.an\\* \\*itemName\\.have\\* arrived at \\*itemLocation\\*)|It was brought to my attention that (?<brought>there's \\*itemName\\.an\\* nearby in \\*itemLocation\\*|\\*itemLocation\\* is home to \\*itemName\\.an\\*|\\*itemName\\.an\\* \\*itemName\\.have\\* arrived at \\*itemLocation\\*)|I (?<visited>visited|trekked|journeyed|traveled|voyaged) (?:to )?\\*itemLocation\\* and (?<saw>saw|found|discovered) \\*itemName\\.an\\*|I (?<visitedLost>visited|trekked|journeyed|traveled|voyaged) (?:to )?\\*itemLocation\\* and lost \\*itemName\\.an\\*|I lost my \\*itemName\\* at \\*itemLocation\\*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PersonalLostMyPattern = new(
        "^(?:(?:Recently|Just a while ago|A short while ago|The other day) )?I lost my \\*itemName\\* at \\*itemLocation\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PersonalNeedPattern = new(
        "^\\*NeedsItemFor\\*, I (?:(?<desire>desire|want|need|covet|require|yearn for|am in need of|have use for|must have|must get a hold of)|(?<would>would like to|would love to|need to|must) (?<have>have|acquire|get a hold of|obtain|procure|snag)) \\*it\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SiteTravelersCamePattern = new(
        "^(?:Traveling|Visiting|Roaming) .+ came to our village the other day$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SiteSpokeOfPlacePattern = new(
        "^While \\*GuestActivity\\*, they spoke of a (?<interesting>interesting|intriguing|delightful|fascinating) place, \\*site\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SiteRecordsIntroPattern = new(
        "^(?<traveler>Adventurer|Wanderer|Traveler|Drifter|Nomad|Friend), we've been (?<study>poring over our records|sharing stories with each other|reading the village annals|speaking to \\*.+\\*) and we (?<learned>learned of|learned about|discovered|found out about|came upon) a nearby location forgotten to our people, \\*siteInitLower\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ShrineToPattern = new(
        "^A (?<shrine>relic|shrine|altar) to $",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex RecoverGreetingPattern = new(
        "^We need someone to recover \\*it\\*, (?<friend>friend|adventurer|wanderer|traveler|drifter|nomad)\\. Will you do it\\?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TakenToKinPattern = new(
        "^My (?<kin>kin|people|kind|folk|kinsfolk|tribe|clan) tell me \\*it\\* \\*has\\* been taken to \\*deliveryTarget\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TakenToHearsayPattern = new(
        "^(?:We|I) hear \\*it\\* \\*has\\* been taken to \\*deliveryTarget\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LostOurNamePattern = new(
        "^\\*name\\* lost our \\*itemName\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LostOurStolenPattern = new(
        "^our \\*itemName\\* \\*were\\* stolen from us$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LostOurFatePattern = new(
        "^.+ has separated us from our \\*itemName\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SiteIfYouFindItPattern = new(
        "^If you (?<find>find|locate|pinpoint) it for us, we will (?<verb>reward|pay you for|compensate you for) your (?<service>services|service|assistance|work|labor)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex IfYouRetrieveForMePattern = new(
        "^[Ii]f you (?<retrieve>retrieve|find|recover|fetch|salvage|acquire|get|procure|snag) \\*it\\* for me, I'll (?<verb>reward|pay you for|compensate you for) your (?<service>services|service|assistance|work|labor)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RetrieveAndRewardPattern = new(
        "^(?<retrieve>[Rr]etrieve|[Ff]ind|[Rr]ecover|[Ff]etch|[Ss]alvage|[Aa]cquire|[Gg]et|[Pp]rocure|[Ss]nag) \\*it\\* and I'll (?<verb>reward|pay you for|compensate you for) your (?<service>services|service|assistance|work|labor)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex WillingToRewardIfRetrievePattern = new(
        "^I'm willing to (?<verb>reward|pay you for|compensate you for) your (?<service>services|service|assistance|work|labor) if you (?<retrieve>retrieve|find|recover|fetch|salvage|acquire|get|procure|snag) \\*it\\* for me$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SiteRadiusPattern = new(
        "^We hear that it's located next to \\*landmark\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SiteDirectionPattern = new(
        "^We hear it's located somewhere between \\*min\\* and \\*max\\* parasangs \\*direction\\* of \\*landmark\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SitePathPattern = new(
        "^We hear you can find it by following the \\*path\\* that passes through \\*landmark\\* \\*direction\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemIntroPattern = new(
        "^(?<traveler>Adventurer|Wanderer|Traveler|Drifter|Nomad|Friend), (?<heard>have you heard of|are you aware of|are you acquainted with|have you been introduced to) the \\*itemName\\* at \\*deliveryTarget\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemItIsHolyPattern = new(
        "^\\*It\\* is a (?<sacred>sacred|holy|divine|hallowed|angelic|consecrated|godly|pure|sanctified|venerable) (?<shrine>relic|shrine|altar) to us$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemItIsDespicablePattern = new(
        "^\\*It\\* is a (?<despicable>despicable|disgraceful|vile|wretched|loathsome|foul|cursed|ghastly) (?<horror>horror|abomination|shame|anathema|atrocity) to \\*sacredThing\\* and everything else we (?<dear>hold dear|cherish|value so highly)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemWillInteractPattern = new(
        "^Often we make pilgrimages to \\*verb\\* \\*it\\* and contemplate \\*sacredThing\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemHonorPattern = new(
        "^It would honor us (?<same>if you would do the same|if you would do it too|if you would do it yourself)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemBlessingPattern = new(
        "^It would be a (?<blessing>blessing|honor|favor|boon|comfort|gift) (?<same>if you would do the same|if you would do it too|if you would do it yourself)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemDesecrateHonorPattern = new(
        "^It would honor us (?<greatly>greatly|mightily|much|immensely) if you \\*verb\\* \\*it\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HolyItemDesecrateBlessingPattern = new(
        "^It would be a (?<blessing>blessing|honor|favor|boon|comfort|gift) if you \\*verb\\* it$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex StrangePlanIntroPattern = new(
        "^\\*looks around (?<mode>suspiciously|tentatively|warily|carefully|anxiously)(?: and (?<lean>leans in|comes close|leans forward|whispers))?\\*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex StrangePlanMyPlanPattern = new(
        "^(?:My \\*plan\\* (?<state>has nearly come to fruition|is nearly complete)\\. (?<step>There is but one more step|There is just a single step remaining|Only one more thing must be done)|I have a secret \\*plan\\* I'm (?<enacting>enacting|setting into motion|putting into place))$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex StrangePlanGoToPattern = new(
        "^I need someone to go to \\*deliveryTarget\\* and \\*verb\\* the \\*itemName\\* there$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex StrangePlanTellNoOnePattern = new(
        "^(?:By \\*sacredThing\\*, )?(?<tell>tell no one of this|speak to no one about this|this conversation never happened)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex QudMarkupPattern = new(
        "\\{\\{(?<style>[^|{}]*)\\|(?<text>[^{}]+)\\}\\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    internal static bool TryTranslate(string? source, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                translated = string.Empty;
                return false;
            }

            translated = source;
            return false;
        }

        if (MessageFrameTranslator.TryStripDirectTranslationMarker(source, out var markedText))
        {
            translated = markedText;
            return false;
        }

        var original = source!;
        var (stripped, spans) = ColorAwareTranslationComposer.Strip(original);
        if (!TryTranslateCore(stripped, out var translatedCore))
        {
            return TryNormalizeTranslatedConstructorComposite(original, out translated);
        }

        translatedCore = NormalizeOwnedJapanesePunctuation(TranslateExpandedCaptures(translatedCore));
        translated = ColorAwareTranslationComposer.RestoreWholeSourceBoundaryWrappersPreservingTranslatedOwnership(
            translatedCore,
            spans,
            stripped.Length,
            original);
        return true;
    }

    private static bool TryNormalizeTranslatedConstructorComposite(string source, out string translated)
    {
        if (!ContainsJapaneseCharacters(source))
        {
            translated = source;
            return false;
        }

        translated = NormalizeOwnedJapanesePunctuation(TranslateExpandedCaptures(source));
        if (ContainsDisallowedAsciiOutsideGeneratedMarkers(translated))
        {
            translated = source;
            return false;
        }

        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool ContainsDisallowedAsciiOutsideGeneratedMarkers(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character == '*' || IsQudMarkupOpen(source, index))
            {
                var markerEnd = FindGeneratedMarkerEnd(source, index);
                if (markerEnd > index)
                {
                    index = markerEnd;
                    continue;
                }
            }

            if (character >= 'A' && character <= 'Z')
            {
                while (index + 1 < source.Length
                    && ((source[index + 1] >= 'A' && source[index + 1] <= 'Z')
                        || (source[index + 1] >= 'a' && source[index + 1] <= 'z')))
                {
                    index++;
                }
                continue;
            }

            if (character >= 'a' && character <= 'z')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQudMarkupOpen(string source, int index)
    {
        return index + 1 < source.Length && source[index] == '{' && source[index + 1] == '{';
    }

    private static int FindGeneratedMarkerEnd(string source, int index)
    {
        if (source[index] == '*')
        {
            return source.IndexOf('*', index + 1);
        }

        var closeIndex = source.IndexOf("}}", index, StringComparison.Ordinal);
        return closeIndex < 0 ? -1 : closeIndex + 1;
    }

    private static bool TryTranslateCore(string source, out string translated)
    {
        if (ExactFrames.TryGetValue(source, out translated!))
        {
            return true;
        }

        var match = RewardTailPattern.Match(source);
        if (match.Success)
        {
            return TryTranslateRewardTail(source, match, out translated);
        }

        if (TryTranslateTaskPattern(TaskForYouPattern, source, task => "あなたに頼みたい" + task + "がある", out translated)
            || TryTranslateTaskPattern(TaskNeedsDoingPattern, source, task => "やるべき" + task + "がある", out translated)
            || TryTranslateTaskPattern(CouldUseYouPattern, source, task => task + "であなたの力を借りたい", out translated)
            || TryTranslateTaskPattern(CouldDoForMePattern, source, task => "あなたにやってもらえる" + task + "がある", out translated)
            || TryTranslateQuestBodyFrame(source, out translated)
            || TryTranslateGreatBoon(source, out translated)
            || TryTranslateWhatTreasures(source, out translated)
            || TryTranslateRecoverPrompt(source, out translated)
            || TryTranslateTakenTo(source, out translated)
            || TryTranslateHolyItem(source, out translated)
            || TryTranslateLostOur(source, out translated)
            || TryTranslateIfYouRetrieveIt(source, out translated)
            || TryTranslateSiteDirections(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateRewardTail(string source, Match match, out string translated)
    {
        if (!TryBuildRewardTranslation(match.Groups["verb"].Value, match.Groups["service"].Value, out translated))
        {
            translated = source;
            return false;
        }

        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryBuildRewardTranslation(string verbSource, string serviceSource, out string translated)
    {
        var service = serviceSource switch
        {
            "services" or "service" => "奉仕",
            "assistance" => "助力",
            "work" => "働き",
            "labor" => "労",
            _ => null,
        };
        if (service is null)
        {
            translated = string.Empty;
            return false;
        }

        translated = verbSource switch
        {
            "reward" => "あなたの" + service + "には報いる",
            "pay you for" => "あなたの" + service + "には代価を支払う",
            "compensate you for" => "あなたの" + service + "には報酬を出す",
            _ => string.Empty,
        };
        return translated.Length > 0;
    }

    private static bool TryTranslateTaskPattern(Regex pattern, string source, Func<string, string> build, out string translated)
    {
        var match = pattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var task = match.Groups["task"].Value switch
        {
            "task" => "仕事",
            "errand" => "用事",
            "job" => "仕事",
            "project" => "計画",
            "charge" => "任務",
            "stint" => "務め",
            _ => null,
        };
        if (task is null)
        {
            translated = source;
            return false;
        }

        translated = build(task);
        return true;
    }

    private static bool TryTranslateQuestBodyFrame(string source, out string translated)
    {
        var match = ComeCloseOnlyPattern.Match(source);
        if (match.Success)
        {
            translated = BuildComeCloseFrame(match);
            return true;
        }

        match = ThankArrivalPattern.Match(source);
        if (match.Success)
        {
            translated = "来てくれてありがたい、" + TranslateTraveler(match.Groups["friend"].Value);
            return true;
        }

        if (SacredPastPattern.IsMatch(source))
        {
            translated = "かつて私の同胞は*Activity*に日々を費やしていた";
            return true;
        }

        if (SacredAfterLearningPattern.IsMatch(source))
        {
            translated = "*sanctityOfSacredThing*を知ってから、われらは習わしを改め、新たな儀式を作った";
            return true;
        }

        translated = source switch
        {
            "No, I cannot tell you why" => "いや、理由は話せない",
            "Unfortunately" => "残念ながら",
            "To our great woe" => "われらの大きな悲しみに",
            "Sadly" => "悲しいことに",
            _ => source,
        };
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return true;
        }

        if (source.StartsWith("May ", StringComparison.Ordinal) && source.EndsWith(" take us", StringComparison.Ordinal))
        {
            translated = "なんという災いか";
            return true;
        }

        if (TryTranslatePersonalItemRumor(source, out translated)
            || TryTranslatePersonalNeed(source, out translated)
            || TryTranslateSiteBody(source, out translated)
            || TryTranslateStrangePlan(source, out translated))
        {
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslatePersonalItemRumor(string source, out string translated)
    {
        if (PersonalLostMyPattern.IsMatch(source))
        {
            translated = "*itemLocation*で*itemName*を失くした";
            return true;
        }

        var match = PersonalItemRumorPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var itemAtPlace = match.Groups["itemAt"].Success ? match.Groups["itemAt"].Value : match.Groups["brought"].Value;
        string body;
        if (!string.IsNullOrEmpty(itemAtPlace))
        {
            body = TranslateItemAtPlace(itemAtPlace);
            if (match.Groups["speaker"].Value == "*villagerName*")
            {
                translated = "*villagerName*が" + body + "と知った";
                return true;
            }

            translated = body + "と知った";
            return true;
        }

        if (match.Groups["visited"].Success)
        {
            translated = "*itemLocation*で*itemName.an*を見かけた";
            return true;
        }

        if (match.Groups["visitedLost"].Success || source.StartsWith("I lost my ", StringComparison.OrdinalIgnoreCase))
        {
            translated = "*itemLocation*で*itemName*を失くした";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateItemAtPlace(string source)
    {
        return source switch
        {
            "there's *itemName.an* nearby in *itemLocation*" => "*itemLocation*の近くに*itemName.an*がある",
            "*itemLocation* is home to *itemName.an*" => "*itemLocation*には*itemName.an*がある",
            "*itemName.an* *itemName.have* arrived at *itemLocation*" => "*itemName.an*が*itemLocation*に届いた",
            _ => source,
        };
    }

    private static string TranslateExpandedCaptures(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var translated = QudMarkupPattern.Replace(source, match =>
        {
            var visible = match.Groups["text"].Value;
            var replacement = DynamicQuestGeneratedQuestTextTranslator.TranslateCaptureVisible(visible);
            return "{{" + match.Groups["style"].Value + "|" + replacement + "}}";
        });

        translated = ApplyLiteralReplacements(translated, ExpandedCaptureReplacements);
        translated = ApplyLiteralReplacements(translated, ExpandedCaptureGrammarFixups);
        return translated;
    }

    private static string NormalizeOwnedJapanesePunctuation(string source)
    {
        if (!ContainsJapaneseCharacters(source))
        {
            return source;
        }

        var normalized = source
            .Replace(", ", "、")
            .Replace(". ", "。")
            .Replace("?", "？")
            .Replace(".", "。")
            .Replace("？ ", "？");
        return ApplyLiteralReplacements(normalized, OwnedJapanesePunctuationFixups);
    }

    private static bool ContainsJapaneseCharacters(string source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if ((character >= '\u3040' && character <= '\u30ff')
                || (character >= '\u3400' && character <= '\u9fff'))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly (string Source, string Replacement)[] ExpandedCaptureReplacements =
    [
        ("the sanctity of salt", "塩の聖性"),
        ("tending hearths", "炉の世話"),
        ("breaking bread", "パンを分け合っている"),
        ("cooking", "料理"),
        ("patience", "忍耐"),
        ("pray at", "祈る"),
        ("north", "北"),
        ("south", "南"),
        ("east", "東"),
        ("west", "西"),
        ("northeast", "北東"),
        ("northwest", "北西"),
        ("southeast", "南東"),
        ("southwest", "南西"),
    ];

    private static readonly (string Source, string Replacement)[] ExpandedCaptureGrammarFixups =
    [
        ("パンを分け合っているの間", "パンを分け合っている間"),
    ];

    private static readonly (string Source, string Replacement)[] OwnedJapanesePunctuationFixups =
    [
        ("*itemName。an*", "*itemName.an*"),
        ("友よ 私", "友よ。私"),
        ("か あなた", "か。あなた"),
    ];

    private static string ApplyLiteralReplacements(string source, IReadOnlyList<(string Source, string Replacement)> replacements)
    {
        var translated = source;
        for (var index = 0; index < replacements.Count; index++)
        {
            translated = translated.Replace(replacements[index].Source, replacements[index].Replacement);
        }

        return translated;
    }

    private static bool TryTranslatePersonalNeed(string source, out string translated)
    {
        if (!PersonalNeedPattern.IsMatch(source))
        {
            translated = source;
            return false;
        }

        translated = "*NeedsItemFor*のために、それが必要だ";
        return true;
    }

    private static bool TryTranslateSiteBody(string source, out string translated)
    {
        if (SiteTravelersCamePattern.IsMatch(source))
        {
            translated = "先日、旅人たちがわれらの村に来た";
            return true;
        }

        if (SiteSpokeOfPlacePattern.IsMatch(source))
        {
            translated = "彼らは*GuestActivity*の間、*site*という興味深い場所について話した";
            return true;
        }

        var match = SiteRecordsIntroPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateTraveler(StringHelpers.LowerAscii(match.Groups["traveler"].Value)) + "、われらは記録を調べ、民に忘れられていた近くの場所、*siteInitLower*を見つけた";
            return true;
        }

        match = ShrineToPattern.Match(source);
        if (match.Success)
        {
            translated = TranslateShrine(match.Groups["shrine"].Value) + "：";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateStrangePlan(string source, out string translated)
    {
        if (StrangePlanIntroPattern.IsMatch(source))
        {
            translated = "*慎重にあたりを見回す*";
            return true;
        }

        if (StrangePlanMyPlanPattern.IsMatch(source))
        {
            translated = source.StartsWith("I have a secret ", StringComparison.OrdinalIgnoreCase)
                ? "私は秘密の*plan*を進めている"
                : "私の*plan*はもうすぐ成就する。残る手順はあと一つだ";
            return true;
        }

        if (StrangePlanGoToPattern.IsMatch(source))
        {
            translated = "*deliveryTarget*へ行き、そこの*itemName*を*verb*してくれる者が必要だ";
            return true;
        }

        var match = StrangePlanTellNoOnePattern.Match(source);
        if (match.Success)
        {
            translated = source.StartsWith("By ", StringComparison.OrdinalIgnoreCase)
                ? "*sacredThing*にかけて、このことは誰にも話すな"
                : "このことは誰にも話すな";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateGreatBoon(string source, out string translated)
    {
        var match = GreatBoonPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var gift = match.Groups["gift"].Value switch
        {
            "gift" or "favor" or "grant" or "dower" or "boon" => "恩恵",
            _ => null,
        };
        var prospect = match.Groups["prospect"].Value switch
        {
            "trade" => "交易",
            "artistic" => "芸術",
            "monetary" => "財政",
            "spiritual" => "精神",
            "prayer" => "祈り",
            "festive" => "祝祭",
            "safety" => "安全",
            "musical" => "音楽",
            "architectural" => "建築",
            "technological" => "技術",
            _ => null,
        };
        if (gift is null || prospect is null)
        {
            translated = source;
            return false;
        }

        translated = "それはわれらの村の" + prospect + "の見通しにとって大きな" + gift + "となる";
        return true;
    }

    private static bool TryTranslateWhatTreasures(string source, out string translated)
    {
        var match = WhatTreasuresPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        var treasure = match.Groups["treasure"].Value switch
        {
            "treasures" => "財宝",
            "secrets" => "秘密",
            "riches" => "富",
            "pearls" => "真珠",
            "mysteries" => "謎",
            _ => null,
        };
        var predicate = match.Groups["verb"].Value switch
        {
            "hold" => "ある",
            "hide" => "隠されている",
            "contain" => "収められている",
            _ => null,
        };
        if (treasure is null || predicate is null)
        {
            translated = source;
            return false;
        }

        translated = "この場所にはどんな" + treasure + "が" + predicate + "のか";
        return true;
    }

    private static bool TryTranslateRecoverPrompt(string source, out string translated)
    {
        translated = source switch
        {
            "Would you be willing to recover *it* for us?" => "われらのために*it*を取り戻してくれるか？",
            "Would you seek *it* out and return *it* to us?" => "*it*を探し出し、われらのもとへ返してくれるか？",
            _ => source,
        };
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return true;
        }

        var match = RecoverGreetingPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        translated = "*it*を取り戻してくれる者が必要だ、" + TranslateTraveler(match.Groups["friend"].Value) + "。引き受けてくれるか？";
        return true;
    }

    private static bool TryTranslateTakenTo(string source, out string translated)
    {
        if (TakenToHearsayPattern.IsMatch(source))
        {
            translated = "*deliveryTarget*に運ばれたと聞いている";
            return true;
        }

        var match = TakenToKinPattern.Match(source);
        if (!match.Success)
        {
            translated = source;
            return false;
        }

        translated = "私の" + TranslateKin(match.Groups["kin"].Value) + "によれば、*deliveryTarget*に運ばれたそうだ";
        return true;
    }

    private static bool TryTranslateLostOur(string source, out string translated)
    {
        if (LostOurNamePattern.IsMatch(source))
        {
            translated = "*name*がわれらの*itemName*を失った";
            return true;
        }

        if (LostOurStolenPattern.IsMatch(source))
        {
            translated = "われらの*itemName*は盗まれた";
            return true;
        }

        if (LostOurFatePattern.IsMatch(source))
        {
            translated = "不運により、われらは*itemName*から引き離された";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateIfYouRetrieveIt(string source, out string translated)
    {
        var match = IfYouRetrieveForMePattern.Match(source);
        if (!match.Success)
        {
            match = RetrieveAndRewardPattern.Match(source);
        }

        if (!match.Success)
        {
            match = WillingToRewardIfRetrievePattern.Match(source);
        }

        if (!match.Success || !TryBuildRewardTranslation(match.Groups["verb"].Value, match.Groups["service"].Value, out var reward))
        {
            translated = source;
            return false;
        }

        translated = "*it*を取り戻してくれれば、" + reward;
        return true;
    }

    private static bool TryTranslateSiteDirections(string source, out string translated)
    {
        var match = SiteIfYouFindItPattern.Match(source);
        if (match.Success && TryBuildRewardTranslation(match.Groups["verb"].Value, match.Groups["service"].Value, out var reward))
        {
            translated = "それを見つけてくれれば、" + reward;
            return true;
        }

        if (SiteRadiusPattern.IsMatch(source))
        {
            translated = "*landmark*の隣にあると聞いている";
            return true;
        }

        if (SiteDirectionPattern.IsMatch(source))
        {
            translated = "*landmark*の*direction*、*min*から*max*パラサング離れたどこかにあると聞いている";
            return true;
        }

        if (SitePathPattern.IsMatch(source))
        {
            translated = "*landmark*を*direction*に通る*path*をたどれば見つかると聞いている";
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryTranslateHolyItem(string source, out string translated)
    {
        var match = HolyItemIntroPattern.Match(source);
        if (match.Success)
        {
            translated = "*deliveryTarget*にある*itemName*のことを聞いたことはあるか、"
                + TranslateTraveler(StringHelpers.LowerAscii(match.Groups["traveler"].Value));
            return true;
        }

        match = HolyItemItIsHolyPattern.Match(source);
        if (match.Success)
        {
            translated = "それはわれらにとって"
                + TranslateHolyItemSacred(match.Groups["sacred"].Value)
                + TranslateHolyItemShrine(match.Groups["shrine"].Value)
                + "だ";
            return true;
        }

        match = HolyItemItIsDespicablePattern.Match(source);
        if (match.Success)
        {
            translated = "それは*sacredThing*とわれらが大切にするすべてにとって"
                + TranslateDespicable(match.Groups["despicable"].Value)
                + TranslateHorror(match.Groups["horror"].Value)
                + "だ";
            return true;
        }

        if (HolyItemWillInteractPattern.IsMatch(source))
        {
            translated = "われらはしばしば巡礼して*it*を*verb*し、*sacredThing*について思索する";
            return true;
        }

        match = HolyItemHonorPattern.Match(source);
        if (match.Success)
        {
            translated = "あなたも同じことをしてくれれば、われらの誉れとなる";
            return true;
        }

        match = HolyItemBlessingPattern.Match(source);
        if (match.Success)
        {
            translated = "あなたもそうしてくれれば、それは" + TranslateBlessing(match.Groups["blessing"].Value) + "となる";
            return true;
        }

        match = HolyItemDesecrateHonorPattern.Match(source);
        if (match.Success)
        {
            translated = "あなたが*it*を*verb*してくれれば、われらにとって大きな誉れとなる";
            return true;
        }

        match = HolyItemDesecrateBlessingPattern.Match(source);
        if (match.Success)
        {
            translated = "あなたが*it*を*verb*してくれれば、それは" + TranslateBlessing(match.Groups["blessing"].Value) + "となる";
            return true;
        }

        translated = source;
        return false;
    }

    private static string TranslateTraveler(string source)
    {
        return source switch
        {
            "friend" => "友よ",
            "adventurer" => "冒険者よ",
            "wanderer" => "放浪者よ",
            "traveler" => "旅人よ",
            "drifter" => "漂泊者よ",
            "nomad" => "遊牧民よ",
            _ => source,
        };
    }

    private static string BuildComeCloseFrame(Match match)
    {
        if (match.Groups["friend"].Success)
        {
            return TranslateTraveler(StringHelpers.LowerAscii(match.Groups["friend"].Value)) + "。";
        }

        if (match.Groups["friend2"].Success)
        {
            return "生きて飲め、" + TranslateTraveler(StringHelpers.LowerAscii(match.Groups["friend2"].Value));
        }

        if (match.Groups["friend3"].Success)
        {
            return "聞け、" + TranslateTraveler(StringHelpers.LowerAscii(match.Groups["friend3"].Value));
        }

        if (match.Groups["friend4"].Success)
        {
            return TranslateTraveler(StringHelpers.LowerAscii(match.Groups["friend4"].Value)) + "、聞け";
        }

        return "近くへ、友よ";
    }

    private static string TranslateKin(string source)
    {
        return source switch
        {
            "kin" => "同胞",
            "people" => "民",
            "kind" => "同族",
            "folk" => "仲間",
            "kinsfolk" => "親族",
            "tribe" => "部族",
            "clan" => "氏族",
            _ => source,
        };
    }

    private static string TranslateSacred(string source)
    {
        return source switch
        {
            "sacred" or "holy" or "divine" or "hallowed" or "angelic" or "consecrated" or "godly" or "pure" or "sanctified" or "venerable" => "聖なる",
            _ => source,
        };
    }

    private static string TranslateShrine(string source)
    {
        return source switch
        {
            "relic" => "聖遺物",
            "shrine" => "聖所",
            "altar" => "祭壇",
            _ => source,
        };
    }

    private static string TranslateHolyItemSacred(string source)
    {
        return TranslateSacred(source) == "聖なる" ? "神聖な" : TranslateSacred(source);
    }

    private static string TranslateHolyItemShrine(string source)
    {
        return source switch
        {
            "shrine" => "祠",
            _ => TranslateShrine(source),
        };
    }

    private static string TranslateDespicable(string source)
    {
        return source switch
        {
            "despicable" or "disgraceful" or "vile" or "wretched" or "loathsome" or "foul" or "cursed" or "ghastly" => "忌まわしい",
            _ => source,
        };
    }

    private static string TranslateHorror(string source)
    {
        return source switch
        {
            "horror" => "恐怖",
            "abomination" => "醜悪なもの",
            "shame" => "恥辱",
            "anathema" => "呪わしきもの",
            "atrocity" => "冒涜",
            _ => source,
        };
    }

    private static string TranslateBlessing(string source)
    {
        return source switch
        {
            "blessing" => "祝福",
            "honor" => "誉れ",
            "favor" => "恩恵",
            "boon" => "恩恵",
            "comfort" => "慰め",
            "gift" => "贈り物",
            _ => source,
        };
    }
}
