using System.Collections;
using System.Collections.Generic;

namespace QudJP.Tests.DummyTargets
{
    internal sealed class DummyStatusContext
    {
        public object? data;
    }

    internal sealed class DummyGameStat
    {
        public int BaseValue { get; set; }
    }

    internal sealed class DummyStatusStatistic
    {
        public string Name { get; set; } = string.Empty;

        public string ShortDisplayName { get; set; } = string.Empty;

        public int Value { get; set; }

        public int BaseValue { get; set; }

        public int Modifier { get; set; }

        public string GetShortDisplayName()
        {
            return ShortDisplayName;
        }
    }

    internal sealed class DummyStatusGameObject
    {
        private readonly Dictionary<string, int> statValues = new Dictionary<string, int>
        {
            ["SP"] = 150,
            ["AP"] = 0,
            ["MP"] = 0,
            ["Hitpoints"] = 10,
            ["XP"] = 100,
            ["Level"] = 1,
        };

        public string DisplayName { get; set; } = "Salt Hopper";

        public string Genotype { get; set; } = "Mutated Human";

        public string Subtype { get; set; } = "Pilgrim";

        public int Level { get; set; } = 1;

        public int Weight { get; set; } = 123;

        public string GetGenotype()
        {
            return Genotype;
        }

        public string GetSubtype()
        {
            return Subtype;
        }

        public int GetStatValue(string stat)
        {
            return statValues.TryGetValue(stat, out var value) ? value : 0;
        }

        public int Stat(string stat)
        {
            return GetStatValue(stat);
        }

        public DummyGameStat GetStat(string stat)
        {
            return new DummyGameStat
            {
                BaseValue = stat == "Hitpoints" ? 10 : GetStatValue(stat),
            };
        }

        public DummyRenderable RenderForUI(string context)
        {
            LastRenderContext = context;
            return new DummyRenderable(context);
        }

        public string? LastRenderContext { get; private set; }
    }

    internal sealed class DummyUiThreeColorProperties
    {
        public object? LastRenderable { get; private set; }

        public void FromRenderable(object renderable)
        {
            LastRenderable = renderable;
        }
    }

    internal sealed class DummyRenderable
    {
        public DummyRenderable(string tile)
        {
            Tile = tile;
        }

        public DummyRenderable(DummyRenderable source)
        {
            Tile = source.Tile;
            TileColor = source.TileColor;
            DetailColor = source.DetailColor;
        }

        public string Tile { get; set; }

        public string? TileColor { get; set; }

        public char DetailColor { get; set; }
    }

    internal sealed class DummyToggleObject
    {
        public bool Active { get; private set; }

        public void SetActive(bool active)
        {
            Active = active;
        }
    }

    internal sealed class DummyBindingScroller
    {
        public List<object?> Items { get; private set; } = new List<object?>();

        public void BeforeShow(IEnumerable selections)
        {
            Items = new List<object?>();
            foreach (var selection in selections)
            {
                Items.Add(selection);
            }
        }
    }

    internal sealed class DummyCharacterAttributeLineTarget
    {
        public DummyStatusContext context = new DummyStatusContext();

        public DummyUITextSkin attributeText = new DummyUITextSkin();

        public DummyUITextSkin valueText = new DummyUITextSkin();

        public DummyUITextSkin modifierText = new DummyUITextSkin();

        public void setData(object data)
        {
            context.data = data;
            if (data is not DummyCharacterAttributeLineDataTarget lineData)
            {
                return;
            }

            attributeText.SetText(lineData.data is null ? lineData.stat ?? string.Empty : lineData.data.GetShortDisplayName());
        }
    }

    internal sealed class DummyCharacterAttributeLineDataTarget
    {
        public DummyStatusStatistic? data { get; set; }

        public DummyStatusGameObject? go { get; set; }

        public string? stat { get; set; }
    }

    internal sealed class DummyCharacterMutationRecord
    {
        public bool ShowLevel { get; set; } = true;

        public string DisplayName { get; set; } = "Force Wall";

        public int Level { get; set; } = 1;

        public int BaseLevel { get; set; } = 1;

        public int MutationCap { get; set; } = 10;

        public bool ShouldShowLevel()
        {
            return ShowLevel;
        }

        public string GetDisplayName()
        {
            return DisplayName;
        }

        public int GetMutationCap()
        {
            return MutationCap;
        }

        public int GetUIDisplayLevel()
        {
            return Level;
        }
    }

    internal sealed class DummyCharacterMutationLineTarget
    {
        public DummyStatusContext context = new DummyStatusContext();

        public DummyUITextSkin text = new DummyUITextSkin();

        public void setData(object data)
        {
            context.data = data;
            if (data is not DummyCharacterMutationLineDataTarget lineData || lineData.mutation is null)
            {
                return;
            }

            text.SetText("{{y|" + lineData.mutation.GetDisplayName() + " ({{C|" + lineData.mutation.GetUIDisplayLevel() + "}})}}");
        }
    }

    internal sealed class DummyCharacterMutationLineDataTarget
    {
        public DummyCharacterMutationRecord? mutation { get; set; }
    }

    internal sealed class DummyStatusEffect
    {
        public string DisplayName { get; set; } = "Beguiled";
    }

    internal sealed class DummyCharacterEffectLineTarget
    {
        public DummyStatusContext context = new DummyStatusContext();

        public DummyUITextSkin text = new DummyUITextSkin();

        public void setData(object data)
        {
            context.data = data;
            if (data is not DummyCharacterEffectLineDataTarget lineData || lineData.effect is null)
            {
                return;
            }

            text.SetText(lineData.effect.DisplayName);
        }
    }

    internal sealed class DummyCharacterEffectLineDataTarget
    {
        public DummyStatusEffect? effect { get; set; }
    }

    internal sealed class DummySkillDefinition
    {
        public int Cost { get; set; } = 100;
    }

    internal sealed class DummyPowerDefinition
    {
        public string Id { get; set; } = "power";
    }

    internal enum DummyLearnedStatus
    {
        None,
        Partial,
        Learned,
    }

    internal sealed class DummySkillsAndPowersScreenTarget
    {
        public DummyStatusGameObject GO { get; set; } = new DummyStatusGameObject();
    }

    internal sealed class DummySPNodeTarget
    {
        public DummySkillDefinition? Skill { get; set; }

        public DummyPowerDefinition? Power { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool Expand { get; set; }

        public DummyRenderable UIIcon { get; set; } = new DummyRenderable("skill");

        public List<DummySPNodeTarget> powers { get; set; } = new List<DummySPNodeTarget>();

        public DummyLearnedStatus LearnedStatus { get; set; }

        public DummyLearnedStatus IsLearned(object? go)
        {
            _ = go;
            return LearnedStatus;
        }

        public string ModernText { get; set; } = "Pyrokinesis";

        public string ModernUIText(object? go)
        {
            _ = go;
            return ModernText;
        }
    }

    internal sealed class DummySkillsAndPowersLineDataTarget
    {
        public DummyStatusGameObject go { get; set; } = new DummyStatusGameObject();

        public DummySPNodeTarget entry { get; set; } = new DummySPNodeTarget();

        public DummySkillsAndPowersScreenTarget screen { get; set; } = new DummySkillsAndPowersScreenTarget();
    }

    internal sealed class DummySkillsAndPowersLineTarget
    {
        public DummyStatusContext context = new DummyStatusContext();

        public DummyUITextSkin skillText = new DummyUITextSkin();

        public DummyUITextSkin skillRightText = new DummyUITextSkin();

        public DummyUITextSkin skillExpander = new DummyUITextSkin();

        public DummyUiThreeColorProperties skillIcon = new DummyUiThreeColorProperties();

        public DummyUITextSkin powerText = new DummyUITextSkin();

        public DummyToggleObject skillType = new DummyToggleObject();

        public DummyToggleObject powerType = new DummyToggleObject();

        public void setData(object data)
        {
            context.data = data;
            if (data is not DummySkillsAndPowersLineDataTarget lineData)
            {
                return;
            }

            if (lineData.entry.Skill is not null)
            {
                skillText.SetText(lineData.entry.Name);
                skillRightText.SetText("Starting Cost {{g|[100 sp]}}");
                return;
            }

            powerText.SetText(lineData.entry.ModernUIText(lineData.screen.GO));
        }
    }

    internal sealed class DummyCharacterStatusScreenBindingTarget
    {
        public static List<DummyStatusStatistic> stats = new List<DummyStatusStatistic>
        {
            new DummyStatusStatistic
            {
                Name = "Strength",
                ShortDisplayName = "STR",
                Value = 18,
                BaseValue = 18,
                Modifier = 2,
            },
        };

        public static List<DummyCharacterMutationRecord> mutations = new List<DummyCharacterMutationRecord>();

        public static List<DummyStatusEffect> effects = new List<DummyStatusEffect>();

        public static string[] PrimaryAttributes = new[] { "Strength" };

        public static string[] SecondaryAttributes = System.Array.Empty<string>();

        public static string[] SecondaryAttributesWithCP = new[] { "CP" };

        public static string[] ResistanceAttributes = System.Array.Empty<string>();

        public static int CP;

        public DummyBindingScroller primaryAttributesController = new DummyBindingScroller();

        public DummyBindingScroller secondaryAttributesController = new DummyBindingScroller();

        public DummyBindingScroller resistanceAttributesController = new DummyBindingScroller();

        public DummyBindingScroller mutationsController = new DummyBindingScroller();

        public DummyBindingScroller effectsController = new DummyBindingScroller();

        public DummyUiThreeColorProperties playerIcon = new DummyUiThreeColorProperties();

        public DummyStatusGameObject GO = new DummyStatusGameObject();

        public string mutationsTerm = "Mutations";

        public string mutationTerm = "Mutation";

        public string mutationTermCapital = "Mutation";

        public string mutationColor = "C";

        public DummyUITextSkin mutationTermText = new DummyUITextSkin();

        public DummyUITextSkin nameText = new DummyUITextSkin();

        public DummyUITextSkin classText = new DummyUITextSkin();

        public DummyUITextSkin levelText = new DummyUITextSkin();

        public DummyUITextSkin attributePointsText = new DummyUITextSkin();

        public DummyUITextSkin mutationPointsText = new DummyUITextSkin();

        public void UpdateViewFromData()
        {
            mutationTermText.SetText("MUTATIONS");
            nameText.SetText(GO.DisplayName);
            classText.SetText(GO.GetGenotype() + " " + GO.GetSubtype());
            levelText.SetText("Level: 1 ¯ HP: 10/10 ¯ XP: 100/200 ¯ Weight: 123#");
            attributePointsText.SetText("Attribute Points: 0");
            mutationPointsText.SetText("Mutation Points: 0");
        }
    }
}

namespace Qud.UI
{
    internal sealed class CharacterAttributeLineData
    {
        public enum Category
        {
            primary,
            secondary,
            resistance,
        }

        public Category category { get; set; }

        public object? data { get; set; }

        public object? go { get; set; }

        public string? stat { get; set; }
    }

    internal sealed class CharacterMutationLineData
    {
        public object? mutation { get; set; }
    }

    internal sealed class CharacterEffectLineData
    {
        public object? effect { get; set; }
    }
}

namespace XRL.Language
{
    internal static class Grammar
    {
        public static string MakeTitleCase(string source)
        {
            return source.Length == 0
                ? source
                : char.ToUpperInvariant(source[0]) + source.Substring(1);
        }

        public static string Pluralize(string source)
        {
            return source.EndsWith("s", System.StringComparison.Ordinal) ? source : source + "s";
        }
    }
}

namespace ConsoleLib.Console
{
    internal static class ColorUtility
    {
        public static string ToUpperExceptFormatting(string source)
        {
            return source.ToUpperInvariant();
        }
    }
}

namespace XRL.World.Capabilities
{
    internal static class Leveler
    {
        public static int GetXPForLevel(int level)
        {
            return level * 100;
        }
    }
}

namespace XRL.Rules
{
    internal static class Stats
    {
        public static int GetCombatAV(object? go)
        {
            _ = go;
            return 5;
        }

        public static int GetCombatDV(object? go)
        {
            _ = go;
            return 7;
        }

        public static int GetCombatMA(object? go)
        {
            _ = go;
            return 9;
        }
    }
}
