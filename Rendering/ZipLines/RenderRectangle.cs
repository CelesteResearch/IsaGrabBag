using Celeste.Core.Display;
using Celeste.Core.Geometry;

namespace Celeste.Mods.IsaGrabBag.Rendering.ZipLines
{
    // Original type: Celeste.Mod.IsaGrabBag.RenderRectangle.
    public struct RenderRectangle
    {
        public IntRect rect;
        public PackedColor color;

        public RenderRectangle(IntRect r, PackedColor c)
        {
            rect = r;
            color = c;
        }
    }
}
