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
        private float Approach(float value, float target, float amount) => ScalarMath.Approach(value, target, amount,
            ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);

        public override void Update()
        {
            base.Update();
            if (_soundTimerX > 0f)
            {
                _soundTimerX -= engine.DeltaTime;
            }

            if (_soundTimerY > 0f)
            {
                _soundTimerY -= engine.DeltaTime;
            }

            if (Hold.IsHeld)
            {
                _prevLiftSpeed = Vector2.Zero;
            }
            else if (!ConveyorMover.IsOnConveyor)
            {
                if (OnGround())
                {
                    CarryableActorMotionSteps.AdvanceGroundSpeed(this, engine, ref Speed, ref _prevLiftSpeed,
                        ref _noGravityTimer);
                }
                else if (Hold.ShouldHaveGravity)
                {
                    CarryableActorMotionSteps.AdvanceAirSpeed(engine, ref Speed, ref _noGravityTimer, 300f);
                }

                MoveH(Speed.X * engine.DeltaTime, onCollideH);
                MoveV(Speed.Y * engine.DeltaTime, onCollideV);
                if (IsSpecial)
                {
                    if (Left < _level.Bounds.Left)
                    {
                        Left = _level.Bounds.Left;
                        Speed.X *= -0.4f;
                    }
                    else if (Right > _level.Bounds.Right)
                    {
                        Right = _level.Bounds.Right;
                        Speed.X *= -0.4f;
                    }
                    else if (Top < _level.Bounds.Top - 4)
                    {
                        Top = _level.Bounds.Top - 4;
                        Speed.Y = 0;
                    }
                    else if (Bottom > _level.Bounds.Bottom + 4)
                    {
                        Shatter();
                    }
                }

                if (_isCrucial && Bottom > _level.Bounds.Bottom + 4)
                {
                    Shatter();
                }

                if (Left > _level.Bounds.Right + 8 || Right < _level.Bounds.Left - 8 ||
                    Top > _level.Bounds.Bottom + 8 || Bottom < _level.Bounds.Top - 8)
                {
                    RemoveSelf();
                }
            }

            if (Collidable)
            {
                Hold.CheckAgainstColliders();
            }

            if (_tutorial && OnGround(4) && !ConveyorMover.IsOnConveyor && _tutorialCarry == null)
            {
                _tutorialCarry = Tutorial("tutorial_carry", Clean("tutorial_hold"), BirdTutorialButton.Grab);
                _tutorialCarry.Open = true;
                ((ISourceScene)Scene).SourceScene.QueueSourceEntity(_tutorialCarry);
            }
        }

        public override bool IsRiding(ISourceEntityCollider solid)
        {
            return Speed.Y == 0f && base.IsRiding(solid);
        }

        private void MoveOnConveyor(float amount)
        {
            float accY = 800f;
            if (Math.Abs(Speed.Y) <= 30f)
            {
                accY *= 0.5f;
            }

            if (_noGravityTimer > 0f)
            {
                _noGravityTimer -= engine.DeltaTime;
            }
            else
            {
                Speed.Y = Approach(Speed.Y, 300f, accY * engine.DeltaTime);
            }

            Speed.X = Approach(Speed.X, amount, CONVEYOR_ACCELERATION * engine.DeltaTime);
            MoveH(Speed.X * engine.DeltaTime, onCollideH);
            MoveV(Speed.Y * engine.DeltaTime, onCollideV);
        }
    }
}
