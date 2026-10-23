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
using Celeste.Mods.FactoryHelper.Hazards.Spikes;
using Celeste.Mods.FactoryHelper.Runtime.Graphics;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.FactoryHelper.Hazards.Spikes
{
    [CustomEntity(
        "FactoryHelper/RustySpikeUp = LoadUp",
        "FactoryHelper/RustySpikeDown = LoadDown",
        "FactoryHelper/RustySpikeLeft = LoadLeft",
        "FactoryHelper/RustySpikeRight = LoadRight")]
    [RuntimeTypeAlias("FactoryHelper.Entities.RustySpike")]
    public class RustySpike : FactorySpike
    {
        public RustySpike(ISourceEngineContext engine, Vector2 position, int size, Directions direction)
            : base(engine, position, size, direction)
        {
            switch (direction)
            {
                case Directions.Up:
                    Collider = new HitboxColliderGeometry(size, 3f, 0f, -3f);
                    Add(new LedgeBlockerState());
                    break;
                case Directions.Down:
                    Collider = new HitboxColliderGeometry(size, 3f);
                    break;
                case Directions.Left:
                    Collider = new HitboxColliderGeometry(3f, size, -3f);
                    Add(new LedgeBlockerState());
                    break;
                case Directions.Right:
                    Collider = new HitboxColliderGeometry(3f, size);
                    Add(new LedgeBlockerState());
                    break;
            }

            Add(pc = new PlayerColliderComponentState(OnCollide));
        }

        public RustySpike(ISourceEngineContext engine, EntityData data, Vector2 offset, Directions dir)
            : this(engine, data.Position + offset, GetSize(data, dir), dir)
        {
        }

        public static SceneEntityState LoadUp(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new RustySpike(level.EngineContext, data, offset, Directions.Up);

        public static SceneEntityState LoadDown(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new RustySpike(level.EngineContext, data, offset, Directions.Down);

        public static SceneEntityState LoadLeft(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new RustySpike(level.EngineContext, data, offset, Directions.Left);

        public static SceneEntityState LoadRight(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new RustySpike(level.EngineContext, data, offset, Directions.Right);

        public override void Added(object scene)
        {
            base.Added(scene);
            string str = Direction.ToString().ToLower();
            List<AtlasEntry> atlasSubtextures =
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas.GetAtlasSubtextures(
                    "objects/FactoryHelper/rustySpike/rusty_" + str);
            for (int j = 0; j < size / 8; j++)
            {
                AtlasEntry texture = ((ISourceRandomEnvironment)engine).Random.Choose(atlasSubtextures);
                SpriteImage image = new(texture);
                switch (Direction)
                {
                    case Directions.Up:
                        image.JustifyOrigin(0.5f, 1f);
                        image.Position = (Vector2.UnitX * (j + 0.5f) * 8f) + Vector2.UnitY;
                        break;
                    case Directions.Down:
                        image.JustifyOrigin(0.5f, 0f);
                        image.Position = (Vector2.UnitX * (j + 0.5f) * 8f) - Vector2.UnitY;
                        break;
                    case Directions.Right:
                        image.JustifyOrigin(0f, 0.5f);
                        image.Position = (Vector2.UnitY * (j + 0.5f) * 8f) - Vector2.UnitX;
                        break;
                    case Directions.Left:
                        image.JustifyOrigin(1f, 0.5f);
                        image.Position = (Vector2.UnitY * (j + 0.5f) * 8f) + Vector2.UnitX;
                        break;
                }

                Add(image);
            }
        }
    }
}
