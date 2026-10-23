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
    [CustomEntity("FactoryHelper/ThrowBox")]
    [RuntimeTypeAlias("FactoryHelper.Entities.ThrowBox")]
    public partial class ThrowBox : SourceActorEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly ThrowBoxRuntime runtime;
        private readonly SourceCollisionHandler onCollideH;
        private readonly SourceCollisionHandler onCollideV;
        public ThrowBoxRuntime Runtime => runtime;

        public static ThrowBox Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            EntityData data) => new(level.EngineContext, data, offset, room.Name);

        private const float HORIZONTAL_BREAK_SPEED = 180f;
        private const float VERTICAL_BREAK_SPEED = 300f;
        private const float CONVEYOR_ACCELERATION = 200f;
        private const float SOUND_RELOAD_TIME = 0.2f;

        public static ParticleTypePresentation P_Impact { get; } = new ParticleTypePresentation
        {
            Color = SourceHexColor.HexToColor("9c8d7b"),
            Size = 1f,
            FadeMode = ParticleTypePresentation.FadeModes.Late,
            DirectionRange = 1.74532926f,
            SpeedMin = 10f,
            SpeedMax = 20f,
            SpeedMultiplier = 0.1f,
            LifeMin = 0.3f,
            LifeMax = 0.8f
        };

        public Vector2 Speed;
        public HoldableState Hold;
        public ConveyorMover ConveyorMover;
        public Action OnRemoved;
        public bool IsSpecial;

        private static readonly Vector2 DISPLACEMENT = new(-8f, -16f);

        private readonly SpriteImage _image;
        private readonly SpriteImage _warningImage;
        private readonly bool _isMetal;
        private readonly string _levelName;
        private readonly bool _tutorial;
        private readonly RoomTransitionListener _transitionListener;
        private readonly bool _canPassThroughSpinners;
        private readonly ParticleTypePresentation _p_Impact;
        private readonly char _debrisTypeOverride;

        private Vector2 _prevLiftSpeed;
        private OriginalLevelSceneRoot _level;
        private bool _shattered;
        private float _soundTimerX = 0f;
        private float _soundTimerY = 0f;
        private float _noGravityTimer;
        private bool _pickedUp;
        private RoomPlayerSession _player;
        private ParticleEmitterState _shimmerParticles;
        private Vector2 _starterPosition;
        private bool _unspecializeOnRemove = true;
        private BirdTutorialGuiState _tutorialCarry;
        private BirdTutorialGuiState _tutorialPutDown;
        private bool _isCrucial;

        public ThrowBox(ISourceEngineContext engine, Vector2 position, bool isMetal, bool tutorial = false,
            bool isSpecial = false, bool isCrucial = false, bool canPassThroughSpinners = false,
            string crateTextureOverride = null, string crucialTextureOverride = null,
            ParticleTypePresentation impactParticlesOverride = null, char debrisTypeOverride = '\0')
            : base(new PixelActor(null, position), engine,
                ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
        {
            this.engine = engine;
            runtime = ThrowBoxRuntime.For(engine);
            onCollideH = OnCollideH;
            onCollideV = OnCollideV;
            Position -= DISPLACEMENT;
            _starterPosition = Position;
            Depth = 100;
            Collider = new HitboxColliderGeometry(8f, 10f, 4f + DISPLACEMENT.X, 6f + DISPLACEMENT.Y);
            _isMetal = isMetal;
            IsSpecial = isSpecial;
            _isCrucial = isCrucial;
            _tutorial = tutorial;
            _canPassThroughSpinners = canPassThroughSpinners;

            string crateTexture;
            if (string.IsNullOrEmpty(crateTextureOverride))
            {
                string pathString = isMetal ? "crate_metal0" : "crate0";
                crateTexture = $"objects/FactoryHelper/crate/{pathString}";
            }
            else
            {
                crateTexture = crateTextureOverride;
            }

            Add(_image =
                new SpriteImage(((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[crateTexture]));
            _image.Position += DISPLACEMENT;

            if (_isCrucial)
            {
                string crucialTexture = string.IsNullOrEmpty(crucialTextureOverride)
                    ? "objects/FactoryHelper/crate/crucial"
                    : crucialTextureOverride;
                Add(_warningImage =
                    new SpriteImage(((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[crucialTexture]));
                _warningImage.Position += DISPLACEMENT;
            }

            Add(Hold = new HoldableState(engine, 0.1f));
            Hold.PickupCollider = new HitboxColliderGeometry(16f, 16f, DISPLACEMENT.X, DISPLACEMENT.Y);
            Hold.SlowFall = false;
            Hold.SlowRun = true;
            Hold.OnPickup = OnPickup;
            Hold.OnRelease = OnRelease;
            Hold.OnHitSpring = HitSpringObject;
            Hold.OnHitSpinner = OnHitSpinner;
            Hold.SpeedGetter = () => Speed;
            Hold.SpeedSetter = (speed) => Speed = speed;

            Add(_transitionListener = new RoomTransitionListener());
            _transitionListener.OnOutBegin = () => _isCrucial = false;

            Add(ConveyorMover = new ConveyorMover());
            ConveyorMover.OnMove = MoveOnConveyor;

            Add(new SteamCollider(OnSteamWall));

            LiftSpeedGraceTime = 0.1f;

            Add(new LightOccludeState(0.2f));
            Add(new MirrorReflectionState());

            _p_Impact = impactParticlesOverride ?? P_Impact;
            _debrisTypeOverride = debrisTypeOverride;
        }

        public ThrowBox(ISourceEngineContext engine, EntityData data, Vector2 offset, string roomName)
            : this(engine, data.Position + offset, data.Bool("isMetal", false), data.Bool("tutorial", false),
                data.Bool("isSpecial", false), data.Bool("isCrucial", false),
                data.Bool("canPassThroughSpinners", false),
                GetCrateTextureOverride(data), GetCrucialTextureOverride(data), GetImpactParticlesOverride(data),
                GetDebrisTypeOverride(data))
        {
            _levelName = roomName;
        }

        private static string GetCrateTextureOverride(EntityData data)
        {
            return data.Bool("overrideTextures", false) ? data.Attr("crateTexturePath") : null;
        }

        private static string GetCrucialTextureOverride(EntityData data)
        {
            return data.Bool("overrideTextures", false) ? data.Attr("crucialTexturePath") : null;
        }

        private static ParticleTypePresentation GetImpactParticlesOverride(EntityData data)
        {
            return data.Bool("overrideParticles", false)
                ? new ParticleTypePresentation(P_Impact)
                {
                    Color = SourceHexColor.HexToColor(data.Attr("impactParticlesColor"))
                }
                : null;
        }

        private static char GetDebrisTypeOverride(EntityData data)
        {
            return data.Bool("overrideDebris", false) ? data.Char("debrisFromTiletype") : '\0';
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            _level = ((OriginalLevelSceneRoot)Scene);
        }

        public override void Removed(object scene)
        {
            OnRemoved?.Invoke();
            if (IsSpecial && _unspecializeOnRemove)
            {
                runtime.Session.SpecialBoxPosition = null;
            }

            base.Removed(scene);
        }
    }
}
