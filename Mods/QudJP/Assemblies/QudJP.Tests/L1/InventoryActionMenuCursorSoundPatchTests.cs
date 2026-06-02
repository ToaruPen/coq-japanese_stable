using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class InventoryActionMenuCursorSoundPatchTests
{
    [Test]
    public void ShouldPlayCursorSound_OnlyWhenInventoryActionMenuSelectionChanges()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                InventoryActionMenuCursorSoundPatch.ShouldPlayCursorSoundForTests(
                    "InventoryActionMenu:abc",
                    previousSelectedOption: 1,
                    currentSelectedOption: 2,
                    isActiveAndEnabled: true),
                Is.True);
            Assert.That(
                InventoryActionMenuCursorSoundPatch.ShouldPlayCursorSoundForTests(
                    "Popup:Other",
                    previousSelectedOption: 1,
                    currentSelectedOption: 2,
                    isActiveAndEnabled: true),
                Is.False);
            Assert.That(
                InventoryActionMenuCursorSoundPatch.ShouldPlayCursorSoundForTests(
                    "InventoryActionMenu:abc",
                    previousSelectedOption: 1,
                    currentSelectedOption: 1,
                    isActiveAndEnabled: true),
                Is.False);
            Assert.That(
                InventoryActionMenuCursorSoundPatch.ShouldPlayCursorSoundForTests(
                    "InventoryActionMenu:abc",
                    previousSelectedOption: 1,
                    currentSelectedOption: 2,
                    isActiveAndEnabled: false),
                Is.False);
        });
    }
}
