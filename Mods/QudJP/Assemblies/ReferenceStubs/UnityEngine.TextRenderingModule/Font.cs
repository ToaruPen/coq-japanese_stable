namespace UnityEngine;

public class Font : Object
{
    public static Font CreateDynamicFontFromOSFont(string fontname, int size) => new Font { name = fontname };
    public static Font CreateDynamicFontFromOSFont(string[] fontnames, int size) =>
        new Font { name = fontnames.Length == 0 ? string.Empty : fontnames[0] };
}

public class TextMesh : Component
{
    public Font? font { get; set; }

    public string text { get; set; } = string.Empty;
}
