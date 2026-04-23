using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPluginShared;
using RetakesPluginShared.Enums;
using System.Text.Json;

using RetakesPlugin.Configs;
using RetakesPlugin.Configs.JsonConverters;
using RetakesPlugin.Events;
using RetakesPlugin.Managers;
using RetakesPlugin.Modules;
using RetakesPlugin.Services;
using RetakesPlugin.Utils;

using RetakesPlugin.Commands.Admin;
using RetakesPlugin.Commands.MapConfig;
using RetakesPlugin.Commands.Player;
using RetakesPlugin.Commands.SpawnEditor;

namespace RetakesPlugin;

[MinimumApiVersion(345)]
public class RetakesPlugin : BasePlugin, IPluginConfig<BaseConfigs>
{
    public const string Version = "3.0.5";
    public const float BuyWindowSeconds = 10.0f;
    public const int NativeBuyMenuWindowSeconds = 60000;

    #region Plugin Info
    public override string ModuleName => "Retakes Plugin";
    public override string ModuleVersion => Version;
    public override string ModuleAuthor => "B3none";
    public override string ModuleDescription => "https://github.com/b3none/cs2-retakes";
    #endregion

    #region Configuration
    public required BaseConfigs Config { get; set; }

    public void OnConfigParsed(BaseConfigs config)
    {
        Config = config;
        Utils.Logger.Initialize(Config.Debug.IsDebugMode);
        Utils.Logger.LogInfo("Main", "Configuration parsed successfully");
    }
    #endregion

    #region Services & Managers
    private readonly Random _random = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private GameManager? _gameManager;
    private SpawnManager? _spawnManager;
    private BreakerManager? _breakerManager;
    private MapConfigService? _mapConfigService;
    private AllocationService? _allocationService;
    private AutoJoinService? _autoJoinService;
    private BuyService? _buyService;
    private AntiAfkService? _antiAfkService;
    private AnnouncementService? _announcementService;
    private ChatMessageService? _chatMessageService;
    private InstantPlantService? _instantPlantService;
    private InstantDefuseService? _instantDefuseService;
    private RetakeStatusService? _retakeStatusService;
    private RoundEventHandlers? _roundEventHandlers;
    private PlayerEventHandlers? _playerEventHandlers;
    private BombEventHandlers? _bombEventHandlers;

    public MapConfigService? MapConfigService => _mapConfigService;
    public SpawnManager? SpawnManager => _spawnManager;
    public GameManager? GameManager => _gameManager;
    #endregion

    #region Commands
    // Admin Commands
    private ForceBombsiteCommand? _forceBombsiteCommand;
    private ForceBombsiteStopCommand? _forceBombsiteStopCommand;
    private ScrambleCommand? _scrambleCommand;
    private DebugQueuesCommand? _debugQueuesCommand;

    // Map Config Commands
    private MapConfigCommand? _mapConfigCommand;
    private MapConfigsCommand? _mapConfigsCommand;

    // Player Commands
    private VoicesCommand? _voicesCommand;

    // Spawn Editor Commands
    private ShowSpawnsCommand? _showSpawnsCommand;
    private AddSpawnCommand? _addSpawnCommand;
    private RemoveSpawnCommand? _removeSpawnCommand;
    private NearestSpawnCommand? _nearestSpawnCommand;
    private HideSpawnsCommand? _hideSpawnsCommand;
    #endregion

    #region Capabilities
    public static PluginCapability<IRetakesPluginEventSender> RetakesPluginEventSenderCapability { get; } = new("retakes_plugin:event_sender");
    #endregion

    #region State
    private readonly HashSet<CCSPlayerController> _hasMutedVoices = [];
    private readonly HashSet<ulong> _awpOptInPlayers = [];
    private readonly Dictionary<CsTeam, ulong> _roundAwpOwners = [];
    private bool _buyWindowOpen;
    private int _buyWindowToken;
    private Bombsite? _currentBombsite;
    #endregion

    public RetakesPlugin()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new VectorJsonConverter(),
                new QAngleJsonConverter()
            }
        };
    }

    public override void Load(bool hotReload)
    {
        Utils.Logger.LogInfo("Main", "Plugin loading...");

        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        AddCommandListener("jointeam", OnCommandJoinTeam);
        AddCommandListener("buy", OnCommandBuy);
        AddCommandListener("buymenu", OnCommandBuyMenu);
        AddCommandListener("autobuy", OnCommandBlockedNativeBuyShortcut);
        AddCommandListener("rebuy", OnCommandBlockedNativeBuyShortcut);

        var retakesPluginEventSender = new RetakesPluginEventSender();
        Capabilities.RegisterPluginCapability(RetakesPluginEventSenderCapability, () => retakesPluginEventSender);

        // Register event handlers
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventRoundPrestart>(OnRoundPreStart);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundPoststart>(OnRoundPostStart);
        RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventBombBeginplant>(OnBombBeginPlant);
        RegisterEventHandler<EventBombBegindefuse>(OnBombBeginDefuse);
        RegisterEventHandler<EventBombPlanted>(OnBombPlanted, HookMode.Pre);
        RegisterEventHandler<EventBombDefused>(OnBombDefused);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventInfernoStartburn>(OnInfernoStartBurn);
        RegisterEventHandler<EventInfernoExtinguish>(OnInfernoExtinguish);
        RegisterEventHandler<EventInfernoExpire>(OnInfernoExpire);
        RegisterEventHandler<EventHegrenadeDetonate>(OnHeGrenadeDetonate);
        RegisterEventHandler<EventMolotovDetonate>(OnMolotovDetonate);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Pre);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam, HookMode.Pre);

        if (hotReload)
        {
            Utils.Logger.LogServer($"Update detected, restarting map...");
            Server.ExecuteCommand($"map {Server.MapName}");
        }

        Utils.Logger.LogInfo("Main", "Plugin loaded successfully");
    }

    #region Map Initialization
    private void OnMapStart(string mapName)
    {
        Utils.Logger.LogInfo("MapStart", $"Map started: {mapName}");

        SpawnService.Reset();
        _autoJoinService?.OnMapStart();
        _buyService?.OnMapStart();
        _antiAfkService?.OnMapStart();
        _currentBombsite = null;

        AddTimer(1.0f, ServerHelper.ExecuteRetakesConfiguration);

        InitializeServices(mapName);
    }

    private void InitializeServices(string mapName, string? customMapConfig = null)
    {
        try
        {
            // Initialize MapConfigService
            _mapConfigService = new MapConfigService(ModuleDirectory, customMapConfig ?? mapName, _jsonOptions);
            _mapConfigService.Load();

            // Initialize Managers
            _spawnManager = new SpawnManager(_mapConfigService);
            _allocationService = new AllocationService(_random);
            _chatMessageService = new ChatMessageService(this);
            _autoJoinService = new AutoJoinService(this, _chatMessageService);
            _buyService = new BuyService(this, _random);
            _antiAfkService = new AntiAfkService(this);
            _instantPlantService = new InstantPlantService(Config.Bomb.IsInstantPlantEnabled, Config.Bomb.IsAutoPlantEnabled);
            _instantDefuseService = new InstantDefuseService(this, _chatMessageService, Config.Bomb.IsInstantDefuseEnabled, Config.Bomb.InstantDefuseThreatRadius);
            _retakeStatusService = new RetakeStatusService(this);

            _gameManager = new GameManager(
                this,
                new QueueManager(
                    this,
                    Config.Game.MaxPlayers,
                    Config.Team.TerroristRatio,
                    Config.Queue.GetPriorityFlags(),
                    Config.Queue.GetImmunityFlags(),
                    Config.Team.ShouldForceEvenTeamsWhenPlayerCountIsMultipleOf10,
                    Config.Team.ShouldPreventTeamChangesMidRound
                ),
                Config.Team.RoundsToScramble,
                Config.Team.IsScrambleEnabled,
                Config.Queue.ShouldRemoveSpectators,
                Config.Team.IsBalanceEnabled
            );

            _breakerManager = new BreakerManager(
                Config.Game.ShouldBreakBreakables,
                Config.Game.ShouldOpenDoors
            );

            _announcementService = new AnnouncementService(
                this,
                _random,
                _hasMutedVoices,
                Config.MapConfig.EnableBombsiteAnnouncementVoices,
                Config.MapConfig.EnableBombsiteAnnouncementCenter
            );

            // Initialize Event Handlers
            _roundEventHandlers = new RoundEventHandlers(
                this,
                _gameManager,
                _spawnManager,
                _breakerManager,
                _allocationService,
                _buyService,
                _announcementService,
                Config.Bomb.IsAutoPlantEnabled,
                Config.Game.EnableFallbackAllocation,
                Config.MapConfig.EnableFallbackBombsiteAnnouncement,
                _random
            );

            _playerEventHandlers = new PlayerEventHandlers(this, _gameManager, _hasMutedVoices, _autoJoinService, _antiAfkService);
            _bombEventHandlers = new BombEventHandlers(_instantPlantService, _instantDefuseService);

            // Initialize Commands
            _forceBombsiteCommand = new ForceBombsiteCommand(this, _roundEventHandlers);
            _forceBombsiteStopCommand = new ForceBombsiteStopCommand(this, _roundEventHandlers);
            _scrambleCommand = new ScrambleCommand(this, _gameManager);
            _debugQueuesCommand = new DebugQueuesCommand(this, _gameManager);

            _mapConfigCommand = new MapConfigCommand(this, ModuleDirectory, (configName) =>
            {
                InitializeServices(Server.MapName, configName);
            });
            _mapConfigsCommand = new MapConfigsCommand(this, ModuleDirectory);

            _voicesCommand = new VoicesCommand(this, Config, _hasMutedVoices);

            _showSpawnsCommand = new ShowSpawnsCommand(this);
            _addSpawnCommand = new AddSpawnCommand(this, _showSpawnsCommand);
            _removeSpawnCommand = new RemoveSpawnCommand(this, _showSpawnsCommand);
            _nearestSpawnCommand = new NearestSpawnCommand(this, _showSpawnsCommand);
            _hideSpawnsCommand = new HideSpawnsCommand(this, _showSpawnsCommand);

            // Set command references in event handlers
            _roundEventHandlers?.SetCommandReferences(_showSpawnsCommand);

            // Register all commands
            RegisterCommands();

            Utils.Logger.LogInfo("Services", "All services initialized successfully");
        }
        catch (Exception ex)
        {
            Utils.Logger.LogException("Services", ex);
        }
    }

    private void RegisterCommands()
    {
        if (_forceBombsiteCommand == null || _forceBombsiteStopCommand == null || _scrambleCommand == null || _debugQueuesCommand == null || _mapConfigCommand == null || _mapConfigsCommand == null || _voicesCommand == null || _showSpawnsCommand == null || _addSpawnCommand == null || _removeSpawnCommand == null || _nearestSpawnCommand == null || _hideSpawnsCommand == null)
        {
            Utils.Logger.LogWarning("Commands", "Cannot register commands - command handlers not initialized");
            return;
        }

        // Admin Commands
        AddCommand("css_forcebombsite", "Force the retakes to occur from a single bombsite.", _forceBombsiteCommand.OnCommand);
        AddCommand("css_forcebombsitestop", "Clear the forced bombsite and return back to normal.", _forceBombsiteStopCommand.OnCommand);
        AddCommand("css_scramble", "Sets teams to scramble on the next round.", _scrambleCommand.OnCommand);
        AddCommand("css_scrambleteams", "Sets teams to scramble on the next round.", _scrambleCommand.OnCommand);
        AddCommand("css_debugqueues", "Prints the state of the queues to the console.", _debugQueuesCommand.OnCommand);

        // Map Config Commands
        AddCommand("css_mapconfig", "Forces a specific map config file to load.", _mapConfigCommand.OnCommand);
        AddCommand("css_setmapconfig", "Forces a specific map config file to load.", _mapConfigCommand.OnCommand);
        AddCommand("css_loadmapconfig", "Forces a specific map config file to load.", _mapConfigCommand.OnCommand);
        AddCommand("css_mapconfigs", "Displays a list of available map configs.", _mapConfigsCommand.OnCommand);
        AddCommand("css_viewmapconfigs", "Displays a list of available map configs.", _mapConfigsCommand.OnCommand);
        AddCommand("css_listmapconfigs", "Displays a list of available map configs.", _mapConfigsCommand.OnCommand);

        // Spawn Editor Commands
        AddCommand("css_showspawns", "Show the spawns for the specified bombsite.", _showSpawnsCommand.OnCommand);
        AddCommand("css_spawns", "Show the spawns for the specified bombsite.", _showSpawnsCommand.OnCommand);
        AddCommand("css_edit", "Show the spawns for the specified bombsite.", _showSpawnsCommand.OnCommand);
        AddCommand("css_add", "Creates a new retakes spawn for the bombsite currently shown.", _addSpawnCommand.OnCommand);
        AddCommand("css_addspawn", "Creates a new retakes spawn for the bombsite currently shown.", _addSpawnCommand.OnCommand);
        AddCommand("css_new", "Creates a new retakes spawn for the bombsite currently shown.", _addSpawnCommand.OnCommand);
        AddCommand("css_newspawn", "Creates a new retakes spawn for the bombsite currently shown.", _addSpawnCommand.OnCommand);
        AddCommand("css_remove", "Deletes the nearest retakes spawn.", _removeSpawnCommand.OnCommand);
        AddCommand("css_removespawn", "Deletes the nearest retakes spawn.", _removeSpawnCommand.OnCommand);
        AddCommand("css_delete", "Deletes the nearest retakes spawn.", _removeSpawnCommand.OnCommand);
        AddCommand("css_deletespawn", "Deletes the nearest retakes spawn.", _removeSpawnCommand.OnCommand);
        AddCommand("css_nearestspawn", "Goes to nearest retakes spawn.", _nearestSpawnCommand.OnCommand);
        AddCommand("css_nearest", "Goes to nearest retakes spawn.", _nearestSpawnCommand.OnCommand);
        AddCommand("css_hidespawns", "Exits the spawn editing mode.", _hideSpawnsCommand.OnCommand);
        AddCommand("css_done", "Exits the spawn editing mode.", _hideSpawnsCommand.OnCommand);
        AddCommand("css_exitedit", "Exits the spawn editing mode.", _hideSpawnsCommand.OnCommand);

        // Player Commands
        AddCommand("css_voices", "Toggles whether or not you want to hear bombsite voice announcements.", _voicesCommand.OnCommand);
        AddCommand("css_awp", "Toggle AWP queue participation.", OnCommandAwp);
        AddCommand("css_a", "Shows the allowed weapons for the current retake round.", OnCommandBuyAlias);
        AddCommand("css_w", "Buys a weapon from the current retake pool.", OnCommandBuyAlias);
        AddCommand("css_1", "Retake buy menu option 1.", OnCommandBuyAlias);
        AddCommand("css_2", "Retake buy menu option 2.", OnCommandBuyAlias);
        AddCommand("css_3", "Retake buy menu option 3.", OnCommandBuyAlias);
        AddCommand("css_4", "Retake buy menu option 4.", OnCommandBuyAlias);
        AddCommand("css_5", "Retake buy menu option 5.", OnCommandBuyAlias);
        AddCommand("css_6", "Retake buy menu option 6.", OnCommandBuyAlias);
        AddCommand("css_7", "Retake buy menu option 7.", OnCommandBuyAlias);
        AddCommand("css_8", "Retake buy menu option 8.", OnCommandBuyAlias);
        AddCommand("css_9", "Retake buy menu option 9.", OnCommandBuyAlias);

        Utils.Logger.LogInfo("Commands", "All commands registered successfully");
    }
    #endregion

    #region Event Handlers
    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        return _playerEventHandlers?.OnPlayerConnectFull(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnRoundPreStart(EventRoundPrestart @event, GameEventInfo info)
    {
        return _roundEventHandlers?.OnRoundPreStart(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        var result = _roundEventHandlers?.OnRoundStart(@event, info) ?? HookResult.Continue;
        _bombEventHandlers?.OnRoundStart(@event, info);
        return result;
    }

    private HookResult OnRoundPostStart(EventRoundPoststart @event, GameEventInfo info)
    {
        return _roundEventHandlers?.OnRoundPostStart(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
    {
        return _roundEventHandlers?.OnRoundFreezeEnd(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        var result = _roundEventHandlers?.OnRoundEnd(@event, info) ?? HookResult.Continue;
        _bombEventHandlers?.OnRoundEnd(@event, info);
        return result;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        return _playerEventHandlers?.OnPlayerSpawn(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        return _playerEventHandlers?.OnPlayerDeath(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnBombBeginPlant(EventBombBeginplant @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnBombBeginPlant(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnBombBeginDefuse(EventBombBegindefuse @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnBombBeginDefuse(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        var result = _roundEventHandlers?.OnBombPlanted(@event, info) ?? HookResult.Continue;
        _bombEventHandlers?.OnBombPlanted(@event, info);
        return result;
    }

    private HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    {
        var result = _roundEventHandlers?.OnBombDefused(@event, info) ?? HookResult.Continue;
        _bombEventHandlers?.OnBombDefused(@event, info);
        return result;
    }

    private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnGrenadeThrown(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnInfernoStartBurn(EventInfernoStartburn @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnInfernoStartBurn(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnInfernoExtinguish(EventInfernoExtinguish @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnInfernoExtinguish(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnInfernoExpire(EventInfernoExpire @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnInfernoExpire(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnHeGrenadeDetonate(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnMolotovDetonate(EventMolotovDetonate @event, GameEventInfo info)
    {
        return _bombEventHandlers?.OnMolotovDetonate(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (PlayerHelper.IsValid(@event.Userid))
        {
            RemoveAwpState(@event.Userid!);
        }

        return _playerEventHandlers?.OnPlayerDisconnect(@event, info) ?? HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        return _playerEventHandlers?.OnPlayerTeam(@event, info) ?? HookResult.Continue;
    }
    #endregion

    #region Command Handlers
    private void OnCommandAwp(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (!PlayerHelper.IsValid(player))
        {
            return;
        }

        ToggleAwpOptIn(player!, true);
    }

    private void OnCommandBuyAlias(CCSPlayerController? player, CommandInfo commandInfo)
    {
        _buyService?.TryHandleBuyCommand(
            player,
            commandInfo.ArgCount >= 2
                ? commandInfo.GetArg(1)
                : commandInfo.GetArg(0).Replace("!", string.Empty).Replace("css_", string.Empty),
            _allocationService
        );
    }

    private HookResult OnCommandBuy(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (!PlayerHelper.IsValid(player))
        {
            return HookResult.Handled;
        }

        var selectedItem = commandInfo.ArgCount >= 2 ? commandInfo.GetArg(1).Trim().Trim('"') : null;
        _buyService?.TryHandleBuyCommand(player!, selectedItem, _allocationService);
        return HookResult.Handled;
    }

    private HookResult OnCommandBuyMenu(CCSPlayerController? player, CommandInfo commandInfo)
    {
        var gameRules = GameRulesHelper.GetGameRulesOrNull();
        if (gameRules?.WarmupPeriod == true)
        {
            return HookResult.Continue;
        }

        if (!PlayerHelper.IsValid(player))
        {
            return HookResult.Continue;
        }

        if (_buyWindowOpen)
        {
            _buyService?.ShowOptions(player!);
            return HookResult.Handled;
        }

        player!.PrintToChat($"{Localizer["retakes.prefix"]} O menu de compra nativo so fica liberado nos primeiros {BuyWindowSeconds:0} segundos do retake.");
        return HookResult.Handled;
    }

    private HookResult OnCommandBlockedNativeBuyShortcut(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (PlayerHelper.IsValid(player))
        {
            player!.PrintToChat($"{Localizer["retakes.prefix"]} Rebuy e autobuy nao sao usados no retake. Escolha a arma pelo menu ou com !a.");
        }

        return HookResult.Handled;
    }

    private HookResult OnCommandJoinTeam(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (_gameManager == null)
        {
            Utils.Logger.LogWarning("Commands", "Game manager not loaded");
            return HookResult.Continue;
        }

        if (!PlayerHelper.IsValid(player) || commandInfo.ArgCount < 2 ||
            !Enum.TryParse<CounterStrikeSharp.API.Modules.Utils.CsTeam>(commandInfo.GetArg(1), out var toTeam))
        {
            return HookResult.Handled;
        }

        var fromTeam = player!.Team;
        Utils.Logger.LogDebug("Commands", $"[{player.PlayerName}] {fromTeam} -> {toTeam}");

        _gameManager.QueueManager.DebugQueues(true);
        var response = _gameManager.QueueManager.PlayerJoinedTeam(player, fromTeam, toTeam);
        _gameManager.QueueManager.DebugQueues(false);

        if (_gameManager.QueueManager.ActivePlayers.Count == 0)
        {
            Utils.Logger.LogDebug("Commands", "No active players, updating queue and restarting game");
            _gameManager.QueueManager.ClearRoundTeams();
            _gameManager.QueueManager.Update();
            GameRulesHelper.RestartGame();
        }

        return response;
    }
    #endregion

    public override void Unload(bool hotReload)
    {
        Utils.Logger.LogInfo("Main", "Plugin unloading...");
        base.Unload(hotReload);
    }

    public void PrepareRoundAwpOwners(IEnumerable<CCSPlayerController> activePlayers)
    {
        _roundAwpOwners.Clear();

        if (_buyService == null || !_buyService.IsAwpAllowedThisRound)
        {
            return;
        }

        var validActivePlayers = activePlayers
            .Where(PlayerHelper.IsValid)
            .Where(p => p.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist)
            .ToList();

        foreach (var team in new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist })
        {
            var candidates = validActivePlayers
                .Where(p => p.Team == team && _awpOptInPlayers.Contains(p.SteamID))
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            var selected = candidates[_random.Next(candidates.Count)];
            _roundAwpOwners[team] = selected.SteamID;
            selected.PrintToChat($"{Localizer["retakes.prefix"]} AWP liberada para voce neste round.");
        }
    }

    public void StartBuyWindow(float durationSeconds)
    {
        _buyWindowOpen = true;
        _buyWindowToken++;
        var token = _buyWindowToken;

        // Keep the server cvars aligned with the intended retake buy rules.
        // sv_buy_status_override=0 means both teams can buy; 3 would disable buying.
        Server.ExecuteCommand($"mp_buytime {NativeBuyMenuWindowSeconds}");
        Server.ExecuteCommand("mp_buy_anywhere 1");
        Server.ExecuteCommand("mp_buy_during_immunity 1");
        Server.ExecuteCommand("sv_buy_status_override 0");

        AddTimer(durationSeconds, () =>
        {
            if (token != _buyWindowToken)
            {
                return;
            }

            _buyWindowOpen = false;
        });
    }

    public bool IsBuyWindowOpen => _buyWindowOpen;

    public void RequestAwp(CCSPlayerController player)
    {
        ToggleAwpOptIn(player, true);
    }

    public void SetCurrentBombsite(Bombsite? bombsite)
    {
        _currentBombsite = bombsite;

        if (bombsite == null)
        {
            _retakeStatusService?.EndRound();
            return;
        }

        _retakeStatusService?.BeginRound(bombsite.Value);
    }

    public void ShowRetakeStatus(CCSPlayerController player)
    {
        if (_currentBombsite == null)
        {
            return;
        }

        _retakeStatusService?.ShowForPlayer(player);
    }

    public void ShowRetakeStatusForActivePlayers()
    {
        if (_currentBombsite == null || _gameManager == null)
        {
            return;
        }

        _retakeStatusService?.ShowForPlayers(_gameManager.QueueManager.ActivePlayers.Where(PlayerHelper.IsValid));
    }

    public bool ShouldReceiveAwpThisRound(CCSPlayerController player)
    {
        if (!PlayerHelper.IsValid(player) || player.Team is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
        {
            return false;
        }

        if (_buyService == null || !_buyService.IsAwpAllowedThisRound)
        {
            return false;
        }

        return _roundAwpOwners.TryGetValue(player.Team, out var ownerSteamId) && ownerSteamId == player.SteamID;
    }

    public void RemoveAwpState(CCSPlayerController player)
    {
        _awpOptInPlayers.Remove(player.SteamID);

        if (_roundAwpOwners.TryGetValue(player.Team, out var ownerSteamId) && ownerSteamId == player.SteamID)
        {
            _roundAwpOwners.Remove(player.Team);
        }
    }

    private void ToggleAwpOptIn(CCSPlayerController player, bool tryImmediateGrant)
    {
        if (!PlayerHelper.IsValid(player) || player.Team is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
        {
            return;
        }

        if (_awpOptInPlayers.Contains(player.SteamID))
        {
            _awpOptInPlayers.Remove(player.SteamID);

            if (_roundAwpOwners.TryGetValue(player.Team, out var ownerSteamId) && ownerSteamId == player.SteamID)
            {
                _roundAwpOwners.Remove(player.Team);
            }

            player.PrintToChat($"{Localizer["retakes.prefix"]} AWP desativada para voce.");
            return;
        }

        _awpOptInPlayers.Add(player.SteamID);
        player.PrintToChat($"{Localizer["retakes.prefix"]} AWP ativada para voce.");

        if (_buyService == null || !_buyService.IsAwpAllowedThisRound)
        {
            player.PrintToChat($"{Localizer["retakes.prefix"]} AWP entrou na fila e so sera entregue em rounds FULL.");
            return;
        }

        if (!tryImmediateGrant || !_buyWindowOpen || !PlayerCanGetImmediateAwp(player))
        {
            if (!_buyWindowOpen)
            {
                player.PrintToChat($"{Localizer["retakes.prefix"]} AWP entrou na fila, mas a liberacao imediata so vale no periodo de compra.");
            }

            return;
        }

        _roundAwpOwners[player.Team] = player.SteamID;
        player.GiveNamedItem(CsItem.AWP);
        player.PrintToChat($"{Localizer["retakes.prefix"]} AWP liberada agora para voce.");
    }

    private bool PlayerCanGetImmediateAwp(CCSPlayerController player)
    {
        if (_gameManager == null)
        {
            return false;
        }

        if (!_gameManager.QueueManager.ActivePlayers.Contains(player))
        {
            return false;
        }

        if (_roundAwpOwners.TryGetValue(player.Team, out var ownerSteamId))
        {
            var ownerStillActive = _gameManager.QueueManager.ActivePlayers
                .Any(p => PlayerHelper.IsValid(p) && p.SteamID == ownerSteamId && p.Team == player.Team);

            if (ownerStillActive)
            {
                return false;
            }

            _roundAwpOwners.Remove(player.Team);
        }

        return true;
    }
}
