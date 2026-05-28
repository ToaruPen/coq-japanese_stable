using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class DecoyHologramDescriptionTranslationPatchTests
{
    [Test]
    public void TranslateHologramDescription_TranslatesGeneratedPrefix()
    {
        var description = new DummyDescriptionPart
        {
            Short = "Light stammers in parallax to form the image of an object. A chrome chair hums quietly.",
        };

        DecoyHologramDescriptionTranslationPatch.TranslateHologramDescriptionForTests(description);

        Assert.That(description.Short, Is.EqualTo("光が視差の中で明滅し、物体の像を形作っている。 A chrome chair hums quietly."));
    }

    [Test]
    public void TranslateHologramDescription_PreservesUnknownAndStripsMarkerPrefixedValues()
    {
        var unknown = new DummyDescriptionPart { Short = "A chrome chair hums quietly." };
        var marker = new DummyDescriptionPart
        {
            Short = "\u0001Light stammers in parallax to form the image of an object. A chrome chair hums quietly.",
        };

        DecoyHologramDescriptionTranslationPatch.TranslateHologramDescriptionForTests(unknown);
        DecoyHologramDescriptionTranslationPatch.TranslateHologramDescriptionForTests(marker);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Short, Is.EqualTo("A chrome chair hums quietly."));
            Assert.That(marker.Short, Is.EqualTo("光が視差の中で明滅し、物体の像を形作っている。 A chrome chair hums quietly."));
            Assert.That(marker.Short![0], Is.Not.EqualTo('\u0001'));
        });
    }

    private sealed class DummyDescriptionPart
    {
        public string? Short { get; set; }
    }
}
