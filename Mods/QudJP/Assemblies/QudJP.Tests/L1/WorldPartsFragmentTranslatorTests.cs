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
    public void EnclosingTranslator_TranslatesExtricatePopup(string input, string expected)
    {
        AssertTranslated(
            EnclosingFragmentTranslator.TryTranslatePopupMessage,
            "Enclosing",
            input,
            expected);
    }

    [TestCase("You extricate {{r|snapjaw}} from {{C|stasis pod}}.", "{{C|stasis pod}}から{{r|snapjaw}}を引き出した。")]
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
