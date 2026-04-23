/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Memory;

namespace InventorySimulator;

public partial class InventorySimulator : BasePlugin
{
    public override string ModuleAuthor => "Ian Lucas";
    public override string ModuleDescription => "Inventory Simulator (inventory.cstrike.app)";
    public override string ModuleName => "InventorySimulator";
    public override string ModuleVersion => "1.0.0";

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);
        RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventRoundPrestart>(OnRoundPrestart);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        if (Extensions.ProcessUsercmds != null && !IsProcessUsercmdsHooked)
        {
            Extensions.ProcessUsercmds.Hook(OnProcessUsercmdsPost, HookMode.Post);
            IsProcessUsercmdsHooked = true;
        }
        if (!IsGiveNamedItemHooked)
        {
            VirtualFunctions.GiveNamedItemFunc.Hook(OnGiveNamedItemPost, HookMode.Post);
            IsGiveNamedItemHooked = true;
        }
        if (Extensions.UpdateSelectTeamPreview != null && !IsUpdateSelectTeamPreviewHooked)
        {
            Extensions.UpdateSelectTeamPreview.Hook(OnUpdateSelectTeamPreview, HookMode.Post);
            IsUpdateSelectTeamPreviewHooked = true;
        }
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeathPre, HookMode.Pre);
        RegisterEventHandler<EventRoundMvp>(OnRoundMvpPre, HookMode.Pre);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        invsim_file.ValueChanged += OnInvsimFileChanged;
        OnInvsimFileChanged(null, invsim_file.Value);

        invsim_require_inventory.ValueChanged += OnInvSimRequireInventoryChange;
        OnInvSimRequireInventoryChange(null, invsim_require_inventory.Value);
    }

    public override void Unload(bool hotReload)
    {
        invsim_file.ValueChanged -= OnInvsimFileChanged;
        invsim_require_inventory.ValueChanged -= OnInvSimRequireInventoryChange;

        if (IsRequireInventoryHooksHooked && Extensions.ConnectFunc != null && Extensions.SetSignonStateFunc != null)
        {
            Extensions.ConnectFunc.Unhook(OnConnect, HookMode.Post);
            Extensions.SetSignonStateFunc.Unhook(OnSetSignonState, HookMode.Pre);
            IsRequireInventoryHooksHooked = false;
        }

        if (IsProcessUsercmdsHooked && Extensions.ProcessUsercmds != null)
        {
            Extensions.ProcessUsercmds.Unhook(OnProcessUsercmdsPost, HookMode.Post);
            IsProcessUsercmdsHooked = false;
        }

        if (IsGiveNamedItemHooked)
        {
            VirtualFunctions.GiveNamedItemFunc.Unhook(OnGiveNamedItemPost, HookMode.Post);
            IsGiveNamedItemHooked = false;
        }

        if (IsUpdateSelectTeamPreviewHooked && Extensions.UpdateSelectTeamPreview != null)
        {
            Extensions.UpdateSelectTeamPreview.Unhook(OnUpdateSelectTeamPreview, HookMode.Post);
            IsUpdateSelectTeamPreviewHooked = false;
        }
    }
}
