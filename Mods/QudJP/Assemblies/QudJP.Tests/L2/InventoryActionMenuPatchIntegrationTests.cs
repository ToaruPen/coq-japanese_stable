using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class InventoryActionMenuPatchIntegrationTests
{
    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        InventoryActionMenuCloseTimingObservability.ResetForTests();
        InventoryActionMenuCursorSoundPatch.SetPlayCursorSoundRequestObserverForTests(null);
        InventoryActionMenuPopupUpdateTimingPatch.ResetForTests();
        InventoryLineRefreshCoordinator.ClearForTests();
    }

    [Test]
    public void CloseTimingHarmonyPatches_SuppressCancelRefreshUntilPopupHideDelayCompletes()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var menu = new DummyInventoryActionMenuTarget();
        var popup = new DummyPopupMessageTarget
        {
            PopupID = "InventoryActionMenu:(noid)",
        };
        menu.PopupToHide = popup;
        var inventory = new DummyInventoryStatusScreenTarget();

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            _ = menu.Show(new ArrayList { "drop" }, cancel: true);
            inventory.UpdateViewFromData();
            popup.Update();
            popup.Update();
            inventory.UpdateViewFromData();
        });

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.EqualTo(1));
            Assert.That(output, Does.Contain("phase=popup-hide-request"));
            Assert.That(output, Does.Contain("phase=inventory-refresh-suppressed"));
            Assert.That(output, Does.Contain("phase=popup-hidden-after-frame-delay"));
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_AllowsDirtyInventoryNameRefreshDuringCancelSuppression()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming(includeNameRefresh: true);
        var menu = new DummyInventoryActionMenuTarget();
        var popup = new DummyPopupMessageTarget
        {
            PopupID = "InventoryActionMenu:(noid)",
        };
        menu.PopupToHide = popup;
        var item = new DummyRefreshItem();
        var inventory = new DummyInventoryStatusScreenTarget
        {
            GO = new DummyRefreshOwner(item),
        };

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            _ = menu.Show(new ArrayList { "look" }, cancel: true);
            InventoryNameRefreshCoordinator.MarkInventoryNameStateChanged(item);
            inventory.UpdateViewFromData();
        });

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.EqualTo(1));
            Assert.That(item.ResetNameCacheCallCount, Is.EqualTo(2));
            Assert.That(output, Does.Not.Contain("phase=inventory-refresh-suppressed"));
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_AllowsFullRefresh_WhenPendingInventoryLineRefreshIsPending()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var menu = new DummyInventoryActionMenuTarget
        {
            PopupToHide = new DummyPopupMessageTarget
            {
                PopupID = "InventoryActionMenu:(noid)",
            },
        };
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem();

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            _ = menu.Show(new ArrayList { "look" }, cancel: true);
            _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(item);
            inventory.UpdateViewFromData();
        });

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.EqualTo(1));
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
            Assert.That(output, Does.Not.Contain("phase=inventory-refresh-suppressed"));
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_ResetFiltersBeforePendingInventoryLineRefresh()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem
        {
            DisplayName = "{{K|空の}} インジェクター",
            Category = "Tonics",
        };
        inventory.GO = new DummyRefreshOwner(item);
        inventory.filterBar.enabledCategories.Clear();
        inventory.filterBar.enabledCategories.Add("Artifacts");

        _ = InventoryLineRefreshCoordinator.MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(item);
        inventory.UpdateViewFromData();

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.EqualTo(1));
            Assert.That(inventory.filterBar.enabledCategories, Is.EqualTo(new[] { "*All" }));
            Assert.That(inventory.LastVisibleInventoryNames, Is.EqualTo(new[] { "{{K|空の}} インジェクター" }));
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_RefreshesDisplayNameChangedByInventoryAction()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem
        {
            DisplayName = "{{K|空の}} エネルギーセル",
            Category = "Energy Cells",
        };
        inventory.GO = new DummyRefreshOwner(item);
        var state = default(InventoryLineRefreshCoordinator.DisplaySnapshot);

        InventoryActionProcessInventoryLineRefreshPatch.Prefix(item, inventory.GO, ref state);
        item.DisplayName = "{{Y|満充電の}} エネルギーセル";
        InventoryActionProcessInventoryLineRefreshPatch.Postfix(item, inventory.GO, state);
        inventory.UpdateViewFromData();

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.EqualTo(1));
            Assert.That(inventory.LastVisibleInventoryNames, Is.EqualTo(new[] { "{{Y|満充電の}} エネルギーセル" }));
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_AllowsRefreshAfterPriorChangedActionEvenWhenNextMenuCancels()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var actionMenu = new DummyInventoryActionMenuTarget();
        var cancelMenu = new DummyInventoryActionMenuTarget
        {
            PopupToHide = new DummyPopupMessageTarget
            {
                PopupID = "InventoryActionMenu:(noid)",
            },
        };
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem
        {
            DisplayName = "zz mystery",
        };
        var state = default(InventoryLineRefreshCoordinator.DisplaySnapshot);

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            _ = actionMenu.Show(new ArrayList { "change cell" }, cancel: false);
            InventoryActionProcessInventoryLineRefreshPatch.Prefix(item, new DummyRefreshOwner(item), ref state);
            item.DisplayName = "aa known";
            InventoryActionProcessInventoryLineRefreshPatch.Postfix(item, new DummyRefreshOwner(item), state);
            _ = cancelMenu.Show(new ArrayList { "look" }, cancel: true);
            inventory.UpdateViewFromData();
        });

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.EqualTo(1));
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
            Assert.That(output, Does.Contain("reason=display-or-category-changed"));
            Assert.That(output, Does.Not.Contain("phase=inventory-refresh-suppressed"));
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_DoesNotAllowRefreshAfterPlainActionSelection()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var actionMenu = new DummyInventoryActionMenuTarget();
        var cancelMenu = new DummyInventoryActionMenuTarget
        {
            PopupToHide = new DummyPopupMessageTarget
            {
                PopupID = "InventoryActionMenu:(noid)",
            },
        };
        var inventory = new DummyInventoryStatusScreenTarget();

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            _ = actionMenu.Show(new ArrayList { "look" }, cancel: false);
            _ = cancelMenu.Show(new ArrayList { "look" }, cancel: true);
            inventory.UpdateViewFromData();
        });

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.Zero);
            Assert.That(InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(), Is.False);
            Assert.That(output, Does.Not.Contain("reason=action-menu-selection"));
            Assert.That(output, Does.Contain("phase=inventory-refresh-suppressed"));
        });
    }

    [Test]
    public void InventoryActionProcessPatch_DefersInventoryLineRefresh_WhenActionChangesDisplayName()
    {
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem
        {
            DisplayName = "zz mystery",
        };
        var state = default(InventoryLineRefreshCoordinator.DisplaySnapshot);

        InventoryActionProcessInventoryLineRefreshPatch.Prefix(item, new DummyRefreshOwner(item), ref state);
        item.DisplayName = "aa known";
        InventoryActionProcessInventoryLineRefreshPatch.Postfix(item, new DummyRefreshOwner(item), state);

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.Zero);
            Assert.That(
                InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(),
                Is.True);
        });
    }

    [Test]
    public void InventoryActionEventPatch_DefersInventoryLineRefresh_WhenDirectEventChangesDisplayName()
    {
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem
        {
            DisplayName = "ontological anchor",
        };
        var state = default(InventoryLineRefreshCoordinator.DisplaySnapshot);

        InventoryActionEventInventoryLineRefreshPatch.Prefix(new DummyRefreshOwner(item), item, ref state);
        item.DisplayName = "ontological anchor [full chem cell]";
        InventoryActionEventInventoryLineRefreshPatch.Postfix(new DummyRefreshOwner(item), item, state);

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.Zero);
            Assert.That(
                InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(),
                Is.True);
        });
    }

    [Test]
    public void InventoryActionProcessPatch_DefersInventoryLineRefresh_WhenActionCompletesWithoutDisplayChange()
    {
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem
        {
            DisplayName = "aa known",
        };
        var state = default(InventoryLineRefreshCoordinator.DisplaySnapshot);

        InventoryActionProcessInventoryLineRefreshPatch.Prefix(item, new DummyRefreshOwner(item), ref state);
        InventoryActionProcessInventoryLineRefreshPatch.Postfix(item, new DummyRefreshOwner(item), state);

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.Zero);
            Assert.That(
                InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(),
                Is.True);
        });
    }

    [Test]
    public void InventoryActionEventPatch_DefersInventoryLineRefresh_WhenDirectEventCompletesWithoutDisplayChange()
    {
        var inventory = new DummyInventoryStatusScreenTarget();
        var item = new DummyRefreshItem
        {
            DisplayName = "ontological anchor",
        };
        var state = default(InventoryLineRefreshCoordinator.DisplaySnapshot);

        InventoryActionEventInventoryLineRefreshPatch.Prefix(new DummyRefreshOwner(item), item, ref state);
        InventoryActionEventInventoryLineRefreshPatch.Postfix(new DummyRefreshOwner(item), item, state);

        Assert.Multiple(() =>
        {
            Assert.That(inventory.RefreshCount, Is.Zero);
            Assert.That(
                InventoryActionMenuCloseTimingObservability.HasPendingInventoryLineRefreshAfterAction(),
                Is.True);
        });
    }

    [Test]
    public void CloseTimingHarmonyPatches_RestoreOuterActiveMenuAfterNestedActionMenu()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchCloseTiming();
        var outerMenu = new DummyInventoryActionMenuTarget();
        var innerMenu = new DummyInventoryActionMenuTarget();
        var popup = new DummyPopupMessageTarget
        {
            PopupID = "InventoryActionMenu:outer",
        };
        outerMenu.NestedMenu = innerMenu;
        outerMenu.PopupToHide = popup;

        var output = TestTraceHelper.CaptureTrace(() =>
            _ = outerMenu.Show(new ArrayList { "outer" }, cancel: true));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("seq=2;phase=menu-return"));
            Assert.That(output, Does.Contain("result\\=action"));
            Assert.That(output, Does.Contain("seq=1;phase=popup-hide-request"));
            Assert.That(output, Does.Contain("seq=1;phase=menu-return"));
            Assert.That(output, Does.Contain("result\\=cancel"));
        });
    }

    [Test]
    public void PopupUpdateTimingPatch_SkipsReflection_WhenNoSuppressionAndVerboseProbesAreDisabled()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(false);
        using var patches = PatchPopupUpdateOnly(typeof(DummyPopupMessageWithoutHideNextFrameTarget));
        var popup = new DummyPopupMessageWithoutHideNextFrameTarget();

        var output = TestTraceHelper.CaptureTrace(popup.Update);

        Assert.That(output, Does.Not.Contain("could not read HideNextFrame"));
    }

    [Test]
    public void PopupUpdateTimingPatch_WarnsOnce_WhenHideNextFrameCannotBeRead()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        using var patches = PatchPopupUpdateOnly(typeof(DummyPopupMessageWithoutHideNextFrameTarget));
        var popup = new DummyPopupMessageWithoutHideNextFrameTarget();

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            popup.Update();
            popup.Update();
        });

        Assert.That(CountOccurrences(output, "could not read HideNextFrame"), Is.EqualTo(1));
    }

    [Test]
    public void CursorSoundHarmonyPatches_RestoreOuterPopupContextAfterNestedPopupHide()
    {
        using var patches = PatchCursorSound();
        var observedPopupIds = new List<string?>();
        InventoryActionMenuCursorSoundPatch.SetPlayCursorSoundRequestObserverForTests(
            (_, popupId) => observedPopupIds.Add(popupId));
        var controller = new DummyMenuControllerTarget();
        var popup = new DummyPopupMessageTarget
        {
            controller = controller,
            PopupID = "InventoryActionMenu:outer",
        };

        popup.ShowPopup();
        popup.PopupID = "Popup:inner";
        popup.ShowPopup();

        Assert.Multiple(() =>
        {
            Assert.That(popup.ShowCount, Is.EqualTo(2));
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out var innerPopupId),
                Is.True);
            Assert.That(innerPopupId, Is.EqualTo("Popup:inner"));
        });

        controller.PlayClick();

        Assert.That(observedPopupIds, Is.EqualTo(new[] { "Popup:inner" }));

        popup.Hide();

        Assert.Multiple(() =>
        {
            Assert.That(
                InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out var restoredPopupId),
                Is.True);
            Assert.That(restoredPopupId, Is.EqualTo("InventoryActionMenu:outer"));
        });

        popup.Hide();

        Assert.That(
            InventoryActionMenuCursorSoundPatch.TryGetRememberedPopupId(controller, out _),
            Is.False);
    }

    private static IDisposable PatchCloseTiming(bool includeNameRefresh = false)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyInventoryActionMenuTarget), nameof(DummyInventoryActionMenuTarget.Show)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuShowTimingPatch),
                nameof(InventoryActionMenuShowTimingPatch.Prefix),
                typeof(object),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope).MakeByRefType())),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuShowTimingPatch),
                nameof(InventoryActionMenuShowTimingPatch.Postfix),
                typeof(object),
                typeof(InventoryActionMenuCloseTimingObservability.TimingScope))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.Hide)),
            prefix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuPopupHideTimingPatch), nameof(InventoryActionMenuPopupHideTimingPatch.Prefix), typeof(object))));
        PatchPopupUpdate(harmony, typeof(DummyPopupMessageTarget));
        harmony.Patch(
            original: RequireMethod(typeof(DummyInventoryStatusScreenTarget), nameof(DummyInventoryStatusScreenTarget.UpdateViewFromData)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuUpdateViewTimingPatch),
                nameof(InventoryActionMenuUpdateViewTimingPatch.Prefix),
                typeof(object),
                typeof(InventoryActionMenuUpdateViewTimingPatch.RefreshState).MakeByRefType())),
            postfix: new HarmonyMethod(RequireMethod(
                typeof(InventoryActionMenuUpdateViewTimingPatch),
                nameof(InventoryActionMenuUpdateViewTimingPatch.Postfix),
                typeof(InventoryActionMenuUpdateViewTimingPatch.RefreshState))));
        if (includeNameRefresh)
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyInventoryStatusScreenTarget), nameof(DummyInventoryStatusScreenTarget.UpdateViewFromData)),
                prefix: new HarmonyMethod(RequireMethod(
                    typeof(InventoryAndEquipmentStatusScreenNameRefreshPatch),
                    nameof(InventoryAndEquipmentStatusScreenNameRefreshPatch.Prefix),
                    typeof(object))));
        }

        return new HarmonyScope(harmony, harmonyId);
    }

    private static IDisposable PatchPopupUpdateOnly(Type popupType)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        PatchPopupUpdate(harmony, popupType);
        return new HarmonyScope(harmony, harmonyId);
    }

    private static void PatchPopupUpdate(Harmony harmony, Type popupType)
    {
        harmony.Patch(
            original: RequireMethod(popupType, "Update"),
            prefix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuPopupUpdateTimingPatch), nameof(InventoryActionMenuPopupUpdateTimingPatch.Prefix), typeof(object), typeof(int).MakeByRefType())),
            postfix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuPopupUpdateTimingPatch), nameof(InventoryActionMenuPopupUpdateTimingPatch.Postfix), typeof(object), typeof(int))));
    }

    private static IDisposable PatchCursorSound()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.ShowPopup)),
            postfix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuCursorSoundPopupContextPatch), nameof(InventoryActionMenuCursorSoundPopupContextPatch.Postfix), typeof(object))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupMessageTarget), nameof(DummyPopupMessageTarget.Hide)),
            prefix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuCursorSoundPopupHideContextPatch), nameof(InventoryActionMenuCursorSoundPopupHideContextPatch.Prefix), typeof(object))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyMenuControllerTarget), nameof(DummyMenuControllerTarget.PlayClick)),
            postfix: new HarmonyMethod(RequireMethod(typeof(InventoryActionMenuCursorSoundPlayClickPatch), nameof(InventoryActionMenuCursorSoundPlayClickPatch.Postfix), typeof(object))));
        return new HarmonyScope(harmony, harmonyId);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        return parameterTypes.Length == 0
            ? AccessTools.Method(type, methodName) ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}")
            : AccessTools.Method(type, methodName, parameterTypes) ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var matchIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + value.Length;
        }
    }

    private sealed class DummyInventoryActionMenuTarget
    {
        public DummyPopupMessageTarget? PopupToHide { get; set; }

        public DummyInventoryActionMenuTarget? NestedMenu { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public object? Show(object actionTable, bool cancel)
        {
            _ = NestedMenu?.Show(new ArrayList { "nested" }, cancel: false);
            if (cancel)
            {
                PopupToHide?.Hide();
            }

            return cancel ? null : new object();
        }
    }

    private sealed class DummyPopupMessageTarget
    {
        public object? controller;
        public string? PopupID;
        public int HideNextFrame;
        public int ShowCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ShowPopup()
        {
            ShowCount++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Hide()
        {
            HideNextFrame = 2;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Update()
        {
            if (HideNextFrame > 0)
            {
                HideNextFrame--;
            }
        }
    }

    private sealed class DummyPopupMessageWithoutHideNextFrameTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Update()
        {
        }
    }

#pragma warning disable CA1308, S1144, S3459, S3604, S4487
    private sealed class DummyInventoryStatusScreenTarget
    {
        private readonly List<DummyInventoryLineData> listItems = new();

        private readonly Dictionary<string, List<DummyInventoryLineData>> objectCategories = new();

        public string sortMode = "Category";

        public DummyRefreshOwner? GO { get; set; }

        public DummyFilterBar filterBar { get; } = new();

        public DummyInventoryController inventoryController { get; } = new();

        public int RefreshCount { get; private set; }

        public IReadOnlyList<string> LastVisibleInventoryNames => inventoryController.LastItems
            .Where(static item => !item.category)
            .Select(static item => item.go!.DisplayName)
            .ToArray();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpdateViewFromData()
        {
            RefreshCount++;
            if (GO is null)
            {
                return;
            }

            listItems.Clear();
            objectCategories.Clear();
            foreach (var item in GO.Inventory.Objects.Cast<DummyRefreshItem>())
            {
                var category = item.GetInventoryCategory();
                if (!filterBar.enabledCategories.Contains("*All")
                    && !filterBar.enabledCategories.Contains(category))
                {
                    continue;
                }

                var line = new DummyInventoryLineData(item);
                listItems.Add(line);
                if (!objectCategories.TryGetValue(category, out var categoryLines))
                {
                    categoryLines = new List<DummyInventoryLineData>();
                    objectCategories.Add(category, categoryLines);
                }

                categoryLines.Add(line);
            }

            inventoryController.BeforeShow(listItems);
        }
    }

    private sealed class DummyFilterBar
    {
        public List<string> enabledCategories { get; } = new() { "*All" };

#pragma warning disable S1144
        public void ResetFilters()
#pragma warning restore S1144
        {
            enabledCategories.Clear();
            enabledCategories.Add("*All");
        }
    }

    private sealed class DummyInventoryController
    {
        public List<DummyInventoryLineData> LastItems { get; private set; } = new();

#pragma warning disable S1144
        public void BeforeShow(IEnumerable selections)
#pragma warning restore S1144
        {
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

        public DummyRefreshItem? go;

        public DummyInventoryLineData(DummyRefreshItem go)
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
#pragma warning restore CA1308, S1144, S3459, S3604, S4487

    private sealed class DummyRefreshOwner
    {
        public DummyRefreshOwner(DummyRefreshItem item)
        {
            Inventory.Objects.Add(item);
        }

        public DummyRefreshInventory Inventory { get; } = new();
    }

    private sealed class DummyRefreshInventory
    {
        public ArrayList Objects { get; } = new();
    }

    private sealed class DummyRefreshItem
    {
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

    private sealed class DummyMenuControllerTarget
    {
        public int ClickCount { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PlayClick()
        {
            ClickCount++;
        }
    }

    private sealed class HarmonyScope : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string harmonyId;

        internal HarmonyScope(Harmony harmony, string harmonyId)
        {
            this.harmony = harmony;
            this.harmonyId = harmonyId;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
