using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;

namespace RetakesPlugin.Services;

public class AllocationService
{
    private readonly Random _random;

    public AllocationService(Random random)
    {
        _random = random;
    }

    public void AllocatePlayer(CCSPlayerController player, CsItem primaryWeapon, CsItem secondaryWeapon, bool assignAwp)
    {
        AllocateEquipment(player);
        AllocateWeapons(player, primaryWeapon, secondaryWeapon, assignAwp);
        AllocateGrenades(player);
    }

    private void AllocateEquipment(CCSPlayerController player)
    {
        player.GiveNamedItem(CsItem.KevlarHelmet);
        player.GiveNamedItem(CsItem.Taser);

        if (
            player.Team == CsTeam.CounterTerrorist
            && player.PlayerPawn.IsValid
            && player.PlayerPawn.Value != null
            && player.PlayerPawn.Value.IsValid
            && player.PlayerPawn.Value.ItemServices != null
        )
        {
            var itemServices = new CCSPlayer_ItemServices(player.PlayerPawn.Value.ItemServices.Handle);
            itemServices.HasDefuser = true;
        }
    }

    private void AllocateWeapons(CCSPlayerController player, CsItem primaryWeapon, CsItem secondaryWeapon, bool assignAwp)
    {
        var grantedWeapons = new HashSet<CsItem>();

        if (assignAwp)
        {
            GiveUniqueWeapon(player, CsItem.AWP, grantedWeapons);
            GiveUniqueWeapon(player, secondaryWeapon, grantedWeapons);
            player.GiveNamedItem(CsItem.Knife);
            return;
        }

        GiveUniqueWeapon(player, primaryWeapon, grantedWeapons);
        GiveUniqueWeapon(player, secondaryWeapon, grantedWeapons);
        player.GiveNamedItem(CsItem.Knife);
    }

    private static void GiveUniqueWeapon(CCSPlayerController player, CsItem weapon, HashSet<CsItem> grantedWeapons)
    {
        if (!grantedWeapons.Add(weapon))
        {
            return;
        }

        player.GiveNamedItem(weapon);
    }

    private void AllocateGrenades(CCSPlayerController player)
    {
        switch (_random.Next(4))
        {
            case 0:
                player.GiveNamedItem(CsItem.SmokeGrenade);
                break;
            case 1:
                player.GiveNamedItem(CsItem.Flashbang);
                break;
            case 2:
                player.GiveNamedItem(CsItem.HEGrenade);
                break;
            case 3:
                player.GiveNamedItem(player.Team == CsTeam.Terrorist ? CsItem.Molotov : CsItem.Incendiary);
                break;
        }
    }
}
