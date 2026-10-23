using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Geometry;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Doors;
using Celeste.Graphics.Animation;
using Celeste.Mods.FactoryHelper.Runtime.Graphics;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Movement;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.FactoryHelper.Entities.Doors
{
    [CustomEntity("FactoryHelper/DoorRusty")]
    [SceneTracked(false)]
    [RuntimeTypeAlias("FactoryHelper.Entities.DoorRusty")]
    public class DoorRusty : SourceActorEntityState, IDoorPresentationSource
    {
        private readonly ISourceEngineContext engine;
        private readonly Sprite sprite;
        private readonly string openSfx;
        private readonly string closeSfx;
        private readonly LightOccludeState occlude;
        private readonly string solidRegistryKey;
        private bool disabled;
        Sprite IDoorPresentationSource.Sprite => sprite;

        public static DoorRusty Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level);

        public DoorRusty(EntityData data, Vector2 offset, OriginalLevelSceneRoot level) : base(
            new PixelActor(level.PlatformWorld, data.Position + offset), level.EngineContext,
            ((ISourcePlatformTargetMotionEnvironment)level.EngineContext).TargetMotionPolicy)
        {
            engine = level.EngineContext;
            Depth = 8499;
            Add(sprite = FactorySpriteBank.For(engine).Sprites.Create("rusty_metal_door"));
            openSfx = "event:/game/03_resort/door_metal_open";
            closeSfx = "event:/game/03_resort/door_metal_close";
            sprite.Play("idle");
            Collider = new HitboxColliderGeometry(12, 22, -6, -23);
            Add(occlude = new LightOccludeState(new IntRect(-1, -24, 2, 24)));
            Add(new PlayerColliderComponentState(HitPlayer));
            solidRegistryKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog
                .Find(typeof(SourceSolidEntityState)).RegistryKey;
        }

        public override bool IsRiding(ISourceEntityCollider solid) => solid.Collidable &&
                                                                      SourceEntityCollision.CheckRect(solid,
                                                                          new IntRect((int)X - 2, (int)Y - 2, 4, 4));

        protected override void OnSquish(PixelCollision data)
        {
        }

        private void HitPlayer(RoomPlayerSession player)
        {
            if (!disabled) Open(player.X);
        }

        public void Open(float x) => DoorPresentation.Open((IOriginalGameplayProcess)engine, this, x, openSfx);

        public override void Update()
        {
            string animation = sprite.CurrentAnimationID;
            base.Update();
            occlude.Visible = sprite.CurrentAnimationID == "idle";
            if (!disabled && CollideCheck(solidRegistryKey)) disabled = true;
            DoorPresentation.PlayCloseIfCompleted((IOriginalGameplayProcess)engine, this, animation, closeSfx);
        }
    }
}
