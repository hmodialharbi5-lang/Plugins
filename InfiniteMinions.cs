using System;
using System.Collections.Generic;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace InfiniteMinions
{
    [ApiVersion(2, 1)]
    public class InfiniteMinions : TerrariaPlugin
    {
        public override string Name => "InfiniteMinions";
        public override string Author => "Grok";
        public override string Description => "Allows players to have a very high minion and sentry limit";
        public override Version Version => new Version(1, 0, 0);

        // Players who currently have infinite minions enabled
        private readonly HashSet<int> EnabledPlayers = new();

        // How many minions / sentries you want to allow
        private const int DesiredMaxMinions = 100;
        private const int DesiredMaxTurrets = 50;

        public InfiniteMinions(Main game) : base(game) { }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(
                permissions: new List<string> { "infiniteminions.use" },
                cmd: ToggleInfinite,
                "infminions", "infminion", "im"
            )
            {
                HelpText = "Toggle infinite minions for yourself. Usage: /infminions"
            });

            // Keep forcing the high limit every tick for enabled players
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);

            TShock.Log.ConsoleInfo("[InfiniteMinions] Loaded successfully!");
        }

        private void ToggleInfinite(CommandArgs args)
        {
            int index = args.Player.Index;

            if (EnabledPlayers.Contains(index))
            {
                EnabledPlayers.Remove(index);
                args.Player.SendSuccessMessage("Infinite minions disabled.");
            }
            else
            {
                EnabledPlayers.Add(index);
                args.Player.SendSuccessMessage($"Infinite minions enabled (max {DesiredMaxMinions} minions / {DesiredMaxTurrets} sentries).");
            }
        }

        private void OnUpdate(EventArgs args)
        {
            // Clean up disconnected players
            EnabledPlayers.RemoveWhere(index =>
            {
                var p = TShock.Players[index];
                return p == null || !p.Active;
            });

            foreach (int index in EnabledPlayers)
            {
                var tsPlayer = TShock.Players[index];
                if (tsPlayer == null || !tsPlayer.Active)
                    continue;

                var player = tsPlayer.TPlayer;

                // Force high limits
                player.maxMinions = DesiredMaxMinions;
                player.maxTurrets = DesiredMaxTurrets;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                Commands.ChatCommands.RemoveAll(c =>
                    c.CommandDelegate.Method?.DeclaringType?.Assembly == this.GetType().Assembly);
            }
            base.Dispose(disposing);
        }
    }
}
