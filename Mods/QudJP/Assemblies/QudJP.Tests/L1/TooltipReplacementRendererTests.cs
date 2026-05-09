#if HAS_TMP
namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class TooltipReplacementRendererTests
{
    [Test]
    public void ShouldAttemptReplacementForTests_TargetsOnlyVisibleNonEmptyBlankTmp()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TooltipReplacementRenderer.ShouldAttemptReplacementForTests(
                    enabled: true,
                    activeInHierarchy: true,
                    text: "日本語",
                    objectName: "BodyText",
                    characterCount: 0),
                Is.True);
            Assert.That(
                TooltipReplacementRenderer.ShouldAttemptReplacementForTests(
                    enabled: true,
                    activeInHierarchy: true,
                    text: "日本語",
                    objectName: "BodyText",
                    characterCount: 3),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldAttemptReplacementForTests(
                    enabled: false,
                    activeInHierarchy: true,
                    text: "日本語",
                    objectName: "BodyText",
                    characterCount: 0),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldAttemptReplacementForTests(
                    enabled: true,
                    activeInHierarchy: false,
                    text: "日本語",
                    objectName: "BodyText",
                    characterCount: 0),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldAttemptReplacementForTests(
                    enabled: true,
                    activeInHierarchy: true,
                    text: string.Empty,
                    objectName: "BodyText",
                    characterCount: 0),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldAttemptReplacementForTests(
                    enabled: true,
                    activeInHierarchy: true,
                    text: "日本語",
                    objectName: "QudJPTooltipReplacementText",
                    characterCount: 0),
                Is.False);
        });
    }

    [Test]
    public void ShouldDisableReplacementForTests_DisablesWhenOriginalTmpRenders()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TooltipReplacementRenderer.ShouldDisableReplacementForTests(
                    enabled: true,
                    activeInHierarchy: true,
                    characterCount: 1),
                Is.True);
            Assert.That(
                TooltipReplacementRenderer.ShouldDisableReplacementForTests(
                    enabled: true,
                    activeInHierarchy: true,
                    characterCount: 0),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldDisableReplacementForTests(
                    enabled: false,
                    activeInHierarchy: true,
                    characterCount: 1),
                Is.False);
        });
    }

    [Test]
    public void ExistingReplacementStateForTests_RefreshesOnlyReusableVisibleSourceText()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TooltipReplacementRenderer.ShouldRefreshExistingReplacementForTests(
                    replacementExists: true,
                    activeInHierarchy: true,
                    text: "更新後"),
                Is.True);
            Assert.That(
                TooltipReplacementRenderer.ShouldRefreshExistingReplacementForTests(
                    replacementExists: true,
                    activeInHierarchy: false,
                    text: "更新後"),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldRefreshExistingReplacementForTests(
                    replacementExists: true,
                    activeInHierarchy: true,
                    text: string.Empty),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldRefreshExistingReplacementForTests(
                    replacementExists: false,
                    activeInHierarchy: true,
                    text: "更新後"),
                Is.False);
        });
    }

    [Test]
    public void ExistingReplacementStateForTests_HidesStaleReplacementWhenSourceIsGone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TooltipReplacementRenderer.ShouldHideExistingReplacementForTests(
                    replacementExists: true,
                    activeInHierarchy: false,
                    text: "前回"),
                Is.True);
            Assert.That(
                TooltipReplacementRenderer.ShouldHideExistingReplacementForTests(
                    replacementExists: true,
                    activeInHierarchy: true,
                    text: string.Empty),
                Is.True);
            Assert.That(
                TooltipReplacementRenderer.ShouldHideExistingReplacementForTests(
                    replacementExists: true,
                    activeInHierarchy: true,
                    text: "更新後"),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldHideExistingReplacementForTests(
                    replacementExists: false,
                    activeInHierarchy: false,
                    text: string.Empty),
                Is.False);
        });
    }

    [Test]
    public void ExistingReplacementStateForTests_RefreshesHiddenExistingReplacementWhenSourceTextReturns()
    {
        Assert.That(
            TooltipReplacementRenderer.ShouldRefreshExistingReplacementForTests(
                replacementExists: true,
                activeInHierarchy: true,
                text: "再表示"),
            Is.True);
    }

    [Test]
    public void ExistingReplacementStateForTests_RestoresOriginalWhenRefreshFails()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TooltipReplacementRenderer.ShouldRestoreOriginalAfterExistingReplacementRefreshForTests(
                    replacementExists: true,
                    renderSucceeded: false),
                Is.True);
            Assert.That(
                TooltipReplacementRenderer.ShouldRestoreOriginalAfterExistingReplacementRefreshForTests(
                    replacementExists: true,
                    renderSucceeded: true),
                Is.False);
            Assert.That(
                TooltipReplacementRenderer.ShouldRestoreOriginalAfterExistingReplacementRefreshForTests(
                    replacementExists: false,
                    renderSucceeded: false),
                Is.False);
        });
    }
}
#endif
