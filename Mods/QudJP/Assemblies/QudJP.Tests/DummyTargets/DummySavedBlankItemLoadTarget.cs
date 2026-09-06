using System.Runtime.CompilerServices;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummySavedBlankItemReader
{
    public string? Blueprint = "Red Security Card";
    public string? DisplayName = "{{r|}}";
    public Dictionary<string, string>? Property = new(StringComparer.Ordinal);
    public Dictionary<string, int>? IntProperty = new(StringComparer.Ordinal);
    public DummySavedBlankItemRender? Render = new();
}

internal sealed class DummySavedBlankItemRender
{
    public string? DisplayName = "new object name";
    public string ColorString = "&y";
}

internal sealed class DummySavedBlankItemLoadTarget
{
    public string? Blueprint = "Object";
    public Dictionary<string, string>? Property = new(StringComparer.Ordinal);
    public Dictionary<string, int>? IntProperty = new(StringComparer.Ordinal);
    public DummySavedBlankItemRender? Render = new();
    public string? NameAtOriginalLoadEnd;
    public string? CachedName;
    public int ResetNameCacheCallCount;
    public int HasProperNameReadCount;
    public int Weight = 7;

    public bool HasProperName
    {
        get
        {
            HasProperNameReadCount++;
            throw new InvalidOperationException("The load repair must not traverse HasProperName.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Load(DummySavedBlankItemReader reader)
    {
        Blueprint = reader.Blueprint;
        Property = reader.Property;
        IntProperty = reader.IntProperty;
        Render = reader.Render;
        if (Render is not null)
        {
            Render.DisplayName = reader.DisplayName;
        }

        ResetNameCache();
        NameAtOriginalLoadEnd = Render?.DisplayName;
        // Real Load ends with its reset. Deliberately prime this test cache afterward
        // to observe postfix invalidation independently of the original reset.
        CachedName = NameAtOriginalLoadEnd;
    }

    public void ResetNameCache()
    {
        ResetNameCacheCallCount++;
        CachedName = null;
    }
}
