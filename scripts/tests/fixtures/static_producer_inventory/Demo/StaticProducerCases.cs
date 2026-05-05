using System.Collections.Generic;
using System.Text;
using XRL.UI;

namespace Demo
{
    public sealed class StaticProducerCases
    {
        public void SameMember(Event E, GameObject ParentObject, string name, StringBuilder stringBuilder2, List<string> options, string dynamicMessage)
        {
            // Messaging.EmitMessage(ParentObject, "Ignored comment");
            string ignored = "Popup.Show(\"Ignored string\")";
            string rawIgnored = """
                AddPlayerMessage("Ignored raw string");
                """;
            char charIgnored = '(';

            Messaging.EmitMessage(ParentObject, "Static emit");
            ParentObject.EmitMessage(Message: "Named instance emit");
            IComponent<GameObject>.EmitMessage(ParentObject, "Static interface emit");
            ParentObject.EmitMessage("Instance emit");
            EmitMessage(E.Actor, stringBuilder2, false);
            AddPlayerMessage("Player static");
            The.Player.AddPlayerMessage($"Player {name}");
            Popup.Show("Popup body", Title: "Ignored title");
            Popup.ShowFail("Failure text");
            Popup.ShowFailAsync($"Failure {name}");
            Popup.ShowKeybindAsync("Press key");
            Popup.ShowBlockWithCopy("Copy body", "Copy prompt", "Copy title", "Copy payload");
            Popup.ShowOptionList("Option title", options, null, null, "Option intro", 0, 0, 0, 0, "Spacing text", null, null, null, null, "Buttons text");
            Popup.ShowColorPicker("Color title", 0, null, 60, RespectOptionNewlines: false, AllowEscape: false, null, "Semantic spacing", includeNone: true, includePatterns: false, allowBackground: false, "Semantic preview");
            Popup.ShowExperimental();
            Popup.Show(dynamicMessage);
            System.Action scopedDelegate = delegate()
            {
                Popup.Show("Delegate body");
            };
            scopedDelegate();
        }

        public void OtherMember(GameObject ParentObject)
        {
            Messaging.EmitMessage(ParentObject, "Other static emit");
        }

        public string PropertyProducer
        {
            get
            {
                Popup.ShowBlock("Block body", "Block title");
                return "ok";
            }
        }
    }
}
