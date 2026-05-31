using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace QudJP;

internal static class ReflectionUtils
{
    private static readonly ConcurrentDictionary<MemberAccessorKey, Func<object, object?>> Accessors = new();

#pragma warning disable S3011
    private const BindingFlags InstanceMemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
#pragma warning restore S3011

    internal static object? GetPropertyOrFieldValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var key = new MemberAccessorKey(instance.GetType(), memberName);
        return Accessors.GetOrAdd(key, static candidate => BuildAccessor(candidate.Type, candidate.MemberName))(instance);
    }

    internal static void ClearAccessorCacheForTests()
    {
        Accessors.Clear();
    }

    internal static int GetAccessorCacheCountForTests()
    {
        return Accessors.Count;
    }

    private static Func<object, object?> BuildAccessor(Type instanceType, string memberName)
    {
        for (var type = instanceType; type is not null; type = type.BaseType)
        {
#pragma warning disable S3011
            var property = type.GetProperty(memberName, InstanceMemberFlags);
#pragma warning restore S3011
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                return target =>
                {
#pragma warning disable S3011
                    return property.GetValue(target, null);
#pragma warning restore S3011
                };
            }

#pragma warning disable S3011
            var field = type.GetField(memberName, InstanceMemberFlags);
#pragma warning restore S3011
            if (field is not null)
            {
                return target => field.GetValue(target);
            }
        }

        return static _ => null;
    }

    private readonly struct MemberAccessorKey : IEquatable<MemberAccessorKey>
    {
        internal MemberAccessorKey(Type type, string memberName)
        {
            Type = type;
            MemberName = memberName;
        }

        internal Type Type { get; }

        internal string MemberName { get; }

        public bool Equals(MemberAccessorKey other)
        {
            return Type == other.Type
                && string.Equals(MemberName, other.MemberName, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is MemberAccessorKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Type.GetHashCode() * 397)
                    ^ StringComparer.Ordinal.GetHashCode(MemberName);
            }
        }
    }
}
