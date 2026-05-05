using System.Collections.Generic;

namespace XRL.UI
{
    public static class Popup
    {
        public static void Forwarded(string Message, string Title, List<string> Options)
        {
            Show(Message);
            Popup.Show(Message);
            ShowOptionList(Title, Options);
        }

        public static void InternalProducer()
        {
            Popup.Show("Popup internal literal");
        }
    }
}
