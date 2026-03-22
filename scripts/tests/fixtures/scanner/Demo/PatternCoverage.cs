namespace Demo
{
    public class PatternCoverage
    {
        private readonly DemoObject obj = new();
        private readonly MessageQueue queue = new();
        private readonly UIText screen = new();

        public void Run()
        {
            screen.SetText("set");
            queue.AddPlayerMessage("message-instance");
            MessageQueue.AddPlayerMessage("message-static");

            Popup.Show("popup-show");
            Popup.ShowFail("popup-show-fail");
            Popup.ShowBlock("popup-show-block");
            Popup.ShowYesNo("popup-yes-no");
            Popup.ShowYesNoCancel("popup-yes-no-cancel");
            Popup.PickOption("popup-pick-option");
            Popup.AskString("popup-ask-string");
            Popup.ShowAsync("popup-show-async");
            Popup.WarnYesNo("popup-warn-yes-no");

            obj.DidX("didx-qualified");
            DidX("didx-unqualified");
            obj.DidXToY("didx-to-y-qualified", obj);
            DidXToY("didx-to-y-unqualified", obj);
            obj.DidXToYWithZ("didx-to-y-with-z-qualified", obj, "extra");
            DidXToYWithZ("didx-to-y-with-z-unqualified", obj, "extra");
            Messaging.XDidY(obj, "xdidy");
            Messaging.XDidYToZ(obj, "xdidytoz", obj);
            Messaging.WDidXToYWithZ("wdidxtoywithz", obj, obj, "extra");

            obj.GetDisplayName();
            obj.Does("does");

            obj.EmitMessage("emit-qualified");
            EmitMessage("emit-unqualified");
            Messaging.EmitMessage("emit-static");

            obj.GetShortDescription();
            obj.GetLongDescription();

            JournalAPI.AddAccomplishment("journal-accomplishment");
            JournalAPI.AddMapNote("journal-map-note");
            JournalAPI.AddObservation("journal-observation");

            HistoricStringExpander.ExpandString("<spice.history>");

            "=subject.T= =verb:strike=".StartReplace();
        }

        private void DidX(string value)
        {
        }

        private void DidXToY(string value, DemoObject target)
        {
        }

        private void DidXToYWithZ(string value, DemoObject target, string extra)
        {
        }

        private void EmitMessage(string value)
        {
        }
    }

    public class MessageQueue
    {
        public void AddPlayerMessage(string value)
        {
        }

        public static void AddPlayerMessage(string value)
        {
        }
    }

    public static class Popup
    {
        public static void Show(string value)
        {
        }

        public static void ShowFail(string value)
        {
        }

        public static void ShowBlock(string value)
        {
        }

        public static void ShowYesNo(string value)
        {
        }

        public static void ShowYesNoCancel(string value)
        {
        }

        public static void PickOption(string value)
        {
        }

        public static void AskString(string value)
        {
        }

        public static void ShowAsync(string value)
        {
        }

        public static void WarnYesNo(string value)
        {
        }
    }

    public static class Messaging
    {
        public static void XDidY(DemoObject actor, string value)
        {
        }

        public static void XDidYToZ(DemoObject actor, string value, DemoObject target)
        {
        }

        public static void WDidXToYWithZ(string value, DemoObject actor, DemoObject target, string extra)
        {
        }

        public static void EmitMessage(string value)
        {
        }
    }

    public static class JournalAPI
    {
        public static void AddAccomplishment(string value)
        {
        }

        public static void AddMapNote(string value)
        {
        }

        public static void AddObservation(string value)
        {
        }
    }

    public static class HistoricStringExpander
    {
        public static void ExpandString(string value)
        {
        }
    }

    public class DemoObject
    {
        public void DidX(string value)
        {
        }

        public void DidXToY(string value, DemoObject target)
        {
        }

        public void DidXToYWithZ(string value, DemoObject target, string extra)
        {
        }

        public void EmitMessage(string value)
        {
        }

        public string GetDisplayName()
        {
            return "demo";
        }

        public string Does(string value)
        {
            return value;
        }

        public string GetShortDescription()
        {
            return "short";
        }

        public string GetLongDescription()
        {
            return "long";
        }
    }

    public class UIText
    {
        public void SetText(string value)
        {
        }
    }
}
