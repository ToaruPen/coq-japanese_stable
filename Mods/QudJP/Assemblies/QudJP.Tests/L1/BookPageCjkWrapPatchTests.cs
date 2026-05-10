using System.Reflection;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class BookPageCjkWrapPatchTests
{
    [Test]
    public void WrapForBookPageForTests_InsertsCjkBreaksBeforeBookPageClipping()
    {
        var method = RequireWrapForBookPageMethod();
        var source = new string('あ', 48);

        var result = method.Invoke(null, new object[] { source });

        Assert.That(result, Is.EqualTo(new string('あ', 47) + "\nあ"));
    }

    [Test]
    public void WrapForBookPageForTests_LeavesNonCjkTextForVanillaClipping()
    {
        var method = RequireWrapForBookPageMethod();
        const string source = "The quick brown fox";

        var result = method.Invoke(null, new object[] { source });

        Assert.That(result, Is.EqualTo(source));
    }

    [Test]
    public void WrapForBookPageForTests_DoesNotBreakAtPunctuationFarFromBookEdge()
    {
        var method = RequireWrapForBookPageMethod();
        var source = new string('あ', 35) + "。" + new string('い', 20);

        var result = (string)method.Invoke(null, new object[] { source })!;
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
    public void WrapForBookPageForTests_DoesNotPreferParticlesBeforeBookEdge(char particle)
    {
        var method = RequireWrapForBookPageMethod();
        var source = new string('あ', 44) + particle + new string('い', 10);

        var result = (string)method.Invoke(null, new object[] { source })!;
        var lines = result.Split('\n');

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Has.Length.EqualTo(47));
            Assert.That(lines[0], Does.Not.EndWith(particle.ToString()));
        });
    }

    [Test]
    public void WrapForBookPageForTests_DoesNotReopenQudColorAfterInsertedBookBreak()
    {
        var method = RequireWrapForBookPageMethod();
        var source = "&y" + new string('あ', 48);

        var result = (string)method.Invoke(null, new object[] { source })!;

        Assert.That(result, Does.Not.Contain("\n&y"));
    }

    [Test]
    public void WrapForBookPageForTests_DoesNotSplitIronshankAtKanjiPrefixOrStartLineWithPunctuation()
    {
        var method = RequireWrapForBookPageMethod();
        const string source =
            "原因不明のこの病に罹ると脚の骨が永久に石灰化し続ける。病人は骨の成長速度が増すにつれ、関節痛と可動域の狭まりを訴える。\n"
            + "ゲル一ドラムと asphalt 一ドラムで作った水薬を、関節が本来の可動域を取り戻すまで毎日服用させること。";

        var result = (string)method.Invoke(null, new object[] { source })!;

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Not.Contain("可\n動域"));
            Assert.That(result, Does.Not.Contain("\n、"));
        });
    }

    [Test]
    public void WrapForBookPageForTests_LeavesAlreadyBookFormattedDataUnchanged()
    {
        var method = RequireWrapForBookPageMethod();
        var source = "&y&y&y" + new string('あ', 60) + "\n&y&y&y可動域の狭まりを訴える。";

        var result = method.Invoke(null, new object[] { source });

        Assert.That(result, Is.EqualTo(source));
    }

    private static MethodInfo RequireWrapForBookPageMethod()
    {
        var type = typeof(Translator).Assembly.GetType("QudJP.Patches.BookPageCjkWrapPatch");
        Assert.That(type, Is.Not.Null, "QudJP.Patches.BookPageCjkWrapPatch is missing.");

        var method = type!.GetMethod("WrapForBookPageForTests", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "WrapForBookPageForTests is missing.");
        return method!;
    }
}
