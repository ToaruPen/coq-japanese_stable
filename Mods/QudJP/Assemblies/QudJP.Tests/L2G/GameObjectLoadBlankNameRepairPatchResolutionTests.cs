#if HAS_GAME_DLL
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2G;

[TestFixture]
[Category("L2G")]
public sealed class GameObjectLoadBlankNameRepairPatchResolutionTests
{
    [Test]
    public void Postfix_RepairsRealPersistedRenderAndInvalidatesRealSortCacheWithoutRecreatingItem()
    {
        // Dummy targets cannot prove the reflected writes reach the game's actual fields.
        // Bypass construction to avoid GameObjectFactory and Unity bootstrap.
        var item = (XRL.World.GameObject)RuntimeHelpers.GetUninitializedObject(typeof(XRL.World.GameObject));
        var render = (XRL.World.Parts.Render)RuntimeHelpers.GetUninitializedObject(typeof(XRL.World.Parts.Render));
        item.Blueprint = "Red Security Card";
        item.Property = new Dictionary<string, string> { ["id"] = "preserved-item-id" };
        item.IntProperty = new Dictionary<string, int> { ["ModCount"] = 2 };
        item.Render = render;
        render.DisplayName = "{{r|}}";
        render.ColorString = "&r";
        var cache = AccessTools.Field(typeof(XRL.World.GameObject), "_CachedDisplayNameForSort");
        Assert.That(cache, Is.Not.Null);
        cache!.SetValue(item, "old cached name");

        GameObjectLoadBlankNameRepairPatch.Postfix(item);

        Assert.Multiple(() =>
        {
            Assert.That(item.Render, Is.SameAs(render));
            Assert.That(render.DisplayName, Is.EqualTo("{{r|労働者用セキュリティカード}}"));
            Assert.That(cache.GetValue(item), Is.Null);
            Assert.That(render.ColorString, Is.EqualTo("&r"));
            Assert.That(item.Property["id"], Is.EqualTo("preserved-item-id"));
            Assert.That(item.IntProperty["ModCount"], Is.EqualTo(2));
        });
    }

    [Test]
    public void TargetMethod_ResolvesExactGameObjectLoadSerializationReaderOverload()
    {
        _ = typeof(XRL.World.GameObject).Assembly;
        var resolver = typeof(GameObjectLoadBlankNameRepairPatch).GetMethod("TargetMethod", BindingFlags.NonPublic | BindingFlags.Static)!;
        var target = resolver.Invoke(null, null) as MethodInfo;

        Assert.That(target, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(target!.DeclaringType, Is.EqualTo(typeof(XRL.World.GameObject)));
            Assert.That(target.Name, Is.EqualTo("Load"));
            Assert.That(target.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(target.IsStatic, Is.False);
            Assert.That(target.GetParameters().Select(parameter => parameter.ParameterType.FullName),
                Is.EqualTo(new[] { "XRL.World.SerializationReader" }));
        });
    }

    [Test]
    public void RealGameMembers_MatchDirectSavedStateAndCacheContracts()
    {
        var gameObject = typeof(XRL.World.GameObject);
        var render = typeof(XRL.World.Parts.Render);

        Assert.Multiple(() =>
        {
            Assert.That(AccessTools.Field(gameObject, "Blueprint")?.FieldType, Is.EqualTo(typeof(string)));
            Assert.That(AccessTools.Field(gameObject, "Property")?.FieldType, Is.EqualTo(typeof(Dictionary<string, string>)));
            Assert.That(AccessTools.Field(gameObject, "IntProperty")?.FieldType, Is.EqualTo(typeof(Dictionary<string, int>)));
            Assert.That(AccessTools.Field(gameObject, "Render")?.FieldType, Is.EqualTo(render));
            Assert.That(AccessTools.Field(render, "DisplayName")?.FieldType, Is.EqualTo(typeof(string)));
            Assert.That(AccessTools.Field(render, "DisplayName")?.IsInitOnly, Is.False);
            var reset = AccessTools.Method(gameObject, "ResetNameCache", Type.EmptyTypes);
            Assert.That(reset, Is.Not.Null);
            Assert.That(reset?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(reset?.IsStatic, Is.False);
        });
    }
}
#endif
