// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Interop;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Everest.Runtime.Events;
using Celeste.Everest.Runtime.Levels.Update;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation.Gameplay.Player.Intro;
using Celeste.Simulation.Gameplay.Player.Presentation.Animation;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;
using Celeste.Simulation.Gameplay.Player.State;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.IsaGrabBag.Runtime.ZipLines
{
    public sealed partial class ZipLineRuntime
    {
        public void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                Gravity = new ZipLineGravityInterop(((ISourceModInteropEnvironment)Engine).ModInterop);
                for (int i = liveLines.Count - 1; i >= 0; i--)
                    if (liveLines[i].TryGetTarget(out var line))
                    {
                        if (line.UsesGravityListener && Gravity.HasInterop) line.RestoreGravityListener();
                    }
                    else liveLines.RemoveAt(i);

                ((IEverestGameEventsEnvironment)Engine).GameEvents.OnLoadLevel += LevelLoaded;
                ((IEverestLevelEntityLoadEnvironment)Engine).LevelUpdateEvents.OnTransitionTo += Transition;
                EverestPlayerSourceEvents.OnRegisterStates += RegisterStates;
                EverestPlayerSourceEvents.OnSpawn += Spawn;
                EverestPlayerSourceEvents.OnAfterUpdate += AfterUpdate;
                animationHook = new Hook(typeof(PlayerAnimation).GetMethod(nameof(PlayerAnimation.Update)),
                    (AnimationHook)UpdateAnimation);
                for (int i = liveControls.Count - 1; i >= 0; i--)
                    if (liveControls[i].TryGetTarget(out var control)) control.Restore();
                    else liveControls.RemoveAt(i);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void RegisterStates(RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, Engine)) StateFor(player);
        }

        private void Spawn(RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, Engine)) Player = player;
        }

        private void AfterUpdate(RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, Engine)) return;
            Buffer = ScalarMath.Approach(Buffer, 0, Engine.DeltaTime);
            if (!player.Input.GrabCheck) Buffer = 0;
        }

        private void LevelLoaded(OriginalLevelSceneRoot level, PlayerIntroType intro, bool fromLoader)
        {
            if (!ReferenceEquals(level.EngineContext, Engine)) return;
            CurrentGrabbed = LastGrabbed = null;
            if (!fromLoader || !(level.Session.StartedFromBeginning || level.Session.RestartedFromGolden)) return;
            foreach (var room in ((Celeste.Game.Content.Maps.OriginalChapterMapData)level.Session.MapData).Levels)
            foreach (var entity in room.Entities)
                if (entity.Name == "isaBag/zipline")
                {
                    level.Session.SetFlag(Entities.ZipLines.ZipLine.NeverUsedZiplineFlag, true);
                    return;
                }
        }

        private void Transition(OriginalLevelSceneRoot level, OriginalLevelRoomData next,
            System.Numerics.Vector2 direction)
        {
            if (ReferenceEquals(level.EngineContext, Engine) && LastGrabbed != null)
                level.Session.SetFlag(Entities.ZipLines.ZipLine.NeverUsedZiplineFlag, false);
        }

        private delegate void AnimationOriginal(PlayerState player, int state, PlayerSprite sprite,
            PlayerAnimationContext context, IPlayerAnimationWorld world, float deltaTime);

        private delegate void AnimationHook(AnimationOriginal original, PlayerState player, int state,
            PlayerSprite sprite, PlayerAnimationContext context, IPlayerAnimationWorld world, float deltaTime);

        private void UpdateAnimation(AnimationOriginal original, PlayerState data, int state, PlayerSprite sprite,
            PlayerAnimationContext context, IPlayerAnimationWorld world, float deltaTime)
        {
            if (state > 25 && sprite.Entity is RoomPlayerSession player &&
                ReferenceEquals(player.EngineContext, Engine) && controls.TryGetValue(player, out var control) &&
                state == control.Index)
            {
                sprite.Scale.X = ScalarMath.Approach(sprite.Scale.X, 1f, 1.75f * Engine.DeltaTime);
                sprite.Scale.Y = ScalarMath.Approach(sprite.Scale.Y, 1f, 1.75f * Engine.DeltaTime);
                if (CurrentGrabbed != null && !CurrentGrabbed.Grabbed) return;
                sprite.PlayOffset("fallSlow_carry", .5f, false);
                sprite.Rate = 0;
            }
            else original(data, state, sprite, context, world, deltaTime);
        }

        public void Dispose()
        {
            if (!loaded) return;
            loaded = false;
            for (int i = liveControls.Count - 1; i >= 0; i--)
                if (liveControls[i].TryGetTarget(out var control)) control.Detach();
                else liveControls.RemoveAt(i);
            animationHook?.Dispose();
            animationHook = null;
            EverestPlayerSourceEvents.OnAfterUpdate -= AfterUpdate;
            EverestPlayerSourceEvents.OnSpawn -= Spawn;
            EverestPlayerSourceEvents.OnRegisterStates -= RegisterStates;
            ((IEverestLevelEntityLoadEnvironment)Engine).LevelUpdateEvents.OnTransitionTo -= Transition;
            ((IEverestGameEventsEnvironment)Engine).GameEvents.OnLoadLevel -= LevelLoaded;
            for (int i = liveLines.Count - 1; i >= 0; i--)
                if (liveLines[i].TryGetTarget(out var line))
                {
                    line.Grabbed = false;
                    line.DetachGravityListener();
                }
                else liveLines.RemoveAt(i);

            Gravity?.Dispose();
            Gravity = null;
            Player = null;
            CurrentGrabbed = LastGrabbed = null;
        }
    }
}
