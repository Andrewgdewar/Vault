using BepInEx;
using BepInEx.Logging;

namespace VaultOwnerFilter;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.dewar.vaultownerfilter";
    public const string PluginName = "Vault Owner Filter";
    public const string PluginVersion = "0.1.1";

    internal static ManualLogSource Log { get; private set; } = null!;

    private void Start()
    {
        Log = Logger;
        new TraderDealScreenAwakePatch().Enable();
        new TraderDealScreenShowPatch().Enable();
        new TraderAssortmentUpdatedPatch().Enable();
        new VaultOwnerFilterPatch().Enable();
        new VaultStackCountPatch().Enable();
        Logger.LogInfo("Vault owner filter loaded");
    }
}
