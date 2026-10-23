using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Randomness;
using Celeste.Core.Maps;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Audio;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Graphics.Display.Colors;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Mods.FactoryHelper.Components.Activation;
using Celeste.Mods.FactoryHelper.Cutscenes.Fuses;

namespace Celeste.Mods.FactoryHelper.Entities.Mechanisms.Fuses
{
    [CustomEntity("FactoryHelper/DashFuseBox")]
    [RuntimeTypeAlias("FactoryHelper.Entities.DashFuseBox")]
    public class DashFuseBox : SourceSolidEntityState
    {
        private readonly ParticleTypePresentation _sparks = new()
        {
            Size = 1f,
            Color = SourceHexColor.HexToColor("d97b00"),
            Color2 = SourceHexColor.HexToColor("f7be00"),
            ColorMode = ParticleTypePresentation.ColorModes.Blink,
            FadeMode = ParticleTypePresentation.FadeModes.Late,
            SpeedMin = 5f,
            SpeedMax = 30f,
            Acceleration = Vector2.UnitY * 60f,
            DirectionRange = (float)Math.PI / 2f,
            Direction = 0f,
            LifeMin = 0.5f,
            LifeMax = 1.0f
        };

        private readonly bool _persistent;
        private readonly Sprite _mainSprite;
        private readonly Sprite _doorSprite;
        private readonly SceneEntityState _door;
        private readonly Direction _direction;
        private readonly HashSet<string> _activationIds = new();
        private readonly bool _startCutscene;
        private bool _activated = false;
        private MapEntityId _id;
        private Vector2 _pressDirection;

        public DashFuseBox(ISourceEngineContext engine, OriginalLevelEntityData data, Vector2 offset,
            ISourceSolidWorld world)
            : base(data.Position + offset, 4f, 16f, false, world)
        {
            this.engine = engine;
            activatorRegistryKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog.Find(typeof(FactoryActivator))
                .RegistryKey;
            string[] activationIds = data.Attr("activationIds", "").Split(',');

            _startCutscene = data.Bool("startCutscene", false);
            _persistent = data.Bool("persistent", false);
            _id = new MapEntityId(data.Level.Name, data.ID);
            _direction = Direction.Right;
            Enum.TryParse(data.Attr("direction", "Right"), out _direction);

            Depth = -20;
            Add(_mainSprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "objects/FactoryHelper/dashFuseBox/"));
            _mainSprite.Add("idle", "idle", 0.1f, "idleBackwards");
            _mainSprite.Add("chaos", "chaos", 0.08f, "chaos");
            _mainSprite.Add("idleBackwards", "idle", 0.1f, "idle", 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0);
            _mainSprite.Play("idle", false, false);

            _door = new SceneEntityState(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                Position)
            {
                Depth = 8490
            };
            _door.Add(_doorSprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "objects/FactoryHelper/dashFuseBox/"));
            _doorSprite.Add("busted", "busted", 0.05f);
            _doorSprite.Play("busted", false, false);
            _doorSprite.Active = false;
            _doorSprite.Visible = false;

            if (_direction == Direction.Left)
            {
                _mainSprite.Scale *= new Vector2(-1f, 1f);
                //_mainSprite.Position.X += 4;
                _doorSprite.Scale *= new Vector2(-1f, 1f);
                _doorSprite.Position.X += 4;

                Collider.Position.X -= 4;
                _pressDirection = Vector2.UnitX;
                _sparks.Direction = (float)Math.PI;
            }
            else
            {
                _pressDirection = -Vector2.UnitX;
            }

            foreach (string activationId in activationIds)
            {
                if (activationId != "")
                {
                    _activationIds.Add(activationId);
                }
            }

            OnDashCollide = OnDashed;
        }

        private readonly ISourceEngineContext engine;
        private readonly string activatorRegistryKey;

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new DashFuseBox(level.EngineContext, data, offset,
            level.SolidWorldFactory.Create(data.Position + offset, false));

        public enum Direction
        {
            Left,
            Right
        }

        private bool _activatedPermanently
        {
            get => (Scene as OriginalLevelSceneRoot).Session.GetFlag($"DashFuseBox:{_id.Key}");
            set => (Scene as OriginalLevelSceneRoot).Session.SetFlag($"DashFuseBox:{_id.Key}", value);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            ((ISourceScene)scene).SourceScene.QueueSourceEntity(_door);
            if (_activatedPermanently || AllCircuitsActive())
            {
                StartBusted();
                _activated = true;
            }
        }

        private void SendOutSignals()
        {
            foreach (FactoryActivator activator in ((ISourceScene)Scene).SourceScene.Components.GetTrackedComponents(
                         activatorRegistryKey))
            {
                if (_activationIds.Contains(activator.ActivationId))
                {
                    activator.Activate();
                }
            }
        }

        private bool AllCircuitsActive()
        {
            var session = (Scene as OriginalLevelSceneRoot).Session;
            foreach (string activationId in _activationIds)
            {
                if (session.GetFlag($"FactoryActivation:{activationId}") == false)
                {
                    return false;
                }
            }

            return true;
        }

        public override void Removed(object scene)
        {
            ((ISourceScene)scene).SourceScene.RemoveScheduledEntity(_door);
            base.Removed(scene);
        }

        public DashCollisionResult OnDashed(RoomPlayerSession player, Vector2 direction)
        {
            if (!_activated && (direction == _pressDirection))
            {
                if (_startCutscene)
                {
                    ((ISourceScene)Scene).SourceScene.QueueSourceEntity(new BreakFirstFuseCutscene(engine, player));
                }

                _activated = true;

                _doorSprite.Active = true;
                _doorSprite.Visible = true;
                _mainSprite.Play("chaos", true, true);
                (Scene as OriginalLevelSceneRoot).Displacement.AddBurst(Center, 0.35f, 8f, 32f, 0.25f);
                SetBustedCollider();

                ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                    "event:/new_content/game/10_farewell/fusebox_hit_2", Position);
                SetSessionTags();
                SendOutSignals();

                player?.RefillDash();

                Sparkle(20);

                Add(new SimulationCoroutine(engine, SparkleSequence()));

                return DashCollisionResult.Rebound;
            }

            return DashCollisionResult.NormalCollision;
        }

        private IEnumerator SparkleSequence()
        {
            while (true)
            {
                yield return SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 4f);
                for (int i = 0; i < 6; i++)
                {
                    yield return SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 0.03f);
                    Sparkle(1);
                }
            }
        }

        private void Sparkle(int count)
        {
            if (Scene == null)
            {
                return;
            }

            ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(_sparks, count, Position + Collider.CenterRight,
                new Vector2(0, Collider.Height / 4));
        }

        private void SetSessionTags()
        {
            if (_persistent)
            {
                foreach (string activationId in _activationIds)
                {
                    ActivatePermanently(activationId);
                }

                _activatedPermanently = true;
            }
        }

        public override void Update()
        {
            base.Update();
            if (_activated)
            {
                DisplacePlayerOnTop();
            }
        }

        public override void Render(Celeste.Core.Rendering.ISourceRenderContext context)
        {
            if (!_activated)
            {
                _mainSprite.DrawSimpleOutline(context);
            }

            base.Render(context);
        }

        private void DisplacePlayerOnTop()
        {
            if (HasPlayerOnTop())
            {
                RoomPlayerSession player = GetPlayerOnTop();
                if (player != null)
                {
                    if (_direction == Direction.Right)
                    {
                        if (player.Left >= Left)
                        {
                            player.Left = Right;
                            player.Y += 1f;
                        }
                    }
                    else
                    {
                        if (player.Right <= Right)
                        {
                            player.Right = Left;
                            player.Y += 1f;
                        }
                    }
                }
            }
        }

        private void StartBusted()
        {
            _mainSprite.Play("chaos", true, true);
            _doorSprite.SetAnimationFrame(4);
            _doorSprite.Visible = true;
            SetBustedCollider();
            Add(new SimulationCoroutine(engine, SparkleSequence()));
        }

        private void SetBustedCollider()
        {
            ((Celeste.Physics.Collision.Shapes.HitboxColliderGeometry)Collider).Width = 2;
            if (_direction == Direction.Left)
            {
                Position += 2 * Vector2.UnitX;
                _mainSprite.Position -= 2 * Vector2.UnitX;
                _doorSprite.Position -= 4 * Vector2.UnitX;
            }
        }

        private void ActivatePermanently(string activationId)
        {
            (Scene as OriginalLevelSceneRoot).Session.SetFlag($"FactoryActivation:{activationId}", true);
        }
    }
}
