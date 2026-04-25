using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;

using RetakesPlugin.Managers;
using RetakesPlugin.Utils;

namespace RetakesPlugin.Services;

public sealed class ClutchAnnounceService
{
    private const int MinOpponentsForClutch = 3;

    private readonly RetakesPlugin _plugin;
    private readonly GameManager _gameManager;

    private int _opponents;
    private CsTeam? _clutchTeam;
    private CCSPlayerController? _clutchPlayer;

    public ClutchAnnounceService(RetakesPlugin plugin, GameManager gameManager)
    {
        _plugin = plugin;
        _gameManager = gameManager;
    }

    public void ResetRoundState()
    {
        _clutchPlayer = null;
        _clutchTeam = null;
        _opponents = 0;
    }

    public void OnPotentialClutchStateChange()
    {
        if (_clutchPlayer != null)
        {
            return;
        }

        var activeAliveTs = _gameManager.QueueManager.ActivePlayers
            .Where(PlayerHelper.IsValid)
            .Where(p => p.Team == CsTeam.Terrorist && PlayerHelper.HasAlivePawn(p))
            .ToList();

        var activeAliveCts = _gameManager.QueueManager.ActivePlayers
            .Where(PlayerHelper.IsValid)
            .Where(p => p.Team == CsTeam.CounterTerrorist && PlayerHelper.HasAlivePawn(p))
            .ToList();

        if (activeAliveTs.Count == 1 && activeAliveCts.Count >= MinOpponentsForClutch)
        {
            _clutchTeam = CsTeam.Terrorist;
            _opponents = activeAliveCts.Count;
            _clutchPlayer = activeAliveTs[0];
            return;
        }

        if (activeAliveCts.Count == 1 && activeAliveTs.Count >= MinOpponentsForClutch)
        {
            _clutchTeam = CsTeam.CounterTerrorist;
            _opponents = activeAliveTs.Count;
            _clutchPlayer = activeAliveCts[0];
        }
    }

    public void AnnounceIfClutched(CsTeam winner)
    {
        if (!PlayerHelper.IsValid(_clutchPlayer) || _clutchTeam == null || winner != _clutchTeam)
        {
            return;
        }

        var clutchMessage = _plugin.Localizer["retakes.clutch_announce.clutched", _clutchPlayer!.PlayerName, _opponents];
        Server.PrintToChatAll($"{_plugin.Localizer["retakes.prefix"]} {clutchMessage}");
    }
}