using Celeste.Mods.IsaGrabBag.Runtime.Variants;
using MonoMod.ModInterop;

namespace Celeste.Mods.IsaGrabBag.Interop
{
    [ModExportName("IsaGrabBag")]
    public sealed class IsaVariantsExports
    {
        private readonly ForceVariantsRuntime runtime;
        public IsaVariantsExports(ForceVariantsRuntime runtime) => this.runtime = runtime;
        public void ResetSession() => runtime.ResetSession();
        public void ResetDefaultVariants() => runtime.ResetDefaultVariants();
    }
}
