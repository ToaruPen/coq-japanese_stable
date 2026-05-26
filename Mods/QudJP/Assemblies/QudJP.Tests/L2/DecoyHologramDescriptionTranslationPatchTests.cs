using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
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
    public void TranslateHologramDescription_PreservesUnknownAndMarkerPrefixedValues()
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
            Assert.That(marker.Short, Does.StartWith("\u0001Light stammers"));
        });
    }

    private sealed class DummyDescriptionPart
    {
        public string? Short { get; set; }
    }
}
