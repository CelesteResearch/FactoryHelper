using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Framework;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Rendering;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering.Primitives;
using Celeste.Graphics.Rendering.Vertices;
using Celeste.Mods.FactoryHelper.Components.Activation;
using Celeste.Mods.FactoryHelper.Components.Steam;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.FactoryHelper.Hazards.Spikes
{
    [CustomEntity(
        "FactoryHelper/ElectrifiedWallUp = LoadUp",
        "FactoryHelper/ElectrifiedWallDown = LoadDown",
        "FactoryHelper/ElectrifiedWallLeft = LoadLeft",
        "FactoryHelper/ElectrifiedWallRight = LoadRight")]
    [RuntimeTypeAlias("FactoryHelper.Entities.ElectrifiedWall")]
    public partial class ElectrifiedWall : FactorySpike
    {
        public static readonly ParticleTypePresentation P_Fizzle = new()
        {
            Size = 1f,
            Color = SourceHexColor.HexToColor("B9FEFE"),
            Color2 = SourceHexColor.HexToColor("FFF263"),
            ColorMode = ParticleTypePresentation.ColorModes.Blink,
            FadeMode = ParticleTypePresentation.FadeModes.Late,
            SpeedMin = 2f,
            SpeedMax = 16f,
            Acceleration = Vector2.UnitY * 10f,
            DirectionRange = (float)Math.PI * 2f,
            Direction = 0f,
            LifeMin = 0.8f,
            LifeMax = 1.5f
        };

        private static readonly float _particleEmittionPeriodVariance = 0.1f;

        private static readonly PackedColor[] _electricityPackedColors = new PackedColor[2]
        {
            SourceHexColor.HexToColor("fcf579"),
            SourceHexColor.HexToColor("8cf7e2")
        };

        private readonly PackedColor[] _electricityPackedColorsLerped;
        private readonly float _baseParticleEmittionPeriod;
        private readonly LedgeBlockerState _ledgeBlocker;
        private Vector2 _start;
        private Vector2 _end;
        private SourceVertexPositionColor[] _edgeVerts = new SourceVertexPositionColor[1024];
        private uint _edgeSeed;
        private float _particleEmittionPeriod;

        public ElectrifiedWall(ISourceEngineContext engine, Vector2 position, int size, Directions direction,
            string activationId, bool startActive)
            : base(engine, position, size, direction)
        {
            Depth = 10;

            _baseParticleEmittionPeriod = size / 8 * 0.2f;
            SetParticleEmittionPeriod();

            Add(Activator = new FactoryActivator());
            Activator.ActivationId = activationId == string.Empty ? null : activationId;
            Activator.StartOn = startActive;
            Activator.OnTurnOff = Activator.OnTurnOn = SwapState;
            Activator.OnStartOff = () =>
            {
                if (_ledgeBlocker != null)
                {
                    _ledgeBlocker.Blocking = false;
                }
            };

            Add(new SteamCollider(OnSteamWall));

            _start = Vector2.Zero;
            Vector2 offset = direction switch
            {
                Directions.Left or Directions.Right => Vector2.UnitY * size,
                _ => Vector2.UnitX * size,
            };
            _end = offset;

            _electricityPackedColorsLerped = new PackedColor[_electricityPackedColors.Length];

            Add(pc = new PlayerColliderComponentState(OnCollide));

            switch (direction)
            {
                case Directions.Up:
                    Collider = new HitboxColliderGeometry(size, 6f, 0f, -3f);
                    Add(_ledgeBlocker = new LedgeBlockerState());
                    break;
                case Directions.Down:
                    Collider = new HitboxColliderGeometry(size, 6f, 0f, -3f);
                    break;
                case Directions.Left:
                case Directions.Right:
                    Collider = new HitboxColliderGeometry(6f, size, -3f);
                    Add(_ledgeBlocker = new LedgeBlockerState());
                    break;
            }
        }

        public ElectrifiedWall(ISourceEngineContext engine, EntityData data, Vector2 offset, Directions dir)
            : this(engine, data.Position + offset, GetSize(data, dir), dir, data.Attr("activationId", ""),
                data.Bool("startActive", true))
        {
        }

        private ISourceRandom Random => ((ISourceRandomEnvironment)engine).Random;
        public FactoryActivator Activator { get; }
        public float Fade { get; private set; } = 0f;

        public static SceneEntityState LoadUp(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new ElectrifiedWall(level.EngineContext, data, offset, Directions.Up);

        public static SceneEntityState LoadDown(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new ElectrifiedWall(level.EngineContext, data, offset, Directions.Down);

        public static SceneEntityState LoadLeft(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new ElectrifiedWall(level.EngineContext, data, offset, Directions.Left);

        public static SceneEntityState LoadRight(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new ElectrifiedWall(level.EngineContext, data, offset, Directions.Right);

        private void OnSteamWall(ISourceEntityCollider obj)
        {
            Activator.ForceDeactivate();
        }

        private void SwapState()
        {
            Sparkle(size / 4);
            if (_ledgeBlocker != null)
            {
                _ledgeBlocker.Blocking = !_ledgeBlocker.Blocking;
            }
        }

        private void SetParticleEmittionPeriod()
        {
            _particleEmittionPeriod =
                Math.Max(
                    _baseParticleEmittionPeriod - (_particleEmittionPeriodVariance / 2) +
                    SourceRandomMath.NextFloat(Random, _particleEmittionPeriodVariance), 0.05f);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            string str = Direction.ToString().ToLower();
            List<AtlasEntry> atlasSubtextures =
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas.GetAtlasSubtextures(
                    "objects/FactoryHelper/electrifiedWall/knob_" + str);
            for (int j = 0; j < 2; j++)
            {
                AtlasEntry texture = Random.Choose(atlasSubtextures);
                SpriteImage image = new(texture);
                switch (Direction)
                {
                    case Directions.Up:
                        image.JustifyOrigin(0.5f, 1f);
                        image.Position = (Vector2.UnitX * ((j * (size - 8f)) + 4f)) + Vector2.UnitY;
                        break;
                    case Directions.Down:
                        image.JustifyOrigin(0.5f, 0f);
                        image.Position = (Vector2.UnitX * ((j * (size - 8f)) + 4f)) - Vector2.UnitY;
                        break;
                    case Directions.Right:
                        image.JustifyOrigin(0f, 0.5f);
                        image.Position = (Vector2.UnitY * ((j * (size - 8f)) + 4f)) - Vector2.UnitX;
                        break;
                    case Directions.Left:
                        image.JustifyOrigin(1f, 0.5f);
                        image.Position = (Vector2.UnitY * ((j * (size - 8f)) + 4f)) + Vector2.UnitX;
                        break;
                }

                Add(image);
            }

            Activator.HandleStartup(scene);
        }

        public override void Update()
        {
            base.Update();
            if (Activator.IsOn)
            {
                if (((OriginalLevelSceneRoot)Scene).SourceScene.Clock.OnInterval(0.05f, engine))
                {
                    _edgeSeed = (uint)Random.Next();
                }

                if (((OriginalLevelSceneRoot)Scene).SourceScene.Clock.OnInterval(_particleEmittionPeriod, engine))
                {
                    Sparkle(1);
                    SetParticleEmittionPeriod();
                }
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);
            if (Activator.IsOn)
            {
                var camera = (Scene as OriginalLevelSceneRoot).Camera;
                if (camera != null)
                {
                    for (int i = 0; i < _electricityPackedColorsLerped.Length; i++)
                    {
                        _electricityPackedColorsLerped[i] =
                            PackedColor.Lerp(_electricityPackedColors[i], PackedColor.White, Fade);
                    }

                    int index = 0;
                    DrawSimpleLightning(ref index, ref _edgeVerts, _edgeSeed, Position, _start, _end,
                        _electricityPackedColorsLerped[0], 1f + (Fade * 1f));
                    DrawSimpleLightning(ref index, ref _edgeVerts, _edgeSeed + 1, Position, _start, _end,
                        _electricityPackedColorsLerped[1], 1f + (Fade * 1f));
                    if (index > 0)
                    {
                        GameplayRendererState.End(context);
                        SourcePrimitiveGraphics.DrawVertices(
                            ((ISourcePrimitiveGraphicsEnvironment)engine).PrimitiveGraphics, camera.Matrix, _edgeVerts,
                            index);
                        GameplayRendererState.Begin(context);
                    }
                }
            }
        }

        protected new void OnCollide(RoomPlayerSession player)
        {
            if (Activator.IsOn)
            {
                Vector2 dir = Direction switch
                {
                    Directions.Left or Directions.Right => player.Center.X < Center.X ? -Vector2.UnitX : Vector2.UnitX,
                    _ => player.Center.Y < Center.Y ? -Vector2.UnitY : Vector2.UnitY,
                };
                player.Die(dir);
            }
        }

        protected override bool IsRiding(SourceSolidEntityState solid)
        {
            bool collision = false;
            switch (Direction)
            {
                case Directions.Up:
                    Collider.Position -= Vector2.UnitY * 3;
                    collision = CollisionOutside(solid, Position + Vector2.UnitY);
                    Collider.Position += Vector2.UnitY * 3;
                    break;
                case Directions.Down:
                    Collider.Position += Vector2.UnitY * 3;
                    collision = CollisionOutside(solid, Position - Vector2.UnitY);
                    Collider.Position -= Vector2.UnitY * 3;
                    break;
                case Directions.Left:
                    Collider.Position -= Vector2.UnitX * 3;
                    collision = CollisionOutside(solid, Position + Vector2.UnitX);
                    Collider.Position += Vector2.UnitX * 3;
                    break;
                case Directions.Right:
                    Collider.Position += Vector2.UnitX * 3;
                    collision = CollisionOutside(solid, Position - Vector2.UnitX);
                    Collider.Position -= Vector2.UnitX * 3;
                    break;
            }

            return collision;
        }

        private void Sparkle(int count)
        {
            if (Scene == null)
            {
                return;
            }

            ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(P_Fizzle, count, Position + Collider.Center,
                new Vector2(Collider.Width / 2, Collider.Height / 2));
        }
    }
}
