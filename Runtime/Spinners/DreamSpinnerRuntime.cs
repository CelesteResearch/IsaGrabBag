using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Everest;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.IsaGrabBag.Configuration;
using Celeste.Mods.IsaGrabBag.Entities.Spinners;
using Celeste.Mods.IsaGrabBag.Rendering.Spinners;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.IsaGrabBag.Runtime.Spinners
{
    public sealed class DreamSpinnerRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, DreamSpinnerRuntime> instances = new();
        private readonly ISourceEngineContext engine;
        private readonly Dictionary<OriginalLevelSceneRoot, GrabBagMetadata> metadata = new();
        private readonly HashSet<DreamSpinnerRenderer> renderers = new();
        private Hook hook;

        public DreamSpinnerRuntime(ISourceEngineContext engine)
        {
            this.engine = engine;
            instances.Add(engine, this);
            try
            {
                hook = new Hook(
                    typeof(OriginalLevelLoaderScene).GetMethod("LoadingThread",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<OriginalLevelLoaderScene>, OriginalLevelLoaderScene>)LoadingThread);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static DreamSpinnerRuntime Get(ISourceEngineContext engine) =>
            instances.TryGetValue(engine, out var runtime)
                ? runtime
                : throw new InvalidOperationException("IsaGrabBag is not loaded for this engine.");

        public GrabBagMetadata Metadata(OriginalLevelSceneRoot level)
        {
            if (metadata.TryGetValue(level, out var result)) return result;
            result = null;
            try
            {
                var keys = ((Celeste.Game.Execution.Areas.IOriginalAreaKeyEnvironment)engine).AreaKeys;
                var sid = keys.GetSID(keys.GetSessionArea(level.Session));
                if (sid != null)
                {
                    var asset = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry.Get(sid, false);
                    if (asset != null && asset.TryGetMeta<GrabBagMetadataDocument>(out var document))
                        result = document?.IsaGrabBag;
                }
            }
            catch (Exception exception)
            {
                var logger = ((IEverestLoggerProcessEnvironment)engine).Logger;
                logger.Log(EverestLogLevel.Warn, "IsaGrabBag", "Unable to properly get metadata");
                logger.LogDetailed(exception);
            }

            metadata.Add(level, result ??= new GrabBagMetadata());
            return result;
        }

        private void LoadingThread(Action<OriginalLevelLoaderScene> orig, OriginalLevelLoaderScene loader)
        {
            DreamSpinnerRenderer renderer = null;
            if (ReferenceEquals(loader.EngineContext, engine))
            {
                renderer = new DreamSpinnerRenderer(engine, this);
                renderers.Add(renderer);
                loader.Level.Scheduler.QueueSourceEntity(renderer);
            }

            try
            {
                orig(loader);
            }
            catch
            {
                if (renderer != null)
                {
                    renderer.Dispose();
                    renderer.RemoveSelf();
                    Release(renderer, loader.Level);
                }

                throw;
            }
        }

        internal void Release(DreamSpinnerRenderer renderer, OriginalLevelSceneRoot level)
        {
            renderers.Remove(renderer);
            metadata.Remove(level);
        }

        public void Dispose()
        {
            hook?.Dispose();
            hook = null;
            foreach (var renderer in new List<DreamSpinnerRenderer>(renderers))
            {
                if (renderer.Scene is OriginalLevelSceneRoot level)
                    foreach (DreamSpinner spinner in level.Scheduler.Entities.GetTrackedEntities(typeof(DreamSpinner)))
                        spinner.Unload();
                renderer.Dispose();
                renderer.RemoveSelf();
            }

            renderers.Clear();
            metadata.Clear();
            instances.Remove(engine);
        }
    }
}
