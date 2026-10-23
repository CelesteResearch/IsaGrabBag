// SPDX-License-Identifier: MIT

using System.Reflection;
using System;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Interop;
using Celeste.Mods.IsaGrabBag.Interop;
using Celeste.Simulation;

namespace Celeste.Mods.IsaGrabBag.Runtime.ZipLines
{
    public sealed class ZipLineGravityInterop : IDisposable
    {
        private readonly ZipLineGravityImports imports = new();
        private readonly IDisposable lease;

        public ZipLineGravityInterop(SourceModInteropProcess process)
        {
            lease = process.RegisterOwned(new SourceModInteropActualTypeBinding(typeof(ZipLineGravityImports), imports,
                exportedMethods: Array.Empty<MethodInfo>()));
        }

        public bool HasInterop => imports.IsPlayerInverted != null;
        public bool IsPlayerInverted() => imports.IsPlayerInverted?.Invoke() ?? false;

        public SourceComponentState CreatePlayerGravityListener(Action<RoomPlayerSession, int, float> callback)
            => imports.CreatePlayerGravityListener?.Invoke(callback);

        public void BeginOverride() => imports.BeginOverride?.Invoke();
        public void EndOverride() => imports.EndOverride?.Invoke();
        public void Dispose() => lease.Dispose();
    }
}
