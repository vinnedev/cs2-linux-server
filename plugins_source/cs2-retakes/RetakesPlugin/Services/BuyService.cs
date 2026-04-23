using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Utils;
using RetakesPluginShared.Enums;

namespace RetakesPlugin.Services;

public enum RoundBuyType
{
    Full,
    Force
}

public readonly record struct BuyLoadout(CsItem PrimaryWeapon, CsItem SecondaryWeapon);

public sealed class BuyService
{
    private sealed record WeaponChoice(string Alias, string DisplayName, CsItem Item, string Category);
    private sealed record MenuState(int Depth, string? Category);

    private static readonly WeaponChoice[] TerroristFullChoices =
    [
        new("ak", "AK-47", CsItem.AK47, "Rifles"),
        new("sg", "SG 553", CsItem.SG556, "Rifles"),
        new("mac10", "MAC-10", CsItem.Mac10, "SMGs")
    ];

    private static readonly WeaponChoice[] CounterTerroristFullChoices =
    [
        new("m4", "M4A1-S", CsItem.M4A1S, "Rifles"),
        new("m4a4", "M4A4", CsItem.M4A4, "Rifles"),
        new("aug", "AUG", CsItem.AUG, "Rifles"),
        new("famas", "FAMAS", CsItem.Famas, "Rifles")
    ];

    private static readonly WeaponChoice[] TerroristForceChoices =
    [
        new("ak", "AK-47", CsItem.AK47, "Rifles"),
        new("mac10", "MAC-10", CsItem.Mac10, "SMGs"),
        new("ump", "UMP-45", CsItem.UMP45, "SMGs")
    ];

    private static readonly WeaponChoice[] CounterTerroristForceChoices =
    [
        new("famas", "FAMAS", CsItem.Famas, "Rifles"),
        new("mp9", "MP9", CsItem.MP9, "SMGs"),
        new("ump", "UMP-45", CsItem.UMP45, "SMGs")
    ];

    private readonly RetakesPlugin _plugin;
    private readonly Random _random;
    private readonly Dictionary<ulong, CsItem> _selectedWeapons = [];
    private readonly Dictionary<ulong, MenuState> _menuStates = [];

    public RoundBuyType CurrentRoundBuyType { get; private set; } = RoundBuyType.Full;

    public BuyService(RetakesPlugin plugin, Random random)
    {
        _plugin = plugin;
        _random = random;
    }

    public void OnMapStart()
    {
        _selectedWeapons.Clear();
        _menuStates.Clear();
        CurrentRoundBuyType = RoundBuyType.Full;
    }

    public void PrepareRound(IEnumerable<CCSPlayerController> activePlayers)
    {
        CurrentRoundBuyType = _random.NextDouble() < 0.65 ? RoundBuyType.Full : RoundBuyType.Force;

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

        if (_selectedWeapons.TryGetValue(player.SteamID, out var selectedWeapon) && allowedChoices.Any(choice => choice.Item == selectedWeapon))
        {
            primaryWeapon = selectedWeapon;
        }

        var secondaryWeapon = CurrentRoundBuyType == RoundBuyType.Full ? CsItem.Deagle : CsItem.P250;
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

        ShowOptions(player!);
    }

    public void ShowRetakeStatus(CCSPlayerController player, Bombsite bombsite)
    {
        if (!PlayerHelper.IsValid(player) || player.Team is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
        {
            return;
        }

        var plainMessage = $"RETAKE {bombsite} - {GetRoundLabel()} BUY";
        var htmlMessage = $"<font color='#f7c948'><b>RETAKE {bombsite}</b></font><br><font color='#6ee7b7'>{GetRoundLabel()} BUY</font>";

        player.PrintToCenter(plainMessage);
        player.PrintToCenterHtml(htmlMessage);
    }

    public string GetRoundLabel()
    {
        return CurrentRoundBuyType == RoundBuyType.Full ? "FULL" : "FORCE";
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

        if (allocationService != null)
        {
            ApplyRoundLoadout(player, allocationService, false);
        }

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {choice.DisplayName} equipada para este round.");
        ShowCategoryMenu(player, choice.Category);
    }

    private void TrySelectAwp(CCSPlayerController player)
    {
        if (!_plugin.IsBuyWindowOpen)
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

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {GetRoundLabel()} BUY: escolha uma categoria.");

        for (var index = 0; index < categories.Count; index++)
        {
            player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {index + 1} - {categories[index]}");
        }

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} AWP: use !awp");
    }

    private void ShowCategoryMenu(CCSPlayerController player, string category)
    {
        var categoryChoices = GetChoices(player)
            .Where(choice => choice.Category == category)
            .ToList();

        player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {category}: escolha a arma. Use !a para voltar ao menu inicial.");

        for (var index = 0; index < categoryChoices.Count; index++)
        {
            player.PrintToChat($"{_plugin.Localizer["retakes.prefix"]} {index + 1} - {categoryChoices[index].DisplayName}");
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
            (CsTeam.Terrorist, RoundBuyType.Force) => TerroristForceChoices,
            (CsTeam.CounterTerrorist, RoundBuyType.Force) => CounterTerroristForceChoices,
            _ => []
        };
    }
}
