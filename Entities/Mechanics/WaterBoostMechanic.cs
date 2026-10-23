using Celeste.Core.Components;
using Celeste.Scene.Entities;
using Celeste.Game.Content.Maps.Entities;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.IsaGrabBag.Runtime.WaterBoost;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.IsaGrabBag.Entities.Mechanics
{
    [CustomEntity("isaBag/waterBoost = Create")]
    [RuntimeTypeAlias("Celeste.Mod.IsaGrabBag.WaterBoostMechanic")]
    public sealed class WaterBoostMechanic : SceneEntityState
    {
        public WaterBoostMechanic(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            WaterBoostRuntime.Get(engine).Metadata.WaterBoost = data.Bool("boostEnabled", false);
        }

        public static WaterBoostMechanic Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset);
    }
}
