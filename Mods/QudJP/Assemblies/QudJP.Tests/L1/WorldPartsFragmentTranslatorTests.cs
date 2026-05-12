using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class WorldPartsFragmentTranslatorTests
{
    [TestCase("You cannot seem to interact with canteen in any way.", "canteenにはどうやっても干渉できないようだ。")]
    [TestCase("The canteen is not owned by you. Are you sure you want to drink from it?", "canteenはあなたの所有物ではない。本当にそこから飲みますか？")]
    [TestCase("You are now {{B|hydrated}}.", "あなたは今、{{B|hydrated}}。")]
    [TestCase("canteen has no drain.", "canteenには排出口がない。")]
    [TestCase("canteen is sealed.", "canteenは密閉されている。")]
    [TestCase("canteen is empty.", "canteenは空だ。")]
    [TestCase("You can't pour from a container into itself.", "それ自身に容器から注ぐことはできない。")]
    [TestCase("You can't pour from a container into {{Y|itself}}.", "{{Y|それ自身}}に容器から注ぐことはできない。")]
    [TestCase("Do you want to empty canteen first?", "canteenを先に空にしますか？")]
    public void LiquidVolumeTranslator_TranslatesPopupFragments(string input, string expected)
    {
        AssertTranslated(
            LiquidVolumeFragmentTranslator.TryTranslatePopupMessage,
            "LiquidVolume",
            input,
            expected);
    }

    [TestCase("The canteen is not owned by you. Are you sure you want to drain it?", "canteenはあなたの所有物ではない。本当に排出しますか？")]
    [TestCase("The canteen is not owned by you. Are you sure you want to fill it?", "canteenはあなたの所有物ではない。本当に満たしますか？")]
    public void LiquidVolumeTranslator_TranslatesOwnershipPopupFragments(string input, string expected)
    {
        AssertTranslated(
            LiquidVolumeFragmentTranslator.TryTranslatePopupMessage,
            "LiquidVolume",
            input,
            expected);
    }

    [TestCase("")]
    [TestCase("This is a random liquid message.")]
    [TestCase("Are you sure you want to drink from canteen?")]
    [TestCase("\u0001You cannot seem to interact with canteen in any way.")]
    public void LiquidVolumeTranslator_ReturnsFalse_ForPassthroughPopupFragments(string input)
    {
        AssertPassthrough(
            LiquidVolumeFragmentTranslator.TryTranslatePopupMessage,
            "LiquidVolume",
            input);
    }

    [TestCase("Do you want to empty the canteen first?", "canteenを先に空にしますか？")]
    [TestCase("You can't pour from a container into yourself.", "自分に容器から注ぐことはできない。")]
    public void LiquidVolumeTranslator_HandlesNormalizedTargets(string input, string expected)
    {
        AssertTranslated(
            LiquidVolumeFragmentTranslator.TryTranslatePopupMessage,
            "LiquidVolume",
            input,
            expected);
    }

    [TestCase("The canteen is not owned by you. Are you sure you want to pour from it?", "canteenはあなたの所有物ではない。本当にそこから注ぎますか？")]
    [TestCase("Are you sure you want to drain the canteen?", "canteenを本当に排出しますか？")]
    [TestCase("The canteen is not owned by you. Are you sure you want to take from it?", "canteenはあなたの所有物ではない。本当にそこから取りますか？")]
    [TestCase("The canteen is not owned by you. Are you sure you want to collect from it?", "canteenはあなたの所有物ではない。本当にそこから集めますか？")]
    [TestCase("You are able to collect 129 drams of {{C|water}}. Are you sure you want to?", "{{C|water}}を129ドラム集められる。本当にそうしますか？")]
    [TestCase("The canteen is not owned by you. Are you sure you want to use {{C|water}} from it?", "canteenはあなたの所有物ではない。{{C|water}}を本当にそこから使いますか？")]
    [TestCase("2 drams of {{C|water}} pours out all over you!", "{{C|water}} 2ドラムがあなたの全身にかかった！")]
    public void LiquidVolumeTranslator_TranslatesInventoriedOwnerPopupFragments(string input, string expected)
    {
        AssertTranslated(
            LiquidVolumeFragmentTranslator.TryTranslatePopupMessage,
            "LiquidVolume",
            input,
            expected);
    }

    [TestCase("It's fizzy.", "シュワシュワしている。")]
    [TestCase("2 drams of {{C|water}} pours out all over snapjaw!", "{{C|water}} 2ドラムがsnapjawの全身にかかった！")]
    public void LiquidVolumeTranslator_TranslatesInventoriedOwnerQueuedFragments(string input, string expected)
    {
        AssertTranslated(
            LiquidVolumeFragmentTranslator.TryTranslateQueuedMessage,
            "LiquidVolume",
            input,
            expected);
    }

    [TestCase("")]
    [TestCase("random garbage text")]
    [TestCase("\u0001It's fizzy.")]
    [TestCase("{{C|water}}\u0001something")]
    public void LiquidVolumeTranslator_ReturnsFalse_ForPassthroughQueuedFragments(string input)
    {
        AssertPassthrough(
            LiquidVolumeFragmentTranslator.TryTranslateQueuedMessage,
            "LiquidVolume",
            input);
    }

    [TestCase("You drop desalination pellet into canteen.\n\nThe water is purified.", "desalination pelletをcanteenに入れた。\n\nThe water is purified.")]
    [TestCase("You drop {{Y|desalination pellet}} into {{W|canteen}}.\n\n{{g|The water is purified.}}", "{{Y|desalination pellet}}を{{W|canteen}}に入れた。\n\n{{g|The water is purified.}}")]
    public void DesalinationPelletTranslator_TranslatesCompositePopupPrefix(string input, string expected)
    {
        AssertTranslated(
            DesalinationPelletFragmentTranslator.TryTranslatePopupMessage,
            "DesalinationPellet",
            input,
            expected);
    }

    [TestCase("")]
    [TestCase("\u0001")]
    [TestCase("You drop desalination pellet.")]
    [TestCase("The water is purified.")]
    public void DesalinationPelletTranslator_ReturnsFalse_ForPassthroughPopupFragments(string input)
    {
        AssertPassthrough(
            DesalinationPelletFragmentTranslator.TryTranslatePopupMessage,
            "DesalinationPellet",
            input);
    }

    [TestCase("You do not have 1 dram of sunslag.", "sunslagを1ドラム持っていない。")]
    public void ClonelingVehicleTranslator_TranslatesPopupFragments(string input, string expected)
    {
        AssertTranslated(
            ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage,
            "WorldParts.Popup",
            input,
            expected);
    }

    [TestCase("You do not have 1 dram of {{C|sunslag}}.", "{{C|sunslag}}を1ドラム持っていない。")]
    public void ClonelingVehicleTranslator_PreservesColorTagsInPopupFragments(string input, string expected)
    {
        AssertTranslated(
            ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage,
            "WorldParts.Popup",
            input,
            expected);
    }

    [TestCase("")]
    [TestCase("This is a random popup message.")]
    [TestCase("\u0001You do not have 1 dram of sunslag.")]
    public void ClonelingVehicleTranslator_ReturnsFalse_ForPassthroughPopupFragments(string input)
    {
        AssertPassthrough(
            ClonelingVehicleFragmentTranslator.TryTranslatePopupMessage,
            "WorldParts.Popup",
            input);
    }

    [TestCase("Your onboard systems are out of cloning draught.", "搭載システムのcloning draughtが切れている。")]
    public void ClonelingVehicleTranslator_TranslatesQueuedFragments(string input, string expected)
    {
        AssertTranslated(
            ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage,
            "WorldParts.Queue",
            input,
            expected);
    }

    [TestCase("Your onboard systems are out of {{G|cloning draught}}.", "搭載システムの{{G|cloning draught}}が切れている。")]
    public void ClonelingVehicleTranslator_PreservesColorTagsInQueuedFragments(string input, string expected)
    {
        AssertTranslated(
            ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage,
            "WorldParts.Queue",
            input,
            expected);
    }

    [TestCase("")]
    [TestCase("This is a random queued message.")]
    [TestCase("\u0001Your onboard systems are out of cloning draught.")]
    public void ClonelingVehicleTranslator_ReturnsFalse_ForPassthroughQueuedFragments(string input)
    {
        AssertPassthrough(
            ClonelingVehicleFragmentTranslator.TryTranslateQueuedMessage,
            "WorldParts.Queue",
            input);
    }

    [TestCase("You extricate yourself from stasis pod.", "stasis podから抜け出した。")]
    [TestCase("You extricate itself from stasis pod.", "stasis podからそれ自身を引き出した。")]
    [TestCase("You extricate snapjaw from stasis pod.", "stasis podからsnapjawを引き出した。")]
    [TestCase("You are already in stasis pod.", "すでにstasis podの中にいる。")]
    [TestCase("You fail to get yourself into stasis pod.", "stasis podに入れなかった。")]
    [TestCase("You fail to get snapjaw into stasis pod.", "snapjawをstasis podの中に入れられなかった。")]
    [TestCase("It is not stasis pod that you are enclosed by.", "閉じ込めているのはstasis podではない。")]
    [TestCase("You cannot do that while enclosed by stasis pod.", "stasis podに閉じ込められている間はそれをできない。")]
    public void EnclosingTranslator_TranslatesExtricatePopup(string input, string expected)
    {
        AssertTranslated(
            EnclosingFragmentTranslator.TryTranslatePopupMessage,
            "Enclosing",
            input,
            expected);
    }

    [TestCase("You extricate {{r|snapjaw}} from {{C|stasis pod}}.", "{{C|stasis pod}}から{{r|snapjaw}}を引き出した。")]
    [TestCase("You fail to get {{r|snapjaw}} into {{C|stasis pod}}.", "{{r|snapjaw}}を{{C|stasis pod}}の中に入れられなかった。")]
    public void EnclosingTranslator_PreservesColorTagsInExtricatePopup(string input, string expected)
    {
        AssertTranslated(
            EnclosingFragmentTranslator.TryTranslatePopupMessage,
            "Enclosing",
            input,
            expected);
    }

    [TestCase("")]
    [TestCase("This is a random enclosing message.")]
    [TestCase("\u0001You extricate snapjaw from stasis pod.")]
    public void EnclosingTranslator_ReturnsFalse_ForPassthroughPopup(string input)
    {
        AssertPassthrough(
            EnclosingFragmentTranslator.TryTranslatePopupMessage,
            "Enclosing",
            input);
    }

    [TestCase("snapjaw tries to get itself into the stasis pod, but fails.", "snapjawはそれ自身をthe stasis podの中に入れようとしたが、失敗した。")]
    public void EnclosingTranslator_TranslatesQueuedFragments(string input, string expected)
    {
        var message = input;

        var ok = EnclosingFragmentTranslator.TryTranslateQueuedMessage(
            ref message,
            null,
            nameof(WorldPartsFragmentTranslatorTests),
            "Enclosing");

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(message, Is.EqualTo(expected));
        });
    }

    [TestCase("Use {{W|Shift+D}} to descend.", "{{W|Shift+D}}で下に降りてください。")]
    [TestCase("Use {{W|Shift+U}} to ascend.", "{{W|Shift+U}}で上に昇ってください。")]
    public void StairsTranslator_TranslatesPopupFragments(string input, string expected)
    {
        if (input.Contains("descend", StringComparison.Ordinal))
        {
            AssertTranslated(
                StairsDownFragmentTranslator.TryTranslatePopupMessage,
                "StairsDown",
                input,
                expected);
        }
        else
        {
            AssertTranslated(
                StairsUpFragmentTranslator.TryTranslatePopupMessage,
                "StairsUp",
                input,
                expected);
        }
    }

    [TestCase("")]
    [TestCase("Use stairs to ascend.")]
    [TestCase("\u0001Use {{W|Shift+D}} to descend.")]
    public void StairsDownTranslator_ReturnsFalse_ForPassthroughPopupFragments(string input)
    {
        AssertPassthrough(
            StairsDownFragmentTranslator.TryTranslatePopupMessage,
            "StairsDown",
            input);
    }

    [TestCase("")]
    [TestCase("Use stairs to descend.")]
    [TestCase("\u0001Use {{W|Shift+U}} to ascend.")]
    public void StairsUpTranslator_ReturnsFalse_ForPassthroughPopupFragments(string input)
    {
        AssertPassthrough(
            StairsUpFragmentTranslator.TryTranslatePopupMessage,
            "StairsUp",
            input);
    }

    private static void AssertTranslated(
        TranslatorDelegate translator,
        string family,
        string input,
        string expected)
    {
        var ok = translator(
            input,
            nameof(WorldPartsFragmentTranslatorTests),
            family,
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    private static void AssertPassthrough(
        TranslatorDelegate translator,
        string family,
        string input)
    {
        var ok = translator(
            input,
            nameof(WorldPartsFragmentTranslatorTests),
            family,
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(input));
        });
    }

    private delegate bool TranslatorDelegate(string source, string route, string family, out string translated);
}
