// SPDX-License-Identifier: MIT

using System.Collections;
using System.Numerics;
using System;
using Celeste.Core.Components.Animation;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Simulation;

namespace Celeste.Mods.IsaGrabBag.Runtime.ZipLines
{
    public sealed class ZipLinePlayerControl
    {
        private readonly ZipLineRuntime runtime;
        private readonly RoomPlayerSession owner;
        private readonly Func<int> update;
        private readonly Func<IEnumerator> coroutine;
        private readonly Action begin, end;
        public int Index { get; }

        public ZipLinePlayerControl(ZipLineRuntime runtime, RoomPlayerSession owner)
        {
            this.runtime = runtime;
            this.owner = owner;
            update = Update;
            coroutine = Coroutine;
            begin = Begin;
            end = End;
            Index = ((NamedSimulationStateMachine)owner.Machine).AddState("IsaGrabBag.ZipLine", update, coroutine,
                begin, end);
        }

        public void Restore() => owner.Machine.SetCallbacks(Index, update, coroutine, begin, end);

        public void Detach()
        {
            owner.Machine.SetCallbacks(Index, null);
            if (owner.Machine.State == Index)
            {
                bool locked = owner.Machine.Locked;
                owner.Machine.Locked = false;
                owner.Machine.State = 0;
                owner.Machine.Locked = locked;
            }
        }

        private void Begin()
        {
            var self = runtime.Player;
            self.Data.Motion.Ducking = false;
            self.Data.Motion.Normal.Speed.Y = 0;
        }

        private void End()
        {
            runtime.CurrentGrabbed.Grabbed = false;
            runtime.CurrentGrabbed = null;
            runtime.Buffer = .35f;
        }

        private int Update()
        {
            var self = runtime.Player;
            if (runtime.CurrentGrabbed == null) return 0;
            if (!runtime.CurrentGrabbed.Grabbed) return Index;
            runtime.CurrentGrabbed.Speed = self.Speed.X;
            if (Math.Sign(self.LiftSpeed.X) * Math.Sign(self.Speed.X) == -1 ||
                Math.Abs(self.LiftSpeed.X) <= Math.Abs(self.Speed.X))
                self.LiftSpeed = self.Speed;
            int moveX = self.Data.MoveX;
            if (Math.Sign(moveX) == -Math.Sign(self.Speed.X))
                self.Data.Motion.Normal.Speed.X =
                    ScalarMath.Approach(self.Speed.X, moveX * 120f, 250f * runtime.Engine.DeltaTime);
            else if (Math.Abs(self.Speed.X) <= 120 || Math.Sign(moveX) != Math.Sign(self.Speed.X))
                self.Data.Motion.Normal.Speed.X =
                    ScalarMath.Approach(self.Speed.X, moveX * 120f, 190f * runtime.Engine.DeltaTime);
            if (!self.Input.GrabCheck || self.Data.Stamina <= 0) return 0;
            if (self.Input.Jump.Pressed)
            {
                self.Input.Jump.ConsumePress();
                if (runtime.CurrentGrabbed.UsesStamina) self.Data.Stamina -= 110f / 8f;
                self.Data.Motion.Normal.Speed.X *= .1f;
                self.Jump(false, true);
                self.LiftSpeed *= .4f;
                runtime.CurrentGrabbed.Speed = ScalarMath.Approach(runtime.CurrentGrabbed.Speed, 0, 20);
                return 0;
            }

            if (self.Motor.Dash.CanDash) return self.StartDash();
            if (runtime.CurrentGrabbed.UsesStamina) self.Data.Stamina -= 5 * runtime.Engine.DeltaTime;
            return Index;
        }

        private IEnumerator Coroutine()
        {
            var self = runtime.Player;
            Vector2 speed = self.Speed;
            self.Speed = Vector2.Zero;
            runtime.CurrentGrabbed.Speed = 0;
            self.Sprite.Play("pickup");
            self.Play("event:/char/madeline/crystaltheo_lift", null, 0f);
            float originalY = runtime.CurrentGrabbed.Y;
            Vector2 playerLerp = new((self.X + runtime.CurrentGrabbed.X) / 2f, originalY + runtime.TargetOffset);
            playerLerp.X = Celeste.Core.Mathematics.Framework.SourceFrameworkMath.Clamp(playerLerp.X,
                runtime.CurrentGrabbed.LeftEdge, runtime.CurrentGrabbed.RightEdge);
            Vector2 zipLerp = new(playerLerp.X, runtime.CurrentGrabbed.Y);
            Vector2 playerInit = self.Position, zipInit = runtime.CurrentGrabbed.Position;
            var tween = SourceTweenState.Create(runtime.Engine.TweenPool, SourceTweenState.TweenMode.Oneshot, null,
                .07f, true);
            while (tween.Active)
            {
                tween.Update();
                playerLerp.Y = originalY + runtime.TargetOffset;
                MovePlayerTo(self, Vector2Interpolation.Lerp(playerInit, playerLerp, tween.Percent, runtime.Scalar));
                runtime.CurrentGrabbed.Position =
                    Vector2Interpolation.Lerp(zipInit, zipLerp, tween.Percent, runtime.Scalar);
                yield return null;
            }

            runtime.CurrentGrabbed.Grabbed = true;
            self.Speed = speed;
            playerLerp.Y = originalY + runtime.TargetOffset;
            MovePlayerTo(self, playerLerp);
            runtime.CurrentGrabbed.Position = zipLerp;
        }

        private void MovePlayerTo(RoomPlayerSession player, Vector2 position)
        {
            runtime.Gravity.BeginOverride();
            player.MoveToX(position.X);
            player.MoveToY(position.Y);
            runtime.Gravity.EndOverride();
        }
    }
}
