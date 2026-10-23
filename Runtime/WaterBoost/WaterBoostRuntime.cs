using Celeste.Everest;
using System;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Runtime.Events;
using Celeste.Everest.Runtime.Events.LevelLoading;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.IsaGrabBag.Configuration;
using Celeste.Simulation;

namespace Celeste.Mods.IsaGrabBag.Runtime.WaterBoost
{
    public sealed class WaterBoostRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, WaterBoostRuntime> instances = new();

        private static readonly ConditionalWeakTable<ISourceEngineContext, StrongBox<GrabBagMetadata>>
            metadataByEngine = new();

        private readonly StrongBox<GrabBagMetadata> metadata;
        public readonly ISourceEngineContext Engine;

        public WaterBoostRuntime(ISourceEngineContext engine)
        {
            Engine = engine;
            metadata = metadataByEngine.GetValue(engine, _ => new StrongBox<GrabBagMetadata>());
            instances.Add(engine, this);
            EverestLevelLoaderSourceEvents.OnLoadingThread += Loading;
            ((IEverestGameEventsEnvironment)engine).GameEvents.OnExit += Exit;
            EverestPlayerSourceEvents.OnSpawn += Spawn;
        }

        public static WaterBoostRuntime Get(ISourceEngineContext engine) => instances.TryGetValue(engine, out var value)
            ? value
            : throw new InvalidOperationException("IsaGrabBag is not loaded for this engine.");

        public GrabBagMetadata Metadata
        {
            get
            {
                if (metadata.Value == null)
                {
                    var scenes = Engine.Frame.Scenes;
                    var level = scenes.CurrentScene as OriginalLevelSceneRoot
                                ?? scenes.NextScene as OriginalLevelSceneRoot;
                    level ??= ((OriginalLevelLoaderScene)scenes.CurrentScene).Level;
                    metadata.Value = Default(level);
                }

                return metadata.Value;
            }
        }

        private GrabBagMetadata Default(OriginalLevelSceneRoot level)
        {
            var keys = ((IOriginalAreaKeyEnvironment)Engine).AreaKeys;
            return new GrabBagMetadata
            {
                WaterBoost = keys.GetLevelSet(keys.GetSessionArea(level.Session)).StartsWith("SpringCollab2020")
            };
        }

        private void Loading(OriginalLevelSceneRoot level)
        {
            if (!ReferenceEquals(level.EngineContext, Engine)) return;
            metadata.Value = null;
            var keys = ((IOriginalAreaKeyEnvironment)Engine).AreaKeys;
            try
            {
                var sid = keys.GetSID(keys.GetSessionArea(level.Session));
                if (sid != null)
                {
                    var asset = ((IEverestContentRegistryEnvironment)Engine).SourceContentRegistry.Get(sid, false);
                    if (asset != null && asset.TryGetMeta<GrabBagMetadataDocument>(out var document))
                        metadata.Value = document?.IsaGrabBag;
                }
            }
            catch (Exception exception)
            {
                var logger = ((IEverestLoggerProcessEnvironment)Engine).Logger;
                logger.Log(EverestLogLevel.Warn, "IsaGrabBag", "Unable to properly get metadata");
                logger.LogDetailed(exception);
            }

            metadata.Value ??= Default(level);
            if (keys.GetLevelSet(keys.GetSessionArea(level.Session)).StartsWith("SpringCollab2020"))
                Metadata.WaterBoost = true;
        }

        private void Spawn(RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, Engine)
                && player.OwnedComponents.Get<WaterBoostHandler>() == null)
                player.OwnedComponents.Add(new WaterBoostHandler(this));
        }

        private void Exit(OriginalLevelSceneRoot level, Celeste.Game.Chapters.Flow.LevelExitSequence exit,
            Celeste.Game.Chapters.Flow.LevelExitMode mode,
            Celeste.Simulation.Runtime.Session.ChapterCompletionSession session,
            Celeste.Game.Presentation.Environment.HiresSnowState snow)
        {
            if (ReferenceEquals(level.EngineContext, Engine)) metadata.Value = null;
        }

        public void Dispose()
        {
            EverestLevelLoaderSourceEvents.OnLoadingThread -= Loading;
            ((IEverestGameEventsEnvironment)Engine).GameEvents.OnExit -= Exit;
            EverestPlayerSourceEvents.OnSpawn -= Spawn;
            instances.Remove(Engine);
        }
    }
}
