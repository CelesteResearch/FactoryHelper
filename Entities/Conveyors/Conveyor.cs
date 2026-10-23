// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.FactoryHelper.Components.Activation;
using Celeste.Mods.FactoryHelper.Components.Steam;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.FactoryHelper.Entities.Conveyors
{
    [CustomEntity("FactoryHelper/Conveyor")]
    [SceneTracked(false)]
    [RuntimeTypeAlias("FactoryHelper.Entities.Conveyor")]
    public class Conveyor : SourceSolidEntityState
    {
        public const float ConveyorMoveSpeed = 40.0f;

        private const string _spriteRoot = "objects/FactoryHelper/conveyor/";
        private const float _beltFrequency = 0.025f;
        private const float _gearFrequency = 0.05f;

        private static readonly ParticleTypePresentation _grindParticleRight = new()
        {
            Size = 1f,
            Color = SourceHexColor.HexToColor("6b675d"),
            Color2 = SourceHexColor.HexToColor("db8d2e"),
            ColorMode = ParticleTypePresentation.ColorModes.Blink,
            FadeMode = ParticleTypePresentation.FadeModes.Late,
            SpeedMin = 15f,
            SpeedMax = 20f,
            Acceleration = Vector2.UnitY * 2f,
            DirectionRange = (float)Math.PI / 2f,
            Direction = 0,
            LifeMin = 1.0f,
            LifeMax = 2.0f
        };

        private static readonly ParticleTypePresentation _grindParticleLeft = new()
        {
            Size = 1f,
            Color = SourceHexColor.HexToColor("6b675d"),
            Color2 = SourceHexColor.HexToColor("db8d2e"),
            ColorMode = ParticleTypePresentation.ColorModes.Blink,
            FadeMode = ParticleTypePresentation.FadeModes.Late,
            SpeedMin = 15f,
            SpeedMax = 20f,
            Acceleration = Vector2.UnitY * 2f,
            DirectionRange = (float)Math.PI / 2f,
            Direction = (float)Math.PI,
            LifeMin = 1.0f,
            LifeMax = 2.0f
        };

        private readonly Sprite[] _edgeSprites = new Sprite[2];
        private readonly Sprite[] _gearSprites = new Sprite[2];
        private readonly SoundSourceComponentState _sfx;
        private readonly ISourceEngineContext engine;
        private Sprite[] _midSprites;

        public Conveyor(ISourceEngineContext engine, Vector2 position, float width, bool startLeft, string activationId,
            ISourceSolidWorld world)
            : base(position, width, 16, false, world)
        {
            this.engine = engine;
            Add(Activator = new FactoryActivator());
            Activator.StartOn = startLeft;
            Activator.ActivationId = activationId;
            Activator.OnTurnOff = () =>
            {
                Add(new SimulationCoroutine(engine, Sparks()));
                ReverseConveyorDirection();
            };
            Activator.OnStartOff = () =>
            {
                StartAnimation();
                ReverseConveyorDirection();
            };
            Activator.OnTurnOn = () =>
            {
                Add(new SimulationCoroutine(engine, Sparks()));
                ReverseConveyorDirection();
            };
            Activator.OnStartOn = StartAnimation;

            Add(new SteamCollider(OnSteamWall));

            SetupSprites();

            Add(new LightOccludeState(0.2f));
            Add(_sfx = ((ISourceSoundSourceEnvironment)engine).SoundSources.Create());
            _sfx.Position = new Vector2(Width / 2, Height / 2);
        }

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
            => new Conveyor(level.EngineContext, data.Position + offset, data.Width,
                data.Bool("startLeft", true), data.Attr("activationId", ""),
                ((ISourcePlatformCollisionScene)level).SolidWorldFactory.Create(data.Position + offset, false));

        public FactoryActivator Activator { get; }
        public bool IsMovingLeft => Activator.IsOn;
        public bool IsBrokenDown { get; private set; } = false;

        private void OnSteamWall(ISourceEntityCollider obj)
        {
            Add(new SimulationCoroutine(engine, Sparks()));
            StopAnimation();
            IsBrokenDown = true;
        }

        private void SetupSprites()
        {
            for (int i = 0; i < 2; i++)
            {
                Add(_edgeSprites[i] = new Sprite(engine,
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, _spriteRoot));
                _edgeSprites[i].Add("left", "belt_edge", _beltFrequency, "left");
                _edgeSprites[i].Position = new Vector2((Width - 16) * i, 0);

                Add(_gearSprites[i] = new Sprite(engine,
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, _spriteRoot));
                _gearSprites[i].Add("left", "gear", _gearFrequency, "left");
                _gearSprites[i].Position = new Vector2((Width - 16) * i, 0);
            }

            _edgeSprites[1].Rotation = (float)Math.PI;
            _edgeSprites[1].Position += new Vector2(16, 16);

            _midSprites = new Sprite[((int)Width / 8) - 4];

            for (int i = 0; i < _midSprites.Length; i++)
            {
                Add(_midSprites[i] = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                    _spriteRoot));
                _midSprites[i].Add("left", "belt_mid", _beltFrequency, "left");
                _midSprites[i].Position = new Vector2(16 + (8 * i), 0);
            }
        }

        public override void Update()
        {
            base.Update();
            if (!_sfx.Playing)
            {
                _sfx.Play("event:/env/local/09_core/conveyor_idle");
            }
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            Activator.HandleStartup(scene);
        }

        private IEnumerator Sparks()
        {
            for (int i = 0; i < 10; i++)
            {
                ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(_grindParticleLeft, 1, Position + new Vector2(8, 8),
                    new Vector2(4, 4));
                ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(_grindParticleRight, 1,
                    Position + new Vector2(Width - 8, 8), new Vector2(4, 4));
                yield return 0.05f;
            }
        }

        private void StartAnimation()
        {
            PlayAnimationInArray(_midSprites);
            PlayAnimationInArray(_edgeSprites);
            PlayAnimationInArray(_gearSprites);
            _edgeSprites[1].SetAnimationFrame(4);
        }

        private void StopAnimation()
        {
            StopAnimationInArray(_midSprites);
            StopAnimationInArray(_edgeSprites);
            StopAnimationInArray(_gearSprites);
        }

        private void ReverseConveyorDirection()
        {
            ReverseAnimationInArray(_midSprites);
            ReverseAnimationInArray(_edgeSprites);
            ReverseAnimationInArray(_gearSprites);
        }

        private void ReverseAnimationInArray(Sprite[] array)
        {
            foreach (Sprite sprite in array)
            {
                sprite.Rate = -sprite.Rate;
            }
        }

        private void PlayAnimationInArray(Sprite[] array)
        {
            foreach (Sprite sprite in array)
            {
                sprite.Play("left");
            }
        }

        private void StopAnimationInArray(Sprite[] array)
        {
            foreach (Sprite sprite in array)
            {
                sprite.Stop();
            }
        }
    }
}
