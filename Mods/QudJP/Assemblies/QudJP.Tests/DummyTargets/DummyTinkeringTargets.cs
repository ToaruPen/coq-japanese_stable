#pragma warning disable CS0649
#pragma warning disable CA1805

using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets;

internal sealed class DummyTinkeringCategoryInfo
{
    public int N;

    public bool Selected;

    public string Name = string.Empty;
}

internal sealed class DummyTinkeringStatusScreenTarget
{
    public bool OriginalExecuted { get; private set; }

    public int CurrentCategory;

    public DummyUITextSkin modeToggleText = new DummyUITextSkin();

    public List<DummyTinkeringCategoryInfo> categoryInfos = new List<DummyTinkeringCategoryInfo>
    {
        new DummyTinkeringCategoryInfo
        {
            Name = "Build",
        },
        new DummyTinkeringCategoryInfo
        {
            N = 1,
            Name = "Mod",
        },
    };

    public void UpdateViewFromData()
    {
        OriginalExecuted = true;
        modeToggleText.SetText(CurrentCategory == 0
            ? "{{hotkey|[~Toggle]}} switch to modifications"
            : "{{hotkey|[~Toggle]}} switch to build");
    }
}

internal sealed class DummyTinkeringRecipeData
{
    public string Type { get; set; } = "Build";
}

internal sealed class DummyTinkeringModObject
{
    public string DisplayName { get; set; } = "modded item";
}

internal sealed class DummyTinkeringLineDataTarget
{
    public bool category;

    public bool categoryExpanded;

    public string categoryName = string.Empty;

    public int categoryCount;

    public int mode;

    public DummyTinkeringRecipeData data = new DummyTinkeringRecipeData();

    public DummyTinkeringModObject? modObject;
}

internal sealed class DummyTinkeringLineTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyStatusContext context = new DummyStatusContext();

    public DummyUITextSkin text = new DummyUITextSkin();

    public DummyUITextSkin categoryText = new DummyUITextSkin();

    public void setData(object data)
    {
        OriginalExecuted = true;
        context.data = data;
        if (data is not DummyTinkeringLineDataTarget tinkeringLineData)
        {
            return;
        }

        if (tinkeringLineData.category)
        {
            if (tinkeringLineData.categoryName == "~<none>")
            {
                categoryText.SetText("{{K|You don't have any schematics.}}");
            }
            else
            {
                categoryText.SetText((tinkeringLineData.categoryExpanded ? "[-]" : "[+]") + " " + tinkeringLineData.categoryName + " [" + tinkeringLineData.categoryCount + "]");
            }

            return;
        }

        if (tinkeringLineData.mode == 1 && tinkeringLineData.modObject is null)
        {
            text.SetText("    <no applicable items>");
            return;
        }

        text.SetText("    translated elsewhere");
    }
}

internal sealed class DummyTinkeringDetailsLineTarget
{
    public bool OriginalExecuted { get; private set; }

    public DummyActiveObject gameObject = new DummyActiveObject();

    public DummyUITextSkin modBitCostText = new DummyUITextSkin();

    public void setData(object data)
    {
        OriginalExecuted = true;
        if (data is not DummyTinkeringLineDataTarget tinkeringLineData)
        {
            return;
        }

        if (tinkeringLineData.category)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        if (tinkeringLineData.data.Type == "Mod")
        {
            modBitCostText.SetText("{{K || Bit Cost |}}\n2 bits\n\n{{K|| Ingredients |}}\n{{G|u}} item A\n-or-\n{{R|X}} item B");
            return;
        }

        modBitCostText.SetText("{{K|| Bit Cost |}}\n1 bit\n\n{{K|| Ingredients |}}\n{{G|u}} item A\n-or-\n{{R|X}} item B");
    }
}
