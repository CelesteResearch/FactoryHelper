// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Enemies.BadelineBoss.Projectiles;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering.Vertices;
using Celeste.Scene.Entities;

namespace Celeste.Mods.FactoryHelper.Entities.DashNegators
{
    [RuntimeTypeAlias("FactoryHelper.Entities.DashNegatorBeam")]
    public sealed class DashNegatorBeam : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly Sprite beamSprite, beamStartSprite;
        private readonly SourceVertexPositionColor[] fade = new SourceVertexPositionColor[24];
        private readonly float angle = Vector2Geometry.Angle(Vector2.UnitY);
        private readonly Vector2 origin;
        private float beamAlpha;
        private float sideFadeAlpha = 1f;

        public DashNegatorBeam(ISourceEngineContext engine, Vector2 origin)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            this.origin = origin;
            var factory = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.SpriteFactory;
            Add(beamSprite = factory.Create("badeline_beam"));
            Add(beamStartSprite = factory.Create("badeline_beam_start"));
            beamSprite.OnLastFrame = animation =>
            {
                if (animation == "shoot") RemoveSelf();
            };
            Depth = -1000000;
            beamSprite.Play("shoot");
            beamStartSprite.Play("shoot");
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            DissipateParticles();
        }

        public override void Update()
        {
            base.Update();
            var extrema = ((ISourceExtremaEnvironment)engine).ExtremaArithmetic;
            beamAlpha = ScalarMath.Approach(beamAlpha, 1f, 16f * engine.DeltaTime, extrema);
            sideFadeAlpha = ScalarMath.Approach(sideFadeAlpha, 0f, engine.DeltaTime * 8f, extrema);
        }

        public override void Render(ISourceRenderContext context)
            => FinalBossBeamDrawing.Render(new BeamDrawingState(this), context);

        private void DissipateParticles()
        {
            var level = (OriginalLevelSceneRoot)Scene;
            var arithmetic = ((ISourceVectorEnvironment)engine).VectorArithmetic;
            Vector2 source = origin + Vector2Geometry.AngleToVector(angle, 8f);
            Vector2 from = Vector2Geometry.AngleToVector(angle, 12f);
            Vector2 to = Vector2Geometry.AngleToVector(angle, 2000f);
            Vector2 perpendicular = arithmetic.SafeNormalize(Vector2Geometry.Perpendicular(to - from));
            Vector2 along = arithmetic.SafeNormalize(to - from);
            FinalBossBeamParticles.Emit(new BeamParticleState(this, level), source, along, perpendicular);
        }

        private readonly struct BeamDrawingState : IFinalBossBeamDrawingState
        {
            private readonly DashNegatorBeam owner;
            public BeamDrawingState(DashNegatorBeam owner) => this.owner = owner;
            public ISourceEngineContext EngineContext => owner.engine;
            public object Scene => owner.Scene;
            public Vector2 BeamOrigin => owner.origin;
            public float Angle => owner.angle;
            public Sprite BeamSprite => owner.beamSprite;
            public Sprite StartSprite => owner.beamStartSprite;
            public float BeamAlpha => owner.beamAlpha;
            public float SideFadeAlpha => owner.sideFadeAlpha;
            public SourceVertexPositionColor[] Fade => owner.fade;

            public ISourceVectorArithmetic VectorArithmetic =>
                ((ISourceVectorEnvironment)owner.engine).VectorArithmetic;
        }

        private readonly struct BeamParticleState : IFinalBossBeamParticleState
        {
            private readonly DashNegatorBeam owner;
            private readonly OriginalLevelSceneRoot level;

            public BeamParticleState(DashNegatorBeam owner, OriginalLevelSceneRoot level)
            {
                this.owner = owner;
                this.level = level;
            }

            public Celeste.Simulation.Presentation.Particles.ParticleSystemPresentation ParticleSystem =>
                level.ParticlesFG;

            public Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation ParticleType =>
                ((IOriginalGameplayProcess)owner.engine).ParticleTypes.FinalBossBeam.P_Dissipate;

            public ISourceRandom Random => ((ISourceRandomEnvironment)owner.engine).Random;
        }
    }
}
