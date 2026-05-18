namespace QudJP.Tests.DummyTargets;

internal sealed class DummyAbilityEntryTarget
{
    public string DisplayName { get; set; } = string.Empty;

    public string? DisplayForHotkey { get; set; }

    public string? Command { get; set; }

    public bool Enabled { get; set; } = true;

    public bool IsAttack { get; set; }

    public bool IsRealityDistortionBased { get; set; }

    public int Cooldown { get; set; }

    public int CooldownRounds { get; set; }

    public bool Toggleable { get; set; }

    public bool ToggleState { get; set; }

    public object GetUITile() => new DummyRenderable(DisplayName);
}

internal sealed class DummyAbilityManagerLineDataTarget
{
    public string? category { get; set; }

    public bool collapsed { get; set; }

    public DummyAbilityEntryTarget? ability { get; set; }

    public char quickKey { get; set; } = 'a';

    public bool realityIsWeak { get; set; }

    public string? hotkeyDescription { get; set; }
}

internal sealed class DummyFallbackAbilityManagerLineDataTarget
{
    public string FallbackText { get; set; } = "ability fallback";
}

internal sealed class DummyAbilityManagerLineTarget
{
    public static DummyMenuOption MOVE_DOWN = new DummyMenuOption("Move Down", "V Positive");

    public static DummyMenuOption MOVE_UP = new DummyMenuOption("Move Up", "V Negative");

    public static DummyMenuOption BIND_KEY = new DummyMenuOption("Bind Key", "CmdInsert");

    public static DummyMenuOption UNBIND_KEY = new DummyMenuOption("Unbind Key", "CmdDelete");

    public bool OriginalExecuted { get; private set; }

    public DummyStatusContext context = new DummyStatusContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyUiIconWithGameObject icon = new DummyUiIconWithGameObject();

    public static void ResetStaticMenuOptions()
    {
        MOVE_DOWN = new DummyMenuOption("Move Down", "V Positive");
        MOVE_UP = new DummyMenuOption("Move Up", "V Negative");
        BIND_KEY = new DummyMenuOption("Bind Key", "CmdInsert");
        UNBIND_KEY = new DummyMenuOption("Unbind Key", "CmdDelete");
    }

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;
        if (data is DummyFallbackAbilityManagerLineDataTarget fallback)
        {
            text.SetText(fallback.FallbackText);
            return;
        }

        if (data is not DummyAbilityManagerLineDataTarget lineData)
        {
            return;
        }

        if (lineData.category is not null)
        {
            text.SetText("[+] " + lineData.category);
            icon.gameObject.SetActive(false);
            return;
        }

        text.SetText(lineData.quickKey + ") " + (lineData.ability?.DisplayName ?? string.Empty));
        icon.gameObject.SetActive(true);
        icon.FromRenderable(lineData.ability?.GetUITile());
    }
}
