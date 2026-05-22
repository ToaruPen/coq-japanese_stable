using System;
using System.Collections.Generic;
#if HAS_GAME_DLL
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using System.Xml.Linq;
#endif
using QudJP.Patches;
#if HAS_GAME_DLL
using XRL.World;
using XRL.World.Parts;
#endif

namespace QudJP.QudTest;

public static class QudTestRouteExecutor
{
    private const string InventoryDisplayNameRoute = "inventory-display-name";
    private const string InventoryDisplayNameGameObjectRoute = "inventory-display-name-game-object";
    private const string InventoryLineColorShapeRoute = "InventoryLineTranslationPatch > field=text";
    private const string InventoryLineFixtureDisplayNameProducer = "QudTest.InventoryDisplayNameFixture";
#if HAS_GAME_DLL
    private const string InventoryLineGameObjectDisplayNameProducer = "InventoryLine.GameObjectDisplayName";
#endif

    public static string Execute(QudTestCase testCase)
    {
        return ExecuteWithArtifacts(testCase).Actual;
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
            case InventoryDisplayNameRoute:
                return InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest(source);
            default:
                throw new NotSupportedException("Unsupported QudTest route: " + route);
        }
    }

    internal static QudTestRouteExecution ExecuteWithArtifacts(QudTestCase testCase)
    {
        if (testCase.Route == "patch-binding")
        {
            return new QudTestRouteExecution(QudTestPatchBindingExecutor.Execute(testCase), null);
        }

        if (testCase.Route == InventoryDisplayNameGameObjectRoute)
        {
            return ExecuteInventoryDisplayNameGameObject(testCase.Input);
        }

        var actual = Execute(testCase.Route, testCase.Input);
        return new QudTestRouteExecution(actual, CaptureColorShape(testCase, actual));
    }

    public static QudTestColorShapeCapture? CaptureColorShape(QudTestCase testCase, string actual)
    {
        if (testCase.Route != InventoryDisplayNameRoute)
        {
            return null;
        }

        return CaptureInventoryLineColorShape(
            testCase.Input,
            actual,
            InventoryLineFixtureDisplayNameProducer);
    }

    private static QudTestColorShapeCapture CaptureInventoryLineColorShape(
        string source,
        string actual,
        string producer)
    {
        var capture = ColorShapeCaptureObservability.Capture(
            InventoryLineColorShapeRoute,
            producer,
            source,
            actual);
        return new QudTestColorShapeCapture
        {
            Route = capture.Route,
            Producer = capture.Producer,
            Source = capture.SourceText,
            SourceVisible = capture.SourceVisibleText,
            Final = capture.FinalText,
            FinalVisible = capture.FinalVisibleText,
            SourceColorSpans = capture.SourceColorSpans,
            FinalColorSpans = capture.FinalColorSpans,
            SourceVisibleSha256 = capture.SourceVisibleSha256,
            FinalVisibleSha256 = capture.FinalVisibleSha256,
            MarkupSemanticStatus = capture.MarkupSemanticStatus,
            MarkupSemanticFlags = capture.MarkupSemanticFlags,
        };
    }

    private static QudTestRouteExecution ExecuteInventoryDisplayNameGameObject(string blueprint)
    {
#if HAS_GAME_DLL
        var source = CreateInventoryDisplayNameSource(blueprint);
        var actual = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest(source);
        return new QudTestRouteExecution(
            actual,
            CaptureInventoryLineColorShape(
                source,
                actual,
                InventoryLineGameObjectDisplayNameProducer));
#else
        throw new NotSupportedException(
            InventoryDisplayNameGameObjectRoute + " requires Assembly-CSharp.dll. Input blueprint: " + blueprint);
#endif
    }

#if HAS_GAME_DLL
    private static string CreateInventoryDisplayNameSource(string blueprint)
    {
        try
        {
            return GameObject.CreateSample(blueprint).DisplayName;
        }
        catch (SecurityException)
        {
            return CreateHeadlessInventoryDisplayNameSource(blueprint);
        }
    }

    private static string CreateHeadlessInventoryDisplayNameSource(string blueprint)
    {
        var renderDisplayName = ReadBlueprintRenderDisplayName(blueprint);
        var gameObject = new GameObject
        {
            Blueprint = blueprint,
            Render = new Render
            {
                DisplayName = renderDisplayName,
            },
        };
        return gameObject.DisplayName;
    }

    private static string ReadBlueprintRenderDisplayName(string blueprint)
    {
        foreach (var file in EnumerateObjectBlueprintFiles())
        {
            using var reader = XmlReader.Create(
                file,
                new XmlReaderSettings
                {
                    CheckCharacters = false,
                });
            var document = XDocument.Load(reader);
            var objectElement = document
                .Descendants("object")
                .FirstOrDefault(element => string.Equals(
                    (string?)element.Attribute("Name"),
                    blueprint,
                    StringComparison.Ordinal));
            var displayName = objectElement?
                .Elements("part")
                .FirstOrDefault(element => string.Equals(
                    (string?)element.Attribute("Name"),
                    "Render",
                    StringComparison.Ordinal))?
                .Attribute("DisplayName")?
                .Value;
            if (displayName is { Length: > 0 })
            {
                return displayName;
            }
        }

        throw new InvalidDataException("Render DisplayName not found for blueprint: " + blueprint);
    }

    private static IEnumerable<string> EnumerateObjectBlueprintFiles()
    {
        foreach (var file in EnumerateLocalizedObjectBlueprintFiles())
        {
            yield return file;
        }

        foreach (var directory in EnumerateObjectBlueprintDirectories().Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.xml", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateLocalizedObjectBlueprintFiles()
    {
        var projectRoot = Environment.GetEnvironmentVariable("QUDJP_PROJECT_ROOT");
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            projectRoot = Directory.GetCurrentDirectory();
        }

        var objectBlueprintsDirectory = Path.Combine(
            projectRoot,
            "Mods",
            "QudJP",
            "Localization",
            "ObjectBlueprints");
        return Directory.Exists(objectBlueprintsDirectory)
            ? Directory.EnumerateFiles(objectBlueprintsDirectory, "*.jp.xml", SearchOption.AllDirectories)
            : [];
    }

    private static IEnumerable<string> EnumerateObjectBlueprintDirectories()
    {
        var managedDir = Environment.GetEnvironmentVariable("COQ_MANAGED_DIR");
        if (!string.IsNullOrWhiteSpace(managedDir))
        {
            yield return Path.Combine(
                Directory.GetParent(managedDir)!.FullName,
                "StreamingAssets",
                "Base",
                "ObjectBlueprints");
        }

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Games",
            "CavesOfQud-stable-ref",
            "CoQ.app",
            "Contents",
            "Resources",
            "Data",
            "StreamingAssets",
            "Base",
            "ObjectBlueprints");
    }
#endif

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

internal sealed class QudTestRouteExecution
{
    public QudTestRouteExecution(string actual, QudTestColorShapeCapture? colorShape)
    {
        Actual = actual;
        ColorShape = colorShape;
    }

    public string Actual { get; }

    public QudTestColorShapeCapture? ColorShape { get; }
}
