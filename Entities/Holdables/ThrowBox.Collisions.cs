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
        private void Play(string path) =>
            ((IOriginalGameplayProcess)engine).AudioSession.Play(path, Position,
                ((IOriginalGameplayProcess)engine).SaveContext);

        private void OnSteamWall(ISourceEntityCollider steamWall)
        {
            Shatter();
        }

        private void OnHitSpinner(object spinner)
        {
            if (!_canPassThroughSpinners)
                Shatter();
        }

        protected override void OnSquish(PixelCollision data)
        {
            if (Collidable && !TrySquishWiggle(data))
            {
                Shatter();
            }
        }

        private bool HitSpringObject(object value) => HitSpring((ISourceSpringEntity)value);

        private bool HitSpring(ISourceSpringEntity spring)
        {
            if (!Hold.IsHeld)
            {
                if (spring.Orientation == SpringOrientation.Floor && Speed.Y >= 0f)
                {
                    Speed.X *= 0.5f;
                    Speed.Y = -200f;
                    _noGravityTimer = 0.3f;
                    return true;
                }

                if (spring.Orientation == SpringOrientation.WallLeft && Speed.X <= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = 220f;
                    Speed.Y = -80f;
                    _noGravityTimer = 0.1f;
                    return true;
                }

                if (spring.Orientation == SpringOrientation.WallRight && Speed.X >= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = -220f;
                    Speed.Y = -80f;
                    _noGravityTimer = 0.1f;
                    return true;
                }
            }

            return false;
        }

        private bool CheckPressureButton()
        {
            foreach (PressurePlate.Button button in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(PressurePlate.Button)))
                if (SourceEntityCollision.Check(this, button))
                    return true;
            return false;
        }

        private void OnCollideV(PixelCollision data)
        {
            if (_soundTimerY <= 0f && Math.Abs(Speed.Y) > 100f)
            {
                PlayHitSound();
                _soundTimerY = SOUND_RELOAD_TIME;
            }

            if (Speed.Y > 160f)
            {
                ImpactParticles(data.Direction);
            }

            if (data.Hit is OriginalDashSwitchState)
            {
                (data.Hit as OriginalDashSwitchState).OnDashCollide(null, Vector2.UnitY * Math.Sign(Speed.Y));
            }

            if (!_isMetal && Math.Abs(Speed.Y) >= VERTICAL_BREAK_SPEED)
            {
                Shatter();
            }
            else if (CheckPressureButton())
            {
                Speed = Vector2.Zero;
            }
            else if (Math.Abs(Speed.Y) > 40f)
            {
                Speed.Y *= -0.25f;
            }
            else
            {
                Speed.Y = 0f;
                Y = (float)Math.Floor(Position.Y);
            }
        }

        private void OnCollideH(PixelCollision data)
        {
            if (Math.Abs(Speed.X) > 100f)
            {
                ImpactParticles(data.Direction);
                if (_soundTimerX <= 0f)
                {
                    PlayHitSound();
                    _soundTimerX = SOUND_RELOAD_TIME;
                }
            }

            if (data.Hit is OriginalDashSwitchState)
            {
                (data.Hit as OriginalDashSwitchState).OnDashCollide(null, Vector2.UnitX * Math.Sign(Speed.X));
            }

            if (data.Hit is DashFuseBox)
            {
                (data.Hit as DashFuseBox).OnDashed(null, Vector2.UnitX * Math.Sign(Speed.X));
            }

            if (!_isMetal && (Math.Abs(Speed.X) >= HORIZONTAL_BREAK_SPEED))
            {
                Shatter();
            }
            else if (Math.Abs(Speed.X) > 10f)
            {
                Speed.X *= -0.4f;
            }
            else
            {
                Speed.X = 0f;
            }
        }

        private void PlayHitSound()
        {
            if (_isMetal)
            {
                ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/char/madeline/landing", Position,
                    ((IOriginalGameplayProcess)engine).SaveContext, "surface_index", 7);
            }
            else
            {
                ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/char/madeline/landing", Position,
                    ((IOriginalGameplayProcess)engine).SaveContext, "surface_index", 18);
            }
        }

        private void ImpactParticles(Vector2 dir)
        {
            float direction;
            Vector2 position;
            Vector2 positionRange;
            if (dir.X > 0f)
            {
                direction = (float)Math.PI;
                position = new Vector2(Right, Center.Y);
                positionRange = Vector2.UnitY * 6f;
            }
            else if (dir.X < 0f)
            {
                direction = 0f;
                position = new Vector2(Left, Center.Y);
                positionRange = Vector2.UnitY * 6f;
            }
            else if (dir.Y > 0f)
            {
                direction = -(float)Math.PI / 2f;
                position = new Vector2(Center.X, Bottom);
                positionRange = Vector2.UnitX * 6f;
            }
            else
            {
                direction = (float)Math.PI / 2f;
                position = new Vector2(Center.X, Top);
                positionRange = Vector2.UnitX * 6f;
            }

            (Scene as OriginalLevelSceneRoot).Particles.Emit(_p_Impact, 12, position, positionRange, direction);
        }

        private void Shatter()
        {
            if (!_shattered && !Hold.IsHeld)
            {
                _shattered = true;
                _image.Visible = false;
                if (_isMetal)
                {
                    Play("event:/game/general/wall_break_ice");
                }
                else
                {
                    Play("event:/game/general/wall_break_wood");
                }

                for (int i = 0; i < Width / 8f; i++)
                {
                    for (int j = 0; j < Height / 8f; j++)
                    {
                        if (_debrisTypeOverride != '\0')
                        {
                            ((ISourceScene)Scene).SourceScene.QueueSourceEntity(engine.EntityPools.Create<DebrisState>()
                                .Init(Position + new Vector2(4 + (i * 8), 4 + (j * 8)) + DISPLACEMENT,
                                    _debrisTypeOverride, false).BlastFrom(Center));
                        }
                        else if (_isMetal)
                        {
                            ((ISourceScene)Scene).SourceScene.QueueSourceEntity(engine.EntityPools.Create<DebrisState>()
                                .Init(Position + new Vector2(4 + (i * 8), 4 + (j * 8)) + DISPLACEMENT, '8', false)
                                .BlastFrom(Center));
                        }
                        else
                        {
                            ((ISourceScene)Scene).SourceScene.QueueSourceEntity(engine.EntityPools.Create<DebrisState>()
                                .Init(Position + new Vector2(4 + (i * 8), 4 + (j * 8)) + DISPLACEMENT, '9', false)
                                .BlastFrom(Center));
                        }
                    }
                }

                RoomPlayerSession player;
                player = ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>();
                if ((IsSpecial && runtime.Session.SpecialBoxPosition != null && runtime.Session.SpecialBoxLevel !=
                        ((OriginalLevelSceneRoot)Scene).Session.Level) || (_isCrucial && _pickedUp))
                {
                    if (player != null)
                    {
                        player.Die(
                            -((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(Position -
                                player.Position));
                    }

                    _unspecializeOnRemove = false;
                }
                else if (IsSpecial)
                {
                    StopBeingSpecial();
                    _unspecializeOnRemove = true;
                }

                RemoveSelf();
            }
        }
    }
}
