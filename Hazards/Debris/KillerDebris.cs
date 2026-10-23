// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Entities.Hazards.Rendering;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Startup;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.FactoryHelper.Hazards.Debris
{
    [CustomEntity("FactoryHelper/KillerDebris")]
    [SceneTracked]
    [RuntimeTypeAlias("FactoryHelper.Entities.KillerDebris")]
    public sealed class KillerDebris : SceneEntityState
    {
        private static readonly Dictionary<DebrisColor, string> foregroundTextures = new()
        {
            [DebrisColor.Silver] = "danger/FactoryHelper/debris/fg_silver",
            [DebrisColor.Bronze] = "danger/FactoryHelper/debris/fg_bronze"
        };

        private static readonly Dictionary<DebrisColor, string> backgroundTextures = new()
        {
            [DebrisColor.Silver] = "danger/FactoryHelper/debris/bg_silver",
            [DebrisColor.Bronze] = "danger/FactoryHelper/debris/bg_bronze"
        };

        private readonly ISourceEngineContext engine;
        private readonly float offset;
        private readonly int randomSeed;
        private readonly DebrisColor color;
        private bool expanded;
        private SceneEntityState filler;
        private StaticHazardBorderState border;
        public bool AttachToSolid;
        private ISourceRandom Random => ((ISourceRandomEnvironment)engine).Random;
        private ISourceVectorArithmetic VectorArithmetic => ((ISourceVectorEnvironment)engine).VectorArithmetic;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public KillerDebris(ISourceEngineContext engine, Vector2 position, bool attachToSolid, string color)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            offset = SourceRandomMath.NextFloat(Random);
            Enum.TryParse(color, out this.color);
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.TransitionUpdate.Value);
            Collider = new ColliderListGeometry(new CircleColliderGeometry(VectorArithmetic, 6f),
                new HitboxColliderGeometry(16f, 4f, -8f, -3f));
            Visible = false;
            Add(new PlayerColliderComponentState(OnPlayer));
            Add(new HoldableColliderState(OnHoldable));
            Add(new LedgeBlockerState());
            Depth = -8500;
            AttachToSolid = attachToSolid;
            if (attachToSolid)
                Add(new SourceStaticMoverState { OnShake = OnShake, SolidChecker = IsRiding, OnDestroy = RemoveSelf });
            randomSeed = Random.Next();
        }

        public KillerDebris(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position + offset, data.Bool("attachToSolid"), data.Attr("color", "bronze"))
        {
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (InView()) CreateSprites();
        }

        public void ForceInstantiate()
        {
            CreateSprites();
            Visible = true;
        }

        public override void Update()
        {
            if (!Visible)
            {
                Collidable = false;
                if (InView())
                {
                    Visible = true;
                    if (!expanded) CreateSprites();
                }
            }
            else
            {
                base.Update();
                if (Level.SourceScene.Clock.OnInterval(.25f, offset, engine) && !InView()) Visible = false;
                if (Level.SourceScene.Clock.OnInterval(.05f, offset, engine))
                {
                    var player = Level.SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>();
                    if (player != null) Collidable = Math.Abs(player.X - X) < 256f && Math.Abs(player.Y - Y) < 128f;
                }
            }

            if (filler != null) filler.Position = Position;
        }

        private bool InView()
        {
            var camera = Level.Camera;
            return X > camera.X - 16f && Y > camera.Y - 16f && X < camera.X + 320f + 16f && Y < camera.Y + 180f + 16f;
        }

        private void CreateSprites()
        {
            if (expanded) return;
            var process = ((ISourceRandomProcessEnvironment)engine).RandomProcess;
            process.PushRandom(randomSeed);
            var atlas = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;
            AtlasEntry texture = Random.Choose(atlas.GetAtlasSubtextures(foregroundTextures[color]));
            if (!SolidCheck(new Vector2(X - 4f, Y - 4f)))
                Add(new SpriteImage(texture.GetSubtexture(0, 0, 14, 14)).SetOrigin(12f, 12f));
            if (!SolidCheck(new Vector2(X + 4f, Y - 4f)))
                Add(new SpriteImage(texture.GetSubtexture(10, 0, 14, 14)).SetOrigin(2f, 12f));
            if (!SolidCheck(new Vector2(X + 4f, Y + 4f)))
                Add(new SpriteImage(texture.GetSubtexture(10, 10, 14, 14)).SetOrigin(2f, 2f));
            if (!SolidCheck(new Vector2(X - 4f, Y + 4f)))
                Add(new SpriteImage(texture.GetSubtexture(0, 10, 14, 14)).SetOrigin(12f, 2f));
            foreach (KillerDebris other in Level.SourceScene.Entities.GetTrackedEntities(typeof(KillerDebris)))
                if (other != this && other.AttachToSolid == AttachToSolid && other.X >= X
                    && VectorArithmetic.Length(other.Position - Position) < 24f)
                    AddConnector(VectorArithmetic.Divide(Position + other.Position, 2f) - Position);
            Level.SourceScene.AddEntity(border = new StaticHazardBorderState(this, filler, engine, PackedColor.Black));
            expanded = true;
            process.PopRandom();
        }

        private void AddConnector(Vector2 offset)
        {
            if (filler == null)
            {
                filler = new SceneEntityState(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                        Position)
                    { Depth = Depth + 1 };
                Level.SourceScene.AddEntity(filler);
            }

            var atlas = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;
            var image = new SpriteImage(Random.Choose(atlas.GetAtlasSubtextures(backgroundTextures[color])))
            {
                Position = offset,
                Rotation = (float)Random.Choose(0, 1, 2, 3) * ((float)Math.PI / 2f)
            };
            image.CenterOrigin();
            filler.Add(image);
        }

        private bool SolidCheck(Vector2 position)
        {
            if (AttachToSolid) return false;
            foreach (SourceSolidEntityState solid in SourceSceneCollision.AllPoint<SourceSolidEntityState>(
                         Level.SourceScene.Entities,
                         "Celeste.Solid", position))
                if (solid is ISourceSolidTilesEntity)
                    return true;
            return false;
        }

        private void OnShake(Vector2 position)
        {
            foreach (var component in Components)
                if (component is SpriteImage image)
                    image.Position = position;
        }

        private bool IsRiding(SourceSolidEntityState solid) => SourceEntityCollision.Check(this, solid);

        private void OnPlayer(RoomPlayerSession player) =>
            player.Die(VectorArithmetic.SafeNormalize(player.Position - Position));

        private void OnHoldable(HoldableState holdable) => holdable.HitSpinner(this);

        public override void Removed(object scene)
        {
            if (filler != null && filler.Scene == scene) filler.RemoveSelf();
            if (border != null && border.Scene == scene) border.RemoveSelf();
            base.Removed(scene);
        }
    }
}
