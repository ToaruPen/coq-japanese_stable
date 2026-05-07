#if HAS_TMP
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
#endif

namespace QudJP;

internal static class UnityRuntimeCompatibility
{
#if HAS_TMP
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    internal static Vector3 ToVector3(Vector2 vector)
    {
        var boxedVector = (object)vector;
        var vectorType = boxedVector.GetType();
        var x = TryGetFloatMemberOrDefault(boxedVector, vectorType, "x");
        var y = TryGetFloatMemberOrDefault(boxedVector, vectorType, "y");

        return new Vector3(x, y, 0f);
    }

    internal static float? TryGetColorAlpha(Color color)
    {
        try
        {
            var boxedColor = (object)color;
            var colorType = boxedColor.GetType();

            var alphaField = colorType.GetField("a", PublicInstance);
            if (alphaField?.FieldType == typeof(float) && alphaField.GetValue(boxedColor) is float fieldValue)
            {
                return fieldValue;
            }

            var alphaProperty = colorType.GetProperty("a", PublicInstance);
            if (alphaProperty?.PropertyType == typeof(float)
                && alphaProperty.GetIndexParameters().Length == 0
                && alphaProperty.GetValue(boxedColor, null) is float propertyValue)
            {
                return propertyValue;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static float? TryGetFaceColorAlpha(Material? material)
    {
        try
        {
            if (material is null || !material.HasProperty("_FaceColor"))
            {
                return null;
            }

            return TryGetColorAlpha(material.GetColor("_FaceColor"));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static float? TryGetFloatMember(object target, Type targetType, string memberName)
    {
        try
        {
            var field = targetType.GetField(memberName, PublicInstance);
            if (field?.FieldType == typeof(float) && field.GetValue(target) is float fieldValue)
            {
                return fieldValue;
            }

            var property = targetType.GetProperty(memberName, PublicInstance);
            if (property?.PropertyType == typeof(float)
                && property.GetIndexParameters().Length == 0
                && property.GetValue(target, null) is float propertyValue)
            {
                return propertyValue;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static float TryGetFloatMemberOrDefault(object target, Type targetType, string memberName)
    {
        var value = TryGetFloatMember(target, targetType, memberName);
        if (value.HasValue)
        {
            return value.Value;
        }

        Trace.TraceWarning(
            "QudJP: UnityRuntimeCompatibility could not read float member '{0}' on '{1}'. Falling back to 0.",
            memberName,
            targetType.FullName);
        return 0f;
    }
#endif
}
