// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using System;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Interop;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Mods.IsaGrabBag.Entities.ZipLines;
using Celeste.Simulation;

namespace Celeste.Mods.IsaGrabBag.Runtime.ZipLines
{
    public sealed partial class ZipLineRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, ZipLineRuntime> instances = new();
        private readonly ConditionalWeakTable<RoomPlayerSession, ZipLinePlayerControl> controls = new();
        private readonly List<WeakReference<ZipLinePlayerControl>> liveControls = new();
        internal ISourceEngineContext Engine { get; }
        internal ISourceScalarArithmetic Scalar { get; }
        internal RoomPlayerSession Player;
        internal ZipLine CurrentGrabbed, LastGrabbed;
        internal float Buffer;
        internal float TargetOffset => Gravity.IsPlayerInverted() ? -2 : 22;
        internal ZipLineGravityInterop Gravity { get; private set; }
        public SpriteBank Sprites { get; private set; }
        private readonly List<WeakReference<ZipLine>> liveLines = new();
        internal void TrackLine(ZipLine line) => liveLines.Add(new WeakReference<ZipLine>(line));
        private bool loaded;
        private IDisposable animationHook;

        private ZipLineRuntime(ISourceEngineContext engine)
        {
            Engine = engine;
            Scalar = ((ISourceScalarEnvironment)engine).ScalarArithmetic;
        }

        public static ZipLineRuntime For(ISourceEngineContext engine) =>
            instances.GetValue(engine, e => new ZipLineRuntime(e));

        internal int StateFor(RoomPlayerSession player) => controls.TryGetValue(player, out var control)
            ? control.Index
            : controls.GetValue(player, RegisterControl).Index;

        private ZipLinePlayerControl RegisterControl(RoomPlayerSession player)
        {
            var control = new ZipLinePlayerControl(this, player);
            liveControls.Add(new WeakReference<ZipLinePlayerControl>(control));
            return control;
        }

        internal bool CanGrab(ZipLine line) => LastGrabbed != line || Buffer <= 0;

        public void LoadContent()
        {
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)Engine).SourceContentRegistry
                   .Get("IsaGrabBag:/Graphics/IsaGrabBag").Stream)
                xml.Load(stream);
            Sprites = new SpriteBank(Engine, ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.GameAtlas, xml)
                { XMLPath = "Graphics/IsaGrabBag.xml" };
        }
    }
}
