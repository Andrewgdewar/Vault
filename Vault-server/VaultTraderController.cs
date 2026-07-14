using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace SharedStorageTrader;

[Injectable(InjectionType.Scoped, TypePriority = int.MaxValue)]
public sealed class VaultTraderController(
    ISptLogger<TraderController> logger,
    TimeUtil timeUtil,
    DatabaseService databaseService,
    TraderAssortHelper traderAssortHelper,
    ProfileHelper profileHelper,
    TraderHelper traderHelper,
    PaymentHelper paymentHelper,
    RagfairPriceService ragfairPriceService,
    TraderPurchasePersisterService traderPurchasePersisterService,
    FenceService fenceService,
    FenceBaseAssortGenerator fenceBaseAssortGenerator,
    ConfigServer configServer,
    SharedStorageService storageService)
    : TraderController(
        logger,
        timeUtil,
        databaseService,
        traderAssortHelper,
        profileHelper,
        traderHelper,
        paymentHelper,
        ragfairPriceService,
        traderPurchasePersisterService,
        fenceService,
        fenceBaseAssortGenerator,
        configServer)
{
    public override TraderAssort GetAssort(MongoId sessionId, MongoId traderId)
    {
        var assort = base.GetAssort(sessionId, traderId);
        return traderId == SharedStorageService.TraderId
            ? storageService.MarkOwnedOffers(assort, sessionId)
            : assort;
    }
}