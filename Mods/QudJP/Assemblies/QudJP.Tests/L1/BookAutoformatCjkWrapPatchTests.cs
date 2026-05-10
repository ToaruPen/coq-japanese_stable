using System.Reflection;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class BookAutoformatCjkWrapPatchTests
{
    [Test]
    public void WrapForAutoformatForTests_InsertsCjkBreaksAtBookPageWidth()
    {
        var method = RequireWrapForAutoformatMethod();
        var source = new string('あ', 48);

        var result = method.Invoke(null, new object[] { source, 2, 2, 1, 2 });

        Assert.That(result, Is.EqualTo(new string('あ', 47) + "\nあ"));
    }

    [Test]
    public void WrapForAutoformatForTests_LeavesNonCjkTextForVanillaWrapper()
    {
        var method = RequireWrapForAutoformatMethod();
        const string source = "The quick brown fox";

        var result = method.Invoke(null, new object[] { source, 2, 2, 2, 2 });

        Assert.That(result, Is.EqualTo(source));
    }

    [Test]
    public void WrapForAutoformatForTests_DoesNotBreakAtPunctuationFarFromBookEdge()
    {
        var method = RequireWrapForAutoformatMethod();
        var source = new string('あ', 35) + "。" + new string('い', 20);

        var result = (string)method.Invoke(null, new object[] { source, 2, 2, 2, 2 })!;
        var lines = result.Split('\n');

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Has.Length.EqualTo(47));
            Assert.That(lines[0], Does.Not.EndWith("。"));
        });
    }

    [TestCase('が')]
    [TestCase('を')]
    [TestCase('に')]
    [TestCase('で')]
    [TestCase('と')]
    [TestCase('も')]
    [TestCase('へ')]
    public void WrapForAutoformatForTests_DoesNotPreferParticlesBeforeBookEdge(char particle)
    {
        var method = RequireWrapForAutoformatMethod();
        var source = new string('あ', 44) + particle + new string('い', 10);

        var result = (string)method.Invoke(null, new object[] { source, 2, 2, 2, 2 })!;
        var lines = result.Split('\n');

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Has.Length.EqualTo(47));
            Assert.That(lines[0], Does.Not.EndWith(particle.ToString()));
        });
    }

    [Test]
    public void WrapForAutoformatForTests_DoesNotReopenQudColorAfterInsertedBookBreak()
    {
        var method = RequireWrapForAutoformatMethod();
        var source = "&y" + new string('あ', 48);

        var result = (string)method.Invoke(null, new object[] { source, 2, 2, 2, 2 })!;

        Assert.That(result, Does.Not.Contain("\n&y"));
    }

    [Test]
    public void WrapForAutoformatForTests_DoesNotSplitIronshankAtKanjiPrefixOrStartLineWithPunctuation()
    {
        var method = RequireWrapForAutoformatMethod();
        const string source =
            "原因不明のこの病に罹ると脚の骨が永久に石灰化し続ける。病人は骨の成長速度が増すにつれ、関節痛と可動域の狭まりを訴える。\n"
            + "ゲル一ドラムと asphalt 一ドラムで作った水薬を、関節が本来の可動域を取り戻すまで毎日服用させること。";

        var result = (string)method.Invoke(null, new object[] { source, 2, 2, 2, 2 })!;

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Not.Contain("可\n動域"));
            Assert.That(result, Does.Not.Contain("\n、"));
        });
    }

    private static MethodInfo RequireWrapForAutoformatMethod()
    {
        var type = typeof(Translator).Assembly.GetType("QudJP.Patches.BookAutoformatCjkWrapPatch");
        Assert.That(type, Is.Not.Null, "QudJP.Patches.BookAutoformatCjkWrapPatch is missing.");

        var method = type!.GetMethod("WrapForAutoformatForTests", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "WrapForAutoformatForTests is missing.");
        return method!;
    }
}
