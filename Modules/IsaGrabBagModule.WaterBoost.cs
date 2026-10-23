using Celeste.Mods.IsaGrabBag.Runtime.WaterBoost;

namespace Celeste.Mods.IsaGrabBag.Modules
{
    public sealed partial class IsaGrabBagModule
    {
        private WaterBoostRuntime waterBoost;
        private void LoadWaterBoost() => waterBoost = new WaterBoostRuntime(SourceEngine);

        private void UnloadWaterBoost()
        {
            waterBoost?.Dispose();
            waterBoost = null;
        }
    }
}
