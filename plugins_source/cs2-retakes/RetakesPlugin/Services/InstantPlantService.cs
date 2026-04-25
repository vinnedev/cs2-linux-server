using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using RetakesPlugin.Utils;

namespace RetakesPlugin.Services;

public sealed class InstantPlantService
{
    private readonly bool _enabled;
    private readonly bool _autoPlantEnabled;

    public InstantPlantService(bool enabled, bool autoPlantEnabled)
    {
        _enabled = enabled;
        _autoPlantEnabled = autoPlantEnabled;
    }

    public void OnBombBeginPlant()
    {
        if (!_enabled || _autoPlantEnabled)
        {
            return;
        }

        var bomb = Utilities.FindAllEntitiesByDesignerName<CC4>("weapon_c4").FirstOrDefault();
        if (bomb == null)
        {
            return;
        }

        bomb.BombPlacedAnimation = false;
        bomb.ArmedTime = 0.0f;
        Logger.LogDebug("InstantPlant", "Applied instant plant state to the carried C4.");
    }
}
