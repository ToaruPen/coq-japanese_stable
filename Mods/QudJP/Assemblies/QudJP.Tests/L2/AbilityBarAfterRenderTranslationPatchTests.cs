using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AbilityBarAfterRenderTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-abilitybar-afterrender-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesBackingsAfterAfterRender()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("poisoned", "毒状態"),
            ("TARGET:", "ターゲット:"),
            ("snapjaw", "スナップジョー"),
            ("Healthy", "健康"),
            ("calm", "平静"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText = "{{Y|ACTIVE EFFECTS:}} poisoned",
                NextTargetText = "{{C|TARGET: snapjaw}}",
                NextTargetHealthText = "Healthy, calm",
            };

            target.AfterRender(core: null, sb: null);

            Assert.Multiple(() =>
            {
                Assert.That(target.GetEffectText(), Is.EqualTo("{{Y|発動中の効果:}} 毒状態"));
                Assert.That(target.GetTargetText(), Is.EqualTo("{{C|ターゲット: スナップジョー}}"));
                Assert.That(target.GetTargetHealthText(), Is.EqualTo("健康、平静"));
            });
        });
    }

    [Test]
    public void Postfix_UsesDisplayNameRouteFallbackWithoutTraceWarning()
    {
        WriteDictionary(
            ("TARGET:", "ターゲット:"),
            ("snapjaw", "スナップジョー"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"));

        var trace = TestTraceHelper.CaptureTrace(() =>
        {
            RunWithPostfixPatch(() =>
            {
                var target = new DummyAbilityBarAfterRenderTarget
                {
                    NextTargetText = "{{C|TARGET: snapjaw [empty]}}",
                };

                target.AfterRender(core: null, sb: null);

                Assert.That(target.GetTargetText(), Is.EqualTo("{{C|ターゲット: スナップジョー [空]}}"));
            });
        });

        Assert.That(trace, Does.Not.Contain("falling back to display-name route translation"));
    }

    [Test]
    public void Postfix_PartiallyTranslatesActiveEffectsAndKeepsMissingEffectsVisible()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wet", "濡れている"),
            ("TARGET:", "ターゲット:"),
            ("Healthy", "健康"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText = "ACTIVE EFFECTS: unknown, wet",
                NextTargetText = "TARGET: mysterious snapjaw",
                NextTargetHealthText = "Healthy, uncertain",
            };

            target.AfterRender(core: null, sb: null);

            Assert.Multiple(() =>
            {
                Assert.That(target.GetEffectText(), Is.EqualTo("発動中の効果: unknown、濡れている"));
                Assert.That(target.GetTargetText(), Is.EqualTo("ターゲット: mysterious snapjaw"));
                Assert.That(target.GetTargetHealthText(), Is.EqualTo("Healthy, uncertain"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("unknown"), Is.EqualTo(1));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        "AbilityBar.ActiveEffects"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesPerEffectTmpSegments_ForMixedTmpAndQudActiveEffects()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "アクティブ効果:"),
            ("wet", "濡れた"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText =
                    "<color=#FFFFFFFF><color=#508d75>ACTIVE EFFECTS:</color></color> <color=#0096FFFF>swimming</color>, <color=#B1C9C3FF>{{B|wet}}</color>",
            };

            target.AfterRender(core: null, sb: null);

            Assert.That(
                target.GetEffectText(),
                Is.EqualTo(
                    "<color=#FFFFFFFF><color=#508d75>アクティブ効果:</color></color> <color=#0096FFFF>swimming</color>、<color=#B1C9C3FF>{{B|濡れた}}</color>"));
        });
    }

    [Test]
    public void Postfix_PreservesSeparatorTmpSegment_WhenCommaBelongsToPreviousEffectColor()
    {
        WriteDictionary(("ACTIVE EFFECTS:", "アクティブ効果:"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText =
                    "<color=#FFFFFFFF><color=#508d75>ACTIVE EFFECTS:</color></color><color=#B1C9C3FF> </color><color=#0096FFFF>浅瀬を進んで</color><color=#B1C9C3FF>いる, </color><color=#0096FFFF>濡れた</color>",
            };

            target.AfterRender(core: null, sb: null);

            Assert.That(
                target.GetEffectText(),
                Is.EqualTo(
                    "<color=#FFFFFFFF><color=#508d75>アクティブ効果:</color></color><color=#B1C9C3FF> </color><color=#0096FFFF>浅瀬を進んで</color><color=#B1C9C3FF>いる、</color><color=#0096FFFF>濡れた</color>"));
        });
    }

    [Test]
    public void Postfix_TranslatesLabelOnlyActiveEffectsAndFlushesToTextField()
    {
        WriteDictionary(("ACTIVE EFFECTS:", "発動中の効果:"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText = "<color=#FFFFFFFF><color=#508d75>ACTIVE EFFECTS:</color></color><color=#B1C9C3FF> </color>",
            };

            target.AfterRender(core: null, sb: null);
            target.Update();

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.GetEffectText(),
                    Is.EqualTo("<color=#FFFFFFFF><color=#508d75>発動中の効果:</color></color><color=#B1C9C3FF> </color>"));
                Assert.That(target.EffectText.text, Is.EqualTo(target.GetEffectText()));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        "AbilityBar.ActiveEffects"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesMarkupWrappedEnglishModifierInTargetDisplayName()
    {
        WriteDictionary(
            ("TARGET:", "ターゲット:"),
            ("dromad merchant", "ドロマド商人"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"),
            ("[sitting]", "[座っている]"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextTargetText = "{{C|TARGET: {{r|bloody}} Tam, dromad merchant [sitting]}}",
            };

            target.AfterRender(core: null, sb: null);

            Assert.That(
                target.GetTargetText(),
                Is.EqualTo("{{C|ターゲット: {{r|血まみれの}}Tam、ドロマド商人 [座っている]}}"));
        });
    }

    [Test]
    public void Postfix_PreservesLabelMarkup_WhenDisplayNameTranslationOwnsMarkup()
    {
        WriteDictionary(
            ("TARGET:", "ターゲット:"),
            ("dromad merchant", "ドロマド商人"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"),
            ("[sitting]", "[座っている]"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextTargetText = "{{C|<color=#3e83a5>TARGET:</color> {{r|bloody}} Tam, dromad merchant [sitting]}}",
            };

            target.AfterRender(core: null, sb: null);

            Assert.That(
                target.GetTargetText(),
                Is.EqualTo("{{C|<color=#3e83a5>ターゲット:</color> {{r|血まみれの}}Tam、ドロマド商人 [座っている]}}"));
        });
    }

    [Test]
    public void Postfix_PreservesAmpersandWholeLineColor_WhenDisplayNameTranslationOwnsMarkup()
    {
        WriteDictionary(
            ("TARGET:", "ターゲット:"),
            ("dromad merchant", "ドロマド商人"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"),
            ("[sitting]", "[座っている]"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextTargetText = "&G<color=#3e83a5>TARGET:</color> {{r|bloody}} Tam, dromad merchant [sitting]&y",
            };

            target.AfterRender(core: null, sb: null);

            Assert.That(
                target.GetTargetText(),
                Is.EqualTo("&G<color=#3e83a5>ターゲット:</color> {{r|血まみれの}}Tam、ドロマド商人 [座っている]&y"));
        });
    }

    [Test]
    public void Postfix_PreservesNestedSameTokenLabelWrapper_WhenDisplayNameTranslationOwnsMarkup()
    {
        WriteDictionary(
            ("TARGET:", "ターゲット:"),
            ("dromad merchant", "ドロマド商人"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"),
            ("[sitting]", "[座っている]"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextTargetText = "{{C|{{C|TARGET:}} {{r|bloody}} Tam, dromad merchant [sitting]}}",
            };

            target.AfterRender(core: null, sb: null);

            Assert.That(
                target.GetTargetText(),
                Is.EqualTo("{{C|{{C|ターゲット:}} {{r|血まみれの}}Tam、ドロマド商人 [座っている]}}"));
        });
    }

    [Test]
    public void Postfix_PreservesTerminalEmptyWrapperInTargetText()
    {
        WriteDictionary(("TARGET:", "ターゲット:"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextTargetText = "{{C|<color=#3e83a5>TARGET:</color> タム、ドロマド商団 [座っている]{{B|}}}}",
            };

            target.AfterRender(core: null, sb: null);

            Assert.That(
                target.GetTargetText(),
                Is.EqualTo("{{C|<color=#3e83a5>ターゲット:</color> タム、ドロマド商団 [座っている]{{B|}}}}"));
        });
    }

    [Test]
    public void Postfix_RecordsOwnerRouteTransforms_WithoutUITextSkinSinkObservation()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("poisoned", "毒状態"),
            ("TARGET:", "ターゲット:"),
            ("snapjaw", "スナップジョー"),
            ("Healthy", "健康"),
            ("calm", "平静"));

        RunWithPostfixPatch(() =>
        {
            var target = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText = "ACTIVE EFFECTS: poisoned",
                NextTargetText = "TARGET: snapjaw",
                NextTargetHealthText = "Healthy, calm",
            };

            target.AfterRender(core: null, sb: null);

            Assert.Multiple(() =>
            {
                Assert.That(target.GetEffectText(), Is.EqualTo("発動中の効果: 毒状態"));
                Assert.That(target.GetTargetText(), Is.EqualTo("ターゲット: スナップジョー"));
                Assert.That(target.GetTargetHealthText(), Is.EqualTo("健康、平静"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        "AbilityBar.ActiveEffects"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        "AbilityBar.TargetText"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        "AbilityBar.TargetHealth"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "ACTIVE EFFECTS: poisoned",
                        "ACTIVE EFFECTS: poisoned"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "TARGET: snapjaw",
                        "TARGET: snapjaw"),
                    Is.EqualTo(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(AbilityBarAfterRenderTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Healthy, calm",
                        "Healthy, calm"),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesEmptyAndMarkerPrefixedInputs()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("TARGET:", "ターゲット:"),
            ("snapjaw", "スナップジョー"),
            ("Healthy", "健康"),
            ("calm", "平静"));

        RunWithPostfixPatch(() =>
        {
            var emptyTarget = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText = string.Empty,
                NextTargetText = string.Empty,
                NextTargetHealthText = string.Empty,
            };
            emptyTarget.AfterRender(core: null, sb: null);

            var markerTarget = new DummyAbilityBarAfterRenderTarget
            {
                NextEffectText = "\u0001ACTIVE EFFECTS: poisoned",
                NextTargetText = "\u0001{{C|TARGET: snapjaw}}",
                NextTargetHealthText = "\u0001Healthy, calm",
            };
            markerTarget.AfterRender(core: null, sb: null);

            Assert.Multiple(() =>
            {
                Assert.That(emptyTarget.GetEffectText(), Is.EqualTo(string.Empty));
                Assert.That(emptyTarget.GetTargetText(), Is.EqualTo(string.Empty));
                Assert.That(emptyTarget.GetTargetHealthText(), Is.EqualTo(string.Empty));
                Assert.That(markerTarget.GetEffectText(), Is.EqualTo("\u0001ACTIVE EFFECTS: poisoned"));
                Assert.That(markerTarget.GetTargetText(), Is.EqualTo("\u0001{{C|TARGET: snapjaw}}"));
                Assert.That(markerTarget.GetTargetHealthText(), Is.EqualTo("\u0001Healthy, calm"));
            });
        });
    }

    private static void RunWithPostfixPatch(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyAbilityBarAfterRenderTarget), nameof(DummyAbilityBarAfterRenderTarget.AfterRender)),
                postfix: new HarmonyMethod(RequirePatchMethod(
                    "QudJP.Patches.AbilityBarAfterRenderTranslationPatch",
                    "Postfix",
                    typeof(object))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        var method = AccessTools.Method(type, methodName);
        Assert.That(method, Is.Not.Null, $"Method not found: {type.FullName}.{methodName}");
        return method!;
    }

    private static MethodInfo RequirePatchMethod(string typeName, string methodName, params Type[] parameterTypes)
    {
        var patchType = typeof(Translator).Assembly.GetType(typeName, throwOnError: false);
        Assert.That(patchType, Is.Not.Null, $"Patch type not found: {typeName}");

        var method = patchType!.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"Method not found: {typeName}.{methodName}");
        return method!;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("abilitybar-afterrender-l2.ja.json", entries);
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var writer = new StreamWriter(path, append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
