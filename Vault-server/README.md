# Vault

SPT 4.0.13 server mod that adds one server-wide storage trader.

- Selling a root item stores its complete item tree in `data/shared-storage.json`.
- The trader pays 1 rouble for each sold root item.
- Every stored offer costs 1 rouble to retrieve.
- Offers are shared by all profiles connected to the server.
- Offers deposited by the current profile receive an FIR checkmark only in that profile's Vault assort response.
- The `/vault/owners` endpoint exposes offer ownership for the optional client-side seller filter.
- Stackable offers remain one atomic Vault offer while the client displays their canonical stored stack quantity.
- Each offer has one stock. A stale purchase receives SPT's standard "offer no longer exists" warning.
- Vault is not listed on or synchronized with the flea market.
- Vault rejects roubles, dollars, euros, and containers holding currency.
- Each profile may have at most 20 active root offers stored. Purchased offers immediately free capacity for that seller.
- Stored item trees retain attachments, contents, durability, resources, stack counts, and each item's original FIR state. Retrieved items receive new IDs.

The JSON file is canonical persistent state and is rewritten atomically after each successful storage change.
