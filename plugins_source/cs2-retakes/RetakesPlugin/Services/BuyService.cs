using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Utils;
using RetakesPluginShared.Enums;
using System.Text.Json;

namespace RetakesPlugin.Services;

public enum RoundBuyType
{
    Full,
    Semi,
    Force
}

public readonly record struct BuyLoadout(CsItem PrimaryWeapon, CsItem SecondaryWeapon);

public sealed class BuyService
{
    private sealed record WeaponChoice(string Alias, string DisplayName, CsItem Item, string Category, params string[] Keywords);
    private sealed record MenuState(int Depth, string? Category);
    private sealed class PersistedPreferences
    {
        public Dictionary<string, Dictionary<string, string>> Players { get; set; } = [];
    }

    private static readonly WeaponChoice[] TerroristFullChoices =
    [
        new("ak", "AK-47", CsItem.AK47, "Rifles", "ak", "ak47", "weapon_ak47"),
        new("sg", "SG 553", CsItem.SG556, "Rifles", "sg", "sg553", "krieg", "weapon_sg556"),
        new("galil", "Galil", CsItem.Galil, "Rifles", "galil", "galilar", "weapon_galilar")
    ];

    private static readonly WeaponChoice[] CounterTerroristFullChoices =
    [
        new("m4", "M4A1-S", CsItem.M4A1S, "Rifles", "m4", "m4a1s", "m4a1_silencer", "weapon_m4a1_silencer"),
        new("m4a4", "M4A4", CsItem.M4A4, "Rifles", "m4a4", "weapon_m4a1"),
        new("aug", "AUG", CsItem.AUG, "Rifles", "aug", "weapon_aug"),
        new("famas", "FAMAS", CsItem.Famas, "Rifles", "famas", "weapon_famas")
    ];

    private static readonly WeaponChoice[] TerroristSemiChoices =
    [
        new("deagle", "Desert Eagle", CsItem.Deagle, "Pistols", "deagle", "weapon_deagle"),
        new("p250", "P250", CsItem.P250, "Pistols", "p250", "weapon_p250"),
        new("glock", "Glock-18", CsItem.Glock18, "Pistols", "glock", "weapon_glock"),
        new("tec9", "Tec-9", CsItem.Tec9, "Pistols", "tec9", "tec-9", "weapon_tec9"),
        new("duals", "Dual Berettas", CsItem.DualBerettas, "Pistols", "duals", "dualies", "elite", "weapon_elite"),
        new("cz", "CZ75-Auto", CsItem.CZ75, "Pistols", "cz", "cz75", "cz75-auto", "weapon_cz75a"),
        new("r8", "R8 Revolver", CsItem.R8, "Pistols", "r8", "revolver", "weapon_revolver")
    ];

    private static readonly WeaponChoice[] CounterTerroristSemiChoices =
    [
        new("deagle", "Desert Eagle", CsItem.Deagle, "Pistols", "deagle", "weapon_deagle"),
        new("p250", "P250", CsItem.P250, "Pistols", "p250", "weapon_p250"),
        new("usp", "USP-S", CsItem.USPS, "Pistols", "usp", "usp-s", "weapon_usp_silencer"),
        new("fiveseven", "Five-SeveN", CsItem.FiveSeven, "Pistols", "fiveseven", "five-seven", "57", "weapon_fiveseven"),
        new("duals", "Dual Berettas", CsItem.DualBerettas, "Pistols", "duals", "dualies", "elite", "weapon_elite"),
        new("p2000", "P2000", CsItem.P2000, "Pistols", "p2000", "hkp2000", "weapon_hkp2000"),
        new("cz", "CZ75-Auto", CsItem.CZ75, "Pistols", "cz", "cz75", "cz75-auto", "weapon_cz75a"),
        new("r8", "R8 Revolver", CsItem.R8, "Pistols", "r8", "revolver", "weapon_revolver")
    ];

    private static readonly WeaponChoice[] TerroristForceChoices =
    [
        new("mac10", "MAC-10", CsItem.Mac10, "SMGs", "mac10", "mac", "weapon_mac10"),
        new("mp7", "MP7", CsItem.MP7, "SMGs", "mp7", "weapon_mp7"),
        new("mp5", "MP5-SD", CsItem.MP5SD, "SMGs", "mp5", "mp5sd", "weapon_mp5sd"),
        new("ump", "UMP-45", CsItem.UMP45, "SMGs", "ump", "ump45", "weapon_ump45"),
        new("bizon", "PP-Bizon", CsItem.PPBizon, "SMGs", "bizon", "ppbizon", "weapon_bizon"),
        new("p90", "P90", CsItem.P90, "SMGs", "p90", "weapon_p90"),
        new("nova", "Nova", CsItem.Nova, "Shotguns", "nova", "weapon_nova"),
        new("xm", "XM1014", CsItem.XM1014, "Shotguns", "xm", "xm1014", "weapon_xm1014"),
        new("sawedoff", "Sawed-Off", CsItem.SawedOff, "Shotguns", "sawedoff", "sawed-off", "weapon_sawedoff")
    ];

    private static readonly WeaponChoice[] CounterTerroristForceChoices =
    [
        new("mp9", "MP9", CsItem.MP9, "SMGs", "mp9", "weapon_mp9"),
        new("mp7", "MP7", CsItem.MP7, "SMGs", "mp7", "weapon_mp7"),
        new("mp5", "MP5-SD", CsItem.MP5SD, "SMGs", "mp5", "mp5sd", "weapon_mp5sd"),
        new("ump", "UMP-45", CsItem.UMP45, "SMGs", "ump", "ump45", "weapon_ump45"),
        new("bizon", "PP-Bizon", CsItem.PPBizon, "SMGs", "bizon", "ppbizon", "weapon_bizon"),
        new("p90", "P90", CsItem.P90, "SMGs", "p90", "weapon_p90"),
        new("nova", "Nova", CsItem.Nova, "Shotguns", "nova", "weapon_nova"),
        new("xm", "XM1014", CsItem.XM1014, "Shotguns", "xm", "xm1014", "weapon_xm1014"),
        new("mag7", "MAG-7", CsItem.MAG7, "Shotguns", "mag7", "mag-7", "weapon_mag7")
    ];

    private readonly RetakesPlugin _plugin;
    private readonly Random _random;
    private readonly Dictionary<ulong, CsItem> _selectedWeapons = [];
    private readonly Dictionary<ulong, MenuState> _menuStates = [];
    private readonly string _preferencesPath;
    private PersistedPreferences _persistedPreferences = new();

    public RoundBuyType CurrentRoundBuyType { get; private set; } = RoundBuyType.Full;
    public bool IsAwpAllowedThisRound => CurrentRoundBuyType == RoundBuyType.Full;

    public BuyService(RetakesPlugin plugin, Random random)
    {
        _plugin = plugin;
        _random = random;
        _preferencesPath = Path.Combine(_plugin.ModuleDirectory, "buy_preferences.json");
        LoadPreferences();
    }

    public void OnMapStart()
    {
        _selectedWeapons.Clear();
        _menuStates.Clear();
        CurrentRoundBuyType = RoundBuyType.Full;
    }

    public void PrepareRound(IEnumerable<CCSPlayerController> activePlayers)
    {
        var roll = _random.NextDouble();
        CurrentRoundBuyType = roll switch
        {
            < 0.45 => RoundBuyType.Full,
            < 0.70 => RoundBuyType.Semi,
            _ => RoundBuyType.Force
        };

        var activeSteamIds = activePlayers
            .Where(PlayerHelper.IsValid)
            .Select(player => player.SteamID)
            .ToHashSet();

        foreach (var steamId in _selectedWeapons.Keys.ToList())
        {
            if (!activeSteamIds.Contains(steamId))
            {
                _selectedWeapons.Remove(steamId);
                _menuStates.Remove(steamId);
            }
        }
    }

    public BuyLoadout GetLoadout(CCSPlayerController player)
    {
        var allowedChoices = GetChoices(player);
        var primaryWeapon = allowedChoices[0].Item;

        var persistedChoice = TryGetPersistedChoice(player);
        if (persistedChoice != null)
        {
            primaryWeapon = persistedChoice.Item;
        }

        if (_selectedWeapons.TryGetValue(player.SteamID, out var selectedWeapon) && allowedChoices.Any(choice => choice.Item == selectedWeapon))
        {
            primaryWeapon = selectedWeapon;
        }

        var secondaryWeapon = CurrentRoundBuyType switch
        {
            RoundBuyType.Full => CsItem.Deagle,
            RoundBuyType.Semi => primaryWeapon,
            _ => CsItem.P250
        };

        return new BuyLoadout(primaryWeapon, secondaryWeapon);
    }

    public void ShowOptions(CCSPlayerController player)
    {
        if (!CanUseBuyCommands(player))
        {
            player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} Comandos de compra so funcionam para jogadores ativos no retake.");
            return;
        }

        _menuStates[player.SteamID] = new MenuState(0, null);
        ShowMainMenu(player);
    }

    public void ApplyRoundLoadout(CCSPlayerController player, AllocationService allocationService, bool assignAwp)
    {
        var loadout = GetLoadout(player);
        allocationService.AllocatePlayer(player, loadout.PrimaryWeapon, loadout.SecondaryWeapon, assignAwp);
    }

    public void TryHandleBuyCommand(CCSPlayerController? player, string? alias, AllocationService? allocationService)
    {
        if (!PlayerHelper.IsValid(player))
        {
            return;
        }

        if (!CanUseBuyCommands(player!))
        {
            player!.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} Comandos de compra so funcionam para jogadores ativos no retake.");
            return;
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            ShowOptions(player!);
            return;
        }

        var normalizedAlias = alias.Trim().ToLowerInvariant();
        if (normalizedAlias is "awp")
        {
            TrySelectAwp(player!);
            return;
        }

        if (int.TryParse(normalizedAlias, out var optionNumber))
        {
            HandleNumericSelection(player!, optionNumber, allocationService);
            return;
        }

        var matchedChoice = TryFindChoice(player!, normalizedAlias);
        if (matchedChoice != null)
        {
            TrySelectWeapon(player!, matchedChoice, allocationService);
            return;
        }

        ShowOptions(player!);
    }

    public string GetRoundLabel()
    {
        return CurrentRoundBuyType switch
        {
            RoundBuyType.Full => "FULL",
            RoundBuyType.Semi => "SEMI",
            _ => "FORCE"
        };
    }

    private void HandleNumericSelection(CCSPlayerController player, int optionNumber, AllocationService? allocationService)
    {
        if (optionNumber <= 0)
        {
            ShowOptions(player);
            return;
        }

        var state = _menuStates.TryGetValue(player.SteamID, out var currentState)
            ? currentState
            : new MenuState(0, null);

        if (state.Depth == 0)
        {
            var categories = GetChoices(player)
                .Select(choice => choice.Category)
                .Distinct()
                .ToList();

            if (optionNumber > categories.Count)
            {
                ShowMainMenu(player);
                return;
            }

            var selectedCategory = categories[optionNumber - 1];
            _menuStates[player.SteamID] = new MenuState(1, selectedCategory);
            ShowCategoryMenu(player, selectedCategory);
            return;
        }

        var categoryChoices = GetChoices(player)
            .Where(choice => choice.Category == state.Category)
            .ToList();

        if (optionNumber > categoryChoices.Count)
        {
            ShowCategoryMenu(player, state.Category!);
            return;
        }

        TrySelectWeapon(player, categoryChoices[optionNumber - 1], allocationService);
    }

    private void TrySelectWeapon(CCSPlayerController player, WeaponChoice choice, AllocationService? allocationService)
    {
        if (!_plugin.IsBuyWindowOpen)
        {
            player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} Periodo de compra encerrado. Abra o menu no inicio do round.");
            return;
        }

        _selectedWeapons[player.SteamID] = choice.Item;
        SavePreference(player, choice);

        if (allocationService != null)
        {
            DropCurrentPrimary(player);
            ApplyRoundLoadout(player, allocationService, false);
        }

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {choice.DisplayName} equipada para este round.");
        ShowCategoryMenu(player, choice.Category);
    }

    private void TrySelectAwp(CCSPlayerController player)
    {
        if (IsAwpAllowedThisRound && !_plugin.IsBuyWindowOpen)
        {
            player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} Periodo de compra encerrado. A AWP segue na fila para o proximo round.");
        }

        _plugin.RequestAwp(player);
    }

    private void ShowMainMenu(CCSPlayerController player)
    {
        var categories = GetChoices(player)
            .Select(choice => choice.Category)
            .Distinct()
            .ToList();

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {GetRoundLabel()} BUY: escolha uma categoria com !1, !2, etc.");

        for (var index = 0; index < categories.Count; index++)
        {
            player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {index + 1} - {categories[index]}");
        }

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} AWP: use !awp");
        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} Comandos: !a abre menu, !0 volta, !awp entra na fila.");
    }

    private void ShowCategoryMenu(CCSPlayerController player, string category)
    {
        var categoryChoices = GetChoices(player)
            .Where(choice => choice.Category == category)
            .ToList();

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {GetRoundLabel()} > {category}: use !1, !2, etc. para escolher.");

        for (var index = 0; index < categoryChoices.Count; index++)
        {
            player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {index + 1} - {categoryChoices[index].DisplayName}");
        }

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} !0 volta para categorias. !awp entra na fila da AWP.");
    }

    private WeaponChoice? TryFindChoice(CCSPlayerController player, string token)
    {
        return GetChoices(player).FirstOrDefault(choice =>
            choice.Alias == token ||
            choice.Keywords.Any(keyword => keyword.Equals(token, StringComparison.OrdinalIgnoreCase)));
    }

    private void DropCurrentPrimary(CCSPlayerController player)
    {
        var weaponServices = player.PlayerPawn.Value?.WeaponServices?.As<CCSPlayer_WeaponServices>();
        if (weaponServices == null)
        {
            return;
        }

        foreach (var handle in weaponServices.MyWeapons)
        {
            var weapon = handle.Value?.As<CCSWeaponBase>();
            var data = weapon?.VData?.As<CCSWeaponBaseVData>();
            if (weapon == null || data == null)
            {
                continue;
            }

            if (data.GearSlot == gear_slot_t.GEAR_SLOT_RIFLE)
            {
                player.ExecuteClientCommand("slot1");
                _plugin.AddTimer(0.1f, () =>
                {
                    if (PlayerHelper.IsValid(player))
                    {
                        player.DropActiveWeapon();
                    }
                });
                return;
            }
        }
    }

    private WeaponChoice? TryGetPersistedChoice(CCSPlayerController player)
    {
        if (!_persistedPreferences.Players.TryGetValue(player.SteamID.ToString(), out var playerPreferences))
        {
            return null;
        }

        if (!playerPreferences.TryGetValue(GetPreferenceKey(player), out var alias))
        {
            return null;
        }

        return TryFindChoice(player, alias);
    }

    private void SavePreference(CCSPlayerController player, WeaponChoice choice)
    {
        var steamId = player.SteamID.ToString();
        if (!_persistedPreferences.Players.TryGetValue(steamId, out var playerPreferences))
        {
            playerPreferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _persistedPreferences.Players[steamId] = playerPreferences;
        }

        playerPreferences[GetPreferenceKey(player)] = choice.Alias;
        PersistPreferences();
    }

    private string GetPreferenceKey(CCSPlayerController player)
    {
        var team = player.Team == CsTeam.CounterTerrorist ? "ct" : "t";
        return $"{team}_{CurrentRoundBuyType.ToString().ToLowerInvariant()}";
    }

    private void LoadPreferences()
    {
        try
        {
            if (!File.Exists(_preferencesPath))
            {
                _persistedPreferences = new PersistedPreferences();
                return;
            }

            var json = File.ReadAllText(_preferencesPath);
            _persistedPreferences = JsonSerializer.Deserialize<PersistedPreferences>(json) ?? new PersistedPreferences();
        }
        catch
        {
            _persistedPreferences = new PersistedPreferences();
        }
    }

    private void PersistPreferences()
    {
        try
        {
            var json = JsonSerializer.Serialize(_persistedPreferences, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_preferencesPath, json);
        }
        catch
        {
            // Ignore persistence failures to avoid interrupting gameplay.
        }
    }

    private bool CanUseBuyCommands(CCSPlayerController player)
    {
        if (_plugin.GameManager == null)
        {
            return false;
        }

        return _plugin.GameManager.QueueManager.ActivePlayers.Contains(player) &&
               player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist &&
               PlayerHelper.HasAlivePawn(player);
    }

    private WeaponChoice[] GetChoices(CCSPlayerController player)
    {
        return (player.Team, CurrentRoundBuyType) switch
        {
            (CsTeam.Terrorist, RoundBuyType.Full) => TerroristFullChoices,
            (CsTeam.CounterTerrorist, RoundBuyType.Full) => CounterTerroristFullChoices,
            (CsTeam.Terrorist, RoundBuyType.Semi) => TerroristSemiChoices,
            (CsTeam.CounterTerrorist, RoundBuyType.Semi) => CounterTerroristSemiChoices,
            (CsTeam.Terrorist, RoundBuyType.Force) => TerroristForceChoices,
            (CsTeam.CounterTerrorist, RoundBuyType.Force) => CounterTerroristForceChoices,
            _ => []
        };
    }

}
