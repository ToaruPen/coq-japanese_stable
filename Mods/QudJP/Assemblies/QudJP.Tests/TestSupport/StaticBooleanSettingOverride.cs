using System.Reflection;
using HarmonyLib;

namespace QudJP.Tests;

internal sealed class StaticBooleanSettingOverride : IDisposable
{
    private readonly FieldInfo? field;
    private readonly PropertyInfo? property;
    private readonly bool originalValue;
    private bool disposed;

    private StaticBooleanSettingOverride(FieldInfo field, bool value)
    {
        this.field = field;
        originalValue = (bool)(field.GetValue(null)
            ?? throw new InvalidOperationException($"Static field returned null: {field.DeclaringType?.FullName}.{field.Name}"));
        field.SetValue(null, value);
    }

    private StaticBooleanSettingOverride(PropertyInfo property, bool value)
    {
        this.property = property;
        originalValue = (bool)(property.GetValue(null)
            ?? throw new InvalidOperationException($"Static property returned null: {property.DeclaringType?.FullName}.{property.Name}"));
        property.SetValue(null, value);
    }

    internal static StaticBooleanSettingOverride ForResolvedType(
        string qualifiedTypeName,
        string fallbackTypeName,
        string memberName,
        bool value)
    {
        var type = AccessTools.TypeByName(qualifiedTypeName)
            ?? AccessTools.TypeByName(fallbackTypeName)
            ?? throw new TypeLoadException(
                $"Static setting type not found: {qualifiedTypeName} (fallback: {fallbackTypeName})");

        var property = AccessTools.Property(type, memberName);
        if (property is not null)
        {
            if (property.PropertyType != typeof(bool)
                || !property.CanRead
                || !property.CanWrite
                || property.GetMethod?.IsStatic != true
                || property.SetMethod?.IsStatic != true)
            {
                throw new InvalidOperationException(
                    $"Static setting property must be a readable, writable, and static Boolean: {type.FullName}.{memberName}");
            }

            return new StaticBooleanSettingOverride(property, value);
        }

        var field = AccessTools.Field(type, memberName)
            ?? throw new MissingFieldException(type.FullName, memberName);
        if (field.FieldType != typeof(bool) || !field.IsStatic)
        {
            throw new InvalidOperationException(
                $"Static setting field must be a Boolean: {type.FullName}.{memberName}");
        }

        return new StaticBooleanSettingOverride(field, value);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (property is not null)
        {
            property.SetValue(null, originalValue);
        }
        else
        {
            field?.SetValue(null, originalValue);
        }
    }
}
