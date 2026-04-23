using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace AutojoinPlugin;

public class AutojoinPlugin : BasePlugin
{
    public override string ModuleName => "AutojoinPlugin";
    public override string ModuleVersion => "1.2.2";

    private const int MaxAutoJoinAttempts = 8;
    private const float AutoJoinRetryDelaySeconds = 1.0f;

    private readonly HashSet<ulong> _welcomedPlayers = [];
    private readonly HashSet<ulong> _pendingAutoJoin = [];
    private readonly Dictionary<ulong, int> _autoJoinAttempts = [];

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        Logger.LogInformation("[AutojoinPlugin] Loaded");
        Server.PrintToConsole("[AutojoinPlugin] Loaded");
    }

    private void OnMapStart(string mapName)
    {
        _pendingAutoJoin.Clear();
        _autoJoinAttempts.Clear();
        Server.PrintToConsole($"[AutojoinPlugin] Map started: {mapName}");
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!ShouldHandle(player))
            return HookResult.Continue;

        // Wait a little longer so Retakes finishes its own connect/setup flow first.
        ScheduleAutoJoin(player!, 3.5f);
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!ShouldHandle(player))
            return HookResult.Continue;

        if (player!.Team is CsTeam.Spectator or CsTeam.None)
        {
            ScheduleAutoJoin(player, 1.0f);
        }

        if (_welcomedPlayers.Add(player.SteamID))
        {
            player.PrintToChat("\x04[ABREU] \x01Bem-vindo ao servidor!");
            player.PrintToChat($"\x01Olá, \x03{player.PlayerName}\x01! Personalize suas armas com \x02!ws\x01.");
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!ShouldHandle(player))
            return HookResult.Continue;

        if ((CsTeam)@event.Team is CsTeam.Spectator or CsTeam.None)
        {
            ScheduleAutoJoin(player!, 1.0f);
            return HookResult.Continue;
        }

        if ((CsTeam)@event.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist)
        {
            // Reconcile bots only after the player is really in an active team.
            ScheduleBotReconcile(2.5f);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
        {
            _pendingAutoJoin.Remove(player.SteamID);
            _autoJoinAttempts.Remove(player.SteamID);
        }

        ScheduleBotReconcile(1.0f);
        return HookResult.Continue;
    }

    private void ScheduleAutoJoin(CCSPlayerController player, float delaySeconds)
    {
        if (!_autoJoinAttempts.ContainsKey(player.SteamID))
        {
            _autoJoinAttempts[player.SteamID] = 0;
        }

        if (_pendingAutoJoin.Contains(player.SteamID))
            return;

        _pendingAutoJoin.Add(player.SteamID);

        AddTimer(delaySeconds, () =>
        {
            _pendingAutoJoin.Remove(player.SteamID);

            if (!ShouldHandle(player))
                return;

            if (player.Team is not (CsTeam.Spectator or CsTeam.None))
            {
                _autoJoinAttempts.Remove(player.SteamID);
                return;
            }

            var attempts = _autoJoinAttempts[player.SteamID] + 1;
            _autoJoinAttempts[player.SteamID] = attempts;

            var targetTeam = GetSmallerTeam();

            // Use the client's command path so Retakes can process its team/queue logic.
            player.ExecuteClientCommand($"jointeam {(int)targetTeam}");
            Logger.LogInformation("[AutojoinPlugin] Requested jointeam {Team} for {Player} (attempt {Attempt}/{MaxAttempts})", targetTeam, player.PlayerName, attempts, MaxAutoJoinAttempts);

            if (attempts < MaxAutoJoinAttempts)
            {
                ScheduleAutoJoin(player, AutoJoinRetryDelaySeconds);
            }
            else
            {
                Logger.LogWarning("[AutojoinPlugin] Failed to move {Player} out of spectator after {MaxAttempts} attempts", player.PlayerName, MaxAutoJoinAttempts);
                _autoJoinAttempts.Remove(player.SteamID);
            }
        });
    }

    private void ScheduleBotReconcile(float delaySeconds)
    {
        AddTimer(delaySeconds, ReconcileBots);
    }

    private void ReconcileBots()
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
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV).ToList();
        var ctCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
        var tCount = players.Count(p => p.Team == CsTeam.Terrorist);
        return tCount <= ctCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
    }
}
