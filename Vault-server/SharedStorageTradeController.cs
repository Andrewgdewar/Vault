using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SharedStorageTrader;

[Injectable(InjectionType.Scoped, TypePriority = int.MaxValue)]
public sealed class SharedStorageTradeController(
    ISptLogger<TradeController> baseLogger,
    ISptLogger<SharedStorageTradeController> logger,
    DatabaseService databaseService,
    EventOutputHolder eventOutputHolder,
    TradeHelper tradeHelper,
    TimeUtil timeUtil,
    RandomUtil randomUtil,
    ItemHelper itemHelper,
    RagfairOfferHelper ragfairOfferHelper,
    RagfairServer ragfairServer,
    HttpResponseUtil httpResponseUtil,
    ServerLocalisationService serverLocalisationService,
    MailSendService mailSendService,
    ConfigServer configServer,
    InventoryHelper inventoryHelper,
    PaymentService paymentService,
    ICloner cloner,
    SharedStorageService storageService)
    : TradeController(
        baseLogger,
        databaseService,
        eventOutputHolder,
        tradeHelper,
        timeUtil,
        randomUtil,
        itemHelper,
        ragfairOfferHelper,
        ragfairServer,
        httpResponseUtil,
        serverLocalisationService,
        mailSendService,
        configServer)
{
    public override ItemEventRouterResponse ConfirmTrading(
        PmcData pmcData,
        ProcessBaseTradeRequestData request,
        MongoId sessionId)
    {
        if (request.TransactionId != SharedStorageService.TraderId)
        {
            return base.ConfirmTrading(pmcData, request, sessionId);
        }

        logger.Info($"Handling Vault transaction '{request.Type}' for {sessionId}");
        lock (storageService.SyncRoot)
        {
            return request.Type switch
            {
                "sell_to_trader" => StoreSoldItems(pmcData, (ProcessSellTradeRequestData)request, sessionId),
                "buy_from_trader" => RetrieveStoredItem(pmcData, (ProcessBuyTradeRequestData)request, sessionId),
                _ => base.ConfirmTrading(pmcData, request, sessionId)
            };
        }
    }

    private ItemEventRouterResponse StoreSoldItems(
        PmcData pmcData,
        ProcessSellTradeRequestData request,
        MongoId sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);
        var warningsBefore = output.Warnings?.Count ?? 0;
        var capturedOffers = new List<StoredOffer>();
        var requestedOfferCount = request.Items?.Count ?? 0;
        var currentOfferCount = storageService.GetOfferCount(sessionId);
        if (currentOfferCount + requestedOfferCount > SharedStorageService.MaxOffersPerSeller)
        {
            httpResponseUtil.AppendErrorToOutput(
                output,
                $"Vault limit exceeded: you have {currentOfferCount}/{SharedStorageService.MaxOffersPerSeller} items stored and attempted to add {requestedOfferCount}");
            return output;
        }

        foreach (var soldItem in request.Items ?? [])
        {
            var inventoryItems = pmcData.Inventory?.Items;
            if (inventoryItems is null)
            {
                httpResponseUtil.AppendErrorToOutput(output, "Unable to store items because the player inventory is unavailable");
                return output;
            }

            var itemTree = inventoryItems.GetItemWithChildren(soldItem.Id);
            if (itemTree.Count == 0)
            {
                httpResponseUtil.AppendErrorToOutput(output, $"Unable to store item {soldItem.Id}; it was not found in the player inventory");
                return output;
            }

            if (itemTree.Any(item => itemHelper.IsOfBaseclass(item.Template, BaseClasses.MONEY)))
            {
                httpResponseUtil.AppendErrorToOutput(output, "Vault does not accept currency");
                return output;
            }

            var capturedTree = cloner.Clone(itemTree)
                ?? throw new InvalidOperationException($"Unable to clone sold item tree {soldItem.Id}");
            capturedOffers.Add(new StoredOffer
            {
                OfferId = capturedTree[0].Id,
                SellerProfileId = sessionId,
                SellerNickname = pmcData.Info?.Nickname,
                StoredAtUtc = DateTime.UtcNow,
                Items = capturedTree
            });
        }

        request.Price = capturedOffers.Count;
        var result = base.ConfirmTrading(pmcData, request, sessionId);
        if ((result.Warnings?.Count ?? 0) > warningsBefore)
        {
            return result;
        }

        storageService.AddOffers(capturedOffers);
        logger.Info($"Stored {capturedOffers.Count} shared offer(s) sold by {sessionId}");
        return result;
    }

    private ItemEventRouterResponse RetrieveStoredItem(
        PmcData pmcData,
        ProcessBuyTradeRequestData request,
        MongoId sessionId)
    {
        var output = eventOutputHolder.GetOutput(sessionId);
        var offer = storageService.GetOffer(request.ItemId);
        if (offer is null)
        {
            httpResponseUtil.AppendErrorToOutput(
                output,
                serverLocalisationService.GetText("ragfair-offer_no_longer_exists"));
            return output;
        }

        if (request.Count != 1)
        {
            httpResponseUtil.AppendErrorToOutput(output, "Shared storage offers must be retrieved one at a time");
            return output;
        }

        var restoredTree = cloner.Clone(offer.Items)
            ?? throw new InvalidOperationException($"Unable to clone stored offer {offer.OfferId}");
        restoredTree.ReplaceIDs();
        restoredTree[0].ParentId = "hideout";
        restoredTree[0].SlotId = "hideout";
        restoredTree[0].Location = null;
        var originalFoundInRaid = restoredTree.ToDictionary(
            item => item.Id,
            item => item.Upd?.SpawnedInSession);

        if (!inventoryHelper.CanPlaceItemsInInventory(sessionId, [restoredTree]))
        {
            httpResponseUtil.AppendErrorToOutput(
                output,
                serverLocalisationService.GetText("inventory-no_stash_space"),
                BackendErrorCodes.NotEnoughSpace);
            return output;
        }

        request.SchemeItems =
        [
            new IdWithCount
            {
                Id = Money.ROUBLES,
                Count = 1
            }
        ];

        var warningsBeforePayment = output.Warnings?.Count ?? 0;
        paymentService.PayMoney(pmcData, request, sessionId, output);
        if ((output.Warnings?.Count ?? 0) > warningsBeforePayment)
        {
            return output;
        }

        inventoryHelper.AddItemsToStash(
            sessionId,
            new AddItemsDirectRequest
            {
                ItemsWithModsToAdd = [restoredTree],
                FoundInRaid = restoredTree[0].Upd?.SpawnedInSession == true,
                UseSortingTable = false,
                Callback = _ =>
                {
                    if (!storageService.RemoveOffer(offer.OfferId))
                    {
                        throw new InvalidOperationException($"Shared storage offer {offer.OfferId} was already removed");
                    }
                }
            },
            pmcData,
            output);

        var profileItems = pmcData.Inventory?.Items
            ?? throw new InvalidOperationException("Player inventory became unavailable during Vault retrieval");
        foreach (var restoredItem in profileItems.Where(item => originalFoundInRaid.ContainsKey(item.Id)))
        {
            restoredItem.Upd ??= new Upd();
            restoredItem.Upd.SpawnedInSession = originalFoundInRaid[restoredItem.Id];
        }

        if (storageService.GetOffer(offer.OfferId) is null)
        {
            logger.Info($"Player {sessionId} retrieved shared offer {offer.OfferId}");
        }

        return output;
    }
}
