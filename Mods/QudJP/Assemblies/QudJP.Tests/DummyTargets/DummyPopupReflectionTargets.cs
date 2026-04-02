using System.Threading.Tasks;

#if !HAS_GAME_DLL
namespace Genkit
{
    internal struct Location2D
    {
    }
}

namespace XRL.UI
{
    internal enum DialogResult
    {
        No = 0,
        Yes = 1,
        Cancel = 2,
    }

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

        public static Task<DialogResult> ShowYesNoAsync(string Message)
        {
            return Task.FromResult(DialogResult.No);
        }

        public static int ShowYesNoCancel(
            string Message,
            string? Sound = null,
            bool AllowEscape = true,
            DialogResult defaultResult = DialogResult.No)
        {
            return (int)defaultResult;
        }

        public static Task<DialogResult> ShowYesNoCancelAsync(string Message)
        {
            return Task.FromResult(DialogResult.Cancel);
        }
    }
}
#endif
