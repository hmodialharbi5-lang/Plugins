# InfiniteMinions — TShock 6.1.0

Server-side TShock plugin for Terraria 1.4.5.6 that gives individual players a configurable minion limit.

## Target

- TShock 6.1.0
- Terraria 1.4.5.6
- .NET 9

## Commands

- `/minions` — show your custom limit
- `/minions 20` — set your own limit to 20
- `/minions 1000` — set your own limit to up to 1000
- `/minions off` — reset your own limit
- `/minions set <player> <amount>` — set another player's limit
- `/minions add <player> <amount>` — add to another player's limit
- `/minions remove <player> <amount>` — remove from another player's limit
- `/minions reset <player>` — reset another player's limit
- `/minions get <player>` — show another player's limit
- `/minions list` — list stored custom limits

## Permissions

- `minions.use` — use `/minions`
- `minions.others` — change another player's limit
- `minions.admin` — administrative access, including `/minions list`

Example permission command:

`/group add default minions.use`

## Build

Put this project folder beside your TShock server files so the layout is:

```text
TShockServer/
  TerrariaServer.dll
  OTAPI.dll
  ServerPlugins/
    TShockAPI.dll
  InfiniteMinions-6.1.0/
    InfiniteMinions.csproj
    InfiniteMinionsPlugin.cs
```

Then run:

```text
dotnet build -c Release
```

Or specify the server directory explicitly:

```text
dotnet build -c Release -p:TShockServerPath="C:/path/to/TShockServer"
```

Copy the resulting `InfiniteMinions.dll` into `ServerPlugins/` and restart TShock.

## Notes

The plugin stores player limits in `tshock/InfiniteMinions.json` using player names.

The maximum custom limit is **1000**. There is no `inf`/`int.MaxValue` mode. Removing a custom limit lets Terraria recalculate the normal minion limit from the player’s equipment and buffs.
