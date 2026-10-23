// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Content.Dialogs;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Entities.Birds;
using Celeste.Game.Gameplay.Platforms.Movement;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.FactoryHelper.Components.Conveyors;
using Celeste.Mods.FactoryHelper.Components.Steam;
using Celeste.Mods.FactoryHelper.Entities.Mechanisms.Fuses;
using Celeste.Mods.FactoryHelper.Entities.Mechanisms.PressurePlates;
using Celeste.Mods.FactoryHelper.Configuration;
using Celeste.Mods.FactoryHelper.Runtime.Holdables;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Actors.Debris;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Hud.Tutorials.Birds;
using Celeste.Simulation.Presentation.Particles.Emission;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.FactoryHelper.Entities.Holdables
{
    public partial class ThrowBox
    {
        private int PersistentTag => ((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value;
        private string Clean(string key) => ((IOriginalFancyTextProcess)engine).Text.Dialog.Clean(key);

        private BirdTutorialGuiState Tutorial(string key, params object[] controls) => new(engine,
            ((IOriginalGameTagsEnvironment)engine).Tags.HUD.Value, ((IOriginalBirdProcess)engine).BirdTutorialResources,
            this, new Vector2(0, -24), Clean(key), controls);

        public void StopBeingSpecial()
        {
            runtime.Session.SpecialBoxPosition = null;
            Remove(_shimmerParticles);
            _shimmerParticles = null;
            IsSpecial = false;
        }

        private void OnRelease(Vector2 force)
        {
            if ((_player == null || _player.Dead) &&
                runtime.Session.SpecialBoxLevel == ((OriginalLevelSceneRoot)Scene).Session.Level)
            {
                StopBeingSpecial();
            }

            Collidable = true;
            _unspecializeOnRemove = true;
            RemoveTag(PersistentTag);
            if (force.X != 0f && force.Y == 0f)
            {
                force.Y = -0.4f;
            }

            Speed = force * 200f;
            if (Speed != Vector2.Zero)
            {
                _noGravityTimer = 0.1f;
            }

            if (_tutorialPutDown != null)
            {
                _tutorialPutDown.Open = false;
            }
        }

        private void OnPickup()
        {
            _player = Hold.Holder;
            _pickedUp = true;
            Collidable = false;
            _unspecializeOnRemove = false;
            Speed = Vector2.Zero;
            AddTag(PersistentTag);
            if (IsSpecial)
            {
                FactoryHelperSession factorySession = runtime.Session;
                if (factorySession.SpecialBoxPosition == null)
                {
                    factorySession.SpecialBoxPosition = _starterPosition;
                    factorySession.OriginalSession = (Scene as OriginalLevelSceneRoot).Session;
                    factorySession.SpecialBoxLevel = _levelName;
                }

                var particlesFG = (Scene as OriginalLevelSceneRoot).ParticlesFG;
                Add(_shimmerParticles = new ParticleEmitterState(particlesFG,
                    ((IOriginalGameplayProcess)engine).ParticleTypes.Key.P_Shimmer, Vector2.UnitY * -6,
                    new Vector2(6f, 6f), 1, 0.2f, engine));
                _shimmerParticles.SimulateCycle();
            }

            if (_tutorialCarry != null)
            {
                _tutorialCarry.Open = false;
            }

            if (_tutorial && _tutorialPutDown == null)
            {
                _tutorialPutDown = Tutorial("tutorial_drop", Clean("tutorial_hold"), new Vector2(0f, 1f), "+ ",
                    Clean("tutorial_release"), BirdTutorialButton.Grab);
                ((ISourceScene)Scene).SourceScene.QueueSourceEntity(_tutorialPutDown);
            }

            if (_tutorialPutDown != null)
            {
                _tutorialPutDown.Open = true;
            }
        }
    }
}
