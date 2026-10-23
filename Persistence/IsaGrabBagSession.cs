using Celeste.Everest.Modules.Persistence;

namespace Celeste.Mods.IsaGrabBag.Persistence
{
    public sealed class IsaGrabBagSession : EverestModuleSession
    {
        public bool?[] Variants_Save { get; set; } = new bool?[11];
    }
}
