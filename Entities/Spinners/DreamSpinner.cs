// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Gameplay.Platforms.Dream;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.IsaGrabBag.Runtime.Spinners;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using Celeste.Simulation.Gameplay.Effects.Debris;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.IsaGrabBag.Entities.Spinners
{
    [SceneTracked]
    [CustomEntity("isaBag/dreamSpinner", "isaBag/dreamSpinFake")]
    [RuntimeTypeAlias("Celeste.Mod.IsaGrabBag.DreamSpinner")]
    public sealed class DreamSpinner : SceneEntityState
    {
        private static readonly PackedColor DebrisColor = new(193, 138, 83);
        private readonly ISourceEngineContext engine;
        private readonly int id;
        private OriginalDreamBlockState block;
        private bool hasCollided;
        public bool OneUse;
        public bool Fake;
        public bool ShouldRender;
        internal readonly float Rotation;
        internal PackedColor Color;
        internal readonly List<Vector2> Offsets = new();

        public DreamSpinner(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            id = data.ID;
            OneUse = data.Bool("useOnce");
            Fake = data.Name == "isaBag/dreamSpinFake";
            Collider = new ColliderListGeometry(
                new CircleColliderGeometry(((ISourceVectorEnvironment)engine).VectorArithmetic, 6),
                new HitboxColliderGeometry(16, 4, -8, -3));
            Add(new PlayerColliderComponentState(OnPlayer));
            Add(new LedgeBlockerState());
            Depth = -8499;
            Collidable = Visible = false;
            Rotation = ((ISourceRandomEnvironment)engine).Random.Next(4) * ((float)System.Math.PI / 2f);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            if (Fake) return;
            var level = (OriginalLevelSceneRoot)scene;
            block = new EverestDreamBlockState(engine, Center - new Vector2(8), 16, 16, null, false, false,
                level.SolidWorldFactory);
            level.Scheduler.QueueSourceEntity(block);
            block.Visible = false;
            if (DreamSpinnerRuntime.Get(engine).Metadata(level).RoundDreamSpinner)
                block.Collider =
                    new CircleColliderGeometry(((ISourceVectorEnvironment)engine).VectorArithmetic, 9, 8, 8);
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            var level = (OriginalLevelSceneRoot)scene;
            Color = !level.Session.Inventory.DreamDash ? new PackedColor(25, 25, 25)
                : OneUse ? new PackedColor(30, 22, 10) : PackedColor.Black;
            foreach (DreamSpinner other in level.Scheduler.Entities.GetTrackedEntities(typeof(DreamSpinner)))
                if (other.OneUse == OneUse && other.id > id && Vector2.DistanceSquared(other.Position, Position) < 576f)
                    Offsets.Add((Position + other.Position) / 2f);
        }

        public override void Update()
        {
            if (Fake) return;
            if (!InView())
            {
                block.Active = false;
                return;
            }

            block.Active = true;
            base.Update();
            var level = (OriginalLevelSceneRoot)Scene;
            var entities = level.Scheduler.Entities.Entities;
            for (int index = 0; index < entities.Count; index++)
                if (entities[index] is CrystalDebrisState debris && debris.Collidable &&
                    SourceEntityCollision.CheckRect(debris, block.Collider.Bounds))
                    debris.RemoveSelf();
            var player = level.Scheduler.Entities.GetTrackedEntity<RoomPlayerSession>();
            if (player == null) return;
            if (OneUse)
            {
                bool overlaps = block.Collidable && SourceEntityCollision.Check(player, block);
                if (!overlaps && hasCollided)
                {
                    RemoveSelf();
                    block.RemoveSelf();
                    ((Celeste.Core.Audio.ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                        "event:/game/06_reflection/fall_spike_smash", Position);
                    CrystalDebrisState.Burst(engine, Center, DebrisColor, false, 4);
                    return;
                }

                hasCollided = overlaps;
            }

            bool dreamDash = (player.DashAttacking || player.Machine.State == 9) && player.Inventory.DreamDash;
            block.Collidable = dreamDash;
            Collidable = !dreamDash;
        }

        public bool InView()
        {
            var camera = ((OriginalLevelSceneRoot)Scene).Camera;
            return X > camera.X - 16f && Y > camera.Y - 16f && X < camera.X + 336f && Y < camera.Y + 196f;
        }

        private void OnPlayer(RoomPlayerSession player) =>
            player.Die(((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(player.Center - Center));

        internal void Unload()
        {
            block?.RemoveSelf();
            RemoveSelf();
        }
    }
}
