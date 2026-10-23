// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Components;
using Celeste.Simulation;
using MonoMod.ModInterop;

namespace Celeste.Mods.IsaGrabBag.Interop
{
    [ModImportName("GravityHelper")]
    public sealed class ZipLineGravityImports
    {
        public Func<bool> IsPlayerInverted;
        public Func<Action<RoomPlayerSession, int, float>, SourceComponentState> CreatePlayerGravityListener;
        public Action BeginOverride;
        public Action EndOverride;
    }
}
