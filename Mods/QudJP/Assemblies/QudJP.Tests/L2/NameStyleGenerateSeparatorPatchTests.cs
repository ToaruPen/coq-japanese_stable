using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class NameStyleGenerateSeparatorPatchTests
{
    [Test]
    public void Transpiler_RewritesSingleNameHyphenSeparators()
    {
        RunWithPatch(() =>
        {
            Assert.That(DummyNameStyleTarget.Generate(), Is.EqualTo("pre・mid・post・end"));
        });
    }

    [Test]
    public void Transpiler_RewritesTwoNameSeparatorBetweenGeneratedNames()
    {
        RunWithPatch(() =>
        {
            Assert.That(DummyNameStyleTarget.Generate(twoNames: true), Is.EqualTo("pre・mid・post・end・pre・mid・post・end"));
        });
    }

    [Test]
    public void Transpiler_LeavesTemplateHandlingUntouched()
    {
        RunWithPatch(() =>
        {
            Assert.That(DummyNameStyleTarget.Generate(template: "Seeker *Name*"), Is.EqualTo("Seeker pre・mid・post・end"));
        });
    }

    [Test]
    public void Transpiler_LeavesInstructionsUnchanged_WhenSeparatorSiteCountIsUnexpected()
    {
        var appendCharMethod = RequireMethod(typeof(StringBuilder), nameof(StringBuilder.Append), typeof(char));
        var originalInstructions = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldc_I4, (int)'-'),
            new(OpCodes.Callvirt, appendCharMethod),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldc_I4, (int)'-'),
            new(OpCodes.Callvirt, appendCharMethod),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldc_I4, (int)'-'),
            new(OpCodes.Callvirt, appendCharMethod),
        };

        var trace = TestTraceHelper.CaptureTrace(() =>
        {
            var rewritten = NameStyleGenerateSeparatorPatch.Transpiler(originalInstructions).ToList();

            Assert.That(GetLoadedChars(rewritten), Is.EqualTo(new[] { '-', '-', '-' }));
        });

        Assert.Multiple(() =>
        {
            Assert.That(GetLoadedChars(originalInstructions), Is.EqualTo(new[] { '-', '-', '-' }));
            Assert.That(trace, Does.Contain("expected 4; leaving instructions unchanged."));
        });
    }

    [Test]
    public void TargetMethod_ReturnsNullAndLogs_WhenResolutionThrows()
    {
        var method = typeof(NameStyleGenerateSeparatorPatch).GetMethod(
            "ResolveTargetMethod",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);

        var trace = TestTraceHelper.CaptureTrace(() =>
        {
            var result = method!.Invoke(
                null,
                new object[]
                {
                    (Func<Type?>)(() => throw new InvalidOperationException("boom")),
                    (Func<Type, MethodInfo?>)(_ => null),
                });

            Assert.That(result, Is.Null);
        });

        Assert.Multiple(() =>
        {
            Assert.That(trace, Does.Contain("NameStyleGenerateSeparatorPatch.TargetMethod failed"));
            Assert.That(trace, Does.Contain("boom"));
        });
    }

    private static void RunWithPatch(Action assertion)
    {
        var harmonyId = $"qudjp.tests.name-style.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyNameStyleTarget), nameof(DummyNameStyleTarget.Generate), typeof(bool), typeof(string)),
                transpiler: new HarmonyMethod(RequireMethod(typeof(NameStyleGenerateSeparatorPatch), nameof(NameStyleGenerateSeparatorPatch.Transpiler))));

            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        var method = parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName)
            : AccessTools.Method(type, methodName, parameterTypes);

        return method
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static IReadOnlyList<char> GetLoadedChars(IEnumerable<CodeInstruction> instructions)
    {
        return instructions
            .Where(instruction => instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int)
            .Select(instruction => (char)(int)instruction.operand!)
            .ToList();
    }
}
