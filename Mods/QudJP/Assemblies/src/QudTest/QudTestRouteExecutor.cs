using System;
using System.Collections.Generic;
using QudJP.Patches;

namespace QudJP.QudTest;

public static class QudTestRouteExecutor
{
    public static string Execute(QudTestCase testCase)
    {
        if (testCase.Route == "patch-binding")
        {
            return QudTestPatchBindingExecutor.Execute(testCase);
        }

        return Execute(testCase.Route, testCase.Input);
    }

    public static string Execute(string route, string source)
    {
        switch (route)
        {
            case "start-replace":
                return ExecuteStartReplace(source);
            case "message-log":
                return ExecuteMessageLog(source);
            case "message-queue":
                return ExecuteMessageQueue(source);
            case "wish-queue":
                return ExecuteWishQueue(source);
            case "popup-text":
                return PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, nameof(PopupTranslationPatch));
            case "popup-askstring-prompt":
                return ExecutePopupAskStringPrompt(source);
            case "popup-menu-item":
                return PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(source, nameof(PopupTranslationPatch));
            case "popup-message-button":
                return PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, nameof(PopupMessageTranslationPatch));
            case "bottom-context-item":
                return ExecuteBottomContextItem(source);
            case "game-summary-menu-literal":
                return GameSummaryScreenMenuBarsTranslationPatch.TranslateLiteral(source);
            default:
                throw new NotSupportedException("Unsupported QudTest route: " + route);
        }
    }

    private static string ExecuteStartReplace(string source)
    {
        var text = source;
        StartReplaceTranslationPatch.Prefix(ref text);
        return text;
    }

    private static string ExecuteMessageLog(string source)
    {
        var message = source;
        _ = MessageLogPatch.Prefix(ref message, null, Capitalize: true);
        return message;
    }

    private static string ExecuteMessageQueue(string source)
    {
        var message = source;
        _ = PhysicsEnterCellPassByTranslationPatch.Prefix(ref message, null, Capitalize: true);
        _ = ZoneManagerSetActiveZoneMessageQueuePatch.Prefix(ref message, null, Capitalize: true);
        _ = CombatAndLogMessageQueuePatch.Prefix(ref message, null, Capitalize: true);
        _ = MessageLogPatch.Prefix(ref message, null, Capitalize: true);
        return message;
    }

    private static string ExecuteWishQueue(string source)
    {
        var message = source;
        WishCommandQueueTranslationPatch.Prefix();
        try
        {
            _ = MessageQueueSemanticPipeline.TryTranslateQueuedMessage(ref message, null);
        }
        finally
        {
            _ = WishCommandQueueTranslationPatch.Finalizer(null);
        }

        _ = MessageFrameTranslator.TryStripDirectTranslationMarker(ref message);
        return message;
    }

    private static string ExecutePopupAskStringPrompt(string source)
    {
        var message = source;
        PopupAskStringTranslationPatch.Prefix(ref message);
        return message;
    }

    private static string ExecuteBottomContextItem(string source)
    {
        var context = new QudTestBottomContext(source);
        QudMenuBottomContextTranslationPatch.NormalizeItemTexts(context);
        return context.items[0].text;
    }

    private sealed class QudTestBottomContext
    {
        internal readonly List<QudTestBottomContextItem> items;

        internal QudTestBottomContext(string text)
        {
            items = [new QudTestBottomContextItem(text)];
        }
    }

    private sealed class QudTestBottomContextItem
    {
        internal string text;

        internal QudTestBottomContextItem(string text)
        {
            this.text = text;
        }
    }
}
