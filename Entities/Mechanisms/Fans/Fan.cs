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

namespace Celeste.Mods.FactoryHelper.Entities.Mechanisms.Fans
{
    [CustomEntity(
        "FactoryHelper/FanHorizontal = LoadHorizontal",
        "FactoryHelper/FanVertical = LoadVertical"
    )]
    [RuntimeTypeAlias("FactoryHelper.Entities.Fan")]
    public class Fan : SourceSolidEntityState
    {
        public FactoryActivator Activator;

        private readonly ISourceEngineContext engine;
        private readonly Sprite _fanSprite;
        private float _percent;
        private bool _speedingUp;

        public Fan(ISourceEngineContext engine, Vector2 position, float width, float height, Directions direction,
            string activationId, bool startActive, ISourceSolidWorldFactory worlds)
            : base(position, width, height, false, worlds.Create(position, false))
        {
            this.engine = engine;
            Add(Activator = new FactoryActivator());
            Activator.ActivationId = activationId != string.Empty ? activationId : null;
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

            Add(_fanSprite = FactorySpriteBank.For(engine).Sprites.Create("fan"));
            _fanSprite.CenterOrigin();
            _fanSprite.Position = new Vector2(width / 2, height / 2);
            switch (direction)
            {
                default:
                case Directions.Horizontal:
                    _fanSprite.Scale.X = width / 24f;
                    break;
                case Directions.Vertical:
                    _fanSprite.Scale.X = height / 24f;
                    _fanSprite.Rotation = (float)Math.PI / 2;
                    break;
            }

            _fanSprite.Play("rotating", true);

            AtlasEntry mTexture =
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas["objects/FactoryHelper/fan/body0"];
            int size = (int)Math.Max(width, height);
            for (int i = 0; i < size; i += 8)
            {
                int x;
                SpriteImage image;
                if (i == 0)
                {
                    x = 0;
                }
                else if (i == size - 8)
                {
                    x = 2;
                }
                else
                {
                    x = 1;
                }

                Add(image = new SpriteImage(mTexture.GetSubtexture(x * 8, 0, 8, 16)));
                switch (direction)
                {
                    default:
                    case Directions.Horizontal:
                        image.X = i;
                        break;
                    case Directions.Vertical:
                        image.Rotation = (float)Math.PI / 2;
                        image.Y = i;
                        image.X += 16;
                        break;
                }
            }

            SurfaceSoundIndex = 7;
            Add(new LightOccludeState(0.5f));
        }

        public Fan(ISourceEngineContext engine, EntityData data, Vector2 offset, Directions direction,
            ISourceSolidWorldFactory worlds)
            : this(engine, data.Position + offset, data.Width, data.Height, direction, data.Attr("activationId"),
                data.Bool("startActive", false), worlds)
        {
        }

        public enum Directions
        {
            Horizontal,
            Vertical
        }

        public static SceneEntityState LoadHorizontal(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new Fan(level.EngineContext, data, offset, Directions.Horizontal, level.SolidWorldFactory);

        public static SceneEntityState LoadVertical(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData data)
            => new Fan(level.EngineContext, data, offset, Directions.Vertical, level.SolidWorldFactory);

        private void OnSteamWall(ISourceEntityCollider obj)
        {
            Activator.ForceDeactivate();
        }

        public override void Update()
        {
            base.Update();
            if (_speedingUp && (_percent < 1f))
            {
                _percent = ScalarMath.Approach(_percent, 1f, engine.DeltaTime / 1f);
            }
            else if (!_speedingUp && (_percent > 0f))
            {
                _percent = ScalarMath.Approach(_percent, 0f, engine.DeltaTime / 1.5f);
            }

            _fanSprite.Rate = _percent;
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            Activator.HandleStartup(scene);
        }
    }
}
