using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class LocationFinderPopupTranslationPatchTests
{
    private const string DiscoverSource = "You discover {{Y|some forgotten ruins}}!";
    private const string TravelSource = "You traveled to {{Y|some forgotten ruins}}!";
    private const string DiscoverTranslated = "{{Y|some forgotten ruins}}を発見した！";
    private const string TravelTranslated = "{{Y|some forgotten ruins}}へ移動した！";

    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-location-finder-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();

        WriteDictionary(
            ("You discover {0}!", "{0}を発見した！"),
            ("You traveled to {0}!", "{0}へ移動した！"));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(DiscoverSource, DiscoverTranslated, "LocationFinderDiscover")]
    [TestCase(TravelSource, TravelTranslated, "LocationFinderTravel")]
    public void Patch_TranslatesLocationFinderPopup_WhenOwnerPatched(
        string source,
        string expected,
        string expectedFamilySuffix)
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyLocationFinderProducer
            {
                PopupMessageToShow = source,
            };

            target.TriggerFind();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.Show." + expectedFamilySuffix),
                    Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotRecordOwnerRoute_WhenOwnerAbsent()
    {
        RunWithPopupPatchOnly(() => DummyPopupShow.Show(DiscoverSource));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(DiscoverTranslated));
            Assert.That(GetDiscoverHitCount(), Is.EqualTo(0));
        });
    }

    [Test]
    public void Patch_StripsDirectMarkedPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(DiscoverSource);

        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyLocationFinderProducer
            {
                PopupMessageToShow = source,
            };

            target.TriggerFind();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(DiscoverSource));
                Assert.That(GetDiscoverHitCount(), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyLocationFinderProducer
            {
                PopupMessageToShow = string.Empty,
            };

            target.TriggerFind();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Patch_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "You found nothing of interest.";

        RunWithOwnerAndPopupPatches(() =>
        {
            var target = new DummyLocationFinderProducer
            {
                PopupMessageToShow = source,
            };

            target.TriggerFind();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(GetDiscoverHitCount(), Is.Zero);
                Assert.That(GetTravelHitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        RunWithOwnerAndPopupPatches(
            [
                nameof(DummyLocationFinderProducer.TriggerFind),
                nameof(DummyLocationFinderProducer.TriggerNestedFind),
            ],
            () =>
            {
                var innerTarget = new DummyLocationFinderProducer
                {
                    PopupMessageToShow = TravelSource,
                };
                var outerTarget = new DummyLocationFinderProducer
                {
                    PopupMessageToShow = DiscoverSource,
                    BeforePopup = () =>
                    {
                        innerTarget.TriggerNestedFind();

                        Assert.Multiple(() =>
                        {
                            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(TravelTranslated));
                            Assert.That(GetTravelHitCount(), Is.EqualTo(1));
                            Assert.That(GetDiscoverHitCount(), Is.Zero);
                        });
                    },
                };

                outerTarget.TriggerFind();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(DiscoverTranslated));
                    Assert.That(GetTravelHitCount(), Is.EqualTo(1));
                    Assert.That(GetDiscoverHitCount(), Is.EqualTo(1));
                });
            });
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameters)
    {
        return (parameters.Length == 0
                ? AccessTools.Method(type, methodName)
                : AccessTools.Method(type, methodName, parameters))
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static void RunWithPopupPatchOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithOwnerAndPopupPatches(Action action)
    {
        RunWithOwnerAndPopupPatches([nameof(DummyLocationFinderProducer.TriggerFind)], action);
    }

    private static void RunWithOwnerAndPopupPatches(string[] ownerMethodNames, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            foreach (var ownerMethodName in ownerMethodNames)
            {
                harmony.Patch(
                    original: RequireMethod(typeof(DummyLocationFinderProducer), ownerMethodName),
                    prefix: new HarmonyMethod(RequireMethod(typeof(LocationFinderPopupTranslationPatch), nameof(LocationFinderPopupTranslationPatch.Prefix))),
                    finalizer: new HarmonyMethod(RequireMethod(typeof(LocationFinderPopupTranslationPatch), nameof(LocationFinderPopupTranslationPatch.Finalizer), typeof(Exception))));
            }

            PatchPopupShow(harmony);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static int GetDiscoverHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.LocationFinderDiscover");
    }

    private static int GetTravelHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.LocationFinderTravel");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append('{');
            builder.Append("\"key\":");
            builder.Append(System.Text.Json.JsonSerializer.Serialize(entries[index].key));
            builder.Append(',');
            builder.Append("\"text\":");
            builder.Append(System.Text.Json.JsonSerializer.Serialize(entries[index].text));
            builder.Append('}');
        }

        builder.Append("]}");
        File.WriteAllText(Path.Combine(tempDirectory, "location-finder.ja.json"), builder.ToString(), Utf8WithoutBom);
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private sealed class DummyLocationFinderProducer
    {
        public string PopupMessageToShow = string.Empty;
        public Action? BeforePopup;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TriggerFind()
        {
            BeforePopup?.Invoke();
            DummyPopupShow.Show(PopupMessageToShow);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TriggerNestedFind()
        {
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
