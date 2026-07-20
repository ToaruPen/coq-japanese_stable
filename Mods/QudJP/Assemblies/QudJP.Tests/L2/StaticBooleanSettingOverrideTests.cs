namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class StaticBooleanSettingOverrideTests
{
    [SetUp]
    public void SetUp()
    {
        DummyStaticBooleanPropertySetting.Enabled = false;
    }

    [TearDown]
    public void TearDown()
    {
        DummyStaticBooleanPropertySetting.Enabled = false;
    }

    [Test]
    public void ForResolvedType_RejectsInstanceBooleanProperty_WithStrictDiagnostic()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            StaticBooleanSettingOverride.ForResolvedType(
                typeof(DummyInstanceBooleanPropertySetting).FullName!,
                nameof(DummyInstanceBooleanPropertySetting),
                nameof(DummyInstanceBooleanPropertySetting.Enabled),
                value: true));

        Assert.That(
            exception!.Message,
            Is.EqualTo(
                $"Static setting property must be a readable, writable, and static Boolean: "
                + $"{typeof(DummyInstanceBooleanPropertySetting).FullName}.Enabled"));
    }

    [Test]
    public void ForResolvedType_OverridesAndRestoresStaticBooleanProperty()
    {
        using (StaticBooleanSettingOverride.ForResolvedType(
            typeof(DummyStaticBooleanPropertySetting).FullName!,
            nameof(DummyStaticBooleanPropertySetting),
            nameof(DummyStaticBooleanPropertySetting.Enabled),
            value: true))
        {
            Assert.That(DummyStaticBooleanPropertySetting.Enabled, Is.True);
        }

        Assert.That(DummyStaticBooleanPropertySetting.Enabled, Is.False);
    }

    [Test]
    public void Dispose_CanBeCalledTwice()
    {
        var settingOverride = StaticBooleanSettingOverride.ForResolvedType(
            typeof(DummyStaticBooleanPropertySetting).FullName!,
            nameof(DummyStaticBooleanPropertySetting),
            nameof(DummyStaticBooleanPropertySetting.Enabled),
            value: true);

        settingOverride.Dispose();

        Assert.DoesNotThrow(settingOverride.Dispose);
        Assert.That(DummyStaticBooleanPropertySetting.Enabled, Is.False);
    }

    [Test]
    public void ForResolvedType_ThrowsForMissingMember()
    {
        Assert.Throws<MissingFieldException>(() =>
            StaticBooleanSettingOverride.ForResolvedType(
                typeof(DummyStaticBooleanPropertySetting).FullName!,
                nameof(DummyStaticBooleanPropertySetting),
                "MissingSetting",
                value: true));
    }
}

internal sealed class DummyInstanceBooleanPropertySetting
{
    public bool Enabled { get; set; }
}

internal static class DummyStaticBooleanPropertySetting
{
    public static bool Enabled { get; set; }
}
