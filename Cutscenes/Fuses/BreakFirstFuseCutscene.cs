using System.Collections;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Levels.Cutscenes;
using Celeste.Simulation;

namespace Celeste.Mods.FactoryHelper.Cutscenes.Fuses
{
    [RuntimeTypeAlias("FactoryHelper.Cutscenes.CS01_FactoryHelper_BreakFirstFuse")]
    public class BreakFirstFuseCutscene : OriginalCutsceneEntityState
    {
        private readonly RoomPlayerSession _player;
        private readonly ISourceEngineContext engine;

        public BreakFirstFuseCutscene(ISourceEngineContext engine, RoomPlayerSession player) : base(engine)
        {
            this.engine = engine;
            _player = player;
        }

        public override void OnBegin(OriginalLevelSceneRoot level)
        {
            Add(new SimulationCoroutine(engine, Cutscene(level)));
        }

        private IEnumerator Cutscene(OriginalLevelSceneRoot level)
        {
            _player.Machine.State = 11;
            while (!_player.OnGround())
            {
                yield return null;
            }

            yield return 1.0f;
            _player.Data.Facing = -1;
            yield return 0.5f;
            Add(new SimulationCoroutine(engine, WaklLeftLookUp()));
            yield return 0.5f;
            yield return PanCameraY(level.Bounds.Top, 0.25f);
            yield return 0.5f;
            _player.Dummy.AutoAnimate = true;
            yield return PanCameraY(_player.CameraTarget.Y, 0.6f);
            yield return null;
            EndCutscene(level);
        }

        private IEnumerator WaklLeftLookUp()
        {
            float target = _player.X - 64f;
            SimulationCoroutine walk;
            _player.Machine.State = 0;
            Add(walk = new SimulationCoroutine(engine, _player.DummyWalkTo(target, speedMultiplier: 1.2f)));
            while (_player.Center.X > target + 3f)
            {
                yield return null;
            }

            Remove(walk);
            yield return null;
            _player.Dummy.AutoAnimate = false;
            _player.Sprite.Play("lookUp");
        }

        private IEnumerator PanCameraY(float to, float speed)
        {
            float from = Level.Camera.Y;
            for (float p = 0f; p < 1f; p += engine.DeltaTime * speed)
            {
                Level.Camera.Y = from + ((to - from) *
                                         ((Celeste.Core.Mathematics.Easing.ISourceEasingEnvironment)engine)
                                         .EasingArithmetic.CubeInOut(p));
                yield return null;
            }
        }

        public override void OnEnd(OriginalLevelSceneRoot level)
        {
            _player.Machine.State = 0;
            _player.Dummy.AutoAnimate = true;
            _player.Speed = Vector2.Zero;
            _player.Data.Facing = -1;
            level.Camera.Position = _player.CameraTarget;
        }
    }
}
