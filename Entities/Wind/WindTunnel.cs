// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Mods.FactoryHelper.Components.Activation;
using Celeste.Mods.FactoryHelper.Components.Steam;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Environment.Wind;

namespace Celeste.Mods.FactoryHelper.Entities.Wind
{
    [CustomEntity("FactoryHelper/WindTunnel")]
    [RuntimeTypeAlias("FactoryHelper.Entities.WindTunnel")]
    public sealed class WindTunnel : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private static readonly float _baseAlpha = 0.7f;
        private static readonly char[] separators = { ',' };

        private readonly float _windUpTime = 0.5f;
        private readonly float _windDownTime = 0.2f;
        private readonly Dictionary<WindMoverState, float> _componentPercentages = new();
        private readonly float _loopWidth;
        private readonly float _loopHeight;
        private readonly float _strength;
        private readonly Direction _direction;
        private readonly Particle[] _particles;
        private Vector2 _scale = Vector2.One;
        private float _percent;
        private bool _speedingUp;
        private Vector2 _defaultWindSpeed;
        private PackedColor[] _colors;

        public WindTunnel(ISourceEngineContext engine, Vector2 position, int width, int height, float strength,
            string direction, string activationId, bool startActive,
            string particleColors, bool showParticles) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            Depth = -1000;
            Position = position;
            _loopWidth = width;
            _loopHeight = height;

            Add(Activator = new FactoryActivator());
            Activator.ActivationId = activationId == string.Empty ? null : activationId;
            Activator.StartOn = startActive;
            Activator.OnTurnOn = () => { _speedingUp = true; };
            Activator.OnTurnOff = () => { _speedingUp = false; };
            Activator.OnStartOff = () =>
            {
                _percent = 0f;
                _speedingUp = false;
            };
            Activator.OnStartOn = () =>
            {
                _percent = 1f;
                _speedingUp = true;
            };

            Add(new SteamCollider(OnSteamWall));

            Collider = new HitboxColliderGeometry(width, height);
            _strength = strength;
            Enum.TryParse(direction, out _direction);

            switch (_direction)
            {
                case Direction.Up:
                    _defaultWindSpeed = -Vector2.UnitY * _strength;
                    break;
                case Direction.Down:
                    _defaultWindSpeed = Vector2.UnitY * _strength;
                    break;
                case Direction.Left:
                    _defaultWindSpeed = -Vector2.UnitX * _strength;
                    break;
                case Direction.Right:
                    _defaultWindSpeed = Vector2.UnitX * _strength;
                    break;
            }

            int particlecount = showParticles ? width * height / 300 : 0;

            _colors = particleColors.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(str => SourceHexColor.HexToColor(str.Trim()))
                .ToArray();

            _particles = new Particle[particlecount];
            for (int i = 0; i < _particles.Length; i++)
            {
                Reset(i, SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, _baseAlpha));
            }
        }

        public WindTunnel(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position + offset, data.Width, data.Height, data.Float("strength", 1f),
                data.Attr("direction", "Up"), data.Attr("activationId", ""),
                data.Bool("startActive", false), data.Attr("particleColors", "808080,545151,ada5a5"),
                data.Bool("showParticles", true))
        {
        }

        public enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }

        public FactoryActivator Activator { get; }

        private Vector2 _actualWindSpeed => _defaultWindSpeed * _percent;

        private void OnSteamWall(ISourceEntityCollider obj)
        {
            Activator.ForceDeactivate();
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            Activator.HandleStartup(scene);
            PositionParticles();
        }

        public override void Update()
        {
            if (_speedingUp && (_percent < 1f))
            {
                _percent = ScalarMath.Approach(_percent, 1f, engine.DeltaTime / 1f);
            }
            else if (!_speedingUp && (_percent > 0f))
            {
                _percent = ScalarMath.Approach(_percent, 0f, engine.DeltaTime / 1.5f);
            }

            PositionParticles();
            foreach (object tracked in ((OriginalLevelSceneRoot)Scene).SourceScene.Components.GetTrackedComponents(
                         "Celeste.WindMover"))
            {
                WindMoverState component = (WindMoverState)tracked;
                if (SourceEntityCollision.Check((SceneEntityState)component.Entity, this))
                {
                    if (_componentPercentages.ContainsKey(component))
                    {
                        _componentPercentages[component] = ScalarMath.Approach(_componentPercentages[component], 1f,
                            engine.DeltaTime / _windUpTime);
                    }
                    else
                    {
                        _componentPercentages.Add(component, 0f);
                    }
                }
                else
                {
                    if (_componentPercentages.ContainsKey(component))
                    {
                        _componentPercentages[component] = ScalarMath.Approach(_componentPercentages[component], 0.0f,
                            engine.DeltaTime / _windDownTime);
                        if (_componentPercentages[component] == 0f)
                        {
                            _componentPercentages.Remove(component);
                        }
                    }
                }
            }

            foreach (WindMoverState component in _componentPercentages.Keys)
            {
                if (component != null && component.Entity != null && ((SceneEntityState)component.Entity).Scene != null)
                {
                    component.Move(_actualWindSpeed * 0.1f * engine.DeltaTime *
                                   ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeInOut(
                                       _componentPercentages[component]));
                }
            }

            base.Update();
        }

        public override void Render(ISourceRenderContext context)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                Vector2 particlePosition = default;
                particlePosition.X = Mod(_particles[i].Position.X, _loopWidth);
                particlePosition.Y = Mod(_particles[i].Position.Y, _loopHeight);
                float percent = _particles[i].Percent;
                float num = (!(percent < 0.7f))
                    ? ScalarMath.ClampedMap(percent, 0.7f, 1f, 1f, 0f)
                    : ScalarMath.ClampedMap(percent, 0f, 0.3f);
                SourcePrimitiveDrawing.Rect(context, this, particlePosition + Position, _scale.X, _scale.Y,
                    _colors[_particles[i].Color] * num);
            }
        }

        private void PositionParticles()
        {
            bool flag = _actualWindSpeed.Y == 0f;
            Vector2 zero;
            if (flag)
            {
                _scale.X = Math.Max(1f, Math.Abs(_actualWindSpeed.X) / 40f);
                _scale.Y = 1f;
                zero = new Vector2(_actualWindSpeed.X, 0f);
            }
            else
            {
                float divisor = _direction == Direction.Down ? 20f : 100f;
                _scale.X = 1f;
                _scale.Y = Math.Max(1f, Math.Abs(_actualWindSpeed.Y) / divisor);
                zero = new Vector2(0f, _actualWindSpeed.Y * 2f);
            }

            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i].Percent >= 1f)
                {
                    Reset(i, 0f);
                }

                float divisor = _direction switch
                {
                    Direction.Up => 4f,
                    Direction.Down => 1.5f,
                    _ => 1f,
                };
                _particles[i].Percent += engine.DeltaTime / _particles[i].Duration;
                _particles[i].Position += ((_particles[i].Direction * _particles[i].Speed) + (zero / divisor)) *
                                          engine.DeltaTime;
                ((ISourceVectorEnvironment)engine).VectorArithmetic.Rotate(_particles[i].Direction,
                    _particles[i].Spin * engine.DeltaTime);
            }
        }

        private float Mod(float x, float m)
        {
            return ((x % m) + m) % m;
        }

        private void Reset(int i, float p)
        {
            _particles[i].Percent = p;
            _particles[i].Position =
                new Vector2(SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, 0, _loopWidth),
                    SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, 0, _loopHeight));
            _particles[i].Speed = SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, 4, 14);
            _particles[i].Spin =
                SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, 0.25f, (float)Math.PI * 6f);
            _particles[i].Duration = SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, 1f, 4f);
            _particles[i].Direction = Vector2Geometry.AngleToVector(
                SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, (float)Math.PI * 2f), 1f);
            _particles[i].Color = ((ISourceRandomEnvironment)engine).Random.Next(_colors.Length);
        }

        public struct Particle
        {
            public Vector2 Position;
            public float Percent;
            public float Duration;
            public Vector2 Direction;
            public float Speed;
            public float Spin;
            public int Color;
        }
    }
}
