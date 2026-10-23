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
    [RuntimeTypeAlias("FactoryHelper.Entities.FactorySpike")]
    public class FactorySpike : SceneEntityState
    {
        public Directions Direction;

        protected PlayerColliderComponentState pc;
        protected readonly ISourceEngineContext engine;
        protected Vector2 imageOffset;
        protected int size;

        public FactorySpike(ISourceEngineContext engine, Vector2 position, int size, Directions direction)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            Depth = -1;
            Direction = direction;
            this.size = size;
            Add(new SourceStaticMoverState
            {
                OnShake = OnShake,
                SolidChecker = IsRiding,
                JumpThruChecker = IsRiding,
                OnEnable = OnEnable,
                OnDisable = OnDisable
            });
        }

        public FactorySpike(ISourceEngineContext engine, EntityData data, Vector2 offset, Directions dir)
            : this(engine, data.Position + offset, GetSize(data, dir), dir)
        {
        }

        public enum Directions
        {
            Up,
            Down,
            Left,
            Right
        }

        private void OnEnable()
        {
            Active = Visible = Collidable = true;
        }

        private void OnDisable()
        {
            Active = Visible = Collidable = false;
        }

        private void OnShake(Vector2 amount)
        {
            imageOffset += amount;
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = Position;
            Position += imageOffset;
            base.Render(context);
            Position = position;
        }

        public void SetOrigins(Vector2 origin)
        {
            foreach (SourceComponentState component in OwnedComponents)
            {
                if (component is SpriteImage image)
                {
                    Vector2 vector = origin - Position;
                    image.Origin = image.Origin + vector - image.Position;
                    image.Position = vector;
                }
            }
        }

        protected virtual void OnCollide(RoomPlayerSession player)
        {
            switch (Direction)
            {
                case Directions.Up:
                    if (player.Data.Motion.Normal.Speed.Y >= 0f && player.Bottom <= Bottom)
                    {
                        player.Die(new Vector2(0f, -1f));
                    }

                    break;
                case Directions.Down:
                    if (player.Data.Motion.Normal.Speed.Y <= 0f)
                    {
                        player.Die(new Vector2(0f, 1f));
                    }

                    break;
                case Directions.Left:
                    if (player.Data.Motion.Normal.Speed.X >= 0f)
                    {
                        player.Die(new Vector2(-1f, 0f));
                    }

                    break;
                case Directions.Right:
                    if (player.Data.Motion.Normal.Speed.X <= 0f)
                    {
                        player.Die(new Vector2(1f, 0f));
                    }

                    break;
            }
        }

        protected static int GetSize(EntityData data, Directions dir)
        {
            return dir switch
            {
                Directions.Up or Directions.Down => data.Width,
                _ => data.Height,
            };
        }

        protected virtual bool IsRiding(SourceSolidEntityState solid)
        {
            return Direction switch
            {
                Directions.Up => CollisionOutside(solid, Position + Vector2.UnitY),
                Directions.Down => CollisionOutside(solid, Position - Vector2.UnitY),
                Directions.Left => CollisionOutside(solid, Position + Vector2.UnitX),
                Directions.Right => CollisionOutside(solid, Position - Vector2.UnitX),
                _ => false,
            };
        }

        protected virtual bool IsRiding(ISourceJumpThruEntity jumpThru)
        {
            return Direction == Directions.Up &&
                   SourceEntityCollision.CheckAt(this, jumpThru, Position + Vector2.UnitY);
        }

        protected bool CollisionOutside(ISourceEntityCollider target, Vector2 position) =>
            !SourceEntityCollision.Check(this, target) && SourceEntityCollision.CheckAt(this, target, position);
    }
}
