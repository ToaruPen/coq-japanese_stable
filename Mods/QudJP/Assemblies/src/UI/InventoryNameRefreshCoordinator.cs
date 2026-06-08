using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace QudJP;

internal static class InventoryNameRefreshCoordinator
{
    private static int dirty;

    internal static void MarkInventoryNameStateChanged(object? item)
    {
        if (item is null)
        {
            return;
        }

        TryResetNameCache(item);
        Interlocked.Exchange(ref dirty, 1);
    }

    internal static bool ResetDirtyInventoryNameCachesBeforeRefresh(object? inventoryScreenInstance)
    {
        if (Volatile.Read(ref dirty) == 0)
        {
            return false;
        }

        var owner = ReflectionUtils.GetPropertyOrFieldValue(inventoryScreenInstance, "GO");
        if (owner is null)
        {
            return false;
        }

        ResetNameCachesForInventoryOwner(owner);
        Interlocked.Exchange(ref dirty, 0);
        return true;
    }

    internal static bool HasPendingRefresh()
    {
        return Volatile.Read(ref dirty) != 0;
    }

    internal static void ClearPendingRefresh()
    {
        Interlocked.Exchange(ref dirty, 0);
    }

    internal static void ClearForTests()
    {
        ClearPendingRefresh();
    }

    private static void ResetNameCachesForInventoryOwner(object owner)
    {
        var seen = new HashSet<object>(ReferenceIdentityComparer.Instance);

        foreach (var item in EnumerateInventoryObjects(owner))
        {
            TryResetNameCacheOnce(item, seen);
        }

        foreach (var item in EnumerateZeroArgMethod(owner, "GetEquippedObjectsReadonly"))
        {
            TryResetNameCacheOnce(item, seen);
        }

        foreach (var item in EnumerateZeroArgMethod(owner, "GetEquippedObjects"))
        {
            TryResetNameCacheOnce(item, seen);
        }
    }

    private static IEnumerable<object> EnumerateInventoryObjects(object owner)
    {
        var inventory = ReflectionUtils.GetPropertyOrFieldValue(owner, "Inventory");
        if (inventory is null)
        {
            yield break;
        }

        var objects = ReflectionUtils.GetPropertyOrFieldValue(inventory, "Objects") as IEnumerable;
        if (objects is not null)
        {
            foreach (var item in EnumerateObjects(objects))
            {
                yield return item;
            }

            yield break;
        }

        foreach (var item in EnumerateZeroArgMethod(inventory, "GetObjectsDirect"))
        {
            yield return item;
        }
    }

    private static IEnumerable<object> EnumerateZeroArgMethod(object instance, string methodName)
    {
        var method = FindZeroArgMethod(instance.GetType(), methodName);
        if (method?.Invoke(instance, null) is not IEnumerable values)
        {
            yield break;
        }

        foreach (var item in EnumerateObjects(values))
        {
            yield return item;
        }
    }

    private static IEnumerable<object> EnumerateObjects(IEnumerable values)
    {
        return values.Cast<object>().Where(static value => value is not null);
    }

    private static MethodInfo? FindZeroArgMethod(Type type, string methodName)
    {
#pragma warning disable S3011
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
#pragma warning restore S3011

        for (var candidateType = type; candidateType is not null; candidateType = candidateType.BaseType)
        {
#pragma warning disable S3011
            var method = candidateType.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
#pragma warning restore S3011
            if (method is not null)
            {
                return method;
            }
        }

        return null;
    }

    private static void TryResetNameCacheOnce(object item, HashSet<object> seen)
    {
        if (seen.Add(item))
        {
            TryResetNameCache(item);
        }
    }

    private static void TryResetNameCache(object? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            FindZeroArgMethod(item.GetType(), "ResetNameCache")?.Invoke(item, null);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "QudJP: InventoryNameRefreshCoordinator.ResetNameCache failed for {0}: {1}",
                item.GetType().FullName,
                ex);
        }
    }

    private sealed class ReferenceIdentityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceIdentityComparer Instance = new();

        private ReferenceIdentityComparer()
        {
        }

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
