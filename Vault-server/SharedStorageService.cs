using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SharedStorageTrader;

[Injectable(InjectionType.Singleton)]
public sealed class SharedStorageService(
    ISptLogger<SharedStorageService> logger,
    DatabaseService databaseService,
    JsonUtil jsonUtil,
    FileUtil fileUtil,
    ICloner cloner)
{
    public static readonly MongoId TraderId = new("687600000000000000000001");
    public const int MaxOffersPerSeller = 20;

    public object SyncRoot { get; } = new();

    private SharedStorageData _data = new();
    private string _storagePath = string.Empty;

    public void Initialize(string storagePath)
    {
        lock (SyncRoot)
        {
            _storagePath = storagePath;
            _data = jsonUtil.DeserializeFromFile<SharedStorageData>(_storagePath) ?? new SharedStorageData();
            ValidateAndRebuildAssort();
            Persist();
            logger.Success($"Loaded {_data.Offers.Count} shared storage offer(s) from {_storagePath}");
        }
    }

    public StoredOffer? GetOffer(MongoId offerId)
    {
        return _data.Offers.FirstOrDefault(offer => offer.OfferId == offerId);
    }

    public int GetOfferCount(MongoId sellerProfileId)
    {
        lock (SyncRoot)
        {
            return _data.Offers.Count(offer => offer.SellerProfileId == sellerProfileId);
        }
    }

    public List<VaultOwner> GetOwners()
    {
        lock (SyncRoot)
        {
            return _data.Offers
                .GroupBy(offer => offer.SellerProfileId)
                .Select(group => new VaultOwner
                {
                    SellerProfileId = group.Key,
                    SellerNickname = group
                        .Select(offer => offer.SellerNickname)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                        ?? $"Player {group.Key.ToString()[..8]}",
                    OfferIds = group.Select(offer => offer.OfferId).ToList()
                })
                .OrderBy(owner => owner.SellerNickname, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public Dictionary<MongoId, int> GetOfferStackCounts()
    {
        lock (SyncRoot)
        {
            return _data.Offers.ToDictionary(
                offer => offer.OfferId,
                offer => Math.Max(1, (int)(offer.Items[0].Upd?.StackObjectsCount ?? 1)));
        }
    }

    public TraderAssort MarkOwnedOffers(TraderAssort assort, MongoId sessionId)
    {
        lock (SyncRoot)
        {
            var ownedOfferIds = _data.Offers
                .Where(offer => offer.SellerProfileId == sessionId)
                .Select(offer => offer.OfferId)
                .ToHashSet();

            foreach (var root in assort.Items.Where(item => item.ParentId == "hideout"))
            {
                var upd = root.Upd ??= new Upd();
                upd.SpawnedInSession = ownedOfferIds.Contains(root.Id);
            }

            return assort;
        }
    }

    public void AddOffers(IEnumerable<StoredOffer> offers)
    {
        var offersToAdd = offers.ToList();
        foreach (var sellerGroup in offersToAdd.GroupBy(offer => offer.SellerProfileId))
        {
            var currentCount = _data.Offers.Count(offer => offer.SellerProfileId == sellerGroup.Key);
            if (currentCount + sellerGroup.Count() > MaxOffersPerSeller)
            {
                throw new InvalidOperationException(
                    $"Vault storage limit exceeded for {sellerGroup.Key}: {currentCount}/{MaxOffersPerSeller} offers already stored");
            }
        }

        _data.Offers.AddRange(offersToAdd);
        ValidateAndRebuildAssort();
        Persist();
    }

    public bool RemoveOffer(MongoId offerId)
    {
        var removed = _data.Offers.RemoveAll(offer => offer.OfferId == offerId) > 0;
        if (!removed)
        {
            return false;
        }

        ValidateAndRebuildAssort();
        Persist();
        return true;
    }

    private void ValidateAndRebuildAssort()
    {
        var duplicate = _data.Offers
            .GroupBy(offer => offer.OfferId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate shared storage offer id: {duplicate.Key}");
        }

        foreach (var offer in _data.Offers)
        {
            if (offer.Items.Count == 0 || offer.Items[0].Id != offer.OfferId)
            {
                throw new InvalidDataException($"Shared storage offer {offer.OfferId} has an invalid item tree");
            }
        }

        var assort = new TraderAssort
        {
            Items = [],
            BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
            LoyalLevelItems = new Dictionary<MongoId, int>()
        };

        foreach (var offer in _data.Offers)
        {
            var displayItems = cloner.Clone(offer.Items)
                ?? throw new InvalidOperationException($"Unable to clone shared storage offer {offer.OfferId}");
            var root = displayItems[0];
            root.ParentId = "hideout";
            root.SlotId = "hideout";
            root.Location = null;
            var rootUpd = root.Upd ??= new Upd();
            rootUpd.StackObjectsCount = 1;
            rootUpd.UnlimitedCount = false;
            rootUpd.BuyRestrictionCurrent = null;
            rootUpd.BuyRestrictionMax = null;

            assort.Items.AddRange(displayItems);
            assort.BarterScheme[offer.OfferId] =
            [
                [
                    new BarterScheme
                    {
                        Count = 1,
                        Template = Money.ROUBLES
                    }
                ]
            ];
            assort.LoyalLevelItems[offer.OfferId] = 1;
        }

        var trader = databaseService.GetTrader(TraderId)
            ?? throw new InvalidOperationException("Shared Storage trader was not registered");
        trader.Assort = assort;
    }

    private void Persist()
    {
        if (string.IsNullOrWhiteSpace(_storagePath))
        {
            throw new InvalidOperationException("Shared storage has not been initialized");
        }

        var json = jsonUtil.Serialize(_data, indented: true)
            ?? throw new InvalidOperationException("Unable to serialize shared storage data");
        fileUtil.WriteFileAsync(_storagePath, json).GetAwaiter().GetResult();
    }
}
