using System.Numerics;
using CounterStrikeSharp.API.Core;
using RetakesPlugin.Services;

namespace RetakesPlugin.Events;

public sealed class BombEventHandlers
{
    private readonly InstantPlantService _instantPlantService;
    private readonly InstantDefuseService _instantDefuseService;

    public BombEventHandlers(InstantPlantService instantPlantService, InstantDefuseService instantDefuseService)
    {
        _instantPlantService = instantPlantService;
        _instantDefuseService = instantDefuseService;
    }

    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _instantDefuseService.OnRoundStart();
        return HookResult.Continue;
    }

    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _instantDefuseService.OnRoundEnd();
        return HookResult.Continue;
    }

    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        _instantDefuseService.OnBombPlanted();
        return HookResult.Continue;
    }

    public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    {
        _instantDefuseService.OnBombDefused();
        return HookResult.Continue;
    }

    public HookResult OnBombBeginPlant(EventBombBeginplant @event, GameEventInfo info)
    {
        _instantPlantService.OnBombBeginPlant();
        return HookResult.Continue;
    }

    public HookResult OnBombBeginDefuse(EventBombBegindefuse @event, GameEventInfo info)
    {
        _instantDefuseService.OnBombBeginDefuse(@event.Userid);
        return HookResult.Continue;
    }

    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        _instantDefuseService.OnGrenadeThrown(@event.Weapon);
        return HookResult.Continue;
    }

    public HookResult OnInfernoStartBurn(EventInfernoStartburn @event, GameEventInfo info)
    {
        _instantDefuseService.OnInfernoStartBurn(@event.Entityid, new Vector3(@event.X, @event.Y, @event.Z));
        return HookResult.Continue;
    }

    public HookResult OnInfernoExtinguish(EventInfernoExtinguish @event, GameEventInfo info)
    {
        _instantDefuseService.OnInfernoExtinguish(@event.Entityid);
        return HookResult.Continue;
    }

    public HookResult OnInfernoExpire(EventInfernoExpire @event, GameEventInfo info)
    {
        _instantDefuseService.OnInfernoExpire(@event.Entityid);
        return HookResult.Continue;
    }

    public HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        _instantDefuseService.OnHeGrenadeDetonate();
        return HookResult.Continue;
    }

    public HookResult OnMolotovDetonate(EventMolotovDetonate @event, GameEventInfo info)
    {
        _instantDefuseService.OnMolotovDetonate();
        return HookResult.Continue;
    }
}
