using System.Xml.Linq;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class NamingXmlTests
{
    private XDocument document = null!;
    private string namingPath = null!;

    [SetUp]
    public void SetUp()
    {
        namingPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization/Naming.jp.xml"));
        document = XDocument.Load(namingPath);
    }

    [Test]
    public void QudishBananaGroveSite_UsesTranslatedNameTemplate()
    {
        var template = GetTemplate("Qudish Banana Grove Site");
        Assert.That(template, Does.Contain("*Name*"));

        var rendered = template.Replace("*Name*", "カー・リル", StringComparison.Ordinal);

        Assert.That(rendered, Is.EqualTo("バナナ園・カー・リル"));
    }

    [Test]
    public void TwoHeaded_UsesAltNameTemplate()
    {
        var template = GetTemplate("Two-Headed");
        Assert.Multiple(() =>
        {
            Assert.That(template, Does.Contain("*Name*"));
            Assert.That(template, Does.Contain("*AltName*"));
        });

        var rendered = template
            .Replace("*Name*", "カー・リル", StringComparison.Ordinal)
            .Replace("*AltName*", "メフメット・ウヤラク", StringComparison.Ordinal);

        Assert.That(rendered, Is.EqualTo("カー・リル／メフメット・ウヤラク"));
    }

    [Test]
    public void Seeker_UsesMiddleDotNoLongerTemplate_WhilePreservingFormat()
    {
        var style = GetNameStyle("Seeker");
        var template = GetTemplate("Seeker");

        var rendered = template.Replace("*Name*", "カー・リル", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(style.Attribute("Format")?.Value, Is.EqualTo("SpacesToHyphens"));
            Assert.That(template, Does.Contain("*Name*"));
            Assert.That(rendered, Is.EqualTo("カー・リル・ノー・ロンガー"));
        });
    }

    [Test]
    public void SeekerHeroTitle_UsesTranslatedPositionTemplateVar()
    {
        var template = GetTemplate("Seeker Hero Title");
        var position = GetTemplateVarValue("Seeker Hero Title", "Position");
        Assert.Multiple(() =>
        {
            Assert.That(template, Does.Contain("*Position*"));
            Assert.That(position, Is.EqualTo("プトーの従者"));
        });

        var rendered = template.Replace("*Position*", position, StringComparison.Ordinal);

        Assert.That(rendered, Is.EqualTo("プトーの従者"));
    }

    private XElement GetNameStyle(string name)
    {
        return document.Root?
                   .Element("namestyles")?
                   .Elements("namestyle")
                   .FirstOrDefault(element => string.Equals(element.Attribute("Name")?.Value, name, StringComparison.Ordinal))
               ?? throw new InvalidOperationException($"namestyle '{name}' not found in {namingPath}");
    }

    private string GetTemplate(string styleName)
    {
        var style = GetNameStyle(styleName);
        return style.Element("templates")?
                   .Elements("template")
                   .Select(element => element.Attribute("Name")?.Value)
                   .FirstOrDefault(value => !string.IsNullOrEmpty(value))
               ?? throw new InvalidOperationException($"template missing for namestyle '{styleName}'");
    }

    private string GetTemplateVarValue(string styleName, string varName)
    {
        var style = GetNameStyle(styleName);
        return style.Element("templatevars")?
                   .Elements("templatevar")
                   .FirstOrDefault(element => string.Equals(element.Attribute("Name")?.Value, varName, StringComparison.Ordinal))?
                   .Elements("value")
                   .Select(element => element.Attribute("Name")?.Value)
                   .FirstOrDefault(value => !string.IsNullOrEmpty(value))
               ?? throw new InvalidOperationException($"templatevar '{varName}' missing for namestyle '{styleName}'");
    }
}
