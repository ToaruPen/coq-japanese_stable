namespace QudJP.Patches;

internal static class PopupShowSemanticPipeline
{
    internal static string TranslateMessage(string source, string route)
    {
        if (string.IsNullOrEmpty(source))
        {
            if (source is null)
            {
                return string.Empty;
            }

            return source;
        }

        if (TradeUiPopupTranslationPatch.TryTranslatePerformOfferTradeWaterMessage(source, out var tradeWaterTranslated))
        {
            return tradeWaterTranslated;
        }

        if (TradeUiPopupTranslationPatch.TryTranslateHasNothingToTradeMessage(source, out var hasNothingTranslated))
        {
            return hasNothingTranslated;
        }

        if (GameObjectStatPopupTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var statTranslated))
        {
            return statTranslated;
        }

        if (GameObjectMoveTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var moveTranslated))
        {
            return moveTranslated;
        }

        if (GameObjectPerformThrowTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var performThrowTranslated))
        {
            return performThrowTranslated;
        }

        if (GameObjectPopupTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var gameObjectPopupTranslated))
        {
            return gameObjectPopupTranslated;
        }

        if (RealityStabilizedInterdictTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var realityStabilizedTranslated))
        {
            return realityStabilizedTranslated;
        }

        if (HackingSifrahResultTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var hackingSifrahTranslated))
        {
            return hackingSifrahTranslated;
        }

        if (QuestLifecyclePopupTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var questLifecycleTranslated))
        {
            return questLifecycleTranslated;
        }

        if (BodyTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var bodyTranslated))
        {
            return bodyTranslated;
        }

        if (ItemModdingSifrahTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var itemModdingTranslated))
        {
            return itemModdingTranslated;
        }

        if (SunderMindTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var sunderMindTranslated))
        {
            return sunderMindTranslated;
        }

        if (KeybindsScreenConflictTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var keybindsTranslated))
        {
            return keybindsTranslated;
        }

        if (AbilityManagerPopupTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var abilityManagerTranslated))
        {
            return abilityManagerTranslated;
        }

        if (RealityStabilizedEventTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var realityEventTranslated))
        {
            return realityEventTranslated;
        }

        if (GeomagneticDiscTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var geomagneticDiscTranslated))
        {
            return geomagneticDiscTranslated;
        }

        if (CampfireCookAvailabilityTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var campfireCookTranslated))
        {
            return campfireCookTranslated;
        }

        if (CampfirePreserveTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var campfirePreserveTranslated))
        {
            return campfirePreserveTranslated;
        }

        if (CookingRuntimeTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var cookingRuntimeTranslated))
        {
            return cookingRuntimeTranslated;
        }

        if (StatusScreenPopupTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var statusScreenTranslated))
        {
            return statusScreenTranslated;
        }

        if (TeleprojectorTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var teleprojectorTranslated))
        {
            return teleprojectorTranslated;
        }

        if (CyberneticsMedassistModuleTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var medassistTranslated))
        {
            return medassistTranslated;
        }

        if (LiquidLoaderTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var liquidLoaderTranslated))
        {
            return liquidLoaderTranslated;
        }

        if (MutatingTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var mutatingTranslated))
        {
            return mutatingTranslated;
        }

        if (LightManipulationTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var lightManipulationTranslated))
        {
            return lightManipulationTranslated;
        }

        if (AsleepOwnerTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var asleepTranslated))
        {
            return asleepTranslated;
        }

        if (BeguilingTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var beguilingTranslated))
        {
            return beguilingTranslated;
        }

        if (AscensionCableTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var ascensionCableTranslated))
        {
            return ascensionCableTranslated;
        }

        if (CarapaceTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var carapaceTranslated))
        {
            return carapaceTranslated;
        }

        if (NephalPropertiesTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var nephalTranslated))
        {
            return nephalTranslated;
        }

        if (IntegratedWeaponHostsTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var integratedWeaponHostsTranslated))
        {
            return integratedWeaponHostsTranslated;
        }

        if (FungalSporeInfectionTranslationPatch.TryTranslatePopupMessage(source, route, "Popup.Show", out var fungalSporeInfectionTranslated))
        {
            return fungalSporeInfectionTranslated;
        }

        return PopupTranslationPatch.TranslatePopupTextForProducerRoute(source, route);
    }
}
