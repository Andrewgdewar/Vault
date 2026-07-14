using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using Path = System.IO.Path;

namespace SharedStorageTrader;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.dewar.sharedstoragetrader";
    public override string Name { get; init; } = "Shared Storage Trader";
    public override string Author { get; init; } = "Dewar";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("0.1.1");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.13");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = false;
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 700)]
public sealed class SharedStorageMod(
    ISptLogger<SharedStorageMod> logger,
    ModHelper modHelper,
    DatabaseService databaseService,
    ImageRouter imageRouter,
    ConfigServer configServer,
    TimeUtil timeUtil,
    ICloner cloner,
    SharedStorageService storageService) : IOnLoad
{
    public Task OnLoad()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var fence = databaseService.GetTrader(Traders.FENCE)
            ?? throw new InvalidOperationException("Fence trader was not found");
        var trader = CreateTrader(cloner.Clone(fence.Base)
            ?? throw new InvalidOperationException("Unable to clone Fence trader base"));

        databaseService.GetTables().Traders[SharedStorageService.TraderId] = new Trader
        {
            Base = trader,
            Assort = new TraderAssort
            {
                Items = [],
                BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
                LoyalLevelItems = new Dictionary<MongoId, int>()
            },
            QuestAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
            {
                ["Started"] = [],
                ["Success"] = [],
                ["Fail"] = []
            },
            Dialogue = []
        };

        var traderConfig = configServer.GetConfig<TraderConfig>();
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            TraderId = SharedStorageService.TraderId,
            Seconds = new MinMax<int>(timeUtil.GetHoursAsSeconds(24), timeUtil.GetHoursAsSeconds(24))
        });

        var avatarPath = Path.Combine(modPath, "assets", "vault-door.png");
        imageRouter.AddRoute(trader.Avatar!.Replace(".png", string.Empty), avatarPath);
        AddLocales(databaseService, trader);

        storageService.Initialize(Path.Combine(modPath, "data", "shared-storage.json"));
        logger.Success("Vault trader registered");
        return Task.CompletedTask;
    }

    private static TraderBase CreateTrader(TraderBase trader)
    {
        trader.Id = SharedStorageService.TraderId;
        trader.Name = "Vault";
        trader.Nickname = "Vault";
        trader.Surname = "";
        trader.Location = "The Vault";
        trader.Avatar = $"/files/trader/avatar/{SharedStorageService.TraderId}.png";
        trader.AvailableInRaid = false;
        trader.IsAvailableInPVE = true;
        trader.UnlockedByDefault = true;
        trader.BalanceRub = 999_999_999;
        trader.GridHeight = 160;
        trader.NextResupply = int.MaxValue;
        trader.ItemsBuy = new ItemBuyData
        {
            Category = [BaseClasses.ITEM],
            IdList = []
        };
        trader.ItemsBuyProhibited = new ItemBuyData
        {
            Category = [BaseClasses.MONEY],
            IdList = []
        };
        trader.LoyaltyLevels =
        [
            new TraderLoyaltyLevel
            {
                BuyPriceCoefficient = 90,
                ExchangePriceCoefficient = 0,
                MinLevel = 1,
                MinSalesSum = 0,
                MinStanding = 0
            }
        ];
        return trader;
    }

    private static void AddLocales(DatabaseService databaseService, TraderBase trader)
    {
        foreach (var locale in databaseService.GetTables().Locales.Global.Values)
        {
            locale.AddTransformer(data =>
            {
                if (data is null)
                {
                    return data;
                }

                data[$"{trader.Id} FullName"] = "Vault";
                data[$"{trader.Id} FirstName"] = "Vault";
                data[$"{trader.Id} Nickname"] = "Vault";
                data[$"{trader.Id} Location"] = "The Vault";
                data[$"{trader.Id} Description"] = "A shared warehouse. Anything sold here can be retrieved by anyone for one rouble.";
                return data;
            });
        }
    }
}
