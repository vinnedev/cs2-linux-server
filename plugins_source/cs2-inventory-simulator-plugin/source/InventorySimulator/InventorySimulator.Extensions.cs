/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace InventorySimulator;

public static class Extensions
{
    private static MemoryFunctionWithReturn<T1, TResult>? TryCreate<T1, TResult>(string signatureName, string? modulePath = null)
    {
        try
        {
            var signature = GameData.GetSignature(signatureName);
            return modulePath == null
                ? new MemoryFunctionWithReturn<T1, TResult>(signature)
                : new MemoryFunctionWithReturn<T1, TResult>(signature, modulePath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static MemoryFunctionWithReturn<T1, T2, TResult>? TryCreate<T1, T2, TResult>(string signatureName, string? modulePath = null)
    {
        try
        {
            var signature = GameData.GetSignature(signatureName);
            return modulePath == null
                ? new MemoryFunctionWithReturn<T1, T2, TResult>(signature)
                : new MemoryFunctionWithReturn<T1, T2, TResult>(signature, modulePath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static MemoryFunctionWithReturn<T1, T2, T3, TResult>? TryCreate<T1, T2, T3, TResult>(string signatureName, string? modulePath = null)
    {
        try
        {
            var signature = GameData.GetSignature(signatureName);
            return modulePath == null
                ? new MemoryFunctionWithReturn<T1, T2, T3, TResult>(signature)
                : new MemoryFunctionWithReturn<T1, T2, T3, TResult>(signature, modulePath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static MemoryFunctionWithReturn<T1, T2, T3, T4, TResult>? TryCreate<T1, T2, T3, T4, TResult>(string signatureName, string? modulePath = null)
    {
        try
        {
            var signature = GameData.GetSignature(signatureName);
            return modulePath == null
                ? new MemoryFunctionWithReturn<T1, T2, T3, T4, TResult>(signature)
                : new MemoryFunctionWithReturn<T1, T2, T3, T4, TResult>(signature, modulePath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static MemoryFunctionWithReturn<T1, T2, T3, T4, T5, T6, T7, T8, TResult>? TryCreate<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string signatureName, string? modulePath = null)
    {
        try
        {
            var signature = GameData.GetSignature(signatureName);
            return modulePath == null
                ? new MemoryFunctionWithReturn<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(signature)
                : new MemoryFunctionWithReturn<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(signature, modulePath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // sajad0x0 from UC ended up helping me figuring out this signature.
    public static readonly MemoryFunctionWithReturn<IntPtr, string, int>? ChangeSubclassMemFunc = TryCreate<IntPtr, string, int>("ChangeSubclass");

    // This was made public by skuzzis.
    // First CS# public implementation by stefanx111.
    public static readonly MemoryFunctionWithReturn<IntPtr, string, float, int>? SetOrAddAttributeValueByNameMemFunc =
        TryCreate<IntPtr, string, float, int>("CAttributeList_SetOrAddAttributeValueByName");

    // This was made public by skuzzis.
    // First CS# public implementation by stefanx111.
    public static readonly MemoryFunctionWithReturn<IntPtr, string, int, int>? SetBodygroupMemFunc =
        TryCreate<IntPtr, string, int, int>("CBaseModelEntity_SetBodygroup");

    public static readonly MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>? IsAbleToApplySprayMemFunc =
        TryCreate<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>("CCSPlayerPawn_IsAbleToApplySpray");

    public static readonly MemoryFunctionWithReturn<IntPtr, IntPtr, int, bool, float>? ProcessUsercmds =
        TryCreate<IntPtr, IntPtr, int, bool, float>("CCSPlayerController_ProcessUsercmds");

    public static readonly MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr>? UpdateSelectTeamPreview =
        TryCreate<IntPtr, IntPtr, IntPtr>("CCSPlayerController_UpdateSelectTeamPreview");

    public static readonly MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, short, IntPtr, byte, int, int, byte>? ConnectFunc =
        TryCreate<IntPtr, IntPtr, IntPtr, short, IntPtr, byte, int, int, byte>("CServerSideClientBase_Connect", Addresses.EnginePath);

    public static readonly MemoryFunctionWithReturn<IntPtr, uint, byte>? SetSignonStateFunc =
        TryCreate<IntPtr, uint, byte>("CServerSideClientBase_SetSignonState", Addresses.EnginePath);

    public static int ChangeSubclass(this CBasePlayerWeapon weapon, ushort itemDef)
    {
        return ChangeSubclassMemFunc == null ? 0 : ChangeSubclassMemFunc.Invoke(weapon.Handle, itemDef.ToString());
    }

    public static int SetOrAddAttributeValueByName(this CAttributeList attributeList, string attribDefName, float value)
    {
        return SetOrAddAttributeValueByNameMemFunc == null ? 0 : SetOrAddAttributeValueByNameMemFunc.Invoke(attributeList.Handle, attribDefName, value);
    }

    public static int SetBodygroup(this CCSPlayerPawn pawn, string group, int value)
    {
        return SetBodygroupMemFunc == null ? 0 : SetBodygroupMemFunc.Invoke(pawn.Handle, group, value);
    }

    public static bool IsAbleToApplySpray(this CCSPlayerPawn pawn, IntPtr ptr = 0)
    {
        return IsAbleToApplySprayMemFunc == null || IsAbleToApplySprayMemFunc.Invoke(pawn.Handle, ptr, 0, 0) == IntPtr.Zero;
    }
}
