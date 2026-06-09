using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP;

internal static class InventoryLineRefreshCoordinator
{
    private static bool pendingInventoryLineRefresh;
    private static bool pendingInventoryLineRefreshRequiresResort;
    private static object? pendingChangedItem;
    private static bool needsInventoryLineResortAfterFullRefresh;
    private static bool expectedPostFullRefreshSort;
    private static bool refreshingBeforeActionMenuReopen;
    private static DisplaySnapshot lastActionMenuItemSnapshot;
    private static readonly Regex LoadedStateSuffixPattern = new(
        @"\s+\[[^\[\]]*\](?=\s*(?:<[^<>]*>)?\s*$)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal readonly struct DisplaySnapshot
    {
        internal DisplaySnapshot(
            object? item,
            string? displayName,
            string? inventoryCategory,
            string? stableDisplaySortKey,
            string? renderSignature,
            object[]? ownerInventoryObjects)
        {
            Item = item;
            DisplayName = displayName;
            InventoryCategory = inventoryCategory;
            StableDisplaySortKey = stableDisplaySortKey;
            RenderSignature = renderSignature;
            OwnerInventoryObjects = ownerInventoryObjects;
        }

        internal object? Item { get; }

        internal string? DisplayName { get; }

        internal string? InventoryCategory { get; }

        internal string? StableDisplaySortKey { get; }

        internal string? RenderSignature { get; }

        internal object[]? OwnerInventoryObjects { get; }
    }

    internal static DisplaySnapshot CaptureDisplaySnapshot(object? item)
    {
        return CaptureDisplaySnapshot(item, owner: null);
    }

    internal static DisplaySnapshot CaptureDisplaySnapshot(object? item, object? owner)
    {
        if (item is null)
        {
            return default;
        }

        var displayName = ReflectionUtils.GetPropertyOrFieldValue(item, "DisplayName") as string;
        return new DisplaySnapshot(
            item,
            displayName,
            InvokeStringMethod(item, "GetInventoryCategory"),
            BuildStableDisplaySortKey(displayName),
            CaptureRenderSignature(item),
            CaptureInventoryObjects(owner));
    }

    internal static bool RefreshAfterInventoryActionIfChanged(
        object? item,
        object? owner,
        DisplaySnapshot before)
    {
        if (item is null || !ReferenceEquals(before.Item, item))
        {
            return false;
        }

        var after = CaptureDisplaySnapshot(item);
        if (HasOwnerInventoryMembershipChanged(before.OwnerInventoryObjects, owner))
        {
            MarkPendingRefresh(item, "owner-inventory-membership-changed", requiresResort: false);
            return true;
        }

        var categoryChanged = HasCategoryChanged(before, after);
        var stableSortKeyChanged = HasStableDisplaySortKeyChanged(before, after);
        if (HasDisplayChanged(before, after, categoryChanged))
        {
            MarkPendingRefresh(
                item,
                "display-or-category-changed",
                requiresResort: categoryChanged || stableSortKeyChanged);
            return true;
        }

        LogRefreshDecision("unchanged", item, before, after, categoryChanged);
        return false;
    }

    internal static bool MarkActiveInventoryLinesRefreshPendingForChangedItem(
        object? changedItem,
        bool requiresResort = true)
    {
        if (changedItem is null)
        {
            return false;
        }

        MarkPendingRefresh(changedItem, "explicit", requiresResort);
        return true;
    }

    internal static bool MarkActiveInventoryLinesRefreshPendingForChangedItemForTests(
        object? changedItem,
        bool requiresResort = true)
    {
        return MarkActiveInventoryLinesRefreshPendingForChangedItem(changedItem, requiresResort);
    }

    internal static bool ConsumePendingInventoryLineRefreshForUpdateView()
    {
        var changedItem = pendingChangedItem;
        if (!pendingInventoryLineRefresh)
        {
            return false;
        }

        pendingInventoryLineRefresh = false;
        var requiresResort = pendingInventoryLineRefreshRequiresResort;
        pendingInventoryLineRefreshRequiresResort = false;
        pendingChangedItem = null;
        needsInventoryLineResortAfterFullRefresh = requiresResort;
        expectedPostFullRefreshSort = requiresResort;
        InventoryActionMenuCloseTimingObservability.ClearInventoryLineRefreshPendingAfterAction();

        LogPendingRefresh("consume-full", changedItem, "allow-original-update");
        return true;
    }

    internal static bool TryResortInventoryLinesAfterFullRefresh(object? inventoryScreen)
    {
        return TryResortInventoryLines(inventoryScreen);
    }

    internal static bool TryResortInventoryLinesAfterFullRefreshForTests(object? inventoryScreen)
    {
        return TryResortInventoryLinesAfterFullRefresh(inventoryScreen);
    }

    internal static bool TryRefreshPendingInventoryLinesBeforeActionMenuReopen()
    {
        return TryRefreshPendingInventoryLinesBeforeActionMenuReopen(FindActiveInventoryScreen());
    }

    internal static bool TryRefreshChangedInventoryLinesBeforeActionMenuOpen(object? item, object? owner)
    {
        return TryRefreshChangedInventoryLinesBeforeActionMenuOpen(FindActiveInventoryScreen(), item, owner);
    }

    internal static bool TryRefreshChangedInventoryLinesBeforeActionMenuOpenForTests(
        object? inventoryScreen,
        object? item,
        object? owner)
    {
        return TryRefreshChangedInventoryLinesBeforeActionMenuOpen(inventoryScreen, item, owner);
    }

    internal static bool TryRefreshPendingInventoryLinesBeforeActionMenuReopenForTests(object? inventoryScreen)
    {
        return TryRefreshPendingInventoryLinesBeforeActionMenuReopen(inventoryScreen);
    }

    internal static bool ResetInventoryFiltersBeforeFullRefresh(object? inventoryScreen)
    {
        if (inventoryScreen is null)
        {
            return false;
        }

        try
        {
            var filterBar = ReflectionUtils.GetPropertyOrFieldValue(inventoryScreen, "filterBar");
            if (filterBar is null)
            {
                LogPreFullRefreshFilterReset("missing-filter-bar", inventoryScreen);
                return false;
            }

            var resetFilters = AccessTools.Method(filterBar.GetType(), "ResetFilters", Type.EmptyTypes);
            if (resetFilters is null)
            {
                LogPreFullRefreshFilterReset("missing-reset-filters", inventoryScreen);
                return false;
            }

            _ = resetFilters.Invoke(filterBar, null);
            LogPreFullRefreshFilterReset("reset-filters", inventoryScreen);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("QudJP: InventoryLineRefreshCoordinator pre-full-refresh filter reset failed: {0}", ex);
            return false;
        }
    }

    internal static void ClearForTests()
    {
        pendingInventoryLineRefresh = false;
        pendingInventoryLineRefreshRequiresResort = false;
        pendingChangedItem = null;
        needsInventoryLineResortAfterFullRefresh = false;
        expectedPostFullRefreshSort = false;
        refreshingBeforeActionMenuReopen = false;
        lastActionMenuItemSnapshot = default;
    }

    private static bool HasCategoryChanged(DisplaySnapshot before, DisplaySnapshot after)
    {
        return !string.Equals(before.InventoryCategory, after.InventoryCategory, StringComparison.Ordinal);
    }

    private static bool HasStableDisplaySortKeyChanged(DisplaySnapshot before, DisplaySnapshot after)
    {
        return !string.Equals(before.StableDisplaySortKey, after.StableDisplaySortKey, StringComparison.Ordinal);
    }

    private static bool HasDisplayChanged(DisplaySnapshot before, DisplaySnapshot after, bool categoryChanged)
    {
        return categoryChanged
            || !string.Equals(before.DisplayName, after.DisplayName, StringComparison.Ordinal)
            || !string.Equals(before.RenderSignature, after.RenderSignature, StringComparison.Ordinal);
    }

    private static void LogRefreshDecision(
        string phase,
        object? item,
        DisplaySnapshot before,
        DisplaySnapshot after,
        bool categoryChanged)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] InventoryLineRefresh/v1: phase=" + phase
            + ";item=" + DescribeObject(item)
            + ";category_changed=" + categoryChanged
            + ";before_name=" + Escape(before.DisplayName)
            + ";after_name=" + Escape(after.DisplayName)
            + ";before_category=" + Escape(before.InventoryCategory)
            + ";after_category=" + Escape(after.InventoryCategory));
    }

    private static void MarkPendingRefresh(object? item, string reason, bool requiresResort = true)
    {
        InventoryActionMenuCloseTimingObservability.MarkInventoryLineRefreshPendingAfterAction();
        pendingInventoryLineRefresh = true;
        pendingInventoryLineRefreshRequiresResort |= requiresResort;
        pendingChangedItem = item;
        LogPendingRefresh("mark", item, reason, requiresResort);
    }

    private static string? BuildStableDisplaySortKey(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return displayName;
        }

        var translatedDisplayName = InventoryLineTranslationPatch.TranslateItemDisplayNameForSortKey(displayName!);
        var visible = ColorAwareTranslationComposer.GetVisibleText(translatedDisplayName);
        return LoadedStateSuffixPattern.Replace(visible, string.Empty);
    }

    private static void LogPendingRefresh(
        string phase,
        object? item,
        string reason,
        bool? requiresResort = null)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] InventoryLineRefresh/v1: phase=" + phase
            + ";kind=Full"
            + ";reason=" + reason
            + (requiresResort.HasValue ? ";requires_resort=" + requiresResort.Value : string.Empty)
            + ";item=" + DescribeObject(item));
    }

    private static void LogPreFullRefreshFilterReset(string phase, object inventoryScreen)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] InventoryLineRefresh/v1: phase=" + phase
            + ";reason=pre-full-refresh"
            + ";screen=" + DescribeType(inventoryScreen));
    }

    private static void LogPostFullRefreshSortAttempt(string phase, object? inventoryScreen)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
            "[QudJP] InventoryLineRefresh/v1: phase=" + phase
            + ";reason=post-full-refresh"
            + ";screen=" + (inventoryScreen is null ? "<null>" : DescribeType(inventoryScreen)));
    }

    private static void LogPostFullRefreshSortResult(
        string phase,
        object inventoryScreen,
        IList? listItems,
        string? sortMode)
    {
        RuntimeDiagnostics.LogVerboseProbe(() =>
        {
            return "[QudJP] InventoryLineRefresh/v1: phase=" + phase
                + ";sort_mode=" + Escape(sortMode)
                + ";line_count=" + (listItems?.Count ?? -1).ToString(CultureInfo.InvariantCulture)
                + ";screen=" + DescribeType(inventoryScreen);
        });
    }

    private static bool TryRefreshPendingInventoryLinesBeforeActionMenuReopen(object? inventoryScreen)
    {
        if (inventoryScreen is null || refreshingBeforeActionMenuReopen)
        {
            return false;
        }

        if (!pendingInventoryLineRefresh && !InventoryNameRefreshCoordinator.HasPendingRefresh())
        {
            return false;
        }

        refreshingBeforeActionMenuReopen = true;
        try
        {
            var resetNameCaches = InventoryNameRefreshCoordinator.ResetDirtyInventoryNameCachesBeforeRefresh(inventoryScreen);
            var consumedInventoryLineRefresh = ConsumePendingInventoryLineRefreshForUpdateView();
            if (!resetNameCaches && !consumedInventoryLineRefresh)
            {
                return false;
            }

            if (consumedInventoryLineRefresh)
            {
                ResetInventoryFiltersBeforeFullRefresh(inventoryScreen);
            }

            var updateView = FindInstanceMethod(inventoryScreen.GetType(), "UpdateViewFromData");
            if (updateView is null)
            {
                LogPendingRefresh("missing-update-view", null, "action-menu-reopen");
                return false;
            }

            updateView.Invoke(inventoryScreen, null);
            _ = TryResortInventoryLinesAfterFullRefresh(inventoryScreen);
            LogPendingRefresh("action-menu-reopen-refresh", null, "before-next-action-menu");
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "QudJP: InventoryLineRefreshCoordinator action-menu-reopen refresh failed: {0}",
                ex);
            return false;
        }
        finally
        {
            refreshingBeforeActionMenuReopen = false;
        }
    }

    private static bool TryRefreshChangedInventoryLinesBeforeActionMenuOpen(
        object? inventoryScreen,
        object? item,
        object? owner)
    {
        MarkActionMenuItemRefreshIfChanged(item, owner);
        var refreshed = TryRefreshPendingInventoryLinesBeforeActionMenuReopen(inventoryScreen);
        lastActionMenuItemSnapshot = item is null
            ? default
            : CaptureDisplaySnapshot(item, owner);
        return refreshed;
    }

    private static void MarkActionMenuItemRefreshIfChanged(object? item, object? owner)
    {
        if (item is null)
        {
            return;
        }

        var before = lastActionMenuItemSnapshot;
        if (!ReferenceEquals(before.Item, item))
        {
            return;
        }

        _ = RefreshAfterInventoryActionIfChanged(item, owner, before);
    }

    private static object? FindActiveInventoryScreen()
    {
        try
        {
            var inventoryScreenType = GameTypeResolver.FindType(
                "Qud.UI.InventoryAndEquipmentStatusScreen",
                "InventoryAndEquipmentStatusScreen");
            var singletonBaseType = AccessTools.TypeByName("Qud.UI.SingletonWindowBase`1");
            if (inventoryScreenType is null || singletonBaseType is null)
            {
                return null;
            }

            var singletonType = singletonBaseType.MakeGenericType(inventoryScreenType);
            return AccessTools.Field(singletonType, "instance")?.GetValue(null);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "QudJP: InventoryLineRefreshCoordinator active inventory screen lookup failed: {0}",
                ex);
            return null;
        }
    }

    private static string DescribeType(object value)
    {
        var type = value.GetType();
        var typeName = type.FullName;
        return string.IsNullOrEmpty(typeName) ? type.Name : typeName;
    }

    private static string DescribeObject(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        var blueprint = ReflectionUtils.GetPropertyOrFieldValue(value, "Blueprint") as string
            ?? ReflectionUtils.GetPropertyOrFieldValue(value, "BlueprintName") as string;
        return (blueprint ?? value.GetType().Name) + "#"
            + RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture);
    }

    private static string Escape(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace(";", "\\;");
    }

    private static bool HasOwnerInventoryMembershipChanged(object[]? beforeObjects, object? owner)
    {
        if (beforeObjects is null)
        {
            return false;
        }

        var afterObjects = CaptureInventoryObjects(owner);
        if (afterObjects is null)
        {
            return false;
        }

        if (beforeObjects.Length != afterObjects.Length)
        {
            return true;
        }

        for (var i = 0; i < beforeObjects.Length; i++)
        {
            if (!ReferenceEquals(beforeObjects[i], afterObjects[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static object[]? CaptureInventoryObjects(object? owner)
    {
        var inventory = ReflectionUtils.GetPropertyOrFieldValue(owner, "Inventory");
        var objects = ReflectionUtils.GetPropertyOrFieldValue(inventory, "Objects") as IEnumerable;
        if (objects is null)
        {
            return null;
        }

        return objects.Cast<object?>()
            .Where(static item => item is not null)
            .Cast<object>()
            .ToArray();
    }

    private static bool TryResortInventoryLines(object? inventoryScreen)
    {
        if (!needsInventoryLineResortAfterFullRefresh)
        {
            if (expectedPostFullRefreshSort)
            {
                expectedPostFullRefreshSort = false;
                LogPostFullRefreshSortAttempt("post-sort-skipped-no-pending", inventoryScreen);
            }

            return false;
        }

        expectedPostFullRefreshSort = false;
        LogPostFullRefreshSortAttempt("post-sort-enter", inventoryScreen);
        if (inventoryScreen is null)
        {
            LogPendingRefresh("missing-screen-instance", null, "post-full-refresh");
            return false;
        }

        needsInventoryLineResortAfterFullRefresh = false;
        try
        {
            var listItems = ReflectionUtils.GetPropertyOrFieldValue(inventoryScreen, "listItems") as IList;
            var objectCategories = ReflectionUtils.GetPropertyOrFieldValue(inventoryScreen, "objectCategories") as IDictionary;
            var inventoryController = ReflectionUtils.GetPropertyOrFieldValue(inventoryScreen, "inventoryController");
            if (listItems is null || objectCategories is null || inventoryController is null)
            {
                LogPostFullRefreshSortResult("missing-screen-state", inventoryScreen, listItems, null);
                return false;
            }

            ResetLineDataCaches(listItems);
            ResetLineDataCaches(objectCategories);
            if (!TryRebuildObjectCategories(listItems, objectCategories))
            {
                LogPostFullRefreshSortResult("rebuild-categories-failed", inventoryScreen, listItems, null);
                return false;
            }

            var sortMode = ReflectionUtils.GetPropertyOrFieldValue(inventoryScreen, "sortMode")?.ToString();
            var sorted = string.Equals(sortMode, "AZ", StringComparison.Ordinal)
                ? TrySortAzList(listItems)
                : TrySortCategoryList(inventoryScreen, listItems, objectCategories);
            if (!sorted)
            {
                LogPostFullRefreshSortResult("sort-failed", inventoryScreen, listItems, sortMode);
                return false;
            }

            var shown = TryInvokeBeforeShow(inventoryController, listItems);
            LogPostFullRefreshSortResult(shown ? "success" : "before-show-failed", inventoryScreen, listItems, sortMode);
            return shown;
        }
        catch (Exception ex)
        {
            RuntimeDiagnostics.LogWarning(
                $"[QudJP] Warning: InventoryLineRefreshCoordinator post-full-refresh sort failed: {ex.GetType().Name}: {ex.Message}");
            Trace.TraceWarning("QudJP: InventoryLineRefreshCoordinator post-full-refresh sort failed: {0}", ex);
            return false;
        }
    }

    private static bool TryRebuildObjectCategories(IList listItems, IDictionary objectCategories)
    {
        var itemLines = CollectItemLines(listItems, objectCategories);
        if (itemLines.Count == 0)
        {
            return true;
        }

        var reusableLists = new Dictionary<string, IList>(StringComparer.Ordinal);
        IList? sampleList = null;
        foreach (DictionaryEntry entry in objectCategories)
        {
            if (entry.Key is string categoryName && entry.Value is IList categoryLines)
            {
                reusableLists[categoryName] = categoryLines;
                sampleList ??= categoryLines;
                categoryLines.Clear();
            }
        }

        objectCategories.Clear();
        foreach (var line in itemLines)
        {
            var categoryName = GetCurrentInventoryCategory(line);
            if (string.IsNullOrEmpty(categoryName))
            {
                return false;
            }

            var resolvedCategoryName = categoryName!;
            SetPropertyOrFieldValue(line, "categoryName", resolvedCategoryName);
            if (!reusableLists.TryGetValue(resolvedCategoryName, out var categoryLines))
            {
                categoryLines = CreateLineList(line.GetType(), sampleList);
                reusableLists[resolvedCategoryName] = categoryLines;
            }

            categoryLines.Add(line);
        }

        foreach (var pair in reusableLists.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value.Count > 0)
            {
                objectCategories[pair.Key] = pair.Value;
            }
        }

        return true;
    }

    private static List<object> CollectItemLines(IList listItems, IDictionary objectCategories)
    {
        var lines = new List<object>();
        var seen = new HashSet<object>();

        AddItemLines(listItems, lines, seen);
        foreach (var value in objectCategories.Values)
        {
            if (value is IList categoryLines)
            {
                AddItemLines(categoryLines, lines, seen);
            }
        }

        return lines;
    }

    private static void AddItemLines(IList source, List<object> lines, HashSet<object> seen)
    {
        foreach (var line in source)
        {
            if (line is not null && !IsCategoryLine(line) && seen.Add(line))
            {
                lines.Add(line);
            }
        }
    }

    private static IList CreateLineList(Type lineType, IList? sampleList)
    {
        if (sampleList is not null)
        {
            var sampleType = sampleList.GetType();
            if (sampleType.IsGenericType && sampleType.GetGenericArguments().Length == 1)
            {
                var sampleInstance = Activator.CreateInstance(sampleType);
                if (sampleInstance is IList sampleCategoryList)
                {
                    return sampleCategoryList;
                }

                Trace.TraceError("QudJP: could not create inventory category list of type {0}.", sampleType.FullName);
                throw new InvalidOperationException("Could not create category list.");
            }
        }

        var listType = typeof(List<>).MakeGenericType(lineType);
        var instance = Activator.CreateInstance(listType);
        if (instance is IList categoryList)
        {
            return categoryList;
        }

        Trace.TraceError("QudJP: could not create inventory category list of type {0}.", listType.FullName);
        throw new InvalidOperationException("Could not create category list.");
    }

    private static string? GetCurrentInventoryCategory(object line)
    {
        var go = ReflectionUtils.GetPropertyOrFieldValue(line, "go");
        return go is null
            ? ReflectionUtils.GetPropertyOrFieldValue(line, "categoryName") as string
            : InvokeStringMethod(go, "GetInventoryCategory");
    }

    private static void ResetLineDataCaches(IList listItems)
    {
        foreach (var line in listItems)
        {
            ResetLineDataCache(line);
        }
    }

    private static void ResetLineDataCaches(IDictionary objectCategories)
    {
        foreach (var value in objectCategories.Values)
        {
            if (value is IList lines)
            {
                ResetLineDataCaches(lines);
            }
        }
    }

    private static void ResetLineDataCache(object? line)
    {
        if (line is null || IsCategoryLine(line))
        {
            return;
        }

        SetPropertyOrFieldValue(line, "displayName", null);
        SetPropertyOrFieldValue(line, "sortString", null);
    }

    private static bool TrySortAzList(IList listItems)
    {
        var itemLines = new List<object>();
        foreach (var line in listItems)
        {
            if (!IsCategoryLine(line))
            {
                itemLines.Add(line);
            }
        }

        var sortKeys = BuildVisibleSortKeys(itemLines);
        itemLines.Sort((left, right) => CompareLineDisplayName(left, right, sortKeys));
        listItems.Clear();
        foreach (var line in itemLines)
        {
            listItems.Add(line);
        }

        return true;
    }

    private static bool TrySortCategoryList(object inventoryScreen, IList listItems, IDictionary objectCategories)
    {
        var headers = listItems.Cast<object>().Where(IsCategoryLine).ToList();
        if (headers.Count == 0)
        {
            return false;
        }

        var categories = GetNonEmptyCategoryNames(objectCategories);
        var headerByCategory = headers
            .Select(static header => new
            {
                Header = header,
                CategoryName = ReflectionUtils.GetPropertyOrFieldValue(header, "categoryName") as string,
            })
            .Where(static pair => !string.IsNullOrEmpty(pair.CategoryName))
            .ToDictionary(static pair => pair.CategoryName!, static pair => pair.Header, StringComparer.Ordinal);
        var reusableHeaders = new Queue<object>(headers.Where(header =>
        {
            var categoryName = ReflectionUtils.GetPropertyOrFieldValue(header, "categoryName") as string;
            return string.IsNullOrEmpty(categoryName) || !categories.Contains(categoryName, StringComparer.Ordinal);
        }));

        listItems.Clear();
        foreach (var categoryName in categories)
        {
            if (!headerByCategory.TryGetValue(categoryName, out var header))
            {
                if (reusableHeaders.Count == 0)
                {
                    return false;
                }

                header = reusableHeaders.Dequeue();
                SetPropertyOrFieldValue(header, "categoryName", categoryName);
            }

            if (objectCategories[categoryName] is not IList categoryLines)
            {
                continue;
            }

            SetPropertyOrFieldValue(header, "category", true);
            SetPropertyOrFieldValue(header, "categoryAmount", categoryLines.Count);
            SetPropertyOrFieldValue(header, "categoryWeight", GetCategoryWeight(categoryLines));
            var collapsed = IsCollapsed(inventoryScreen, categoryName);
            SetPropertyOrFieldValue(header, "categoryExpanded", !collapsed);
            SetPropertyOrFieldValue(header, "categoryOffset", 0);
            listItems.Add(header);

            if (collapsed)
            {
                continue;
            }

            SortLineList(categoryLines);
            var offset = 1;
            foreach (var line in categoryLines)
            {
                SetPropertyOrFieldValue(line, "categoryOffset", offset);
                offset++;
                listItems.Add(line);
            }
        }

        return true;
    }

    private static List<string> GetNonEmptyCategoryNames(IDictionary objectCategories)
    {
        var categories = new List<string>();
        foreach (DictionaryEntry entry in objectCategories)
        {
            if (entry.Key is string categoryName && entry.Value is IList { Count: > 0 })
            {
                categories.Add(categoryName);
            }
        }

        categories.Sort(StringComparer.Ordinal);
        return categories;
    }

    private static int GetCategoryWeight(IList categoryLines)
    {
        var weight = 0;
        foreach (var line in categoryLines)
        {
            var go = ReflectionUtils.GetPropertyOrFieldValue(line, "go");
            if (ReflectionUtils.GetPropertyOrFieldValue(go, "Weight") is int itemWeight)
            {
                weight += itemWeight;
            }
        }

        return weight;
    }

    private static void SortLineList(IList lines)
    {
        var sorted = new List<object>();
        foreach (var line in lines)
        {
            sorted.Add(line);
        }

        var sortKeys = BuildVisibleSortKeys(sorted);
        sorted.Sort((left, right) => CompareLineDisplayName(left, right, sortKeys));
        lines.Clear();
        foreach (var line in sorted)
        {
            lines.Add(line);
        }
    }

    private static Dictionary<object, string> BuildVisibleSortKeys(IEnumerable<object> lines)
    {
        var sortKeys = new Dictionary<object, string>(ReferenceObjectEqualityComparer.Instance);
        foreach (var line in lines)
        {
            if (!sortKeys.ContainsKey(line))
            {
                sortKeys[line] = GetVisibleSortKey(line);
            }
        }

        return sortKeys;
    }

    private static int CompareLineDisplayName(
        object? left,
        object? right,
        IReadOnlyDictionary<object, string> sortKeys)
    {
        return CultureInfo.CurrentCulture.CompareInfo.Compare(
            GetCachedVisibleSortKey(left, sortKeys),
            GetCachedVisibleSortKey(right, sortKeys),
            CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth);
    }

    private static string GetCachedVisibleSortKey(object? line, IReadOnlyDictionary<object, string> sortKeys)
    {
        return line is not null && sortKeys.TryGetValue(line, out var sortKey)
            ? sortKey
            : string.Empty;
    }

    private static string GetVisibleSortKey(object? line)
    {
        var displayName = ReflectionUtils.GetPropertyOrFieldValue(line, "displayName") as string;
        if (string.IsNullOrEmpty(displayName))
        {
            return string.Empty;
        }

        var translatedDisplayName = InventoryLineTranslationPatch.TranslateItemDisplayNameForSortKey(displayName!);
        return ColorAwareTranslationComposer.GetVisibleText(translatedDisplayName);
    }

    private static bool IsCategoryLine(object? line)
    {
        return ReflectionUtils.GetPropertyOrFieldValue(line, "category") is true;
    }

    private static bool IsCollapsed(object inventoryScreen, string categoryName)
    {
        var method = FindInstanceMethod(inventoryScreen.GetType(), "isCollapsed", typeof(string));
        return method?.Invoke(inventoryScreen, new object[] { categoryName }) is true;
    }

    private static bool TryInvokeBeforeShow(object inventoryController, IList listItems)
    {
        var selectionType = listItems.GetType();
        foreach (var method in GetInstanceMethods(inventoryController.GetType(), "BeforeShow"))
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 1
                && parameters[0].ParameterType.IsAssignableFrom(selectionType)
                && TryInvokeBeforeShowCandidate(method, inventoryController, new object[] { listItems }))
            {
                return true;
            }

            if (parameters.Length == 2
                && CanPassNull(parameters[0].ParameterType)
                && parameters[1].ParameterType.IsAssignableFrom(selectionType)
                && TryInvokeBeforeShowCandidate(method, inventoryController, new object?[] { null, listItems }))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInvokeBeforeShowCandidate(MethodInfo method, object inventoryController, object?[] parameters)
    {
        try
        {
            method.Invoke(inventoryController, parameters);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (TargetParameterCountException)
        {
            return false;
        }
    }

    private static bool CanPassNull(Type parameterType)
    {
        return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) is not null;
    }

    private static string? CaptureRenderSignature(object item)
    {
        var render = InvokeRenderForUi(item);
        if (render is null)
        {
            return null;
        }

        return string.Join(
            "\u001f",
            ReflectionUtils.GetPropertyOrFieldValue(render, "Tile") as string ?? string.Empty,
            ReflectionUtils.GetPropertyOrFieldValue(render, "RenderString") as string ?? string.Empty,
            ReflectionUtils.GetPropertyOrFieldValue(render, "ColorString") as string ?? string.Empty,
            ReflectionUtils.GetPropertyOrFieldValue(render, "BackgroundString") as string ?? string.Empty,
            ReflectionUtils.GetPropertyOrFieldValue(render, "DetailColor") as string ?? string.Empty,
            ReflectionUtils.GetPropertyOrFieldValue(render, "HFlip")?.ToString() ?? string.Empty,
            ReflectionUtils.GetPropertyOrFieldValue(render, "VFlip")?.ToString() ?? string.Empty);
    }

    private static object? InvokeRenderForUi(object item)
    {
        var method = FindAnyInstanceMethod(item.GetType(), "RenderForUI");
        if (method is null)
        {
            return null;
        }

        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return method.Invoke(item, null);
        }

        if (parameters.Length == 1)
        {
            return method.Invoke(item, new object?[] { "Inventory" });
        }

        return method.Invoke(item, new object?[] { "Inventory", false });
    }

    private static string? InvokeStringMethod(object instance, string methodName)
    {
        foreach (var method in GetInstanceMethods(instance.GetType(), methodName))
        {
            if (method.ReturnType != typeof(string))
            {
                continue;
            }

            var parameters = method.GetParameters();
            var args = BuildDefaultArguments(parameters);
            if (args is null)
            {
                continue;
            }

            return method.Invoke(instance, args) as string;
        }

        return null;
    }

    private static void SetPropertyOrFieldValue(object instance, string memberName, object? value)
    {
#pragma warning disable S3011
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
#pragma warning restore S3011

        for (var type = instance.GetType(); type is not null; type = type.BaseType)
        {
#pragma warning disable S3011
            var property = type.GetProperty(memberName, flags);
#pragma warning restore S3011
            if (property is not null && property.GetIndexParameters().Length == 0 && property.CanWrite)
            {
                property.SetValue(instance, value, null);
                return;
            }

#pragma warning disable S3011
            var field = type.GetField(memberName, flags);
#pragma warning restore S3011
            if (field is not null)
            {
                field.SetValue(instance, value);
                return;
            }
        }
    }

    private static MethodInfo? FindInstanceMethod(Type type, string methodName, params Type[] parameterTypes)
    {
#pragma warning disable S3011
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
#pragma warning restore S3011

        for (var candidateType = type; candidateType is not null; candidateType = candidateType.BaseType)
        {
            var method = AccessTools.Method(candidateType, methodName, parameterTypes);
            if (method is not null)
            {
                return method;
            }

#pragma warning disable S3011
            method = candidateType.GetMethod(methodName, flags, null, parameterTypes, null);
#pragma warning restore S3011
            if (method is not null)
            {
                return method;
            }
        }

        return null;
    }

    private static MethodInfo? FindAnyInstanceMethod(Type type, string methodName)
    {
#pragma warning disable S3011
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
#pragma warning restore S3011

        for (var candidateType = type; candidateType is not null; candidateType = candidateType.BaseType)
        {
            var method = AccessTools.Method(candidateType, methodName);
            if (method is not null)
            {
                return method;
            }

#pragma warning disable S3011
            method = candidateType.GetMethod(methodName, flags);
#pragma warning restore S3011
            if (method is not null)
            {
                return method;
            }
        }

        return null;
    }

    private static object?[]? BuildDefaultArguments(ParameterInfo[] parameters)
    {
        if (parameters.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (!parameter.HasDefaultValue)
            {
                return null;
            }

            args[i] = parameter.DefaultValue is DBNull
                ? GetDefaultValue(parameter.ParameterType)
                : parameter.DefaultValue;
        }

        return args;
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static IEnumerable<MethodInfo> GetInstanceMethods(Type type, string methodName)
    {
#pragma warning disable S3011
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
#pragma warning restore S3011

        for (var candidateType = type; candidateType is not null; candidateType = candidateType.BaseType)
        {
#pragma warning disable S3011
            foreach (var method in candidateType
                .GetMethods(flags)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal)))
#pragma warning restore S3011
            {
                yield return method;
            }
        }
    }

    private sealed class ReferenceObjectEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceObjectEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
