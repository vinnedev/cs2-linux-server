/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Numerics;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace InventorySimulator;

public static class HackExtensions
{
    // CounterStrikeSharp 1.0.367 no longer exposes the old view model services/classes used by
    // this plugin, so we intentionally skip the optional first-person viewmodel sync path.
    public static CBaseEntity? GetViewModel(this CCSPlayerController player)
    {
        return null;
    }
}

// This is a hack by Nuko.
[StructLayout(LayoutKind.Explicit, Size = 0x44)]
public unsafe struct TraceHitboxData
{
    [FieldOffset(0x38)]
    public int HitGroup;

    [FieldOffset(0x40)]
    public int HitboxId;
}

[StructLayout(LayoutKind.Explicit, Size = 0xB8)]
public unsafe struct GameTrace
{
    [FieldOffset(0)]
    public void* Surface;

    [FieldOffset(0x8)]
    public void* HitEntity;

    [FieldOffset(0x10)]
    public TraceHitboxData* HitboxData;

    [FieldOffset(0x50)]
    public uint Contents;

    [FieldOffset(0x78)]
    public Vector3 StartPos;

    [FieldOffset(0x84)]
    public Vector3 EndPos;

    [FieldOffset(0x90)]
    public Vector3 Normal;

    [FieldOffset(0x9C)]
    public Vector3 Position;

    [FieldOffset(0xAC)]
    public float Fraction;

    [FieldOffset(0xB6)]
    public bool AllSolid;
}
