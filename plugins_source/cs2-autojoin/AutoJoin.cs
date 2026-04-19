using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace AutojoinPlugin;

public class AutojoinPlugin : BasePlugin
{
    public override string ModuleName => "AutojoinPlugin";
    public override string ModuleVersion => "1.1.0";

    private readonly HashSet<ulong> _welcomedPlayers = new();

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        Console.WriteLine("[AutojoinPlugin] Plugin carregado com sucesso!");
    }

    private HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        var botToKick = Utilities.GetPlayers().FirstOrDefault(p => p.IsBot);
        if (botToKick != null)
            Server.ExecuteCommand($"kick \"{botToKick.PlayerName}\"");

        AddTimer(0.5f, () => AssignTeam(player));

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        if (!_welcomedPlayers.Contains(player.SteamID))
        {
            _welcomedPlayers.Add(player.SteamID);
            player.PrintToChat("\x04[ABREU] \x01Bem-vindo ao servidor!");
            player.PrintToChat($"\x01Olá, \x03{player.PlayerName}\x01! Personalize suas armas com \x02!ws\x01.");
        }

        if (player.Team is CsTeam.Spectator or CsTeam.None)
            AssignTeam(player);

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        AddTimer(0.1f, () =>
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsBot || !player.IsValid)
                    continue;

                if (player.Team is not (CsTeam.Spectator or CsTeam.None))
                    continue;

                AssignTeam(player);
                AddTimer(0.5f, () => ForceRespawn(player));
            }
        });

        return HookResult.Continue;
    }

    private void AssignTeam(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot)
            return;

        if (player.Team is not (CsTeam.Spectator or CsTeam.None))
            return;

        var targetTeam = GetSmallerTeam();
        player.SwitchTeam(targetTeam);

        AddTimer(0.2f, () => ForceRespawn(player));

        var activePlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && p.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist);
        if (activePlayers != 1)
            return;

        var botTeam = (targetTeam == CsTeam.Terrorist) ? "ct" : "t";
        Server.ExecuteCommand("bot_quota 1");
        Server.ExecuteCommand($"bot_add_{botTeam}");
    }

    private static void ForceRespawn(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot)
            return;

        if (player.Team is CsTeam.Spectator or CsTeam.None)
            return;

        if (player.PawnIsAlive)
            return;

        player.Respawn();
    }

    private static CsTeam GetSmallerTeam()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
        int ctCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
        int tCount = players.Count(p => p.Team == CsTeam.Terrorist);
        return tCount <= ctCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
    }
}
