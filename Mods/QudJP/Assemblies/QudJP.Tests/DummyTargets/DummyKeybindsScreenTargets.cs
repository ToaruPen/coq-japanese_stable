#pragma warning disable CS0649

using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyKeybindsScreenTarget
{
    public bool OriginalExecuted { get; private set; }

    public static DummyMenuOption REMOVE_BIND = new DummyMenuOption("remove keybind", "CmdDelete", "Delete");

    public static DummyMenuOption RESTORE_DEFAULTS = new DummyMenuOption("restore defaults", "Restore", "R");

    public DummyUITextSkin inputTypeText = new DummyUITextSkin();

    public readonly List<object> menuItems = new List<object>();

    public bool KeyboardMode { get; set; } = true;

    public string CurrentControllerName { get; set; } = "Arcade Pad";

    public void QueryKeybinds()
    {
        OriginalExecuted = true;
        menuItems.Clear();
        if (KeyboardMode)
        {
            inputTypeText.SetText("{{C|Configuring Controller:}} {{c|Keyboard && Mouse}}");
        }
        else
        {
            inputTypeText.SetText("{{C|Configuring Controller:}} {{c|" + CurrentControllerName + "}}");
        }

        menuItems.Add(new DummyKeybindCategoryRowTarget
        {
            CategoryId = "Basic Move / Attack",
            CategoryDescription = "Basic Move / Attack",
        });
        menuItems.Add(new DummyKeybindDataRowTarget
        {
            CategoryId = "Basic Move / Attack",
            KeyId = "InteractNearby",
            KeyDescription = "Interact Nearby",
            SearchWords = "Basic Move / Attack Interact Nearby",
            Bind1 = "Ctrl+Space",
        });
    }
}
