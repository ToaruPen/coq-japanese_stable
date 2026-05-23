using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class BeginBeingUnequippedFailureMessageTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Prefix_TranslatesCannotRemoveFailureMessage()
    {
        WithPatchedAddFailureMessage(() =>
        {
            var target = new DummyBeginBeingUnequippedFailureMessageTarget();

            target.AddFailureMessage("You can't remove {{Y|blorple scepter}}.");

            Assert.Multiple(() =>
            {
                Assert.That(target.FailureMessage, Is.EqualTo("{{Y|blorple scepter}}を外せない。"));
                Assert.That(HitCount(), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Prefix_LeavesUnrelatedFailureMessageUnchanged()
    {
        WithPatchedAddFailureMessage(() =>
        {
            var target = new DummyBeginBeingUnequippedFailureMessageTarget();

            target.AddFailureMessage("The equipped item refused to be unequipped.");

            Assert.Multiple(() =>
            {
                Assert.That(target.FailureMessage, Is.EqualTo("The equipped item refused to be unequipped."));
                Assert.That(HitCount(), Is.Zero);
            });
        });
    }

    [Test]
    public void TryTranslateFailureMessage_LeavesEmptyInputUnchanged()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                BeginBeingUnequippedFailureMessageTranslationPatch.TryTranslateFailureMessage(null, out var nullTranslated),
                Is.False);
            Assert.That(nullTranslated, Is.Empty);

            Assert.That(
                BeginBeingUnequippedFailureMessageTranslationPatch.TryTranslateFailureMessage(string.Empty, out var emptyTranslated),
                Is.False);
            Assert.That(emptyTranslated, Is.Empty);
        });
    }

    private static void WithPatchedAddFailureMessage(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(
                    typeof(DummyBeginBeingUnequippedFailureMessageTarget),
                    nameof(DummyBeginBeingUnequippedFailureMessageTarget.AddFailureMessage),
                    typeof(string)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(BeginBeingUnequippedFailureMessageTranslationPatch),
                    nameof(BeginBeingUnequippedFailureMessageTranslationPatch.Prefix),
                    typeof(string).MakeByRefType())));

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                parameterTypes,
                null)
            ?? throw new InvalidOperationException(type.FullName + "." + methodName + " was not found.");
    }

    private static int HitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(BeginBeingUnequippedFailureMessageTranslationPatch),
            "CannotRemoveItem");
    }

    private sealed class DummyBeginBeingUnequippedFailureMessageTarget
    {
        public string? FailureMessage { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AddFailureMessage(string Message)
        {
            if (string.IsNullOrEmpty(FailureMessage))
            {
                FailureMessage = Message;
            }
            else if (!FailureMessage.Contains(Message, StringComparison.Ordinal))
            {
                FailureMessage += " " + Message;
            }
        }
    }
}
