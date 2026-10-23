// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Mods.FactoryHelper.Components.Activation;
using Celeste.Mods.FactoryHelper.Components.Steam;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Scheduling;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.FactoryHelper.Entities.DashNegators
{
    [CustomEntity("FactoryHelper/DashNegator = Load")]
    [RuntimeTypeAlias("FactoryHelper.Entities.DashNegator")]
    public sealed class DashNegator : SceneEntityState
    {
        public static readonly ParticleTypePresentation NegatorFieldParticle = new()
        {
            Size = 1f, Color = PackedColor.FromHex("800000") * .8f,
            Color2 = PackedColor.FromHex("c40000") * .8f,
            ColorMode = ParticleTypePresentation.ColorModes.Static,
            FadeMode = ParticleTypePresentation.FadeModes.InAndOut,
            SpeedMin = 4f, SpeedMax = 8f, SpinMin = .002f, SpinMax = .005f,
            Acceleration = Vector2.Zero, DirectionRange = (float)Math.PI * 2f, Direction = 0f,
            LifeMin = 1.5f, LifeMax = 2.5f
        };

        public readonly FactoryActivator Activator;
        private readonly ISourceEngineContext engine;
        private readonly Sprite[] turretSprites;
        private readonly SourceSolidEntityState[] turretSolids;
        private readonly float particleSpanPeriod;

        public static DashNegator Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public DashNegator(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : this(engine, data.Position + offset, data.Width, data.Height, data.Attr("activationId"),
                data.Bool("startActive"), worlds)
        {
        }

        public DashNegator(ISourceEngineContext engine, Vector2 position, int width, int height,
            string activationId, bool startActive, ISourceSolidWorldFactory worlds)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            Add(Activator = new FactoryActivator
            {
                ActivationId = activationId == string.Empty ? null : activationId,
                StartOn = startActive, OnStartOff = OnStartOff, OnStartOn = OnStartOn,
                OnTurnOff = OnTurnOff, OnTurnOn = OnTurnOn
            });
            Add(new SteamCollider(_ => Activator.ForceDeactivate()));
            Collider = new HitboxColliderGeometry(width - 4, height, 2, 0);
            width = 16 * (width / 16);
            Depth = -8999;
            Add(new SourceStaticMoverState { OnShake = OnShake, SolidChecker = IsRiding, OnDestroy = RemoveSelf });
            Add(new PlayerColliderComponentState(OnPlayer));
            int length = width / 16;
            turretSprites = new Sprite[length];
            turretSolids = new SourceSolidEntityState[length];
            var atlas = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;
            for (int index = 0; index < length; index++)
            {
                var sprite = turretSprites[index] = new Sprite(engine, atlas, "danger/FactoryHelper/dashNegator/");
                Add(sprite);
                sprite.Add("inactive", "turret", 1f, 0);
                sprite.Add("rest", "turret", .2f, new Chooser<string>("active", 1f), 0);
                sprite.Add("active", "turret", .05f, new Chooser<string>("rest", 1f));
                sprite.Position = new Vector2(-2 + 16 * index, -2);
                var solidPosition = position + new Vector2(16 * index, 0);
                turretSolids[index] =
                    new SourceSolidEntityState(solidPosition, 16, 4, false, worlds.Create(solidPosition, false));
            }

            particleSpanPeriod = 256f / (width * height);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            foreach (var solid in turretSolids) ((ISourceScene)scene).SourceScene.AddEntity(solid);
            Activator.HandleStartup((OriginalLevelSceneRoot)scene);
        }

        public override void Removed(object scene)
        {
            foreach (var solid in turretSolids) ((ISourceScene)scene).SourceScene.RemoveEntity(solid);
            base.Removed(scene);
        }

        public override void Update()
        {
            base.Update();
            if (Visible && Activator.IsOn &&
                ((ISourceScene)Scene).SourceScene.Clock.OnInterval(particleSpanPeriod, engine))
                ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(NegatorFieldParticle, 1, Position + Collider.Center,
                    new Vector2(Width / 2, Height / 2));
        }

        public override void Render(ISourceRenderContext context)
        {
            if (Visible && Activator.IsOn)
            {
                SourcePrimitiveDrawing.Rect(context, this, Left, Top, Width, Height, PackedColor.DarkRed * .3f);
                var player =
                    ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(
                        typeof(RoomPlayerSession).FullName);
                if (player != null && !(player.Top - 4f > Bottom || player.Bottom < Top))
                {
                    float left = Math.Min(Math.Max(Left, player.Left - 2f), Right);
                    float right = Math.Max(Math.Min(Right, player.Right + 2f), left);
                    SourcePrimitiveDrawing.Rect(context, this, left, Top, right - left, Height, PackedColor.Red * .3f);
                }
            }

            base.Render(context);
        }

        private void OnStartOn()
        {
            foreach (var sprite in turretSprites) sprite.Play("active", true);
        }

        private void OnStartOff()
        {
            foreach (var sprite in turretSprites) sprite.Play("inactive");
        }

        private void OnTurnOn()
        {
            OnStartOn();
            Fizzle();
        }

        private void OnTurnOff()
        {
            OnStartOff();
            Fizzle();
        }

        private void Fizzle()
        {
            for (int x = 0; x < Width; x += 16)
            for (int y = 0; y < Height; y += 16)
                ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(NegatorFieldParticle, 1,
                    Position + new Vector2(4 + x, 4 + y), new Vector2(4, 4));
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (!Activator.IsOn || !player.Data.StartedDashing) return;
            ShootClosestLaserToPlayer(player);
            player.Die(Vector2.UnitY);
        }

        private void ShootClosestLaserToPlayer(RoomPlayerSession player)
        {
            ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/char/badeline/boss_laser_fire",
                player.Position);
            var position = new Vector2(Position.X, Position.Y + 8);
            position.X += Math.Min((int)(player.Center.X - Left) / 16 * 16, Width - 12) + 8;
            ((ISourceScene)Scene).SourceScene.AddEntity(new DashNegatorBeam(engine, position));
        }

        private void OnShake(Vector2 position)
        {
            foreach (var component in Components)
                if (component is SpriteImage image)
                    image.Position = position;
        }

        private bool IsRiding(SourceSolidEntityState solid)
        {
            foreach (var turret in turretSolids)
                if (SourceEntityCollision.CheckAt(turret, solid, turret.Position - Vector2.UnitY))
                    return true;
            return false;
        }
    }
}
