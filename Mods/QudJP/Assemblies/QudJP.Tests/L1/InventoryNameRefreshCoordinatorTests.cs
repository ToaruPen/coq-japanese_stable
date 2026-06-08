using System.Collections;
using QudJP;
using QudJP.Patches;

#pragma warning disable CA1308, S1144, S2325, S3604, S4144, S4487

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class InventoryNameRefreshCoordinatorTests
{
    [SetUp]
    public void SetUp()
    {
        InventoryNameRefreshCoordinator.ClearForTests();
        InventoryLineRefreshCoordinator.ClearForTests();
        InventoryActionMenuCloseTimingObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        InventoryNameRefreshCoordinator.ClearForTests();
        InventoryLineRefreshCoordinator.ClearForTests();
        InventoryActionMenuCloseTimingObservability.ResetForTests();
    }

    [Test]
    public void ResetDirtyInventoryNameCachesBeforeRefresh_ResetsVisibleInventoryOnce()
    {
        var owner = new DummyRefreshOwner();
        var examined = new DummyRefreshItem { InInventory = owner };
        var matchingUnseen = new DummyRefreshItem { InInventory = owner };
        var unrelated = new DummyRefreshItem { InInventory = owner };
        owner.Inventory.Objects.Add(examined);
        owner.Inventory.Objects.Add(matchingUnseen);
        owner.Inventory.Objects.Add(unrelated);

        InventoryNameRefreshCoordinator.MarkInventoryNameStateChanged(examined);

        var firstReset = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(
            new DummyRefreshInventoryScreen { GO = owner });
        var secondReset = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(
            new DummyRefreshInventoryScreen { GO = owner });

        Assert.Multiple(() =>
        {
            Assert.That(firstReset, Is.True);
            Assert.That(secondReset, Is.False);
            Assert.That(examined.ResetNameCacheCallCount, Is.EqualTo(2));
            Assert.That(matchingUnseen.ResetNameCacheCallCount, Is.EqualTo(1));
            Assert.That(unrelated.ResetNameCacheCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ResetDirtyInventoryNameCachesBeforeRefresh_DoesNotClearDirtyState_WhenScreenHasNoOwner()
    {
        var owner = new DummyRefreshOwner();
        var item = new DummyRefreshItem { InInventory = owner };
        owner.Inventory.Objects.Add(item);

        InventoryNameRefreshCoordinator.MarkInventoryNameStateChanged(item);

        var missingOwnerReset = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(new object());
        var realReset = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(
            new DummyRefreshInventoryScreen { GO = owner });

        Assert.Multiple(() =>
        {
            Assert.That(missingOwnerReset, Is.False);
            Assert.That(realReset, Is.True);
            Assert.That(item.ResetNameCacheCallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void ExaminerPostfix_AndInventoryScreenPrefix_ResetInventoryNameCaches()
    {
        var owner = new DummyRefreshOwner();
        var examined = new DummyRefreshItem { InInventory = owner };
        var matchingUnseen = new DummyRefreshItem { InInventory = owner };
        owner.Inventory.Objects.Add(examined);
        owner.Inventory.Objects.Add(matchingUnseen);

        ExaminerInventoryNameRefreshPatch.Postfix(new DummyRefreshExaminer { ParentObject = examined });
        InventoryAndEquipmentStatusScreenNameRefreshPatch.Prefix(new DummyRefreshInventoryScreen { GO = owner });

        Assert.Multiple(() =>
        {
            Assert.That(examined.ResetNameCacheCallCount, Is.EqualTo(2));
            Assert.That(matchingUnseen.ResetNameCacheCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ExaminerPostfix_DefersInventoryLineFullRefreshUntilUpdateView()
    {
        var owner = new DummyRefreshOwner();
        var examined = new DummyRefreshItem
        {
            DisplayName = "{{K|空の}} インジェクター",
            InInventory = owner,
        };
        owner.Inventory.Objects.Add(examined);
        var screen = DummyRefreshInventoryScreen.CreateCategory(
            owner,
            DummyInventoryLineData.CreateCategory("Artifacts"),
            new DummyInventoryLineData(examined));

        ExaminerInventoryNameRefreshPatch.Postfix(new DummyRefreshExaminer { ParentObject = examined });
        var fullRefreshNeeded = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(screen);
        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();

        Assert.Multiple(() =>
        {
            Assert.That(fullRefreshNeeded, Is.True);
            Assert.That(consumed, Is.True);
            Assert.That(screen.Controller.BeforeShowCount, Is.Zero);
            Assert.That(screen.FullRefreshCount, Is.Zero);
        });
    }

    private sealed class DummyRefreshInventoryScreen
    {
        public DummyRefreshOwner? GO { get; set; }

        private readonly List<DummyInventoryLineData> listItems = new();
        private readonly Dictionary<string, List<DummyInventoryLineData>> objectCategories = new();

        public string sortMode = "Category";

        public DummyInventoryController Controller { get; } = new();

        public object inventoryController => Controller;

        public int FullRefreshCount { get; private set; }

        public static DummyRefreshInventoryScreen CreateCategory(
            DummyRefreshOwner owner,
            DummyInventoryLineData header,
            params DummyInventoryLineData[] lines)
        {
            var screen = new DummyRefreshInventoryScreen
            {
                GO = owner,
            };
            screen.listItems.Add(header);
            screen.listItems.AddRange(lines);
            screen.objectCategories[header.categoryName] = lines.ToList();
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

    private sealed class DummyRefreshOwner
    {
        public DummyRefreshInventory Inventory { get; } = new();
    }

    private sealed class DummyRefreshExaminer
    {
        public DummyRefreshItem? ParentObject { get; set; }
    }

    private sealed class DummyRefreshInventory
    {
        public ArrayList Objects { get; } = new();
    }

    private sealed class DummyRefreshItem
    {
        public DummyRefreshOwner? InInventory { get; set; }

        public string DisplayName { get; set; } = "dummy item";

        public string Category { get; set; } = "Artifacts";

        public int ResetNameCacheCallCount { get; private set; }

#pragma warning disable S1144
        public string GetInventoryCategory()
#pragma warning restore S1144
        {
            return Category;
        }

#pragma warning disable S1144
        public void ResetNameCache()
#pragma warning restore S1144
        {
            ResetNameCacheCallCount++;
        }
    }

    private sealed class DummyInventoryController
    {
        public int BeforeShowCount { get; private set; }

#pragma warning disable S1144
        public void BeforeShow(IEnumerable selections)
#pragma warning restore S1144
        {
            _ = selections;
            BeforeShowCount++;
        }
    }

    private sealed class DummyInventoryLineData
    {
        private string? cachedDisplayName;

        private string? cachedSortString;

        public bool category;

        public string categoryName = "";

#pragma warning disable S1144
        public int categoryAmount { get; set; }

        public int categoryWeight { get; set; }

        public int categoryOffset { get; set; }

        public bool categoryExpanded { get; set; }
#pragma warning restore S1144

        public DummyRefreshItem? go;

        public DummyInventoryLineData(DummyRefreshItem go)
        {
            this.go = go;
            categoryName = go.Category;
        }

        private DummyInventoryLineData()
        {
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

        public static DummyInventoryLineData CreateCategory(string categoryName)
        {
            return new DummyInventoryLineData
            {
                category = true,
                categoryName = categoryName,
            };
        }
    }
}
