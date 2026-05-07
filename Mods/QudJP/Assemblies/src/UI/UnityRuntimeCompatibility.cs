#if HAS_TMP
using System;
using System.Reflection;
using UnityEngine;
#endif

namespace QudJP;

internal static class UnityRuntimeCompatibility
{
#if HAS_TMP
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    /// <summary>
    /// Convert a Vector2 to a Vector3 with a z component of 0.
    /// </summary>
    /// <param name="value">The source 2D vector.</param>
    /// <returns>A Vector3 whose x and y match <paramref name="value"/> and whose z is 0.</returns>
    internal static Vector3 ToVector3(Vector2 value)
    {
        return new Vector3(value.x, value.y, 0f);
    }

    /// <summary>
    /// Extracts the alpha component from a UnityEngine.Color-like value using reflection.
    /// </summary>
    /// <param name="color">The color value to inspect.</param>
    /// <returns>`float` containing the alpha component if it can be obtained; `null` if the alpha field/property is not found or an error occurs.</returns>
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

    /// <summary>
    /// Obtains the alpha component of a material's `_FaceColor`, if available.
    /// </summary>
    /// <param name="material">The material to inspect; may be null.</param>
    /// <returns>The alpha component of the material's `_FaceColor` if present and retrievable; otherwise `null` (including when the material is null, lacks `_FaceColor`, or an error occurs).</returns>
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
#endif
}
