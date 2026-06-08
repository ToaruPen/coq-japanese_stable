using System.Collections;
using QudJP;
using QudJP.Patches;

#pragma warning disable CA1308, S1144, S2325, S3604, S4487

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class InventoryLineRefreshCoordinatorTests
{
    [TearDown]
    public void TearDown()
    {
        InventoryLineRefreshCoordinator.ClearForTests();
        InventoryActionMenuCloseTimingObservability.ResetForTests();
    }

    [Test]
    public void MarkActiveInventoryLinesRefreshPendingForChangedItemForTests_ConsumesPendingAndAllowsOriginalUpdate()
    {
        var item = new DummyItem("{{K|空の}} インジェクター", "Artifacts");
        var screen = DummyInventoryScreen.CreateAz(new DummyInventoryLineData(item));
        var marked = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(item);

        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.True);
            Assert.That(consumed, Is.True);
            Assert.That(screen.Controller.BeforeShowCount, Is.Zero);
            Assert.That(screen.FullRefreshCount, Is.Zero);
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
    }

    [Test]
    public void RefreshAfterInventoryActionIfChanged_MarksDirtyAndAllowsOriginalUpdate()
    {
        var item = new DummyItem("{{K|オントロジカルアンカー}}", "Artifacts");
        var before = InventoryLineRefreshCoordinator.CaptureDisplaySnapshot(item, owner: null);
        var screen = DummyInventoryScreen.CreateAz(new DummyInventoryLineData(item));

        item.DisplayName = "{{W|オントロジカルアンカー}}";
        var marked = InventoryLineRefreshCoordinator.RefreshAfterInventoryActionIfChanged(item, owner: null, before);
        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.True);
            Assert.That(consumed, Is.True);
            Assert.That(screen.Controller.BeforeShowCount, Is.Zero);
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
    }

    [Test]
    public void RefreshAfterInventoryActionIfChanged_DoesNotMarkDirty_WhenItemDoesNotMatchSnapshot()
    {
        var before = InventoryLineRefreshCoordinator.CaptureDisplaySnapshot(
            new DummyItem("amber", "Artifacts"),
            owner: null);
        var other = new DummyItem("{{K|空の}} インジェクター", "Artifacts");

        var marked = InventoryLineRefreshCoordinator.RefreshAfterInventoryActionIfChanged(other, owner: null, before);
        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.False);
            Assert.That(consumed, Is.False);
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
    }

    [Test]
    public void RefreshAfterInventoryActionIfChanged_DoesNotMarkDirty_WhenDisplayStateIsUnchanged()
    {
        var item = new DummyItem("{{W|オントロジカルアンカー}}", "Artifacts");
        var before = InventoryLineRefreshCoordinator.CaptureDisplaySnapshot(item, owner: null);

        var marked = InventoryLineRefreshCoordinator.RefreshAfterInventoryActionIfChanged(item, owner: null, before);
        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.False);
            Assert.That(consumed, Is.False);
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
    }

    [Test]
    public void InventoryAndEquipmentStatusScreenInventoryLineSortPatch_ResortsAfterFullRefresh()
    {
        var later = new DummyItem("{{K|空の}} インジェクター", "Artifacts");
        var earlier = new DummyItem("{{m|アーバリー}} x25", "Artifacts");
        var laterLine = new DummyInventoryLineData(later);
        var earlierLine = new DummyInventoryLineData(earlier);
        var screen = DummyInventoryScreen.CreateAz(laterLine, earlierLine);

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(earlier);
        _ = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen);

        Assert.That(
            screen.Controller.LastItems.Select(static item => item.go!.DisplayName),
            Is.EqualTo(new[] { "{{m|アーバリー}} x25", "{{K|空の}} インジェクター" }));
    }

    [Test]
    public void InventoryAndEquipmentStatusScreenInventoryLineSortPatch_DoesNotResortWithoutPendingFullRefresh()
    {
        var later = new DummyItem("{{K|空の}} インジェクター", "Artifacts");
        var earlier = new DummyItem("{{m|アーバリー}} x25", "Artifacts");
        var laterLine = new DummyInventoryLineData(later);
        var earlierLine = new DummyInventoryLineData(earlier);
        var screen = DummyInventoryScreen.CreateAz(laterLine, earlierLine);

        InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen);

        Assert.That(screen.Controller.BeforeShowCount, Is.Zero);
    }

    [Test]
    public void InventoryActionMenuUpdateViewTimingPatch_ResortsAfterOriginalFullRefresh()
    {
        var later = new DummyItem("{{K|空の}} インジェクター", "Artifacts");
        var earlier = new DummyItem("{{m|アーバリー}} x25", "Artifacts");
        var laterLine = new DummyInventoryLineData(later);
        var earlierLine = new DummyInventoryLineData(earlier);
        var screen = DummyInventoryScreen.CreateAz(laterLine, earlierLine);
        var state = new InventoryActionMenuUpdateViewTimingPatch.RefreshState(
            screen,
            InventoryActionMenuCloseTimingObservability.TimingScope.Empty);

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(earlier);
        _ = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        InventoryActionMenuUpdateViewTimingPatch.Postfix(state);

        Assert.That(
            screen.Controller.LastItems.Select(static item => item.go!.DisplayName),
            Is.EqualTo(new[] { "{{m|アーバリー}} x25", "{{K|空の}} インジェクター" }));
    }

    private sealed class DummyInventoryScreen
    {
        private readonly List<DummyInventoryLineData> listItems = new();
        private readonly Dictionary<string, List<DummyInventoryLineData>> objectCategories = new();

        public string sortMode = "AZ";

        public DummyInventoryController Controller { get; } = new();

        public object inventoryController => Controller;

        public int FullRefreshCount { get; private set; }

        public static DummyInventoryScreen CreateAz(params DummyInventoryLineData[] lines)
        {
            var screen = new DummyInventoryScreen();
            screen.sortMode = "AZ";
            screen.listItems.AddRange(lines);
            screen.objectCategories["Artifacts"] = lines.ToList();
            return screen;
        }

#pragma warning disable S1144
        public void UpdateViewFromData()
#pragma warning restore S1144
        {
            FullRefreshCount++;
        }

#pragma warning disable S1144
        public bool isCollapsed(string category)
#pragma warning restore S1144
        {
            _ = category;
            return false;
        }
    }

    private sealed class DummyInventoryController
    {
        public int BeforeShowCount { get; private set; }

        public List<DummyInventoryLineData> LastItems { get; private set; } = new();

#pragma warning disable S1144
        public void BeforeShow(IEnumerable selections)
#pragma warning restore S1144
        {
            BeforeShowCount++;
            LastItems = selections.Cast<DummyInventoryLineData>().ToList();
        }
    }

    private sealed class DummyInventoryLineData
    {
        private string? cachedDisplayName;

        private string? cachedSortString;

        public bool category;

        public string categoryName = "";

#pragma warning disable S1144
        public int categoryOffset { get; set; }
#pragma warning restore S1144

        public DummyItem? go;

        public DummyInventoryLineData(DummyItem go)
        {
            this.go = go;
            category = false;
            categoryName = go.Category;
        }

#pragma warning disable S1144
        public string? displayName
        {
            get => cachedDisplayName ??= go?.DisplayName;
            set => cachedDisplayName = value;
        }

        public string? sortString
        {
            get => cachedSortString ??= displayName?.ToLowerInvariant();
            set => cachedSortString = value;
        }
#pragma warning restore S1144
    }

    private sealed class DummyItem
    {
        public DummyItem(string displayName, string category)
        {
            DisplayName = displayName;
            Category = category;
        }

        public string DisplayName { get; set; }

        public string Category { get; set; }

#pragma warning disable S1144
        public string GetInventoryCategory()
#pragma warning restore S1144
        {
            return Category;
        }
    }
}
