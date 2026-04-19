using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

using RetakesPlugin.Utils;

namespace RetakesPlugin.Services;

public class SoloBotService
{
    public void Reconcile()
    {
        var players = Utilities.GetPlayers();
        var humanPlayers = players
            .Where(player => PlayerHelper.IsValid(player) && !player.IsBot && player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist)
            .ToList();
        var botPlayers = players
            .Where(player => PlayerHelper.IsValid(player) && player.IsBot && !player.IsHLTV)
            .ToList();

        if (humanPlayers.Count == 1 && botPlayers.Count == 0)
        {
            Server.ExecuteCommand("bot_difficulty 3");
            Server.ExecuteCommand("bot_quota_mode fill");
            Server.ExecuteCommand("bot_quota 1");
            Server.ExecuteCommand("bot_join_after_player 0");
            Server.ExecuteCommand("bot_allow_grenades 1");
            Server.ExecuteCommand("bot_allow_pistols 1");
            Server.ExecuteCommand("bot_allow_sub_machine_guns 1");
            Server.ExecuteCommand("bot_allow_rifles 1");
            Server.ExecuteCommand("bot_allow_snipers 1");
            Server.ExecuteCommand("bot_allow_shotguns 1");
            Server.ExecuteCommand("bot_allow_machine_guns 1");
            Server.ExecuteCommand("bot_dont_shoot 0");
            Server.ExecuteCommand("bot_add");
            Logger.LogInfo("SoloBot", "Adicionado bot expert temporario para acompanhar unico jogador humano.");
            return;
        }

        if (humanPlayers.Count >= 2 && botPlayers.Count > 0)
        {
            foreach (var bot in botPlayers)
            {
                Server.ExecuteCommand($"kick \"{bot.PlayerName}\"");
            }

            Server.ExecuteCommand("bot_quota 0");
            Logger.LogInfo("SoloBot", $"Removidos {botPlayers.Count} bots temporarios apos entrada de novos jogadores.");
        }
    }
}
