using System.Reflection;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WorldPartGeneratedDisplayNameTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-world-part-generated-display-name-l2", Guid.NewGuid().ToString("N"));
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
    public void ModQuantumReverbPostfix_TranslatesReturnedHologramDisplayName()
    {
        WriteDictionary(("snapjaw", "スナップジョー"));

        RunWithPostfix(
            typeof(DummyModQuantumReverbTarget),
            nameof(DummyModQuantumReverbTarget.CreateHologramOf),
            new[] { typeof(DummyGameObject) },
            typeof(ModQuantumReverbDisplayNameTranslationPatch),
            nameof(ModQuantumReverbDisplayNameTranslationPatch.Postfix),
            () =>
            {
                var result = DummyModQuantumReverbTarget.CreateHologramOf(new DummyGameObject("snapjaw"));
                Assert.That(result.Render.DisplayName, Is.EqualTo("スナップジョーのホログラム"));
            });
    }

    [TestCase(null, "snapjawのホログラム")]
    [TestCase("hologram of a {{C|snapjaw}}", "{{C|snapjaw}}のホログラム")]
    [TestCase("", "")]
    [TestCase("\u0001hologram of a snapjaw", "hologram of a snapjaw")]
    public void ModQuantumReverbPostfix_HandlesFallbackEmptyAndDirectMarkedDisplayNames(
        string? generatedNameOverride,
        string expected)
    {
        DummyModQuantumReverbTarget.GeneratedDisplayNameOverride = generatedNameOverride;
        try
        {
            RunWithPostfix(
                typeof(DummyModQuantumReverbTarget),
                nameof(DummyModQuantumReverbTarget.CreateHologramOf),
                new[] { typeof(DummyGameObject) },
                typeof(ModQuantumReverbDisplayNameTranslationPatch),
                nameof(ModQuantumReverbDisplayNameTranslationPatch.Postfix),
                () =>
                {
                    var result = DummyModQuantumReverbTarget.CreateHologramOf(new DummyGameObject("snapjaw"));
                    Assert.That(result.Render.DisplayName, Is.EqualTo(expected));
                });
        }
        finally
        {
            DummyModQuantumReverbTarget.GeneratedDisplayNameOverride = null;
        }
    }

    [Test]
    public void RandomStatuePostfix_TranslatesParentRenderDisplayName()
    {
        WriteDictionaryWithContexts(
            ("snapjaw", "スナップジョー", null),
            ("stone", "石", "GetDisplayName.GeneratedRandomStatue.Component"),
            ("statue", "像", "GetDisplayName.GeneratedRandomStatue.Component"));

        RunWithPostfix(
            typeof(DummyRandomStatueTarget),
            nameof(DummyRandomStatueTarget.SetCreature),
            new[] { typeof(DummyGameObject) },
            typeof(RandomStatueDisplayNameTranslationPatch),
            nameof(RandomStatueDisplayNameTranslationPatch.Postfix),
            () =>
            {
                var part = new DummyRandomStatueTarget();
                part.SetCreature(new DummyGameObject("snapjaw"));
                Assert.That(part.ParentObject.Render.DisplayName, Is.EqualTo("スナップジョーの石の像"));
            });
    }

    [Test]
    public void RandomStatuePostfix_PreservesColorTagsInGeneratedDisplayName()
    {
        WriteDictionaryWithContexts(
            ("snapjaw", "スナップジョー", null),
            ("stone", "石", "GetDisplayName.GeneratedRandomStatue.Component"),
            ("statue", "像", "GetDisplayName.GeneratedRandomStatue.Component"));

        RunWithPostfix(
            typeof(DummyRandomStatueTarget),
            nameof(DummyRandomStatueTarget.SetCreature),
            new[] { typeof(DummyGameObject) },
            typeof(RandomStatueDisplayNameTranslationPatch),
            nameof(RandomStatueDisplayNameTranslationPatch.Postfix),
            () =>
            {
                var part = new DummyRandomStatueTarget();
                part.SetCreature(new DummyGameObject("{{C|snapjaw}}"));
                Assert.That(part.ParentObject.Render.DisplayName, Is.EqualTo("{{C|スナップジョー}}の石の像"));
            });
    }

    [Test]
    public void PetPhylacteryPostfix_TranslatesParentRenderDisplayName()
    {
        WriteDictionary(("High Templar", "高位聖堂騎士"));

        RunWithPostfix(
            typeof(DummyPetPhylacteryTarget),
            nameof(DummyPetPhylacteryTarget.HandleEvent),
            new[] { typeof(DummyAfterObjectCreatedEvent) },
            typeof(PetPhylacteryDisplayNameTranslationPatch),
            nameof(PetPhylacteryDisplayNameTranslationPatch.Postfix),
            () =>
            {
                var part = new DummyPetPhylacteryTarget();
                part.HandleEvent(new DummyAfterObjectCreatedEvent());
                Assert.That(part.ParentObject.Render.DisplayName, Is.EqualTo("高位聖堂騎士のファイラクテリー"));
            });
    }

    [TestCase("phylactery of Unknown", "Unknownのファイラクテリー")]
    [TestCase("", "")]
    [TestCase("phylactery of {{C|Unknown}}", "{{C|Unknown}}のファイラクテリー")]
    [TestCase("\u0001phylactery of Unknown", "phylactery of Unknown")]
    public void PetPhylacteryPostfix_HandlesFallbackAndDirectMarkedDisplayNames(string generatedName, string expected)
    {
        DummyPetPhylacteryTarget.GeneratedDisplayNameOverride = generatedName;
        try
        {
            RunWithPostfix(
                typeof(DummyPetPhylacteryTarget),
                nameof(DummyPetPhylacteryTarget.HandleEvent),
                new[] { typeof(DummyAfterObjectCreatedEvent) },
                typeof(PetPhylacteryDisplayNameTranslationPatch),
                nameof(PetPhylacteryDisplayNameTranslationPatch.Postfix),
                () =>
                {
                    var part = new DummyPetPhylacteryTarget();
                    part.HandleEvent(new DummyAfterObjectCreatedEvent());
                    Assert.That(part.ParentObject.Render.DisplayName, Is.EqualTo(expected));
                });
        }
        finally
        {
            DummyPetPhylacteryTarget.GeneratedDisplayNameOverride = null;
        }
    }

    [Test]
    public void TombCultistTemplatePostfix_TranslatesDeathPilgrimDisplayName()
    {
        WriteDictionary(("crypt keeper", "墓所の番人"));

        RunWithTombCultistPatch(() =>
        {
            var go = new DummyGameObject("crypt keeper");
            go.Render.DisplayName = "crypt keeper";
            DummyTombCultistTemplateTarget.Apply(go, new DummyHistoricEntitySnapshot("Argyve's Own"));

            Assert.That(go.DisplayName, Is.EqualTo("{{Y|Argyve's Own}}の死の巡礼者、墓所の番人"));
        });
    }

    [Test]
    public void TombCultistTemplatePostfix_LeavesFallbackDisplayNameUntouched()
    {
        RunWithTombCultistPatch(() =>
        {
            var go = new DummyGameObject("unknown pilgrim");
            go.Render.DisplayName = "unknown pilgrim";
            DummyTombCultistTemplateTarget.Apply(go, new DummyHistoricEntitySnapshot("Argyve's Own"));

            Assert.That(go.DisplayName, Is.EqualTo("{{Y|Argyve's Own}}の死の巡礼者、unknown pilgrim"));
        });
    }

    [TestCase("", " and death pilgrim of the {{Y|Argyve's Own}}")]
    [TestCase("{{C|unknown pilgrim}}", "{{Y|Argyve's Own}}の死の巡礼者、{{C|unknown pilgrim}}")]
    [TestCase("\u0001unknown pilgrim", "unknown pilgrim and death pilgrim of the {{Y|Argyve's Own}}")]
    public void TombCultistTemplatePostfix_HandlesEdgeDisplayNames(string displayName, string expected)
    {
        RunWithTombCultistPatch(() =>
        {
            var go = new DummyGameObject(displayName);
            go.Render.DisplayName = displayName;
            DummyTombCultistTemplateTarget.Apply(go, new DummyHistoricEntitySnapshot("Argyve's Own"));

            Assert.That(go.DisplayName, Is.EqualTo(expected));
        });
    }

    private static void RunWithPostfix(
        Type targetType,
        string targetMethodName,
        Type[] targetParameterTypes,
        Type patchType,
        string postfixName,
        Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(targetType, targetMethodName, targetParameterTypes),
                postfix: new HarmonyMethod(RequireMethod(patchType, postfixName, typeof(object))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithTombCultistPatch(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyTombCultistTemplateTarget),
                    nameof(DummyTombCultistTemplateTarget.Apply),
                    typeof(DummyGameObject),
                    typeof(DummyHistoricEntitySnapshot)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(TombCultistTemplateDisplayNameTranslationPatch),
                    nameof(TombCultistTemplateDisplayNameTranslationPatch.Postfix),
                    typeof(object))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryWithContexts(Array.ConvertAll(entries, entry => (entry.key, entry.text, (string?)null)));
    }

    private void WriteDictionaryWithContexts(params (string key, string text, string? context)[] entries)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(entries[index].key);
            builder.Append('"');
            if (entries[index].context is not null)
            {
                builder.Append(",\"context\":\"");
                builder.Append(entries[index].context);
                builder.Append('"');
            }

            builder.Append(",\"text\":\"");
            builder.Append(entries[index].text);
            builder.Append("\"}");
        }

        builder.Append("]}\n");
        File.WriteAllText(Path.Combine(tempDirectory, "world-part-generated-display-name-l2.ja.json"), builder.ToString());
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
        AccessTools.Method(type, name, parameters)
        ?? throw new MissingMethodException(type.FullName, name);

    private sealed class DummyRender
    {
        public string DisplayName = string.Empty;
    }

    private sealed class DummyGameObject
    {
        public DummyGameObject()
        {
        }

        public DummyGameObject(string displayName)
        {
            DisplayName = displayName;
            Render.DisplayName = displayName;
        }

        public DummyRender Render { get; } = new();

        public string DisplayName = string.Empty;
    }

    private static class DummyModQuantumReverbTarget
    {
        public static string? GeneratedDisplayNameOverride { get; set; }

        public static DummyGameObject CreateHologramOf(DummyGameObject source)
        {
            return new DummyGameObject
            {
                Render =
                {
                    DisplayName = GeneratedDisplayNameOverride ?? "hologram of a " + source.DisplayName,
                },
            };
        }
    }

    private sealed class DummyRandomStatueTarget
    {
        public DummyGameObject ParentObject { get; } = new();

        public void SetCreature(DummyGameObject creature)
        {
            ParentObject.Render.DisplayName = "stone statue of a " + creature.DisplayName;
        }
    }

    private sealed class DummyAfterObjectCreatedEvent
    {
    }

    private sealed class DummyPetPhylacteryTarget
    {
        public static string? GeneratedDisplayNameOverride { get; set; }

        public DummyGameObject ParentObject { get; } = new();

        public void HandleEvent(DummyAfterObjectCreatedEvent e)
        {
            _ = e;
            ParentObject.Render.DisplayName = GeneratedDisplayNameOverride ?? "phylactery of High Templar";
        }
    }

    private sealed class DummyHistoricEntitySnapshot
    {
        private readonly string cultName;

        public DummyHistoricEntitySnapshot(string cultName)
        {
            this.cultName = cultName;
        }

        public string GetProperty(string name)
        {
            return name == "cultName" ? cultName : string.Empty;
        }
    }

    private static class DummyTombCultistTemplateTarget
    {
        public static void Apply(DummyGameObject GO, DummyHistoricEntitySnapshot snapshot)
        {
            GO.DisplayName = GO.Render.DisplayName + " and death pilgrim of the {{Y|" + snapshot.GetProperty("cultName") + "}}";
        }
    }
}
