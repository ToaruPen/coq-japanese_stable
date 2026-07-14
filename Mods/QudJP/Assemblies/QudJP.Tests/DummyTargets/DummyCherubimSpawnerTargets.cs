namespace QudJP.Tests.DummyTargets
{
    internal sealed class DummyCherubimDescriptionPart
    {
        public string Short { get; set; } = string.Empty;

        public string _Short = string.Empty;
    }

    internal sealed class RulesDescription
    {
        public string Text { get; set; } = string.Empty;
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

        public List<object> PartsList { get; } = new();

        public int ResetNameCacheCallCount { get; private set; }

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

            if (string.Equals(typeof(T).FullName, "XRL.World.Parts.Description", StringComparison.Ordinal))
            {
                return null!;
            }

            throw new InvalidOperationException($"Unsupported part type: {typeof(T).FullName}");
        }

        public T AddPart<T>() where T : class, new()
        {
            var part = new T();
            PartsList.Add(part);
            return part;
        }

        public void ResetNameCache()
        {
            ResetNameCacheCallCount++;
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

    internal sealed class DummyCherubimGameObjectWithThrowingDescriptionLookup : DummyCherubimGameObject
    {
        public DummyCherubimDescriptionPart CapturedDescriptionPart => base.DescriptionPart;

        public new DummyCherubimDescriptionPart DescriptionPart =>
            throw new InvalidOperationException("Injected description lookup failure.");

        public new T GetPart<T>() where T : class
        {
            throw new InvalidOperationException("Injected description lookup failure.");
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

        public static void BestowElement(DummyCherubimGameObject Object, string Element, bool PrependName = true)
        {
            switch (Element)
            {
                case "glass":
                    if (PrependName)
                    {
                        Object.Render.DisplayName = "glass " + Object.Render.DisplayName;
                    }

                    Object.AddPart<RulesDescription>().Text = "\nThis creature belongs to the caste of glass cherubim.\n• Attacks have a 10% chance to dismember.\n• Reflects 25% damage back at attackers.";
                    break;
                case "time":
                    if (PrependName)
                    {
                        Object.Render.DisplayName = "time " + Object.Render.DisplayName;
                    }

                    Object.AddPart<RulesDescription>().Text = "\nThis creature belongs to the caste of time cherubim.\n• Temporal Fugue 10";
                    break;
                case "chance":
                    if (PrependName)
                    {
                        Object.Render.DisplayName = "chaotic " + Object.Render.DisplayName;
                    }

                    Object.AddPart<RulesDescription>().Text = "\nThis creature belongs to the caste of chaotic cherubim.\n• Whenever this creature is about to take damage, there's a 25% chance they blink away instead.\n• Whenever this creature attacks, 50% of the time the Fates have their way.";
                    break;
                default:
                    Object.AddPart<RulesDescription>().Text = "unknown";
                    break;
            }
        }
    }

    internal sealed class DummyBeforeObjectCreatedEvent
    {
        public DummyCherubimGameObject? ReplacementObject { get; set; }
    }
}
