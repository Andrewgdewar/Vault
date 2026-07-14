using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SharedStorageTrader;

public sealed record SharedStorageData
{
    public int Version { get; init; } = 1;
    public List<StoredOffer> Offers { get; init; } = [];
}

public sealed record StoredOffer
{
    public required MongoId OfferId { get; init; }
    public required MongoId SellerProfileId { get; init; }
    public string? SellerNickname { get; init; }
    public required DateTime StoredAtUtc { get; init; }
    public required List<Item> Items { get; init; }
}

public sealed record VaultOwner
{
    public required MongoId SellerProfileId { get; init; }
    public required string SellerNickname { get; init; }
    public required List<MongoId> OfferIds { get; init; }
}

public sealed record VaultOwnerResponse
{
    public required MongoId CurrentProfileId { get; init; }
    public required List<VaultOwner> Owners { get; init; }
    public required Dictionary<MongoId, int> OfferStackCounts { get; init; }
}
