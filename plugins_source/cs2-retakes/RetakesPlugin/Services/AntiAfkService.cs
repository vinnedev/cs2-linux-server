using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Utils;

namespace RetakesPlugin.Services;

public sealed class AntiAfkService
{
    private sealed class AfkState
    {
        public Vector? LastOrigin { get; set; }
        public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;
        public bool CountdownStarted { get; set; }
        public HashSet<int> WarnedRemainingSeconds { get; } = [];
    }

    private const float TickIntervalSeconds = 1.0f;
    private const float IdleThresholdUnits = 6.0f;
    private const int IdleSecondsBeforeCountdown = 10;
    private const int CountdownSeconds = 15;

    private readonly RetakesPlugin _plugin;
    private readonly Dictionary<ulong, AfkState> _states = [];

    public AntiAfkService(RetakesPlugin plugin)
    {
        _plugin = plugin;
        ScheduleTick();
    }

    public void OnMapStart()
    {
        _states.Clear();
    }

    public void OnPlayerDisconnect(CCSPlayerController player)
    {
        _states.Remove(player.SteamID);
    }

    public void OnPlayerSpawn(CCSPlayerController player)
    {
        ResetActivity(player);
    }

    private void ScheduleTick()
    {
        _plugin.AddTimer(TickIntervalSeconds, () =>
        {
            Tick();
            ScheduleTick();
        });
    }

    private void Tick()
    {
        foreach (var player in Utilities.GetPlayers().Where(PlayerHelper.IsValid))
        {
            if (!ShouldTrack(player))
            {
                if (player != null)
                {
                    ResetActivity(player);
                }

                continue;
            }

            var state = GetState(player!);
            var origin = player!.PlayerPawn.Value!.AbsOrigin;
            if (origin == null)
            {
                continue;
            }

            var buttonsPressed = player.Buttons != 0;
            var moved = state.LastOrigin == null || GetDistance(state.LastOrigin, origin) > IdleThresholdUnits;

            if (moved || buttonsPressed)
            {
                ResetActivity(player, origin);
                continue;
            }

            if (state.LastOrigin == null)
            {
                state.LastOrigin = new Vector(origin.X, origin.Y, origin.Z);
            }

            var idleForSeconds = (int)(DateTime.UtcNow - state.LastActivityAtUtc).TotalSeconds;
            if (idleForSeconds < IdleSecondsBeforeCountdown)
            {
                continue;
            }

            if (!state.CountdownStarted)
            {
                state.CountdownStarted = true;
                player.ExecuteClientCommand("play buttons/blip1");
                player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} AFK detectado. Voce sera expulso em {CountdownSeconds} segundos se continuar inativo.");
                state.WarnedRemainingSeconds.Add(CountdownSeconds);
            }

            var countdownElapsed = idleForSeconds - IdleSecondsBeforeCountdown;
            var remainingSeconds = CountdownSeconds - countdownElapsed;
            if (remainingSeconds <= 0)
            {
                Kick(player);
                _states.Remove(player.SteamID);
                continue;
            }

            if (remainingSeconds % 5 == 0 && state.WarnedRemainingSeconds.Add(remainingSeconds))
            {
                player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} Voce sera expulso em {remainingSeconds} segundos por inatividade.");
            }
        }
    }

    private AfkState GetState(CCSPlayerController player)
    {
        if (!_states.TryGetValue(player.SteamID, out var state))
        {
            state = new AfkState();
            _states[player.SteamID] = state;
        }

        return state;
    }

    private void ResetActivity(CCSPlayerController player, Vector? originOverride = null)
    {
        var state = GetState(player);
        var origin = originOverride ?? player.PlayerPawn.Value?.AbsOrigin;

        state.LastActivityAtUtc = DateTime.UtcNow;
        state.CountdownStarted = false;
        state.WarnedRemainingSeconds.Clear();

        if (origin != null)
        {
            state.LastOrigin = new Vector(origin.X, origin.Y, origin.Z);
        }
    }

    private bool ShouldTrack(CCSPlayerController? player)
    {
        if (!PlayerHelper.IsValid(player) || player!.IsBot || player.IsHLTV)
        {
            return false;
        }

        if (_plugin.GameManager == null || !_plugin.GameManager.QueueManager.ActivePlayers.Contains(player))
        {
            return false;
        }

        return player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist && PlayerHelper.HasAlivePawn(player);
    }

    private static float GetDistance(Vector first, Vector second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        var dz = first.Z - second.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static void Kick(CCSPlayerController player)
    {
        if (player.UserId == null)
        {
            return;
        }

        Server.ExecuteCommand($"kickid {player.UserId} Inatividade");
    }
}
