using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class KeybindBoxTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-keybind-box-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesPressKeyPrompt_WhenPatched()
    {
        WriteDictionary(("{{R|press key...}}", "Qud.UI.KeybindBox", "{{R|キーを押してください...}}"));

        var harmonyId = "qudjp.tests.keybind-box." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindBoxTarget), nameof(DummyKeybindBoxTarget.Update)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(KeybindBoxTranslationPatch),
                    nameof(KeybindBoxTranslationPatch.Postfix))));

            var target = new DummyKeybindBoxTarget { editMode = true };
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(target.textSkin.text, Is.EqualTo("{{R|キーを押してください...}}"));
                Assert.That(target.textSkin.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        KeybindBoxTranslationPatch.Context,
                        KeybindBoxTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_PreservesColorTagsAroundTranslatedPrompt_WhenPatched()
    {
        WriteDictionary(("press key...", "Qud.UI.KeybindBox", "キーを押してください..."));

        var harmonyId = "qudjp.tests.keybind-box." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindBoxTarget), nameof(DummyKeybindBoxTarget.Update)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(KeybindBoxTranslationPatch),
                    nameof(KeybindBoxTranslationPatch.Postfix))));

            var target = new DummyKeybindBoxTarget { editMode = true };
            target.textSkin.text = "{{R|press key...}}";
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(target.textSkin.text, Is.EqualTo("{{R|キーを押してください...}}"));
                Assert.That(target.textSkin.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        KeybindBoxTranslationPatch.Context,
                        KeybindBoxTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("UP", "上")]
    [TestCase("{{Y|UP}}", "{{Y|上}}")]
    public void Postfix_TranslatesLowerAsciiFallback_WhenPatched(string source, string expected)
    {
        WriteDictionary(("up", "Qud.UI.KeybindBox", "上"));

        var harmonyId = "qudjp.tests.keybind-box." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindBoxTarget), nameof(DummyKeybindBoxTarget.Update)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(KeybindBoxTranslationPatch),
                    nameof(KeybindBoxTranslationPatch.Postfix))));

            var target = new DummyKeybindBoxTarget { textSkin = { text = source } };
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(target.textSkin.text, Is.EqualTo(expected));
                Assert.That(target.textSkin.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        KeybindBoxTranslationPatch.Context,
                        KeybindBoxTranslationPatch.Family),
                    Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesUnknownTextUnchanged_WhenPatched()
    {
        var harmonyId = "qudjp.tests.keybind-box." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindBoxTarget), nameof(DummyKeybindBoxTarget.Update)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(KeybindBoxTranslationPatch),
                    nameof(KeybindBoxTranslationPatch.Postfix))));

            var target = new DummyKeybindBoxTarget { textSkin = { text = "{{c|None}}" } };
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(target.textSkin.text, Is.EqualTo("{{c|None}}"));
                Assert.That(target.textSkin.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        KeybindBoxTranslationPatch.Context,
                        KeybindBoxTranslationPatch.Family),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_StripsDirectMarkedText_WhenPatched()
    {
        var harmonyId = "qudjp.tests.keybind-box." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindBoxTarget), nameof(DummyKeybindBoxTarget.Update)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(KeybindBoxTranslationPatch),
                    nameof(KeybindBoxTranslationPatch.Postfix))));

            var target = new DummyKeybindBoxTarget
            {
                textSkin = { text = MessageFrameTranslator.MarkDirectTranslation("{{c|None}}") },
            };
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(target.textSkin.text, Is.EqualTo("{{c|None}}"));
                Assert.That(target.textSkin.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        KeybindBoxTranslationPatch.Context,
                        KeybindBoxTranslationPatch.Family),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesEmptyTextUnchanged_WhenPatched()
    {
        var harmonyId = "qudjp.tests.keybind-box." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyKeybindBoxTarget), nameof(DummyKeybindBoxTarget.Update)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(KeybindBoxTranslationPatch),
                    nameof(KeybindBoxTranslationPatch.Postfix))));

            var target = new DummyKeybindBoxTarget { textSkin = { text = string.Empty } };
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(target.textSkin.text, Is.EqualTo(string.Empty));
                Assert.That(target.textSkin.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        KeybindBoxTranslationPatch.Context,
                        KeybindBoxTranslationPatch.Family),
                    Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string context, string text)[] entries)
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

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"context\":\"");
            builder.Append(EscapeJson(entries[index].context));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        File.WriteAllText(Path.Combine(tempDirectory, "ui-keybinds.ja.json"), builder.ToString());
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private sealed class DummyKeybindBoxTarget
    {
        public DummyTextSkin textSkin = new();
        public bool editMode;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Update()
        {
            if (editMode)
            {
                textSkin.text = "{{R|press key...}}";
            }

            textSkin.Apply();
        }
    }

    private sealed class DummyTextSkin
    {
        public string text = string.Empty;
        public int AppliedCount { get; private set; }

        public void Apply()
        {
            AppliedCount++;
        }
    }
}
