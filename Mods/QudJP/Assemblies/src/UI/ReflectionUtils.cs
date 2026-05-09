using System.Reflection;

namespace QudJP;

internal static class ReflectionUtils
{
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

        for (var type = instance.GetType(); type is not null; type = type.BaseType)
        {
#pragma warning disable S3011
            var property = type.GetProperty(memberName, InstanceMemberFlags);
#pragma warning restore S3011
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
#pragma warning disable S3011
                return property.GetValue(instance, null);
#pragma warning restore S3011
            }

#pragma warning disable S3011
            var field = type.GetField(memberName, InstanceMemberFlags);
#pragma warning restore S3011
            if (field is not null)
            {
                return field.GetValue(instance);
            }
        }

        return null;
    }
}
