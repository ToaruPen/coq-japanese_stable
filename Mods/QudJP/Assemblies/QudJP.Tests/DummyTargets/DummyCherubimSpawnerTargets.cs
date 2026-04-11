namespace QudJP.Tests.DummyTargets
{
    internal sealed class DummyCherubimDescriptionPart
    {
        public string _Short = string.Empty;
    }
}

namespace QudJP.Tests.DummyTargets
{
    internal sealed class DummyCherubimRender
    {
        public string DisplayName { get; set; } = string.Empty;
    }

    internal class DummyCherubimGameObject
    {
        private readonly Dictionary<string, string> tags = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Category, string Name), string> xTags = new();
        private readonly DummyCherubimDescriptionPart description = new();

        public DummyCherubimRender Render { get; } = new();

        public DummyCherubimDescriptionPart DescriptionPart => description;

        public bool HasTag(string name)
        {
            return tags.ContainsKey(name);
        }

        public string GetTag(string name)
        {
            return tags[name];
        }

        public string GetxTag(string category, string name)
        {
            return xTags.TryGetValue((category, name), out var value) ? value : string.Empty;
        }

        public T GetPart<T>() where T : class
        {
            if (typeof(T) == typeof(DummyCherubimDescriptionPart))
            {
                return (T)(object)description;
            }

            throw new InvalidOperationException($"Unsupported part type: {typeof(T).FullName}");
        }

        public void SetTag(string name, string value)
        {
            tags[name] = value;
        }

        public void SetxTag(string category, string name, string value)
        {
            xTags[(category, name)] = value;
        }
    }

    internal sealed class DummyCherubimGameObjectWithNullSkin : DummyCherubimGameObject
    {
        public new string? GetxTag(string category, string name)
        {
            return null;
        }
    }

    internal static class DummyCherubimSpawnerTarget
    {
        public static void ReplaceDescription(DummyCherubimGameObject Object, string Description, string Features)
        {
            var creatureType = Object.HasTag("AlternateCreatureType")
                ? Object.GetTag("AlternateCreatureType")
                : Object.Render.DisplayName.Substring(0, Object.Render.DisplayName.IndexOf(' '));
            Object.GetPart<DummyCherubimDescriptionPart>()._Short = Description
                .Replace("*skin*", Object.GetxTag("TextFragments", "Skin"))
                .Replace("*creatureType*", creatureType)
                .Replace("*features*", Features);
        }
    }
}
