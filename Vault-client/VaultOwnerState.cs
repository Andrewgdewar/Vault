using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using SPT.Common.Http;

namespace VaultOwnerFilter;

internal static class VaultOwnerState
{
    internal const string VaultTraderId = "687600000000000000000001";

    private static readonly Dictionary<string, string> OfferOwners = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> OfferStackCounts = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsVaultActive { get; private set; }
    internal static string CurrentProfileId { get; private set; } = string.Empty;
    internal static string? SelectedOwnerId { get; private set; }
    internal static IReadOnlyList<VaultOwner> Owners { get; private set; } = Array.Empty<VaultOwner>();

    internal static void SetActive(bool active)
    {
        IsVaultActive = active;
        if (!active)
        {
            SelectedOwnerId = null;
        }
    }

    internal static bool Refresh()
    {
        try
        {
            var json = RequestHandler.GetJson("/vault/owners");
            var response = JsonConvert.DeserializeObject<SptResponse<VaultOwnerResponse>>(json)?.Data;
            if (response is null)
            {
                throw new InvalidOperationException("Vault owner endpoint returned no data");
            }

            CurrentProfileId = response.CurrentProfileId;
            Owners = response.Owners
                .OrderBy(owner => owner.SellerNickname, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            OfferOwners.Clear();
            OfferStackCounts.Clear();
            foreach (var owner in Owners)
            {
                foreach (var offerId in owner.OfferIds)
                {
                    OfferOwners[offerId] = owner.SellerProfileId;
                }
            }

            foreach (var pair in response.OfferStackCounts)
            {
                OfferStackCounts[pair.Key] = pair.Value;
            }

            if (SelectedOwnerId is not null && Owners.All(owner => !owner.SellerProfileId.Equals(SelectedOwnerId, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedOwnerId = null;
            }

            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError($"Unable to refresh Vault owners: {exception}");
            OfferOwners.Clear();
            OfferStackCounts.Clear();
            Owners = Array.Empty<VaultOwner>();
            SelectedOwnerId = null;
            return false;
        }
    }

    internal static void SelectOwner(string? ownerId)
    {
        SelectedOwnerId = ownerId;
    }

    internal static IEnumerable<Item> Filter(IEnumerable<Item> items)
    {
        if (!IsVaultActive || SelectedOwnerId is null)
        {
            return items;
        }

        return items.Where(item =>
            OfferOwners.TryGetValue(item.Id.ToString(), out var ownerId)
            && ownerId.Equals(SelectedOwnerId, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool TryGetStackCount(string offerId, out int stackCount)
    {
        stackCount = 1;
        return IsVaultActive && OfferStackCounts.TryGetValue(offerId, out stackCount);
    }
}
