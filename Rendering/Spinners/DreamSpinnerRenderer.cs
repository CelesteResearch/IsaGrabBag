using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Rendering;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering.Batches;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Mods.IsaGrabBag.Entities.Spinners;
using Celeste.Mods.IsaGrabBag.Runtime.Spinners;
using Celeste.Scene.Entities;
using Celeste.Scene.Scheduling.ExecutionTags;

namespace Celeste.Mods.IsaGrabBag.Rendering.Spinners
{
    public sealed partial class DreamSpinnerRenderer : SceneEntityState, IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly DreamSpinnerRuntime runtime;
        private readonly AtlasEntry[] particlesTextures;
        private readonly AtlasEntry foreground, background, foregroundBorder, backgroundBorder;
        private readonly List<DreamSpinner> visible = new();
        private VirtualRenderTarget target;
        private DreamParticle[] particles;
        private bool dreamDash;
        private float animation;

        private static readonly SourceBlendState ParticleBlend = new()
        {
            ColorSourceBlend = SourceBlendFactor.DestinationAlpha,
            ColorDestinationBlend = SourceBlendFactor.InverseSourceAlpha,
            AlphaSourceBlend = SourceBlendFactor.Zero, AlphaDestinationBlend = SourceBlendFactor.One,
            AlphaBlendFunction = SourceBlendFunction.Add
        };

        public DreamSpinnerRenderer(ISourceEngineContext engine, DreamSpinnerRuntime runtime)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            this.runtime = runtime;
            Depth = -8500;
            Tags = ((ISourceGlobalFrozenExecutionTagsEnvironment)engine).GlobalFrozenExecutionTags.Global.Value |
                   ((ISourcePersistentTransitionExecutionTagsEnvironment)engine).PersistentTransitionExecutionTags
                   .TransitionUpdate.Value;
            Add(new BeforeRenderCallbackComponentState(BeforeRender));
            var atlas = Graphics.GameAtlas.Entries;
            foreground = atlas["isafriend/danger/crystal/dreamSpinner"].GetSubtexture(0, 0, 24, 24);
            background = atlas["isafriend/danger/crystal/dreamSpinner"].GetSubtexture(24, 0, 24, 24);
            foregroundBorder = atlas["isafriend/danger/crystal/dreamBorder"].GetSubtexture(0, 0, 24, 24);
            backgroundBorder = atlas["isafriend/danger/crystal/dreamBorder"].GetSubtexture(24, 0, 24, 24);
            var texture = atlas["objects/dreamblock/particles"];
            particlesTextures = new[]
            {
                texture.GetSubtexture(14, 0, 7, 7), texture.GetSubtexture(7, 0, 7, 7),
                texture.GetSubtexture(0, 0, 7, 7), texture.GetSubtexture(7, 0, 7, 7)
            };
        }

        private OriginalGameGraphicsResources Graphics =>
            (OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)engine).GameGraphics;

        private SourceSpriteBatch Batch => ((ISourceDrawScope)engine).Draw.SpriteBatch;

        public override void Update()
        {
            base.Update();
            animation += 6f * engine.DeltaTime;
        }

        public override void Render(ISourceRenderContext context)
        {
            if (visible.Count == 0) return;
            var draw = new RenderTargetTextureDraw(target.Target, ((OriginalLevelSceneRoot)Scene).Camera.Position,
                PackedColor.White);
            ((ISourceRenderCommandSink<RenderTargetTextureDraw>)context).Draw(in draw, this);
        }

        public override void SceneEnd(object scene)
        {
            base.SceneEnd(scene);
            Dispose();
            runtime.Release(this, (OriginalLevelSceneRoot)scene);
        }

        public void Dispose()
        {
            target?.Dispose();
            target = null;
            visible.Clear();
        }

        private void BeforeRender()
        {
            var level = (OriginalLevelSceneRoot)Scene;
            visible.Clear();
            foreach (DreamSpinner spinner in level.Scheduler.Entities.GetTrackedEntities(typeof(DreamSpinner)))
                if (spinner.InView())
                    visible.Add(spinner);
            if (visible.Count == 0) return;
            dreamDash = level.Session.Inventory.DreamDash;
            target ??=
                ((ISourceVirtualContentEnvironment)engine).VirtualContent.CreateRenderTarget("dream-spinner-renderer",
                    320,
                    180);
            var device = ((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice;
            var context = ((ISourceRenderContextScene)level).SourceRenderContext;
            device.SetRenderTarget(Graphics.Buffers.TempA.Target);
            device.Clear(PackedColor.Transparent);
            Begin(SourceBlendState.AlphaBlend, level.Camera.Matrix);
            DrawSpinners(context, false);
            Batch.End();
            Begin(ParticleBlend, Matrix4x4.Identity);
            DrawParticles(context, level.Camera.Position);
            Batch.End();
            device.SetRenderTarget(target.Target);
            device.Clear(PackedColor.Transparent);
            Begin(SourceBlendState.AlphaBlend, level.Camera.Matrix);
            DrawSpinners(context, true);
            Batch.Draw(Graphics.Buffers.TempA.Target, level.Camera.Position, PackedColor.White.PackedValue);
            Batch.End();
        }

        private void Begin(SourceBlendState blend, Matrix4x4 matrix) => Batch.Begin(SourceSpriteSortMode.Deferred,
            blend, SourceSamplerState.PointClamp, SourceDepthStencilState.None, SourceRasterizerState.CullNone, null,
            matrix);

        private void DrawTexture(ISourceRenderContext context, AtlasEntry texture, Vector2 position, Vector2 origin,
            PackedColor color, float rotation = 0f)
        {
            var draw = new AtlasTextureDraw(texture, position, origin, color, Vector2.One, rotation);
            ((ISourceRenderCommandSink<AtlasTextureDraw>)context).Draw(in draw, this);
        }

        private void DrawSpinners(ISourceRenderContext context, bool borders)
        {
            foreach (var spinner in visible)
            {
                var color = borders
                    ? !dreamDash ? PackedColor.Gray : spinner.OneUse ? PackedColor.Orange * .9f : PackedColor.White
                    : spinner.Color;
                DrawTexture(context, borders ? foregroundBorder : foreground, spinner.Position, new Vector2(12), color,
                    spinner.Rotation);
                foreach (var offset in spinner.Offsets)
                    DrawTexture(context, borders ? backgroundBorder : background, offset, new Vector2(12), color);
            }
        }
    }
}
