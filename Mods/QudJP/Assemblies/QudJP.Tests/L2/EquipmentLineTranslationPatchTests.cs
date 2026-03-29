using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

public sealed partial class Issue201StatusScreensBatch2Tests
{
    [Test]
    public void EquipmentLinePostfix_RewritesSlotAndItemTexts_AfterOriginalSetText()
    {
        WriteDictionary(
            ("Right Hand", "右手"),
            ("Chain Glove", "鎖の手袋"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEquipmentLineTarget), nameof(DummyEquipmentLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EquipmentLineTranslationPatch), nameof(EquipmentLineTranslationPatch.Postfix))));

            var target = new DummyEquipmentLineTarget();
            target.setData(new DummyEquipmentLineDataTarget
            {
                bodyPart = new DummyBodyPart
                {
                    Name = "Hand",
                    Primary = true,
                    CardinalDescription = "Right Hand",
                    Equipped = new DummyStatusGameObject { DisplayName = "Chain Glove" },
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.text.Text, Does.Contain("右手"));
                Assert.That(target.text.Text, Does.StartWith("{{G|*}}"));
                Assert.That(target.itemText.Text, Is.EqualTo("鎖の手袋"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(EquipmentLineTranslationPatch),
                        "EquipmentLine.SlotName"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(EquipmentLineTranslationPatch),
                        "EquipmentLine.ItemName"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EquipmentLinePostfix_PreservesObservationOnlySinkContract_WhileFixingOverwrite()
    {
        WriteDictionary(
            ("Right Hand", "右手"),
            ("Chain Glove", "鎖の手袋"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyUITextSkin), nameof(DummyUITextSkin.SetText)),
                prefix: new HarmonyMethod(RequireMethod(typeof(UITextSkinTranslationPatch), nameof(UITextSkinTranslationPatch.Prefix))));
            harmony.Patch(
                original: RequireMethod(typeof(DummyEquipmentLineTarget), nameof(DummyEquipmentLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EquipmentLineTranslationPatch), nameof(EquipmentLineTranslationPatch.Postfix))));

            var output = TestTraceHelper.CaptureTrace(() =>
            {
                var target = new DummyEquipmentLineTarget();
                target.setData(new DummyEquipmentLineDataTarget
                {
                    bodyPart = new DummyBodyPart
                    {
                        Name = "Hand",
                        CardinalDescription = "Right Hand",
                        Equipped = new DummyStatusGameObject { DisplayName = "Chain Glove" },
                    },
                });

                Assert.Multiple(() =>
                {
                    Assert.That(target.OriginalExecuted, Is.True);
                    Assert.That(target.text.Text, Is.EqualTo("右手"));
                    Assert.That(target.itemText.Text, Is.EqualTo("鎖の手袋"));
                });
            });

            Assert.That(output, Does.Contain("source='Right Hand'"),
                "original EquipmentLine.setData still writes English through UITextSkin before the owner-route rewrites it");
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void EquipmentLinePostfix_LeavesOriginalOutput_OnUnsupportedInput()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyEquipmentLineTarget), nameof(DummyEquipmentLineTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(EquipmentLineTranslationPatch), nameof(EquipmentLineTranslationPatch.Postfix))));

            var target = new DummyEquipmentLineTarget();
            target.setData(new DummyFallbackEquipmentLineDataTarget());

            Assert.Multiple(() =>
            {
                Assert.That(target.OriginalExecuted, Is.True);
                Assert.That(target.itemText.Text, Is.EqualTo("equipment fallback"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
