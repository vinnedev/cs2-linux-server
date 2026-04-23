using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Utils;

namespace RetakesPlugin.Services;

public sealed class InstantDefuseService
{
    private readonly RetakesPlugin _plugin;
    private readonly ChatMessageService _chatMessageService;
    private readonly bool _enabled;
    private readonly float _threatRadius;

    private double _bombPlantedTime = double.NaN;
    private bool _bombTicking;
    private int _heThreatCount;
    private int _molotovThreatCount;
    private readonly HashSet<int> _infernoThreatIds = [];

    public InstantDefuseService(RetakesPlugin plugin, ChatMessageService chatMessageService, bool enabled, float threatRadius)
    {
        _plugin = plugin;
        _chatMessageService = chatMessageService;
        _enabled = enabled;
        _threatRadius = threatRadius;
    }

    public void OnRoundStart()
    {
        ResetState();
    }

    public void OnRoundEnd()
    {
        ResetState();
    }

    public void OnBombPlanted()
    {
        if (!_enabled)
        {
            return;
        }

        _bombPlantedTime = Server.CurrentTime;
        _bombTicking = true;
        Logger.LogDebug("InstantDefuse", $"Bomb planted. Instant defuse armed at time {_bombPlantedTime:n3}.");
    }

    public void OnBombDefused()
    {
        ResetState();
    }

    public void OnGrenadeThrown(string? weapon)
    {
        if (!_enabled)
        {
            return;
        }

        switch (weapon)
        {
            case "hegrenade":
                _heThreatCount++;
                break;
            case "incgrenade":
            case "molotov":
                _molotovThreatCount++;
                break;
            default:
                return;
        }

        LogThreatLevels();
    }

    public void OnInfernoStartBurn(int entityId, Vector3 infernoPosition)
    {
        if (!_enabled || !_bombTicking)
        {
            return;
        }

        var plantedBombPosition = GetPlantedBombPosition();
        if (plantedBombPosition == null)
        {
            return;
        }

        var distance = Vector3.Distance(infernoPosition, plantedBombPosition.Value);
        if (distance > _threatRadius)
        {
            return;
        }

        _infernoThreatIds.Add(entityId);
        LogThreatLevels();
    }

    public void OnInfernoExtinguish(int entityId)
    {
        if (!_enabled)
        {
            return;
        }

        _infernoThreatIds.Remove(entityId);
    }

    public void OnInfernoExpire(int entityId)
    {
        if (!_enabled)
        {
            return;
        }

        _infernoThreatIds.Remove(entityId);
    }

    public void OnHeGrenadeDetonate()
    {
        if (!_enabled)
        {
            return;
        }

        if (_heThreatCount > 0)
        {
            _heThreatCount--;
        }
    }

    public void OnMolotovDetonate()
    {
        if (!_enabled)
        {
            return;
        }

        if (_molotovThreatCount > 0)
        {
            _molotovThreatCount--;
        }
    }

    public void OnBombBeginDefuse(CCSPlayerController? player)
    {
        if (!_enabled || !PlayerHelper.IsValid(player) || !player!.PawnIsAlive)
        {
            return;
        }

        AttemptInstantDefuse(player);
    }

    private void AttemptInstantDefuse(CCSPlayerController defuser)
    {
        if (!_bombTicking)
        {
            return;
        }

        var plantedBomb = FindPlantedBomb();
        if (plantedBomb == null || plantedBomb.CannotBeDefused)
        {
            return;
        }

        if (TeamHasAlivePlayers(CsTeam.Terrorist))
        {
            Logger.LogDebug("InstantDefuse", "Skipped instant defuse because terrorists are still alive.");
            return;
        }

        if (HasActiveThreat())
        {
            _chatMessageService.BroadcastPrefixed(_plugin.Localizer["retakes.instadefuse.not_possible"]);
            Logger.LogDebug("InstantDefuse", "Skipped instant defuse because grenade threats are active.");
            return;
        }

        var bombTimeUntilDetonation = (double)plantedBomb.TimerLength - (Server.CurrentTime - _bombPlantedTime);

        var defuseLength = plantedBomb.DefuseLength;
        if (Math.Abs(defuseLength - 5.0f) > 0.01f && Math.Abs(defuseLength - 10.0f) > 0.01f)
        {
            defuseLength = defuser.PawnHasDefuser ? 5.0f : 10.0f;
        }

        var timeLeftAfterDefuse = bombTimeUntilDetonation - defuseLength;
        if (timeLeftAfterDefuse < 0.0f)
        {
            _chatMessageService.BroadcastPrefixed(
                _plugin.Localizer["retakes.instadefuse.unsuccessful", defuser.PlayerName, $"{Math.Abs(timeLeftAfterDefuse):n3}"]
            );

            Server.NextFrame(() =>
            {
                var bomb = FindPlantedBomb();
                if (bomb != null)
                {
                    bomb.C4Blow = 1.0f;
                }
            });

            return;
        }

        Server.NextFrame(() =>
        {
            var bomb = FindPlantedBomb();
            if (bomb == null)
            {
                return;
            }

            bomb.DefuseCountDown = 0;
            _bombTicking = false;

            _chatMessageService.BroadcastPrefixed(
                _plugin.Localizer["retakes.instadefuse.successful", defuser.PlayerName, $"{Math.Abs(bombTimeUntilDetonation):n3}"]
            );
        });
    }

    private Vector3? GetPlantedBombPosition()
    {
        var bombOrigin = FindPlantedBomb()?.CBodyComponent?.SceneNode?.AbsOrigin;
        if (bombOrigin == null)
        {
            return null;
        }

        return new Vector3(bombOrigin.X, bombOrigin.Y, bombOrigin.Z);
    }

    private static CPlantedC4? FindPlantedBomb()
    {
        return Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4").FirstOrDefault();
    }

    private bool HasActiveThreat()
    {
        return _heThreatCount > 0 || _molotovThreatCount > 0 || _infernoThreatIds.Count > 0;
    }

    private static bool TeamHasAlivePlayers(CsTeam team)
    {
        return Utilities.GetPlayers().Any(player =>
            PlayerHelper.IsValid(player) &&
            player.Team == team &&
            player.PawnIsAlive);
    }

    private void ResetState()
    {
        _bombPlantedTime = double.NaN;
        _bombTicking = false;
        _heThreatCount = 0;
        _molotovThreatCount = 0;
        _infernoThreatIds.Clear();
    }

    private void LogThreatLevels()
    {
        Logger.LogDebug(
            "InstantDefuse",
            $"Threats => HE: {_heThreatCount}, Molotov: {_molotovThreatCount}, Infernos: {_infernoThreatIds.Count}"
        );
    }
}
