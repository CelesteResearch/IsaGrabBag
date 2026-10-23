using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Rendering;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Graphics.Rendering;
using Celeste.Mods.IsaGrabBag.Entities.ZipLines;
using Celeste.Scene.Entities;

namespace Celeste.Mods.IsaGrabBag.Rendering.ZipLines
{
    // Original type: Celeste.Mod.IsaGrabBag.ZipLineRender (IsaGrabBag 1.7.3).
    [Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.IsaGrabBag.ZipLineRender")]
    public class ZipLineRenderer : SceneEntityState
    {
        private static readonly PackedColor darkLine = PackedColor.FromHex("9292a9");
        private static readonly PackedColor lightLine = PackedColor.FromHex("bbc0ce");

        private readonly RenderRectangle[] renderList = new RenderRectangle[4];
        private readonly ZipLine zipInst;
        private readonly Sprite sprite;

        public ZipLineRenderer(ZipLine instance, SpriteBank sprites)
            : base(((ISourceEntitySourceDataEnvironment)instance.EngineContext).EntitySourceDataRegistration)
        {
            zipInst = instance;
            sprite = sprites.Create("zipline");
            sprite.Play("idle");
            sprite.JustifyOrigin(new Vector2(0.5f, 0.25f));
            Add(sprite);
            Depth = 500;
        }

        public override void Render(ISourceRenderContext context)
        {
            Position = zipInst.Position;

            IntRect tempRect = new((int)zipInst.LeftEdge, (int)zipInst.Y,
                (int)(zipInst.RightEdge - zipInst.LeftEdge), 1);
            tempRect.Inflate(8, 0);
            renderList[0] = new RenderRectangle(tempRect, darkLine);
            tempRect.Y -= 1;
            renderList[1] = new RenderRectangle(tempRect, lightLine);
            int left = tempRect.Left, right = tempRect.Right;
            renderList[2] = new RenderRectangle(new IntRect(left - 2, (int)Y - 3, 2, 6), darkLine);
            renderList[3] = new RenderRectangle(new IntRect(right, (int)Y - 3, 2, 6), darkLine);

            foreach (RenderRectangle render in renderList)
            {
                IntRect rect = render.rect;
                rect.Inflate(1, 0);
                SourcePrimitiveDrawing.Rect((ISourceDrawScope)context, rect, PackedColor.Black);
                rect.Inflate(-1, 1);
                SourcePrimitiveDrawing.Rect((ISourceDrawScope)context, rect, PackedColor.Black);
            }

            foreach (RenderRectangle render in renderList)
                SourcePrimitiveDrawing.Rect((ISourceDrawScope)context, render.rect, render.color);

            base.Render(context);
        }
    }
}
