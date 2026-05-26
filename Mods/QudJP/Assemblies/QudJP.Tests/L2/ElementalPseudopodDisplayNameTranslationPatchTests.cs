using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ElementalPseudopodDisplayNameTranslationPatchTests
{
    [TestCase("Fire", "{{R|flaming pseudopod}}", "{{R|燃える仮足}}")]
    [TestCase("Ice", "{{C|hoary pseudopod}}", "{{C|霜に覆われた仮足}}")]
    [TestCase("Acid", "{{G|acidic pseudopod}}", "{{G|酸性の仮足}}")]
    [TestCase("Shocking", "{{W|sparking pseudopod}}", "{{W|火花を散らす仮足}}")]
    public void SetupPod_TranslatesElementalJellyPseudopodDisplayName_WhenPatched(
        string form,
        string source,
        string expected)
    {
        WithPatchedSetupPod(
            typeof(DummyElementalJelly),
            () =>
            {
                var owner = new DummyElementalJelly { Form = form };
                var pod = new DummyPod { Render = new DummyRender { DisplayName = source } };

                owner.SetupPod(pod);

                Assert.That(pod.Render.DisplayName, Is.EqualTo(expected));
            });
    }

    [TestCase("Fire", "{{R|flaming pseudopod}}", "{{R|燃える仮足}}")]
    [TestCase("Ice", "{{C|hoary pseudopod}}", "{{C|霜に覆われた仮足}}")]
    [TestCase("Acid", "{{G|acidic pseudopod}}", "{{G|酸性の仮足}}")]
    [TestCase("Shocking", "{{W|sparking pseudopod}}", "{{W|火花を散らす仮足}}")]
    public void SetupPod_TranslatesPanhumorPseudopodDisplayName_WhenPatched(
        string form,
        string source,
        string expected)
    {
        WithPatchedSetupPod(
            typeof(DummyPanhumor),
            () =>
            {
                var owner = new DummyPanhumor { Form = form };
                var pod = new DummyPod { Render = new DummyRender { DisplayName = source } };

                owner.SetupPod(pod);

                Assert.That(pod.Render.DisplayName, Is.EqualTo(expected));
            });
    }

    [Test]
    public void SetupPod_LeavesUnknownDisplayNameUnchanged_WhenPatched()
    {
        WithPatchedSetupPod(
            typeof(DummyElementalJelly),
            () =>
            {
                var owner = new DummyElementalJelly { Form = "Unknown" };
                var pod = new DummyPod { Render = new DummyRender { DisplayName = "{{M|strange pseudopod}}" } };

                owner.SetupPod(pod);

                Assert.That(pod.Render.DisplayName, Is.EqualTo("{{M|strange pseudopod}}"));
            });
    }

    [Test]
    public void SetupPod_StripsDirectMarkedUnknownDisplayName_WhenPatched()
    {
        WithPatchedSetupPod(
            typeof(DummyElementalJelly),
            () =>
            {
                var owner = new DummyElementalJelly { Form = "Unknown" };
                var pod = new DummyPod { Render = new DummyRender { DisplayName = "\u0001{{M|strange pseudopod}}" } };

                owner.SetupPod(pod);

                Assert.That(pod.Render.DisplayName, Is.EqualTo("{{M|strange pseudopod}}"));
            });
    }

    private static void WithPatchedSetupPod(Type targetType, Action action)
    {
        var harmonyId = "qudjp.tests.elemental-pseudopod-display-name." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(targetType, "SetupPod", typeof(DummyPod)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(ElementalPseudopodDisplayNameTranslationPatch),
                    nameof(ElementalPseudopodDisplayNameTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }
}

internal sealed class DummyElementalJelly
{
    public string Form { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetupPod(DummyPod pod)
    {
        pod.Render.DisplayName = Form switch
        {
            "Fire" => "{{R|flaming pseudopod}}",
            "Ice" => "{{C|hoary pseudopod}}",
            "Acid" => "{{G|acidic pseudopod}}",
            "Shocking" => "{{W|sparking pseudopod}}",
            _ => pod.Render.DisplayName,
        };
    }
}

internal sealed class DummyPanhumor
{
    public string Form { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetupPod(DummyPod pod)
    {
        pod.Render.DisplayName = Form switch
        {
            "Fire" => "{{R|flaming pseudopod}}",
            "Ice" => "{{C|hoary pseudopod}}",
            "Acid" => "{{G|acidic pseudopod}}",
            "Shocking" => "{{W|sparking pseudopod}}",
            _ => pod.Render.DisplayName,
        };
    }
}

internal sealed class DummyPod
{
    public DummyRender Render { get; init; } = new();
}

internal sealed class DummyRender
{
    public string DisplayName { get; set; } = string.Empty;
}
