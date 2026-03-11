using System;
using System.Reflection;
using HarmonyLib;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class HarmonyIntegrationTests
{
    [SetUp]
    public void SetUp()
    {
        DummyMessageQueue.Reset();
    }

    [Test]
    public void Prefix_CanOverrideDummyGrammarA()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGrammar), nameof(DummyGrammar.A)),
                prefix: new HarmonyMethod(RequireMethod(typeof(DummyGrammarAPrefixPatch), nameof(DummyGrammarAPrefixPatch.Prefix))));

            var result = DummyGrammar.A("apple", capitalize: false);

            Assert.That(result, Is.EqualTo("patched:apple"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_CanModifyDummyGrammarPluralizeResult()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGrammar), nameof(DummyGrammar.Pluralize)),
                postfix: new HarmonyMethod(RequireMethod(typeof(DummyGrammarPluralizePostfixPatch), nameof(DummyGrammarPluralizePostfixPatch.Postfix))));

            var result = DummyGrammar.Pluralize("dish");

            Assert.That(result, Is.EqualTo("dishs [jp]"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_CanRewriteDummyMessageQueueArguments()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(RequireMethod(typeof(DummyMessageQueuePrefixPatch), nameof(DummyMessageQueuePrefixPatch.Prefix))));

            DummyMessageQueue.AddPlayerMessage("hello", "&w", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("[JP] hello"));
                Assert.That(DummyMessageQueue.LastColor, Is.EqualTo("&Y"));
                Assert.That(DummyMessageQueue.LastCapitalize, Is.True);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static class DummyGrammarAPrefixPatch
    {
        public static bool Prefix(string name, bool capitalize, ref string __result)
        {
            _ = capitalize;
            __result = $"patched:{name}";
            return false;
        }
    }

    private static class DummyGrammarPluralizePostfixPatch
    {
        public static void Postfix(ref string __result)
        {
            __result += " [jp]";
        }
    }

    private static class DummyMessageQueuePrefixPatch
    {
        public static void Prefix(ref string Message, ref string Color, ref bool Capitalize)
        {
            Message = $"[JP] {Message}";
            Color = "&Y";
            Capitalize = true;
        }
    }
}
