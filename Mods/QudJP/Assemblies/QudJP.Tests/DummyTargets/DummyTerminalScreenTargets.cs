using System.Collections.Generic;
using System.Text;

namespace QudJP.Tests.DummyTargets;

internal class DummyTerminalScreen
{
    public string MainText = string.Empty;

    public List<string> Options = new List<string>();

    public string RenderedText = string.Empty;

    protected virtual void OnUpdate()
    {
    }

    public void Update()
    {
        OnUpdate();

        var builder = new StringBuilder();
        builder.Append(MainText).Append("\n\n");
        for (var index = 0; index < Options.Count; index++)
        {
            builder.Append((char)('A' + index)).Append(". ").Append(Options[index]).Append('\n');
        }

        RenderedText = builder.ToString();
    }
}

internal class CyberneticsScreen : DummyTerminalScreen
{
    public string ScreenFamily = "Cybernetics";
}

internal sealed class DummyConstructorCyberneticsScreen : CyberneticsScreen
{
    public DummyConstructorCyberneticsScreen()
    {
        MainText = "Your curiosity is admirable, aristocrat.\n\nCybernetics are bionic augmentations implanted in your body to assist in your self-actualization. You can have implants installed at becoming nooks such as this one. Either load them in the rack or carry them on your person.";
        Options.Add("How many implants can I install?");
        Options.Add("Return To Main Menu");
    }
}

internal sealed class DummyOnUpdateCyberneticsScreen : CyberneticsScreen
{
    protected override void OnUpdate()
    {
        MainText = "You are becoming, aristocrat. Choose an implant to install.";
        Options.Clear();
        Options.Add("Install Cybernetics");
        Options.Add("Return to main menu");
    }
}

internal sealed class DummyDynamicCyberneticsScreen : CyberneticsScreen
{
    protected override void OnUpdate()
    {
        MainText = "Welcome, Aristocrat, to a becoming nook. you are one step closer to the Grand Unification. Please choose from the following options.";
        Options.Clear();
        Options.Add("Night Vision Goggles {{C|[3 license points]}}");
        Options.Add("Optic Chisel [will replace Night Vision Goggles]");
    }
}

internal sealed class DummyNonCyberneticsScreen : DummyTerminalScreen
{
    protected override void OnUpdate()
    {
        MainText = "You are becoming, aristocrat. Choose an implant to install.";
        Options.Clear();
        Options.Add("Install Cybernetics");
    }
}
