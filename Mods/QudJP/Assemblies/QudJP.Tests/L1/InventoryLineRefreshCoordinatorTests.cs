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
        Translator.ResetForTests();
        InventoryLineTranslationPatch.ClearTranslationCachesForTests();
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
    public void MarkActiveInventoryLinesRefreshPendingForChangedItemForTests_DoesNotResort_WhenRequestedAsRefreshOnly()
    {
        var item = new DummyItem(
            "{{Y|傑作}} スコープ付き カービン {{c|\u001A}}9 {{r|\u0003}}1d8 {{y|[鉛スラッグ x24]}}",
            "Missile Weapons");
        var screen = DummyInventoryScreen.CreateCategory(new DummyInventoryLineData(item));
        var marked = InventoryLineRefreshCoordinator
            .MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(item, requiresResort: false);

        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen);

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.True);
            Assert.That(consumed, Is.True);
            Assert.That(screen.Controller.BeforeShowCount, Is.Zero);
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
    public void RefreshAfterInventoryActionIfChanged_DoesNotResort_WhenOnlyLoadedAmmoStateChanges()
    {
        AssertDoesNotResortForLoadedStateOnlyChange(
            "{{Y|傑作}} スコープ付き カービン {{c|\u001A}}9 {{r|\u0003}}1d8 {{y|[鉛スラッグ x24]}}",
            "{{Y|傑作}} スコープ付き カービン {{c|\u001A}}9 {{r|\u0003}}1d8 {{y|[{{K|空}}]}}",
            "Missile Weapons");
    }

    [Test]
    public void RefreshAfterInventoryActionIfChanged_DoesNotResort_WhenOnlyLoadedCellStateChanges()
    {
        AssertDoesNotResortForLoadedStateOnlyChange(
            "{{K|オントロジカルアンカー}} {{y|[{{c|大容量}} {{c|ケムセル}} {{y|({{g|残量多}})}}]}}",
            "{{K|オントロジカルアンカー}} {{y|[{{K|セルなし}}]}}",
            "Artifacts");
    }

    [Test]
    public void RefreshAfterInventoryActionIfChanged_DoesNotResort_WhenOnlyLoadedFuelStateChanges()
    {
        AssertDoesNotResortForLoadedStateOnlyChange(
            "{{rocket|ロケット}} {{K|スケート}} {{b|\u0004}}0 {{K|\t}}1 {{y|[22ドラムの油]}}",
            "{{rocket|ロケット}} {{K|スケート}} {{b|\u0004}}0 {{K|\t}}1 {{y|[{{K|空}}]}}",
            "Footwear");
    }

    [Test]
    public void RefreshAfterInventoryActionIfChanged_DoesNotResort_WhenOnlyOwnerInventoryMembershipChanges()
    {
        var item = new DummyItem(
            "{{Y|傑作}} スコープ付き カービン {{c|\u001A}}9 {{r|\u0003}}1d8 {{y|[鉛スラッグ x24]}}",
            "Missile Weapons");
        var ammo = new DummyItem("鉛スラッグ x3210", "Ammo");
        var owner = new DummyOwner(item, ammo);
        var before = InventoryLineRefreshCoordinator.CaptureDisplaySnapshot(item, owner);
        var screen = DummyInventoryScreen.CreateCategory(new DummyInventoryLineData(item));

        owner.Inventory.Objects.Remove(ammo);
        var marked = InventoryLineRefreshCoordinator.RefreshAfterInventoryActionIfChanged(item, owner, before);
        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen);

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
            screen.Controller.LastItems.Select(static item => GetDisplayName(item.go)),
            Is.EqualTo(new[] { "{{m|アーバリー}} x25", "{{K|空の}} インジェクター" }));
    }

    [Test]
    public void InventoryAndEquipmentStatusScreenInventoryLineSortPatch_ResortsByTranslatedDisplayNameAfterFullRefresh()
    {
        Translator.RegisterRuntimeTranslationForOwnerRoute("alpha tonic", "んトニック");
        Translator.RegisterRuntimeTranslationForOwnerRoute("zeta tonic", "あトニック");

        var rawEarlier = new DummyItem("alpha tonic", "Artifacts");
        var translatedEarlier = new DummyItem("zeta tonic", "Artifacts");
        var screen = DummyInventoryScreen.CreateAz(
            new DummyInventoryLineData(rawEarlier),
            new DummyInventoryLineData(translatedEarlier));

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(translatedEarlier);
        _ = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        var trace = TestTraceHelper.CaptureTrace(() =>
            InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen));

        Assert.Multiple(() =>
        {
            Assert.That(trace, Does.Not.Contain("post-full-refresh sort failed"));
            Assert.That(
                screen.Controller.LastItems.Select(static item => GetDisplayName(item.go)),
                Is.EqualTo(new[] { "zeta tonic", "alpha tonic" }));
            });
    }

    [Test]
    public void InventoryAndEquipmentStatusScreenInventoryLineSortPatch_SkipsIncompatibleBeforeShowOverloads()
    {
        var later = new DummyItem("{{K|空の}} インジェクター", "Artifacts");
        var earlier = new DummyItem("{{m|アーバリー}} x25", "Artifacts");
        var screen = DummyInventoryScreen.CreateAzWithIncompatibleBeforeShowOverload(
            new DummyInventoryLineData(later),
            new DummyInventoryLineData(earlier));

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(earlier);
        _ = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        var trace = TestTraceHelper.CaptureTrace(() =>
            InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen));

        Assert.Multiple(() =>
        {
            Assert.That(trace, Does.Not.Contain("post-full-refresh sort failed"));
            Assert.That(screen.Controller.BeforeShowCount, Is.EqualTo(1));
            Assert.That(screen.Controller.LastItems.Select(static item => GetDisplayName(item.go)),
                Is.EqualTo(new[] { "{{m|アーバリー}} x25", "{{K|空の}} インジェクター" }));
        });
    }

    [Test]
    public void InventoryAndEquipmentStatusScreenInventoryLineSortPatch_InvokesOptionalInventoryCategoryParameter()
    {
        var later = new DummyItemWithOptionalInventoryCategory("{{K|空の}} インジェクター", "Artifacts");
        var earlier = new DummyItemWithOptionalInventoryCategory("{{m|アーバリー}} x25", "Artifacts");
        var screen = DummyInventoryScreen.CreateAz(
            new DummyInventoryLineData(later),
            new DummyInventoryLineData(earlier));

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(earlier);
        _ = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        var trace = TestTraceHelper.CaptureTrace(() =>
            InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen));

        Assert.Multiple(() =>
        {
            Assert.That(trace, Does.Not.Contain("post-full-refresh sort failed"));
            Assert.That(
                screen.Controller.LastItems.Select(static item => GetDisplayName(item.go)),
                Is.EqualTo(new[] { "{{m|アーバリー}} x25", "{{K|空の}} インジェクター" }));
        });
    }

    [Test]
    public void InventoryAndEquipmentStatusScreenInventoryLineSortPatch_ReadsDisplaySortKeyOncePerLine()
    {
        var lines = new[]
        {
            new DummyInventoryLineData(new DummyItem("{{W|ゼタ}}", "Artifacts")),
            new DummyInventoryLineData(new DummyItem("{{W|イータ}}", "Artifacts")),
            new DummyInventoryLineData(new DummyItem("{{W|デルタ}}", "Artifacts")),
            new DummyInventoryLineData(new DummyItem("{{W|ベータ}}", "Artifacts")),
            new DummyInventoryLineData(new DummyItem("{{W|アルファ}}", "Artifacts")),
        };
        var screen = DummyInventoryScreen.CreateAz(lines);

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(lines[^1].go);
        _ = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen);

        Assert.That(lines.Select(static line => line.DisplayNameReadCount), Is.All.EqualTo(1));
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
            screen.Controller.LastItems.Select(static item => GetDisplayName(item.go)),
            Is.EqualTo(new[] { "{{m|アーバリー}} x25", "{{K|空の}} インジェクター" }));
    }

    [Test]
    public void ActionMenuReopenRefresh_ConsumesPendingAndRunsFullRefreshBeforeCancelReturn()
    {
        var later = new DummyItem("{{K|空の}} インジェクター", "Artifacts");
        var earlier = new DummyItem("{{m|アーバリー}} x25", "Artifacts");
        var screen = DummyInventoryScreen.CreateAz(
            new DummyInventoryLineData(later),
            new DummyInventoryLineData(earlier));

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(earlier);
        var refreshed = InventoryLineRefreshCoordinator.TryRefreshPendingInventoryLinesBeforeActionMenuReopenForTests(screen);
        var consumedAgain = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();

        Assert.Multiple(() =>
        {
            Assert.That(refreshed, Is.True);
            Assert.That(consumedAgain, Is.False);
            Assert.That(screen.FullRefreshCount, Is.EqualTo(1));
            Assert.That(screen.Controller.LastItems.Select(static item => GetDisplayName(item.go)),
                Is.EqualTo(new[] { "{{m|アーバリー}} x25", "{{K|空の}} インジェクター" }));
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
    }

    [Test]
    public void ActionMenuOpenRefresh_DetectsChangedItemSincePreviousActionMenuOpen()
    {
        var item = new DummyItem("{{K|オントロジカルアンカー}} [{{K|セルなし}}]", "Artifacts");
        var screen = DummyInventoryScreen.CreateAz(new DummyInventoryLineData(item));

        var firstOpenRefreshed = InventoryLineRefreshCoordinator
            .TryRefreshChangedInventoryLinesBeforeActionMenuOpenForTests(screen, item, owner: null);

        item.DisplayName = "{{K|オントロジカルアンカー}} [{{c|大容量}} {{c|ケムセル}}]";
        var secondOpenRefreshed = InventoryLineRefreshCoordinator
            .TryRefreshChangedInventoryLinesBeforeActionMenuOpenForTests(screen, item, owner: null);
        var consumedAgain = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();

        Assert.Multiple(() =>
        {
            Assert.That(firstOpenRefreshed, Is.False);
            Assert.That(secondOpenRefreshed, Is.True);
            Assert.That(consumedAgain, Is.False);
            Assert.That(screen.FullRefreshCount, Is.EqualTo(1));
            Assert.That(screen.Controller.BeforeShowCount, Is.Zero);
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
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

        public static DummyInventoryScreen CreateCategory(params DummyInventoryLineData[] lines)
        {
            var screen = new DummyInventoryScreen();
            screen.sortMode = "Category";
            foreach (var group in lines.GroupBy(static line => line.categoryName))
            {
                var categoryLines = group.ToList();
                screen.listItems.Add(DummyInventoryLineData.CreateCategoryHeader(group.Key));
                screen.listItems.AddRange(categoryLines);
                screen.objectCategories[group.Key] = categoryLines;
            }

            return screen;
        }

        public static DummyInventoryScreen CreateAzWithIncompatibleBeforeShowOverload(params DummyInventoryLineData[] lines)
        {
            var screen = CreateAz(lines);
            screen.Controller.EnableIncompatibleBeforeShowOverload = true;
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

    private static string? GetDisplayName(object? item)
    {
        return ReflectionUtils.GetPropertyOrFieldValue(item, "DisplayName") as string;
    }

    private static void AssertDoesNotResortForLoadedStateOnlyChange(
        string beforeName,
        string afterName,
        string category)
    {
        var item = new DummyItem(beforeName, category);
        var before = InventoryLineRefreshCoordinator.CaptureDisplaySnapshot(item, owner: null);
        var screen = DummyInventoryScreen.CreateCategory(new DummyInventoryLineData(item));

        item.DisplayName = afterName;
        var marked = InventoryLineRefreshCoordinator.RefreshAfterInventoryActionIfChanged(item, owner: null, before);
        var consumed = InventoryLineRefreshCoordinator.ConsumePendingInventoryLineRefreshForUpdateView();
        InventoryAndEquipmentStatusScreenInventoryLineSortPatch.Postfix(screen);

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.True);
            Assert.That(consumed, Is.True);
            Assert.That(screen.Controller.BeforeShowCount, Is.Zero);
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
    }

    private sealed class DummyInventoryController
    {
        public int BeforeShowCount { get; private set; }

        public List<DummyInventoryLineData> LastItems { get; private set; } = new();

        public bool EnableIncompatibleBeforeShowOverload { get; set; }

#pragma warning disable S1144
        public void BeforeShow(IEnumerable<DummyIncompatibleInventoryLineData> selections)
#pragma warning restore S1144
        {
            _ = selections;
            if (EnableIncompatibleBeforeShowOverload)
            {
                throw new InvalidOperationException("Wrong BeforeShow overload was selected.");
            }
        }

#pragma warning disable S1144
        public void BeforeShow(IEnumerable selections)
#pragma warning restore S1144
        {
            BeforeShowCount++;
            LastItems = selections.Cast<DummyInventoryLineData>().ToList();
        }
    }

    private sealed class DummyIncompatibleInventoryLineData
    {
        public string Marker { get; } = nameof(DummyIncompatibleInventoryLineData);
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

        public object? go;

        public int DisplayNameReadCount { get; private set; }

        public DummyInventoryLineData(object go)
        {
            this.go = go;
            category = false;
            categoryName = ReflectionUtils.GetPropertyOrFieldValue(go, "Category") as string ?? string.Empty;
        }

        public static DummyInventoryLineData CreateCategoryHeader(string categoryName)
        {
            return new DummyInventoryLineData(new DummyItem(string.Empty, categoryName))
            {
                category = true,
                categoryName = categoryName,
            };
        }

#pragma warning disable S1144
        public string? displayName
        {
            get
            {
                DisplayNameReadCount++;
                return cachedDisplayName ??= ReflectionUtils.GetPropertyOrFieldValue(go, "DisplayName") as string;
            }

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

    private sealed class DummyItemWithOptionalInventoryCategory
    {
        public DummyItemWithOptionalInventoryCategory(string displayName, string category)
        {
            DisplayName = displayName;
            Category = category;
        }

        public string DisplayName { get; set; }

        public string Category { get; set; }

#pragma warning disable S1144
        public string GetInventoryCategory(bool asIfKnown = false)
#pragma warning restore S1144
        {
            _ = asIfKnown;
            return Category;
        }
    }

    private sealed class DummyOwner
    {
        public DummyOwner(params object[] objects)
        {
            Inventory = new DummyInventory(objects);
        }

        public DummyInventory Inventory { get; }
    }

    private sealed class DummyInventory
    {
        public DummyInventory(IEnumerable<object> objects)
        {
            Objects = objects.ToList();
        }

        public List<object> Objects { get; }
    }
}
