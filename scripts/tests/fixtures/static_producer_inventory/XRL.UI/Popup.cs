using System.Collections.Generic;

namespace XRL.UI
{
    public static class Popup
    {
        public static void Show(string Message)
        {
        }

        public static void ShowOptionList(string Title, List<string> Options)
        {
        }

        public static void ShowColorPicker(
            string Title,
            int selectedColor,
            string Intro,
            int width,
            bool RespectOptionNewlines,
            bool AllowEscape,
            string shortcut,
            string SpacingText,
            bool includeNone,
            bool includePatterns,
            bool allowBackground,
            string PreviewContent)
        {
        }

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
