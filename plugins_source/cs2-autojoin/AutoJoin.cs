using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace AutojoinPlugin;

public class AutojoinPlugin : BasePlugin
{
    public override string ModuleName => "AutojoinPlugin";
    public override string ModuleVersion => "1.0.0";

    private readonly HashSet<ulong> welcomedPlayers = new();

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
        Console.WriteLine("[AutojoinPlugin] Plugin carregado com sucesso!");
    }

    private HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;
        
        var botToKick = Utilities.GetPlayers().FirstOrDefault(p => p.IsBot);
        if (botToKick != null)
        {
            Server.ExecuteCommand($"kick \"{botToKick.PlayerName}\"");
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
            return HookResult.Continue;
        
        if (!welcomedPlayers.Contains(player.SteamID))
        {
            welcomedPlayers.Add(player.SteamID);
            player.PrintToChat("\x04[ABREU] \x01Bem-vindo ao servidor!");
            player.PrintToChat($"\x01Olá, \x03{player.PlayerName}\x01! Personalize suas armas com \x02!ws\x01.");
        }
        
        if (player.Team == CsTeam.Spectator)
        {
            var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();
            int ctCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
            int tCount = players.Count(p => p.Team == CsTeam.Terrorist);

            var targetTeam = (tCount <= ctCount) ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
            player.ChangeTeam(targetTeam);
            
            if (players.Count == 1)
            {
                var botTeam = (targetTeam == CsTeam.Terrorist) ? "ct" : "t";
                Server.ExecuteCommand($"bot_quota 1");
                Server.ExecuteCommand($"bot_add_{botTeam}");
            }
        }

        return HookResult.Continue;
    }
}
