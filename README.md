# InfiniteMinions - TShock Plugin

Allows players to bypass the normal minion/sentry limit by forcing a high `maxMinions` and `maxTurrets` value.

## Features
- Toggle infinite (high) minion slots with `/infminions` (aliases: `/infminion`, `/im`)
- Configurable limits (currently hardcoded to 100 minions / 50 sentries)
- Permission-based: `infiniteminions.use`

## Installation
1. Build the project (see below) or use a pre-built DLL.
2. Place `InfiniteMinions.dll` into your TShock `ServerPlugins` folder.
3. Restart the server.
4. Give the permission:
   ```
   /group addperm default infiniteminions.use
   ```
   (or only to specific groups)

## Building
You need the following DLLs from your TShock installation in a `lib/` folder next to the `.csproj`:
- TShockAPI.dll
- OTAPI.dll (or OTAPI.Runtime.dll)
- TerrariaServer.dll

Then run:
```bash
dotnet build -c Release
```

The output DLL will be in `bin/Release/net9.0/InfiniteMinions.dll`.

## Notes
- Extremely high minion counts can hit Terraria's global projectile limit (~1000) and cause lag.
- Some special minions (Stardust Dragon, etc.) may behave unexpectedly at high counts.
- The client also has some limit checks; results are best on a properly configured server.

## Commands
| Command          | Permission              | Description                     |
|------------------|-------------------------|---------------------------------|
| /infminions      | infiniteminions.use     | Toggle infinite minions on/off  |
| /im              | infiniteminions.use     | Same as above                   |

