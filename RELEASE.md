# Vault v0.1.1

Patch release for SPT 4.0.13.

Vault now advertises 10% of normal trader sale value so automatic best-price selling mods do not select it by default. Vault deposits still pay exactly 1 rouble per root item, and retrieval still costs 1 rouble.

Download `artifacts/Vault-v0.1.1.zip`, open it, and drag the included `BepInEx` and `SPT` folders into the root of an SPT installation.

Runtime layout:

```text
BepInEx/plugins/Vault/Vault-client.dll
SPT/user/mods/Vault/Vault-server.dll
SPT/user/mods/Vault/assets/vault-door.png
SPT/user/mods/Vault/README.md
SPT/user/mods/Vault/LICENSE
```

Persistent `data/shared-storage.json` is intentionally absent from the archive and is created on first server start.
