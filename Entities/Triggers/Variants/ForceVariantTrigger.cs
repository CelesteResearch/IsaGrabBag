using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.IsaGrabBag.Runtime.Variants;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.IsaGrabBag.Entities.Triggers.Variants
{
    [CustomEntity("ForceVariantTrigger")]
    [RuntimeTypeAlias("Celeste.Mod.IsaGrabBag.ForceVariantTrigger")]
    public sealed class ForceVariantTrigger : SourceTriggerEntityState
    {
        public ForcedVariant Variant;
        public ForcedVariantState Enable;
        private bool previous;
        private readonly ForceVariantsRuntime runtime;

        public ForceVariantTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            runtime = ForceVariantsRuntime.Get(engine);
            Variant = EntityAttributeReader.Enum(data.Values, "variantChange", ForcedVariant.Hiccups);
            Enable = EntityAttributeReader.Enum(data.Values, "enableStyle", ForcedVariantState.Enabled);
            if (data.Has("enforceValue"))
            {
                bool enforce = data.Bool("enforceValue");
                if (Enable == ForcedVariantState.Enabled) Enable = ForcedVariantState.EnabledTemporary;
                else if (Enable == ForcedVariantState.Disabled) Enable = ForcedVariantState.DisabledTemporary;
                else if (Enable == ForcedVariantState.EnabledPermanent && !enforce) Enable = ForcedVariantState.Enabled;
                else if (Enable == ForcedVariantState.DisabledPermanent && !enforce)
                    Enable = ForcedVariantState.Disabled;
            }
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            previous = runtime.GetVariantStatus(Variant);
            runtime.SetVariant(Variant, Enable);
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            base.OnLeave(player);
            if (Enable is ForcedVariantState.EnabledTemporary or ForcedVariantState.DisabledTemporary)
                runtime.SetVariant(Variant, previous ? ForcedVariantState.Enabled : ForcedVariantState.Disabled);
        }
    }
}
