#if HAS_GAME_DLL
using QudJP.Patches;
using QudJP.UI;
using XRL.UI;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class LookTooltipInformationWrapPatchTests
{
    [Test]
    public void Postfix_WrapsLongDescriptionBeforeTooltipRtfFormatting()
    {
        var source = new string('あ', JapaneseBlockWrap.DefaultTooltipVisibleColumns + 1);
        var information = new Look.TooltipInformation
        {
            LongDescription = source,
        };

        LookTooltipInformationWrapPatch.Postfix(ref information);

        Assert.That(
            information.LongDescription,
            Is.EqualTo(new string('あ', JapaneseBlockWrap.DefaultTooltipVisibleColumns) + "\nあ"));
    }

    [TestCase("")]
    [TestCase("The quick brown fox")]
    public void Postfix_PreservesNoOpLongDescriptions(string source)
    {
        var information = new Look.TooltipInformation
        {
            LongDescription = source,
        };

        LookTooltipInformationWrapPatch.Postfix(ref information);

        Assert.That(information.LongDescription, Is.EqualTo(source));
    }
}
#endif
