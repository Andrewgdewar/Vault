using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace SharedStorageTrader;

[Injectable]
public sealed class VaultOwnerRouter(
    JsonUtil jsonUtil,
    SharedStorageService storageService,
    ProfileHelper profileHelper,
    HttpResponseUtil httpResponseUtil)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/vault/owners",
                (url, info, sessionId, output) =>
                    new ValueTask<string>(httpResponseUtil.GetBody(new VaultOwnerResponse
                    {
                        CurrentProfileId = sessionId,
                        OfferStackCounts = storageService.GetOfferStackCounts(),
                        Owners = storageService.GetOwners()
                            .Select(owner => owner with
                            {
                                SellerNickname = profileHelper
                                    .GetFullProfile(owner.SellerProfileId)?
                                    .CharacterData?
                                    .PmcData?
                                    .Info?
                                    .Nickname
                                    ?? owner.SellerNickname
                            })
                            .ToList()
                    })))
        ])
{
}