using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPluginShared.Enums;
using RetakesPlugin.Utils;

namespace RetakesPlugin.Services;

public sealed class RetakeStatusService
{
    private readonly RetakesPlugin _plugin;
    private readonly Dictionary<ulong, int> _shownTokenByPlayer = [];
    private int _roundToken;
    private Bombsite? _currentBombsite;

    public RetakeStatusService(RetakesPlugin plugin)
    {
        _plugin = plugin;
    }

    public void BeginRound(Bombsite bombsite)
    {
        _roundToken++;
        _currentBombsite = bombsite;
        _shownTokenByPlayer.Clear();
    }

    public void EndRound()
    {
        _roundToken++;
        _currentBombsite = null;
        _shownTokenByPlayer.Clear();
    }

    public void ShowForPlayer(CCSPlayerController player)
    {
        if (!CanShowTo(player) || _currentBombsite == null)
        {
            return;
        }

        if (_shownTokenByPlayer.TryGetValue(player.SteamID, out var shownToken) && shownToken == _roundToken)
        {
            return;
        }

        _shownTokenByPlayer[player.SteamID] = _roundToken;
        QueueStatusHtml(player, _currentBombsite.Value, _roundToken, 0.20f);
    }

    public void ShowForPlayers(IEnumerable<CCSPlayerController> players)
    {
        foreach (var player in players)
        {
            ShowForPlayer(player);
        }
    }

    private void QueueStatusHtml(CCSPlayerController player, Bombsite bombsite, int token, float delaySeconds)
    {
        _plugin.AddTimer(delaySeconds, () =>
        {
            if (token != _roundToken || _currentBombsite != bombsite || !CanShowTo(player))
            {
                return;
            }

            var htmlMessage = "<font color='#22c55e'><b>RETAKE</b></font> "
                            + $"<font color='#ef4444'><b>{bombsite}</b></font>";

            player.PrintToCenterHtml(htmlMessage, 5);
        });
    }

    private static bool CanShowTo(CCSPlayerController? player)
    {
        return PlayerHelper.IsValid(player) &&
               PlayerHelper.IsConnected(player!) &&
               PlayerHelper.HasAlivePawn(player) &&
               player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist;
    }
}
