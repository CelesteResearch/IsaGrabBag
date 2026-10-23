using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Chapters.Flow;
using Celeste.Everest.Runtime.Events;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Everest.Runtime.Levels.Update;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Triggers.Respawning;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Controls.Source;
using Celeste.Game.Save.Data;
using Celeste.Mods.IsaGrabBag.Persistence;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;
using Celeste.Simulation.UI.Menus.Text;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.IsaGrabBag.Runtime.Variants
{
    public sealed class ForceVariantsRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, ForceVariantsRuntime> instances = new();

        private static readonly ForcedVariant[] menuLayout =
        {
            ForcedVariant.MirrorMode, ForcedVariant.ThreeSixtyDashing, ForcedVariant.InvisibleMotion,
            ForcedVariant.NoGrabbing, ForcedVariant.LowFriction, ForcedVariant.SuperDashing, ForcedVariant.Hiccups,
            ForcedVariant.PlayAsBadeline, ForcedVariant.InfiniteStamina, ForcedVariant.DashAssist,
            ForcedVariant.Invincible
        };

        private readonly ISourceEngineContext engine;
        private readonly Func<IsaGrabBagSession> session;
        private bool[] defaults = new bool[11];
        private readonly bool?[] variants = new bool?[11];
        private readonly List<IDisposable> hooks = new();
        private readonly Dictionary<TextMenuPresentation.OnOff, Action<bool>> menuCallbacks = new();
        private ref FileAssists Assists => ref ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists;

        public ForceVariantsRuntime(ISourceEngineContext engine, Func<IsaGrabBagSession> session)
        {
            this.engine = engine;
            this.session = session;
            instances.Add(engine, this);
            try
            {
                ((IEverestLevelEnterEventsEnvironment)engine).LevelEnterEvents.OnEnter += LevelEnter;
                ((IEverestGameEventsEnvironment)engine).GameEvents.OnExit += LevelExit;
                ((IEverestLevelEntityLoadEnvironment)engine).LevelUpdateEvents.OnTransitionTo += Transition;
                EverestPlayerSourceEvents.OnSpawn += PlayerSpawn;
                hooks.Add(new Hook(typeof(OriginalChangeRespawnTrigger).GetMethod("OnEnter"),
                    (Action<Action<OriginalChangeRespawnTrigger, RoomPlayerSession>, OriginalChangeRespawnTrigger,
                        RoomPlayerSession>)ChangeRespawn));
                hooks.Add(new Hook(typeof(TextMenuPresentation).GetMethod("Close"),
                    (Action<Action<TextMenuPresentation>, TextMenuPresentation>)((orig, menu) =>
                    {
                        orig(menu);
                        if (ReferenceEquals(menu.EngineContext, engine)) ReleaseMenu(menu);
                    })));
                foreach (string name in new[] { "AssistMode", "VariantMode" })
                {
                    bool assist = name == "AssistMode";
                    hooks.Add(new Hook(
                        typeof(OriginalLevelSceneRoot).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance),
                        (Action<Action<OriginalLevelSceneRoot, int, bool>, OriginalLevelSceneRoot, int, bool>)((orig,
                            level, index, minimal) =>
                        {
                            orig(level, index, minimal);
                            if (!ReferenceEquals(level.EngineContext, engine)) return;
                            var pending = level.Scheduler.GetToAdd();
                            OnVariantMenu(pending[pending.Count - 1] as TextMenuPresentation, assist);
                        })));
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static ForceVariantsRuntime Get(ISourceEngineContext engine) =>
            instances.TryGetValue(engine, out var runtime)
                ? runtime
                : throw new InvalidOperationException("IsaGrabBag is not loaded for this engine.");

        public bool? GetVariantModdedValue(ForcedVariant variant) => variants[(int)variant];

        public void GetDefaults()
        {
            for (int i = 0; i < 11; i++) defaults[i] = GetVariantStatus((ForcedVariant)i);
        }

        public void SaveToSession()
        {
            var saved = session();
            for (int i = 0; i < 11; i++) saved.Variants_Save[i] = variants[i];
        }

        public void GetFromSession()
        {
            var saved = session();
            for (int i = 0; i < 11; i++)
            {
                if (variants[i].HasValue && !saved.Variants_Save[i].HasValue)
                    SetVariant((ForcedVariant)i, ForcedVariantState.SetToDefault);
                variants[i] = saved.Variants_Save[i];
            }
        }

        public void ReinforceSession()
        {
            var saved = session();
            for (int i = 0; i < 11; i++)
            {
                variants[i] = saved.Variants_Save[i];
                if (variants[i].HasValue)
                    SetVariant((ForcedVariant)i,
                        variants[i].Value ? ForcedVariantState.EnabledPermanent : ForcedVariantState.DisabledPermanent);
            }
        }

        public void ResetDefaultVariants() => defaults = new bool[11];

        public void ResetSession()
        {
            for (int i = 0; i < 11; i++) SetVariant((ForcedVariant)i, ForcedVariantState.SetToDefault);
        }

        public void SetVariant(ForcedVariant variant, ForcedVariantState state)
        {
            if (state == ForcedVariantState.SetToDefault)
            {
                variants[(int)variant] = null;
                SetVariantInGame(variant, defaults[(int)variant]);
                return;
            }

            bool permanent = state is ForcedVariantState.EnabledPermanent or ForcedVariantState.DisabledPermanent;
            bool value =
                state is ForcedVariantState.Enabled or ForcedVariantState.EnabledTemporary
                    or ForcedVariantState.EnabledPermanent ||
                state == ForcedVariantState.Toggle && !GetVariantStatus(variant);
            SetVariantInGame(variant, value);
            if (permanent) variants[(int)variant] = value;
        }

        public bool GetVariantStatus(ForcedVariant variant) => variant switch
        {
            ForcedVariant.Hiccups => Assists.Hiccups, ForcedVariant.InfiniteStamina => Assists.InfiniteStamina,
            ForcedVariant.Invincible => Assists.Invincible, ForcedVariant.InvisibleMotion => Assists.InvisibleMotion,
            ForcedVariant.LowFriction => Assists.LowFriction, ForcedVariant.MirrorMode => Assists.MirrorMode,
            ForcedVariant.NoGrabbing => Assists.NoGrabbing, ForcedVariant.PlayAsBadeline => Assists.PlayAsBadeline,
            ForcedVariant.SuperDashing => Assists.SuperDashing,
            ForcedVariant.ThreeSixtyDashing => Assists.ThreeSixtyDashing,
            ForcedVariant.DashAssist => Assists.DashAssist, _ => false
        };

        private void SetVariantInGame(ForcedVariant variant, bool value)
        {
            switch (variant)
            {
                case ForcedVariant.Hiccups: Assists.Hiccups = value; break;
                case ForcedVariant.InfiniteStamina: Assists.InfiniteStamina = value; break;
                case ForcedVariant.Invincible: Assists.Invincible = value; break;
                case ForcedVariant.InvisibleMotion: Assists.InvisibleMotion = value; break;
                case ForcedVariant.LowFriction: Assists.LowFriction = value; break;
                case ForcedVariant.MirrorMode:
                    Assists.MirrorMode = value;
                    var input = ((IOriginalGameInputProcessEnvironment)engine).InputSource;
                    input.Aim.InvertedX = input.MoveX.Inverted = value;
                    break;
                case ForcedVariant.NoGrabbing: Assists.NoGrabbing = value; break;
                case ForcedVariant.PlayAsBadeline:
                    bool previous = Assists.PlayAsBadeline;
                    Assists.PlayAsBadeline = value;
                    if (previous == value) break;
                    var scene = engine.Frame.Scenes.CurrentScene as Celeste.Scene.Tracking.ISourceScene;
                    var player =
                        scene?.SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(typeof(RoomPlayerSession)
                            .FullName);
                    if (player != null)
                    {
                        var mode = value ? PlayerSpriteMode.MadelineAsBadeline : player.DefaultSpriteMode;
                        if (player.Active) player.ResetSpriteNextFrame(mode);
                        else player.ResetSprite(mode);
                    }

                    break;
                case ForcedVariant.SuperDashing: Assists.SuperDashing = value; break;
                case ForcedVariant.ThreeSixtyDashing: Assists.ThreeSixtyDashing = value; break;
                case ForcedVariant.DashAssist: Assists.DashAssist = value; break;
            }
        }

        public void OnVariantMenu(TextMenuPresentation menu, bool assist)
        {
            int index = assist ? 8 : 0;
            foreach (var item in menu.Items)
            {
                if (item is not TextMenuPresentation.OnOff onOff) continue;
                ForcedVariant variant = menuLayout[index++];
                Action<bool> original = onOff.OnValueChange;
                menuCallbacks.Add(onOff, original);
                onOff.OnValueChange = value =>
                {
                    original(value);
                    defaults[(int)variant] = onOff.Index >= 1;
                };
            }
        }

        private void ReleaseMenu(TextMenuPresentation menu)
        {
            foreach (var item in menu.Items)
                if (item is TextMenuPresentation.OnOff onOff && menuCallbacks.TryGetValue(onOff, out var original))
                {
                    onOff.OnValueChange = original;
                    menuCallbacks.Remove(onOff);
                }
        }

        private void LevelEnter(Celeste.Simulation.Runtime.Session.ChapterCompletionSession value, bool fromSaveData)
        {
            GetDefaults();
            ReinforceSession();
        }

        private void LevelExit(OriginalLevelSceneRoot level, Celeste.Game.Chapters.Flow.LevelExitSequence exit,
            Celeste.Game.Chapters.Flow.LevelExitMode mode,
            Celeste.Simulation.Runtime.Session.ChapterCompletionSession value,
            Celeste.Game.Presentation.Environment.HiresSnowState snow)
        {
            if (ReferenceEquals(level.EngineContext, engine)) ResetSession();
        }

        private void Transition(OriginalLevelSceneRoot level, OriginalLevelRoomData next, Vector2 direction)
        {
            if (ReferenceEquals(level.EngineContext, engine)) SaveToSession();
        }

        private void PlayerSpawn(RoomPlayerSession player)
        {
            if (player.Scene is OriginalLevelSceneRoot level && ReferenceEquals(level.EngineContext, engine))
                GetFromSession();
        }

        private void ChangeRespawn(Action<OriginalChangeRespawnTrigger, RoomPlayerSession> orig,
            OriginalChangeRespawnTrigger trigger, RoomPlayerSession player)
        {
            orig(trigger, player);
            if (trigger.Scene is OriginalLevelSceneRoot level && ReferenceEquals(level.EngineContext, engine))
                SaveToSession();
        }

        public void Dispose()
        {
            ((IEverestLevelEnterEventsEnvironment)engine).LevelEnterEvents.OnEnter -= LevelEnter;
            ((IEverestGameEventsEnvironment)engine).GameEvents.OnExit -= LevelExit;
            ((IEverestLevelEntityLoadEnvironment)engine).LevelUpdateEvents.OnTransitionTo -= Transition;
            EverestPlayerSourceEvents.OnSpawn -= PlayerSpawn;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            foreach (var item in menuCallbacks) item.Key.OnValueChange = item.Value;
            menuCallbacks.Clear();
            instances.Remove(engine);
        }
    }
}
