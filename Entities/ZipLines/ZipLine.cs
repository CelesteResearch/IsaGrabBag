// SPDX-License-Identifier: MIT

using System.Numerics;
using System;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Rendering;
using Celeste.Mods.IsaGrabBag.Rendering.ZipLines;
using Celeste.Mods.IsaGrabBag.Runtime.ZipLines;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Actors.Facing;
using Celeste.Simulation;

namespace Celeste.Mods.IsaGrabBag.Entities.ZipLines
{
    [CustomEntity("isaBag/zipline")]
    [RuntimeTypeAlias("Celeste.Mod.IsaGrabBag.ZipLine")]
    public class ZipLine : SceneEntityState
    {
        public const string NeverUsedZiplineFlag = "IsaGrabBag_NeverUsedZipline";
        private readonly float height;
        private readonly Sprite sprite;
        internal readonly bool UsesStamina;
        internal float Speed;
        internal bool Grabbed;
        private readonly ZipLineRuntime runtime;
        public ISourceEngineContext EngineContext { get; }
        public float LeftEdge { get; }
        public float RightEdge { get; }

        public ZipLine(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            EngineContext = engine;
            runtime = ZipLineRuntime.For(engine);
            LeftEdge = RightEdge = X;
            foreach (Vector2 node in data.Nodes)
            {
                LeftEdge = Math.Min(node.X + offset.X, LeftEdge);
                RightEdge = Math.Max(node.X + offset.X, RightEdge);
            }

            UsesStamina = data.Bool("usesStamina", true);
            height = (data.Position + offset).Y;
            Collider = new HitboxColliderGeometry(20, 16, -10, 1);
            runtime.CurrentGrabbed = null;
            Depth = -500;
            sprite = runtime.Sprites.Create("zipline");
            sprite.Play("idle");
            sprite.JustifyOrigin(new Vector2(.5f, .25f));
            Add(sprite);
            UsesGravityListener = runtime.Gravity.HasInterop;
            runtime.TrackLine(this);
            if (UsesGravityListener)
            {
                RestoreGravityListener();
            }
        }

        internal readonly bool UsesGravityListener;
        private SourceComponentState gravityListener;

        internal void RestoreGravityListener()
        {
            gravityListener = runtime.Gravity.CreatePlayerGravityListener(GravityChanged);
            Add(gravityListener);
        }

        internal void DetachGravityListener()
        {
            if (gravityListener != null) Remove(gravityListener);
            gravityListener = null;
        }

        private void GravityChanged(RoomPlayerSession player, int gravity, float momentum)
        {
            if (runtime.CurrentGrabbed == null || !runtime.CurrentGrabbed.Grabbed) return;
            float targetY = runtime.CurrentGrabbed.Y + runtime.TargetOffset;
            if (targetY != player.Y)
            {
                runtime.Gravity.BeginOverride();
                player.MoveToY(targetY);
                runtime.Gravity.EndOverride();
            }
        }

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            EntityData data)
            => new ZipLine(level.EngineContext, data, offset);

        public override void Added(object scene)
        {
            base.Added(scene);
            ((OriginalLevelSceneRoot)scene).Scheduler.QueueSourceEntity(new ZipLineRenderer(this, runtime.Sprites));
        }

        public override void Update()
        {
            base.Update();
            RoomPlayerSession player = runtime.Player;
            if (player == null || player.Dead) return;
            if (Grabbed)
            {
                if (Math.Abs(player.Speed.X) > 20) player.LiftSpeed = player.Speed;
                if (player.CenterX > RightEdge || player.CenterX < LeftEdge) player.Data.Motion.Normal.Speed.X = 0;
                player.CenterX =
                    Celeste.Core.Mathematics.Framework.SourceFrameworkMath.Clamp(player.CenterX, LeftEdge, RightEdge);
                X = player.CenterX;
                Y = height;
            }
            else
            {
                if (runtime.CurrentGrabbed == null && !player.Dead && player.World.Contacts.CanUnDuck &&
                    player.Input.GrabCheck && runtime.CanGrab(this))
                {
                    bool isTired = player.Data.IsTired;
                    if (Celeste.Physics.Collision.SourceEntityCollision.Check(player, this) && !isTired)
                    {
                        runtime.CurrentGrabbed = this;
                        runtime.LastGrabbed = runtime.CurrentGrabbed;
                        player.Machine.State = runtime.StateFor(player);
                    }
                }

                X += Speed * EngineContext.DeltaTime;
                X = Celeste.Core.Mathematics.Framework.SourceFrameworkMath.Clamp(X, LeftEdge, RightEdge);
                Y = height;
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            if (Grabbed)
            {
                sprite.Visible = true;
                sprite.Play(runtime.Player.Facing == (int)FacingDirection.Left ? "held_l" : "held_r");
            }
            else sprite.Visible = false;

            base.Render(context);
        }
    }
}
