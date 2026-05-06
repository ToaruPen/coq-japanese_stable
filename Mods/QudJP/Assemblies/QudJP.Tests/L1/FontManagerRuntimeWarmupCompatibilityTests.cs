namespace QudJP.Tests.L1;

[NUnit.Framework.TestFixture]
[NUnit.Framework.Category("L1")]
public sealed class FontManagerRuntimeWarmupCompatibilityTests
{
    [NUnit.Framework.Test]
    public void TryWarmFontCharactersForTests_UsesStringOutStringOverload_WhenStringOnlyOverloadIsAbsent()
    {
        var fontAsset = new OutStringOnlyFontAsset();

        var warmed = FontManager.TryWarmFontCharactersForTests(fontAsset, "日本語テスト");

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(warmed, NUnit.Framework.Is.True);
            NUnit.Framework.Assert.That(fontAsset.CapturedCharacters, NUnit.Framework.Is.EqualTo("日本語テスト"));
        });
    }

    [NUnit.Framework.Test]
    public void TryWarmFontCharactersForTests_UsesStringOnlyOverload_WhenThatIsTheAvailableRuntimeApi()
    {
        var fontAsset = new StringOnlyFontAsset();

        var warmed = FontManager.TryWarmFontCharactersForTests(fontAsset, "日本語テスト");

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(warmed, NUnit.Framework.Is.True);
            NUnit.Framework.Assert.That(fontAsset.CapturedCharacters, NUnit.Framework.Is.EqualTo("日本語テスト"));
        });
    }

    [NUnit.Framework.Test]
    public void TryWarmFontCharactersForTests_ReturnsFalse_WhenNoCompatibleOverloadExists()
    {
        var warmed = FontManager.TryWarmFontCharactersForTests(new object(), "日本語テスト");

        NUnit.Framework.Assert.That(warmed, NUnit.Framework.Is.False);
    }

    private sealed class OutStringOnlyFontAsset
    {
        public string? CapturedCharacters { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "Invoked through the runtime-overload reflection helper under test.")]
        public bool TryAddCharacters(string characters, out string missingCharacters)
        {
            CapturedCharacters = characters;
            missingCharacters = string.Empty;
            return true;
        }
    }

    private sealed class StringOnlyFontAsset
    {
        public string? CapturedCharacters { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S1144:Unused private types or members should be removed",
            Justification = "Invoked through the runtime-overload reflection helper under test.")]
        public bool TryAddCharacters(string characters)
        {
            CapturedCharacters = characters;
            return true;
        }
    }
}
