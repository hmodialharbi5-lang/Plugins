using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace InfiniteMinions;

[ApiVersion(2, 1)]
public sealed class InfiniteMinionsPlugin : TerrariaPlugin
{
    private const string UsePermission = "minions.use";
    private const string OthersPermission = "minions.others";
    private const string AdminPermission = "minions.admin";
    private const int MaxMinionLimit = 1000;

    private readonly List<Command> registeredCommands = new();
    private readonly Dictionary<string, int> limits = new(StringComparer.OrdinalIgnoreCase);

    private string configPath = string.Empty;

    public override string Name => "InfiniteMinions";
    public override string Author => "OpenAI";
    public override string Description => "Per-player increased minion limits for TShock 6.x.";
    public override Version Version => new(1, 0, 1);

    public InfiniteMinionsPlugin(Main game) : base(game)
    {
    }

    public override void Initialize()
    {
        LoadConfig();
        ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
            ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            DeregisterCommands();
            SaveConfig();
        }

        base.Dispose(disposing);
    }

    private void OnGameInitialize(EventArgs args)
    {
        RegisterCommands();
    }

    private void RegisterCommands()
    {
        if (registeredCommands.Count > 0)
            return;

        var command = new Command(UsePermission, MinionsCommand, "minions", "minion")
        {
            HelpText = "/minions [amount|set|add|remove|reset|get|list] [player] [amount]"
        };

        Commands.ChatCommands.Add(command);
        registeredCommands.Add(command);
    }

    private void DeregisterCommands()
    {
        foreach (var command in registeredCommands)
            Commands.ChatCommands.Remove(command);

        registeredCommands.Clear();
    }

    private void OnGameUpdate(EventArgs args)
    {
        for (var i = 0; i < TShock.Players.Length; i++)
        {
            var tsPlayer = TShock.Players[i];
            if (tsPlayer == null || !tsPlayer.Active)
                continue;

            if (!limits.TryGetValue(Normalize(tsPlayer.Name), out var limit))
                continue;

            tsPlayer.TPlayer.maxMinions = limit;
        }
    }

    private void MinionsCommand(CommandArgs args)
    {
        if (args.Parameters.Count == 0)
        {
            if (!RequireInGame(args))
                return;

            var name = args.Player.Name;
            var limit = GetLimitForPlayer(name, args.Player.TPlayer.maxMinions);
            args.Player.SendInfoMessage($"Your custom minion limit is {limit:n0}.");
            return;
        }

        var first = args.Parameters[0];

        if (TryParseAmount(first, out var selfAmount))
        {
            if (!RequireInGame(args))
                return;

            if (selfAmount > MaxMinionLimit)
            {
                args.Player.SendErrorMessage($"The maximum minion limit is {MaxMinionLimit}.");
                return;
            }

            SetLimit(args.Player.Name, selfAmount);
            args.Player.SendSuccessMessage($"Your custom minion limit is now {selfAmount:n0}.");
            ApplyImmediately(args.Player, selfAmount);
            return;
        }

        switch (first.ToLowerInvariant())
        {
            case "off":
            case "reset":
                if (args.Parameters.Count == 1 && !RequireInGame(args))
                    return;

                if (!CanManageOthers(args.Player, args.Parameters.Count > 1 ? args.Parameters[1] : null))
                    return;

                var resetTarget = ResolveTarget(args, args.Parameters.Count > 1 ? args.Parameters[1] : null);
                if (resetTarget == null)
                    return;

                RemoveLimit(resetTarget.Name);
                // Do not force a guessed vanilla value here. Terraria will recalculate maxMinions normally.
                resetTarget.SendSuccessMessage("Your custom minion limit has been reset. Terraria will restore the normal limit.");
                args.Player.SendSuccessMessage($"Reset custom minion limit for {resetTarget.Name}.");
                return;

            case "set":
            case "add":
            case "remove":
            case "get":
                HandleSubcommand(args, first.ToLowerInvariant());
                return;

            case "list":
                if (!Has(args.Player, AdminPermission))
                {
                    args.Player.SendErrorMessage("You need the minions.admin permission.");
                    return;
                }

                if (limits.Count == 0)
                {
                    args.Player.SendInfoMessage("No custom minion limits are configured.");
                    return;
                }

                args.Player.SendInfoMessage("Configured minion limits:");
                foreach (var entry in limits.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
                    args.Player.SendInfoMessage($"- {entry.Key}: {FormatLimit(entry.Value)}");
                return;

            default:
                args.Player.SendInfoMessage($"/minions [amount (0-{MaxMinionLimit})|off]");
                args.Player.SendInfoMessage("/minions set <player> <amount>");
                args.Player.SendInfoMessage("/minions add <player> <amount>");
                args.Player.SendInfoMessage("/minions remove <player> <amount>");
                args.Player.SendInfoMessage("/minions reset <player>");
                args.Player.SendInfoMessage("/minions get <player>");
                args.Player.SendInfoMessage("/minions list");
                return;
        }
    }

    private void HandleSubcommand(CommandArgs args, string subcommand)
    {
        if (subcommand == "get")
        {
            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Usage: /minions get <player>");
                return;
            }

            if (!CanManageOthers(args.Player, args.Parameters[1]))
                return;

            var target = ResolveTarget(args, args.Parameters[1]);
            if (target == null)
                return;

            var value = GetStoredOrCurrentLimit(target);
            args.Player.SendInfoMessage($"{target.Name}'s custom minion limit is {FormatLimit(value)}.");
            return;
        }

        if (args.Parameters.Count < 3)
        {
            args.Player.SendErrorMessage($"Usage: /minions {subcommand} <player> <amount>");
            return;
        }

        if (!CanManageOthers(args.Player, args.Parameters[1]))
            return;

        if (!TryParseAmount(args.Parameters[2], out var amount))
        {
            args.Player.SendErrorMessage($"Amount must be a whole number from 0 to {MaxMinionLimit}.");
            return;
        }

        var targetPlayer = ResolveTarget(args, args.Parameters[1]);
        if (targetPlayer == null)
            return;

        var current = GetStoredOrCurrentLimit(targetPlayer);
        int result;

        switch (subcommand)
        {
            case "set":
                result = amount;
                break;
            case "add":
                result = SafeAdd(current, amount);
                break;
            case "remove":
                result = Math.Max(0, SafeSubtract(current, amount));
                break;
            default:
                return;
        }

        SetLimit(targetPlayer.Name, result);
        ApplyImmediately(targetPlayer, result);
        args.Player.SendSuccessMessage($"{targetPlayer.Name}'s custom minion limit is now {FormatLimit(result)}.");
        if (targetPlayer != args.Player)
            targetPlayer.SendInfoMessage($"Your custom minion limit was changed to {FormatLimit(result)} by {args.Player.Name}.");
    }

    private TSPlayer? ResolveTarget(CommandArgs args, string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            if (args.Player == null || args.Player.Index < 0)
            {
                args.Player.SendErrorMessage("Specify a player name.");
                return null;
            }

            return args.Player;
        }

        var matches = TSPlayer.FindByNameOrID(rawName);
        if (matches.Count != 1)
        {
            args.Player.SendErrorMessage($"Could not uniquely find player '{rawName}'.");
            return null;
        }

        return matches[0];
    }

    private bool CanManageOthers(TSPlayer actor, string? targetName)
    {
        var target = string.IsNullOrWhiteSpace(targetName) ? actor.Name : targetName;
        if (string.Equals(actor.Name, target, StringComparison.OrdinalIgnoreCase))
            return true;

        if (Has(actor, OthersPermission) || Has(actor, AdminPermission))
            return true;

        actor.SendErrorMessage("You need the minions.others permission to change another player.");
        return false;
    }

    private static bool Has(TSPlayer player, string permission)
    {
        return player != null && player.HasPermission(permission);
    }

    private static bool RequireInGame(CommandArgs args)
    {
        if (args.Player != null && args.Player.Index >= 0 && args.Player.Active)
            return true;

        args.Player.SendErrorMessage("This form of /minions must be used in-game.");
        return false;
    }

    private bool TryParseAmount(string value, out int amount)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)
            && amount >= 0
            && amount <= MaxMinionLimit;
    }

    private static int SafeAdd(int a, int b)
    {
        return Math.Min(MaxMinionLimit, a + b);
    }

    private static int SafeSubtract(int a, int b)
    {
        return Math.Max(0, a - b);
    }

    private int GetStoredOrCurrentLimit(TSPlayer player)
    {
        if (limits.TryGetValue(Normalize(player.Name), out var stored))
            return stored;

        return Math.Clamp(player.TPlayer.maxMinions, 0, MaxMinionLimit);
    }

    private int GetLimitForPlayer(string playerName, int vanillaCurrent)
    {
        if (limits.TryGetValue(Normalize(playerName), out var value))
            return value;

        return Math.Clamp(vanillaCurrent, 0, MaxMinionLimit);
    }

    private void SetLimit(string playerName, int amount)
    {
        limits[Normalize(playerName)] = Math.Clamp(amount, 0, MaxMinionLimit);
        SaveConfig();
    }

    private static void ApplyImmediately(TSPlayer player, int amount)
    {
        if (player?.TPlayer == null)
            return;

        player.TPlayer.maxMinions = Math.Clamp(amount, 0, MaxMinionLimit);
    }

    private void RemoveLimit(string playerName)
    {
        limits.Remove(Normalize(playerName));
        SaveConfig();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string FormatLimit(int value)
    {
        return value.ToString("n0", CultureInfo.InvariantCulture);
    }

    private void LoadConfig()
    {
        try
        {
            configPath = Path.Combine(TShock.SavePath, "InfiniteMinions.json");
            if (!File.Exists(configPath))
                return;

            var json = File.ReadAllText(configPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (data == null)
                return;

            limits.Clear();
            foreach (var entry in data)
            {
                if (entry.Value >= 0)
                    limits[Normalize(entry.Key)] = Math.Clamp(entry.Value, 0, MaxMinionLimit);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"InfiniteMinions: Failed to load config: {ex.Message}");
        }
    }

    private void SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(configPath))
            configPath = Path.Combine(TShock.SavePath, "InfiniteMinions.json");

        try
        {
            var json = JsonSerializer.Serialize(limits, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"InfiniteMinions: Failed to save config: {ex.Message}");
        }
    }
}
