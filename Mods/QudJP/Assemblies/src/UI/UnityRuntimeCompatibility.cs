#if HAS_TMP
using System.Reflection;
using UnityEngine;
#endif

namespace QudJP;

internal static class UnityRuntimeCompatibility
{
#if HAS_TMP
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    internal static Vector3 ToVector3(Vector2 value)
    {
        return new Vector3(value.x, value.y, 0f);
    }

    internal static float? TryGetColorAlpha(Color color)
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

    internal static float? TryGetFaceColorAlpha(Material? material)
    {
        if (material is null || !material.HasProperty("_FaceColor"))
        {
            return null;
        }

        return TryGetColorAlpha(material.GetColor("_FaceColor"));
    }
#endif
}
