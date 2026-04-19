using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;

using RetakesPlugin.Managers;
using RetakesPlugin.Services;
using RetakesPlugin.Utils;

namespace RetakesPlugin.Events;

public class PlayerEventHandlers
{
    private const string DefaultVipFlag = "@css/vip";
    private readonly RetakesPlugin _plugin;
    private readonly GameManager _gameManager;
    private readonly HashSet<CCSPlayerController> _hasMutedVoices;
    private readonly PlayerAccountStore? _playerAccountStore;
    private readonly SoloBotService? _soloBotService;
    private readonly HashSet<ulong> _welcomedPlayers = [];

    public PlayerEventHandlers(RetakesPlugin plugin, GameManager gameManager, HashSet<CCSPlayerController> hasMutedVoices, PlayerAccountStore? playerAccountStore, SoloBotService? soloBotService)
    {
        _plugin = plugin;
        _gameManager = gameManager;
        _hasMutedVoices = hasMutedVoices;
        _playerAccountStore = playerAccountStore;
        _soloBotService = soloBotService;
    }

    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (!PlayerHelper.IsValid(player))
        {
            return HookResult.Continue;
        }

        player.ForceTeamTime = 3600.0f;

        // Add small delay to ensure player is fully connected
        _plugin.AddTimer(1.0f, () =>
        {
            if (!PlayerHelper.IsValid(player))
            {
                return;
            }

            player.ChangeTeam(CsTeam.Spectator);
            player.ExecuteClientCommand("teammenu");
        });

        HandlePlayerAccount(player);

        // Grant VIP to contributors
        if (new List<ulong> { 76561198028510846, 76561198044886803, 76561198414501446, 76561199074660131 }.Contains(player.SteamID))
        {
            var grant = GetPreferredVipFlag();
            Logger.LogInfo("Queue", $"You have been given queue priority {grant} for being a Retakes contributor!");
            AdminManager.AddPlayerPermissions(player, grant);
            Logger.LogInfo("Player", $"Granted VIP to contributor {player.PlayerName}");
        }

        _soloBotService?.Reconcile();
        Logger.LogInfo("Player", $"{player.PlayerName} connected");
        return HookResult.Continue;
    }

    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (!PlayerHelper.IsValid(player) || !PlayerHelper.IsConnected(player))
        {
            return HookResult.Continue;
        }

        Logger.LogDebug("Player", $"[{player.PlayerName}] Spawned");

        if (!_gameManager.QueueManager.ActivePlayers.Contains(player))
        {
            if (player.PlayerPawn.Value != null && player.PlayerPawn.IsValid && player.PlayerPawn.Value.IsValid)
            {
                player.PlayerPawn.Value.CommitSuicide(false, true);
            }

            if (!player.IsBot)
            {
                player.ChangeTeam(CsTeam.Spectator);
            }
            else if (!player.IsHLTV)
            {
                _gameManager.QueueManager.ActivePlayers.Add(player);
                Logger.LogInfo("Player", $"Force added bot {player.PlayerName} to active players");
            }

            _soloBotService?.Reconcile();
            return HookResult.Continue;
        }

        _soloBotService?.Reconcile();
        TryPrintWelcomeMessages(player);
        return HookResult.Continue;
    }

    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var assister = @event.Assister;

        if (PlayerHelper.IsValid(attacker))
        {
            _gameManager.AddKill(attacker);
        }

        if (PlayerHelper.IsValid(assister))
        {
            _gameManager.AddAssist(assister);
        }

        return HookResult.Continue;
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null)
        {
            return HookResult.Continue;
        }

        _gameManager.QueueManager.RemovePlayerFromQueues(player);
        _hasMutedVoices.Remove(player);
        _welcomedPlayers.Remove(player.SteamID);

        if (!player.IsBot && player.IsValid)
        {
            _playerAccountStore?.SetPlayerOnlineStatus(player.SteamID, false, player.IpAddress);
        }

        _soloBotService?.Reconcile();
        Logger.LogInfo("Player", $"{player.PlayerName} disconnected");
        return HookResult.Continue;
    }

    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        @event.Silent = true;
        return _gameManager.RemoveSpectators(@event, _hasMutedVoices);
    }

    private void HandlePlayerAccount(CCSPlayerController player)
    {
        if (_playerAccountStore == null)
        {
            return;
        }

        var account = _playerAccountStore.GetPlayerBySteamId(player.SteamID);
        if (account == null)
        {
            player.PrintToConsole("[Retakes] Voce ainda nao tem conta cadastrada. Sem prioridade de fila.");
            _playerAccountStore.RegisterPlayer(player.SteamID, player.PlayerName, player.IpAddress);
            _playerAccountStore.SetPlayerOnlineStatus(player.SteamID, true, player.IpAddress);
            return;
        }

        if (account.Vip?.IsActive != true)
        {
            player.PrintToConsole("[Retakes] Voce nao e VIP. Sem prioridade de fila.");
            _playerAccountStore.SetPlayerOnlineStatus(player.SteamID, true, player.IpAddress);
            return;
        }

        if (account.Vip.Expiration.HasValue && account.Vip.Expiration.Value <= DateTime.UtcNow)
        {
            player.PrintToConsole($"[Retakes] Seu VIP expirou em {account.Vip.Expiration:u}. Sem prioridade de fila.");
            _playerAccountStore.SetPlayerOnlineStatus(player.SteamID, true, player.IpAddress);
            return;
        }

        var grant = GetPreferredVipFlag();
        AdminManager.AddPlayerPermissions(player, grant);
        player.PrintToConsole($"[Retakes] Voce e VIP ativo ate {account.Vip.Expiration:u}. Prioridade de fila {grant} aplicada.");
        _playerAccountStore.SetPlayerOnlineStatus(player.SteamID, true, player.IpAddress);
    }

    private string GetPreferredVipFlag()
    {
        return _plugin.Config.Queue.GetPriorityFlags().FirstOrDefault()?.Flag?.Trim() ?? DefaultVipFlag;
    }

    private void TryPrintWelcomeMessages(CCSPlayerController player)
    {
        if (player.IsBot || !_welcomedPlayers.Add(player.SteamID))
        {
            return;
        }

        if (player.Team != CsTeam.Terrorist && player.Team != CsTeam.CounterTerrorist)
        {
            return;
        }

        player.PrintToChat(" ");
        player.PrintToChat(" ");
        player.PrintToChat($"\x04[NEXUS] \x01 Bem-vindo ao servidor, \x04{player.PlayerName}\x01!");
        player.PrintToChat(" \x04[NEXUS] \x01 Para usar skins, digite \x04!ws\x01 no chat.");
        player.PrintToChat(" ");
    }
}
