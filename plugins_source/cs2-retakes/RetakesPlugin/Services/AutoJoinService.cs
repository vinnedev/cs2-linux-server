using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace RetakesPlugin.Services;

public class AutoJoinService
{
    private const int MaxAutoJoinAttempts = 8;
    private const float RetryDelaySeconds = 1.0f;

    private readonly RetakesPlugin _plugin;
    private readonly HashSet<ulong> _welcomedPlayers = [];
    private readonly HashSet<ulong> _pendingAutoJoin = [];
    private readonly Dictionary<ulong, int> _autoJoinAttempts = [];

    public AutoJoinService(RetakesPlugin plugin)
    {
        _plugin = plugin;
    }

    public void OnMapStart()
    {
        _pendingAutoJoin.Clear();
        _autoJoinAttempts.Clear();
        _welcomedPlayers.Clear();
        ScheduleBotReconcile(1.0f);
    }

    public void OnPlayerConnectFull(CCSPlayerController player)
    {
        // Give Retakes connect flow time to finish before requesting join.
        ScheduleAutoJoin(player, 3.0f);
    }

    public void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_welcomedPlayers.Add(player.SteamID))
        {
            player.PrintToChat("\x04[ABREU] \x01Bem vindo ao servidor!");
            player.PrintToChat($"\x01Ola, \x03{player.PlayerName}\x01! Personalize suas armas com \x02!ws\x01.");
        }

        if (player.Team is CsTeam.Spectator or CsTeam.None)
        {
            ScheduleAutoJoin(player, 1.0f);
        }
    }

    public void OnPlayerTeam(CCSPlayerController player, CsTeam toTeam)
    {
        if (toTeam is CsTeam.Spectator or CsTeam.None)
        {
            ScheduleAutoJoin(player, 1.0f);
            return;
        }

        if (toTeam is CsTeam.Terrorist or CsTeam.CounterTerrorist)
        {
            _autoJoinAttempts.Remove(player.SteamID);
            ScheduleBotReconcile(2.5f);
        }
    }

    public void OnPlayerDisconnect(CCSPlayerController player)
    {
        _pendingAutoJoin.Remove(player.SteamID);
        _autoJoinAttempts.Remove(player.SteamID);
        _welcomedPlayers.Remove(player.SteamID);
        ScheduleBotReconcile(1.0f);
    }

    private void ScheduleAutoJoin(CCSPlayerController player, float delaySeconds)
    {
        if (!ShouldHandle(player))
        {
            return;
        }

        if (!_autoJoinAttempts.ContainsKey(player.SteamID))
        {
            _autoJoinAttempts[player.SteamID] = 0;
        }

        if (_pendingAutoJoin.Contains(player.SteamID))
        {
            return;
        }

        _pendingAutoJoin.Add(player.SteamID);

        _plugin.AddTimer(delaySeconds, () =>
        {
            _pendingAutoJoin.Remove(player.SteamID);

            if (!ShouldHandle(player))
            {
                return;
            }

            if (player.Team is not (CsTeam.Spectator or CsTeam.None))
            {
                _autoJoinAttempts.Remove(player.SteamID);
                return;
            }

            var attempts = _autoJoinAttempts[player.SteamID] + 1;
            _autoJoinAttempts[player.SteamID] = attempts;

            player.ExecuteClientCommand($"jointeam {(int)GetSmallerTeam()}");

            if (attempts < MaxAutoJoinAttempts)
            {
                ScheduleAutoJoin(player, RetryDelaySeconds);
            }
            else
            {
                _autoJoinAttempts.Remove(player.SteamID);
            }
        });
    }

    private void ScheduleBotReconcile(float delaySeconds)
    {
        _plugin.AddTimer(delaySeconds, ReconcileBots);
    }

    private static void ReconcileBots()
    {
        var humans = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && (p.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist))
            .ToList();

        var bots = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.IsBot && (p.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist))
            .ToList();

        if (humans.Count == 0)
        {
            if (bots.Count > 0)
            {
                Server.ExecuteCommand("bot_kick");
            }

            return;
        }

        if (humans.Count == 1)
        {
            var human = humans[0];
            var desiredBotTeam = human.Team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
            var botsOnDesiredTeam = bots.Count(bot => bot.Team == desiredBotTeam);

            if (bots.Count > 1 || bots.Any(bot => bot.Team != desiredBotTeam))
            {
                Server.ExecuteCommand("bot_kick");
                botsOnDesiredTeam = 0;
            }

            if (botsOnDesiredTeam == 0)
            {
                Server.ExecuteCommand("bot_quota 1");
                Server.ExecuteCommand(desiredBotTeam == CsTeam.Terrorist ? "bot_add_t" : "bot_add_ct");
            }

            return;
        }

        if (bots.Count > 0)
        {
            Server.ExecuteCommand("bot_kick");
        }
    }

    private static bool ShouldHandle(CCSPlayerController? player)
    {
        return player != null && player.IsValid && !player.IsBot && !player.IsHLTV;
    }

    private static CsTeam GetSmallerTeam()
    {
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV)
            .ToList();

        var ctCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
        var tCount = players.Count(p => p.Team == CsTeam.Terrorist);
        return tCount <= ctCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
    }
}
