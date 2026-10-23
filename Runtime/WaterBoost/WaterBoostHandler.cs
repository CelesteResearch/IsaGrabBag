using System.Numerics;
using Celeste.Core.Components;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;

namespace Celeste.Mods.IsaGrabBag.Runtime.WaterBoost
{
    public sealed class WaterBoostHandler : SourceComponentState
    {
        private readonly WaterBoostRuntime runtime;

        public WaterBoostHandler(WaterBoostRuntime runtime) : base(true, false)
        {
            this.runtime = runtime;
        }

        public override void Update()
        {
            if (!runtime.Metadata.WaterBoost) return;
            var player = Entity as RoomPlayerSession;
            if (player == null || !player.Collidable) return;
            Vector2 at = player.Position + player.Speed * runtime.Engine.DeltaTime * 2f;
            var entities = ((ISourceScene)player.Scene).SourceScene.Entities;
            if (!SourceTrackedEntityCollision.CheckAt(entities, "Celeste.Water", player, at)
                && !SourceTrackedEntityCollision.CheckAt(entities, "Celeste.Water", player, at + Vector2.UnitY * -8f)
                && player.Machine.State == 3
                && (player.Speed.Y < 0f || player.Input.MoveY.Value == -1 || player.Input.Jump.Check))
            {
                player.Data.Motion.Normal.Speed.Y =
                    player.Input.MoveY.Value == -1 || player.Input.Jump.Check ? -110 : 0;
                if (player.Speed.Y < -1f)
                {
                    player.Data.Motion.Normal.Speed.X *= 1.1f;
                }
            }
        }
    }
}
