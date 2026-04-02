namespace XRL.UI
{
    internal static class Popup
    {
        public static void Show(
            string Message,
            string? Title = null,
            string? Sound = null,
            bool CopyScrap = true,
            bool Capitalize = true,
            bool DimBackground = true,
            bool LogMessage = true,
            Genkit.Location2D? Position = null)
        {
        }

        public static void ShowFail(
            string Message,
            bool CopyScrap = true,
            bool Capitalize = true,
            bool DimBackground = true)
        {
        }

        public static int ShowYesNo(
            string Message,
            string? Sound = null,
            bool AllowEscape = true,
            DialogResult defaultResult = DialogResult.No)
        {
            return (int)defaultResult;
        }

        public static int ShowYesNoCancel(
            string Message,
            string? Sound = null,
            bool AllowEscape = true,
            DialogResult defaultResult = DialogResult.No)
        {
            return (int)defaultResult;
        }
    }
}
