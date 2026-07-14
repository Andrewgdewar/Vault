using System.Collections.Generic;
using Newtonsoft.Json;

namespace VaultOwnerFilter;

internal sealed class SptResponse<T>
{
    [JsonProperty("data")]
    public T? Data { get; set; }
}

internal sealed class VaultOwnerResponse
{
    [JsonProperty("currentProfileId")]
    public string CurrentProfileId { get; set; } = string.Empty;

    [JsonProperty("owners")]
    public List<VaultOwner> Owners { get; set; } = new();

    [JsonProperty("offerStackCounts")]
    public Dictionary<string, int> OfferStackCounts { get; set; } = new();
}

internal sealed class VaultOwner
{
    [JsonProperty("sellerProfileId")]
    public string SellerProfileId { get; set; } = string.Empty;

    [JsonProperty("sellerNickname")]
    public string SellerNickname { get; set; } = string.Empty;

    [JsonProperty("offerIds")]
    public List<string> OfferIds { get; set; } = new();
}
