using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using TMPro;

namespace VaultOwnerFilter;

internal sealed class TraderDealScreenAwakePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TraderDealScreen), "Awake");
    }

    [PatchPostfix]
    private static void Postfix(
        TraderDealScreen __instance,
        DefaultUIButton ____updateAssort,
        TradingGridView ____traderGridView)
    {
        var panel = __instance.GetComponent<VaultOwnerFilterPanel>()
            ?? __instance.gameObject.AddComponent<VaultOwnerFilterPanel>();
        panel.Initialize(____traderGridView, ____updateAssort);
    }
}

internal sealed class TraderDealScreenShowPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.GetDeclaredMethods(typeof(TraderDealScreen))
            .Single(method =>
                method.Name == "Show"
                && method.GetParameters().Length == 7
                && method.GetParameters()[0].ParameterType == typeof(TraderClass));
    }

    [PatchPrefix]
    private static void Prefix(TraderClass trader)
    {
        var isVault = trader.Id.ToString().Equals(VaultOwnerState.VaultTraderId, StringComparison.OrdinalIgnoreCase);
        VaultOwnerState.SetActive(isVault);
    }

    [PatchPostfix]
    private static void Postfix(TraderDealScreen __instance, TraderClass trader)
    {
        __instance.GetComponent<VaultOwnerFilterPanel>()?.ShowForTrader(trader);
    }
}

internal sealed class TraderAssortmentUpdatedPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TraderDealScreen), "method_10");
    }

    [PatchPostfix]
    private static void Postfix(TraderDealScreen __instance)
    {
        __instance.GetComponent<VaultOwnerFilterPanel>()?.RefreshIfActive();
    }
}

internal sealed class VaultOwnerFilterPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.DeclaredMethod(typeof(HandbookFilterPanel), nameof(HandbookFilterPanel.GetFilteredItems));
    }

    [PatchPostfix]
    private static void Postfix(ref IEnumerable<Item> __result)
    {
        __result = VaultOwnerState.Filter(__result).ToArray();
    }
}

internal sealed class VaultStackCountPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.DeclaredMethod(typeof(TradingItemView), nameof(TradingItemView.UpdateInfo));
    }

    [PatchPostfix]
    private static void Postfix(TradingItemView __instance, TraderClass ___Trader)
    {
        if (___Trader is null
            || !___Trader.Id.ToString().Equals(VaultOwnerState.VaultTraderId, StringComparison.OrdinalIgnoreCase)
            || __instance.Item is null
            || !VaultOwnerState.TryGetStackCount(__instance.Item.Id.ToString(), out var stackCount)
            || stackCount <= 1)
        {
            return;
        }

        __instance.ItemValue.SetText(stackCount.ToString());
        __instance.SetValueVisibility(true);
    }
}
