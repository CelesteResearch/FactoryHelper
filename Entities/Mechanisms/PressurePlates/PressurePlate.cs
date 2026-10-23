using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Mods.FactoryHelper.Components.Activation;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.FactoryHelper.Entities.Mechanisms.PressurePlates
{
    [CustomEntity("FactoryHelper/PressurePlate")]
    [RuntimeTypeAlias("FactoryHelper.Entities.PressurePlate")]
    public partial class PressurePlate : SourceSolidEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly HashSet<string> activationIds = new();
        private readonly Button button;
        private readonly string actorRegistryKey;
        private readonly string activatorRegistryKey;
        private bool currentButtonState;
        private bool previousButtonState;
        public SpriteImage CaseImage { get; private set; }

        public static PressurePlate Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) =>
            new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public PressurePlate(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : this(engine, data.Position + offset, data.Attr("activationIds", ""), worlds)
        {
        }

        public PressurePlate(ISourceEngineContext engine, Vector2 position, string ids, ISourceSolidWorldFactory worlds)
            : base(position, 16, 6, false, worlds.Create(position, false))
        {
            this.engine = engine;
            string[] array = ids.Split(',');
            Collider.Position.Y += 10;
            foreach (string id in array)
                if (id != "")
                    activationIds.Add(id);
            var atlas = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;
            Add(CaseImage = new SpriteImage(atlas["objects/FactoryHelper/pressurePlate/plate_case0"]));
            CaseImage.Position -= new Vector2(2, 2);
            button = new Button(engine, position);
            Depth = 8020;
            button.Depth = Depth + 1;
            var catalog = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog;
            actorRegistryKey = catalog
                .Find(typeof(Celeste.Simulation.Gameplay.Actors.Foundation.SourceActorEntityState)).RegistryKey;
            activatorRegistryKey = catalog.Find(typeof(FactoryActivator)).RegistryKey;
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            ((ISourceScene)scene).SourceScene.QueueSourceEntity(button);
        }

        public override void Removed(object scene)
        {
            ((ISourceScene)scene).SourceScene.RemoveEntity(button);
            base.Removed(scene);
        }

        public override void Update()
        {
            base.Update();
            var scheduler = ((ISourceScene)Scene).SourceScene;
            var actors = scheduler.Entities.GetTrackedEntities(actorRegistryKey);
            currentButtonState = false;
            foreach (var actor in actors)
                if (SourceEntityCollision.Check((Celeste.Scene.Entities.SceneEntityState)actor, button))
                {
                    currentButtonState = true;
                    break;
                }

            if (currentButtonState != previousButtonState) SendOutSignals(currentButtonState);
            int target = currentButtonState ? 2 : -2;
            button.Image.Position.Y = ScalarMath.Approach(button.Image.Position.Y, target, 40f * engine.DeltaTime);
            previousButtonState = currentButtonState;
        }

        private void SendOutSignals(bool shouldActivate = true)
        {
            foreach (FactoryActivator activator in ((ISourceScene)Scene).SourceScene.Components.GetTrackedComponents(
                         activatorRegistryKey))
                if (activationIds.Contains(activator.ActivationId))
                {
                    if (shouldActivate) activator.Activate(false);
                    else activator.Deactivate(false);
                }
        }
    }
}
