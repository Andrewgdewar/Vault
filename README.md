# Vault

Vault is an SPT 4.0.13 shared-storage trader. Players deposit complete item trees for 1 rouble and any player can retrieve an offer for 1 rouble.

<p align="center">
	<img src="docs/images/vault-door.png" alt="Vault trader door" width="420">
</p>

Vault gives a group one persistent, server-wide place to exchange equipment. Sell an item to Vault to place it in shared storage; another player can then retrieve that exact item for 1 rouble.

## Features

- Server-wide shared trader inventory persisted to a real JSON file.
- Exact attachments, contents, durability, resources, stack quantities, and FIR state are preserved.
- Stackable items remain one atomic offer and consume one storage slot.
- Maximum 20 active root offers per player; purchases immediately free capacity.
- Roubles, dollars, euros, and containers holding currency are rejected.
- Owner dropdown combines with native trader category filters and shows per-player counts.
- Owner-only FIR display marker in Vault.
- Vault is excluded from the flea market.

## In Game

### Shared Inventory And Owner Filter

![Vault trader inventory with owner dropdown](docs/images/dropdown.png)

The owner dropdown filters the shared assortment without replacing the normal item-category filters:

- **All owners** shows every item currently stored in Vault.
- **Mine** shows only items deposited by the current profile.
- Each player entry shows their active offer count against the 20-item limit.
- The selection combines with native categories, so users can view one player's meds, weapons, ammunition, or other item groups.
- Stackable items show their original quantity but remain one atomic Vault offer and consume only one storage slot.

Items deposited by the current profile receive an FIR checkmark while viewed in Vault. This is a display-only ownership marker; the item's actual FIR state is preserved in storage and restored when purchased.

### Per-Player Storage Limit

![Vault storage limit error](docs/images/error.png)

Each player may have up to 20 active root offers in Vault. A stack of ammunition, a weapon with attachments, or a container with contents each counts as one root offer. If a deposit would exceed the limit, the entire transaction is rejected before any items leave the player's inventory.

When another player purchases one of your offers, that slot becomes available immediately.

## Install

1. Download `Vault-v0.1.1.zip` from GitHub Releases.
2. Open the zip and drag `BepInEx` and `SPT` into the root of the SPT game installation.
3. Start the SPT server and client normally.

The archive installs:

```text
BepInEx/plugins/Vault/Vault-client.dll
SPT/user/mods/Vault/Vault-server.dll
SPT/user/mods/Vault/assets/vault-door.png
SPT/user/mods/Vault/README.md
```

Vault creates this persistent file on first server start:

```text
SPT/user/mods/Vault/data/shared-storage.json
```

Release archives deliberately omit that JSON file so updating Vault cannot overwrite stored items.

The client plugin is UI-only and is not required on a dedicated headless host.

## Source Layout

```text
Vault-client/  BepInEx owner filter and stack-count display
Vault-server/  SPT trader, persistence, transactions, and owner endpoint
```

## Build

Requirements:

- .NET 9 SDK
- An SPT 4.0.13 installation

```powershell
.\build-release.ps1 -TarkovDir "C:\SPT" -Version "0.1.1"
```

The direct-drop zip and checksum are written to `artifacts/`.

Build projects independently:

```powershell
dotnet build .\Vault-server\Vault-server.csproj -c Release
dotnet build .\Vault-client\Vault-client.csproj -c Release -p:TarkovDir="C:\SPT\"
```

## License

MIT
