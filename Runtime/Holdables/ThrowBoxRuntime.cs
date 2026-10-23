// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.FactoryHelper.Configuration;
using Celeste.Mods.FactoryHelper.Entities.Holdables;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collectibles.Strawberry;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Presentation.Player.Death;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.FactoryHelper.Runtime.Holdables
{
    public sealed class ThrowBoxRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, ThrowBoxRuntime> owners = new();
        public static ThrowBoxRuntime For(ISourceEngineContext engine) => owners.GetValue(engine, e => new(e));
        private readonly ISourceEngineContext engine;
        private SourceEverestModule module;
        private readonly List<IDisposable> hooks = new();
        private bool loaded;
        public FactoryHelperSession Session => (FactoryHelperSession)module._Session;

        private ThrowBoxRuntime(ISourceEngineContext engine) => this.engine = engine;

        public void Load(SourceEverestModule owner)
        {
            if (loaded) throw new InvalidOperationException("ThrowBox runtime is already loaded.");
            module = owner;
            loaded = true;
            try
            {
                hooks.Add(new Hook(typeof(PlayerHoldingController).GetMethod(nameof(PlayerHoldingController.Pickup)),
                    (Func<Func<PlayerHoldingController, HoldableState, bool>, PlayerHoldingController, HoldableState,
                        bool>)Pickup));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Die)),
                    (Func<Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation>, RoomPlayerSession,
                        Vector2, bool, bool, PlayerDeathPresentation>)Die));
                hooks.Add(new Hook(
                    typeof(LevelExitSequence).GetMethod("Routine", BindingFlags.NonPublic | BindingFlags.Instance),
                    (Func<Func<LevelExitSequence, IEnumerator>, LevelExitSequence, IEnumerator>)Routine));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Pickup(Func<PlayerHoldingController, HoldableState, bool> original,
            PlayerHoldingController self, HoldableState pickup) =>
            !(self.Holding?.Entity is ThrowBox box && ReferenceEquals(box.Runtime, this)) && original(self, pickup);

        private PlayerDeathPresentation Die(
            Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation> original,
            RoomPlayerSession self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
        {
            if (!ReferenceEquals(self.EngineContext, engine))
                return original(self, direction, evenIfInvincible, registerDeathInStats);
            var session = ((OriginalLevelSceneRoot)self.Scene).Session;
            var corpse = original(self, direction, evenIfInvincible, registerDeathInStats);
            // Preserve the original Mod read point after orig, including its live follower list behavior.
            bool hasGolden = false;
            if (corpse != null)
                foreach (var follower in self.Leader.Followers)
                    if (follower.Entity is ISourceStrawberryFollower berry && berry.Golden && !berry.Winged)
                        hasGolden = true;
            if (corpse != null && !hasGolden && Session.SpecialBoxPosition != null)
                corpse.DeathAction = () =>
                    engine.Frame.Scenes.NextScene =
                        ((IOriginalLevelExitEnvironment)engine).LevelExits.CreateLevelExit(LevelExitMode.Restart,
                            session);
            return corpse;
        }

        private IEnumerator Routine(Func<LevelExitSequence, IEnumerator> original, LevelExitSequence self)
        {
            if (ReferenceEquals(self.EngineContext, engine) && Session.SpecialBoxPosition != null)
            {
                Session.OriginalSession.Level = Session.SpecialBoxLevel;
                Session.OriginalSession.RespawnPoint = Session.SpecialBoxPosition;
                engine.Frame.Scenes.NextScene =
                    ((IOriginalLevelLoaderEnvironment)engine).LevelLoaders.CreateLevelLoader(Session.OriginalSession);
                Session.SpecialBoxPosition = null;
            }
            else
            {
                var iterator = original(self);
                while (iterator.MoveNext()) yield return iterator.Current;
            }
        }

        public void Dispose()
        {
            if (!loaded) return;
            loaded = false;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
