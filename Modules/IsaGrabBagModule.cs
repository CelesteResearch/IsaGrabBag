using System;
using Celeste.Core.Scheduling;
using Celeste.Mods.IsaGrabBag.Persistence;
using Celeste.Mods.IsaGrabBag.Runtime.Variants;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.IsaGrabBag.Runtime.Spinners;

namespace Celeste.Mods.IsaGrabBag.Modules
{
    public sealed partial class IsaGrabBagModule : SourceEverestModule
    {
        private DreamSpinnerRuntime runtime;
        private ForceVariantsRuntime variants;
        private IDisposable variantsInterop;
        public override Type SessionType => typeof(IsaGrabBagSession);
        public IsaGrabBagSession Session => (IsaGrabBagSession)_Session;

        public IsaGrabBagModule(ISourceEngineContext engine) : base(engine)
        {
        }

        public override void Load()
        {
            runtime = new DreamSpinnerRuntime(SourceEngine);
            try
            {
                variants = new ForceVariantsRuntime(SourceEngine, () => Session);
                variantsInterop = ((Celeste.Core.Reflection.Interop.ISourceModInteropEnvironment)SourceEngine)
                    .ModInterop
                    .RegisterOwned(new Celeste.Core.Reflection.Interop.SourceModInteropActualTypeBinding(
                        typeof(Interop.IsaVariantsExports), new Interop.IsaVariantsExports(variants)));
                LoadWaterBoost();
                LoadZipLines();
            }
            catch
            {
                variantsInterop?.Dispose();
                variantsInterop = null;
                UnloadZipLines();
                UnloadWaterBoost();
                variants?.Dispose();
                variants = null;
                runtime.Dispose();
                runtime = null;
                throw;
            }
        }

        public override void Unload()
        {
            variantsInterop?.Dispose();
            variantsInterop = null;
            UnloadZipLines();
            UnloadWaterBoost();
            variants?.Dispose();
            variants = null;
            runtime?.Dispose();
            runtime = null;
        }
    }
}
