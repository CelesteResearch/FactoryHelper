// SPDX-License-Identifier: MIT

using System.Collections;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Mods.FactoryHelper.Components.Activation;
using Celeste.Mods.FactoryHelper.Components.Steam;

namespace Celeste.Mods.FactoryHelper.Entities.Lighting
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("FactoryHelper.Entities.RustyLamp")]
    [CustomEntity("FactoryHelper/RustyLamp")]
    public sealed class RustyLamp : SceneEntityState
    {
        public static readonly PackedColor Color = new(255, 210, 127);
        private readonly ISourceEngineContext engine;

        private readonly Sprite sprite;
        private readonly VertexLightState light;
        private readonly BloomPointState bloom;
        private readonly string strobePatternString;
        private float initialDelay;
        private SimulationCoroutine strobePattern;
        private bool startedOn;

        public RustyLamp(ISourceEngineContext engine, Vector2 position, Vector2 offset, string activationId,
            float initialDelay, string strobePattern, bool startActive)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position + offset)
        {
            this.engine = engine;
            Depth = 8500;
            Position = position + offset;

            Collider = new HitboxColliderGeometry(12, 12, 2, 2);

            Add(Activator = new FactoryActivator());
            Activator.ActivationId = activationId == string.Empty ? null : activationId;
            Activator.StartOn = startActive;
            Activator.OnTurnOff = OnTurnOff;
            Activator.OnTurnOn = OnTurnOn;
            Activator.OnStartOff = OnStartOff;
            Activator.OnStartOn = OnStartOn;

            Add(new SteamCollider(OnSteamWall));

            this.initialDelay = initialDelay;
            Add(sprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "objects/FactoryHelper/rustyLamp/rustyLamp"));
            sprite.Add("frames", "");
            sprite.Play("frames");
            sprite.Active = false;

            strobePatternString = strobePattern;

            Add(light = new VertexLightState(engine, Color, 0f, 128, 128));
            Add(bloom = new BloomPointState(0.0f, 16f));
            light.Position = new Vector2(8, 8);
            bloom.Position = new Vector2(8, 8);
        }

        public RustyLamp(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position, offset, data.Attr("activationId", ""), data.Float("initialDelay", 0f),
                data.Attr("strobePattern", "None"), data.Bool("startActive", false))
        {
        }

        public FactoryActivator Activator { get; }

        public override void Update()
        {
            base.Update();
            if (Activator.IsOn)
            {
                if (initialDelay > 0f)
                {
                    initialDelay -= engine.DeltaTime;
                }
            }
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            Activator.HandleStartup(scene);
        }

        private void OnSteamWall(ISourceEntityCollider steamWall)
        {
            Activator.ForceDeactivate();
        }

        private void OnStartOn()
        {
            startedOn = true;
            initialDelay = 0f;
            TurnOn();
            SetLightLevel(1f);
            SetStrobePattern(strobePatternString);
        }

        private void OnStartOff()
        {
            startedOn = false;
            SetLightLevel(0f);
        }

        private void OnTurnOn()
        {
            SetStrobePattern(strobePatternString);
        }

        private void OnTurnOff()
        {
            SetStrobePattern("FlickerOff");
            startedOn = false;
        }

        private void SetStrobePattern(string pattern)
        {
            if (strobePattern != null)
            {
                Remove(strobePattern);
            }

            switch (pattern)
            {
                default:
                case "None":
                    Add(strobePattern = new SimulationCoroutine(engine, PatternNone()));
                    break;
                case "FlickerOn":
                    Add(strobePattern = new SimulationCoroutine(engine, PatternFlickerOn()));
                    break;
                case "FlickerOff":
                    Add(strobePattern = new SimulationCoroutine(engine, FlickerOff()));
                    break;
                case "LightFlicker":
                    Add(strobePattern = new SimulationCoroutine(engine, PatternLightFlicker()));
                    break;
                case "TurnOffFlickerOn":
                    Add(strobePattern = new SimulationCoroutine(engine, PatternTurnOffFlickerOn()));
                    break;
            }
        }

        private IEnumerator WaitForActivation()
        {
            while (!Activator.IsOn || (initialDelay > 0f))
            {
                yield return null;
            }
        }

        private IEnumerator PatternNone()
        {
            yield return WaitForActivation();
            TurnOn();
        }

        private IEnumerator PatternFlickerOn()
        {
            yield return WaitForActivation();
            if (!startedOn)
            {
                yield return FlickerOn();
            }
        }

        private IEnumerator PatternLightFlicker()
        {
            yield return WaitForActivation();
            bool firstRun = true;
            for (;;)
            {
                if (!startedOn && firstRun)
                {
                    yield return FlickerOn();
                    firstRun = false;
                }

                yield return SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 10f) + 2f;
                int flickerCount = ((ISourceRandomEnvironment)engine).Random.Next(3, 7);
                float flickerLength = SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 0.02f) +
                                      0.01f;
                for (int i = 0; i < flickerCount; i++)
                {
                    SetLightLevel(0.9f);
                    yield return flickerLength;
                    SetLightLevel(1.0f);
                    yield return flickerLength;
                }
            }
        }

        private IEnumerator PatternTurnOffFlickerOn()
        {
            yield return WaitForActivation();
            bool firstRun = true;
            for (;;)
            {
                if (!startedOn && firstRun)
                {
                    yield return FlickerOn();
                    firstRun = false;
                }

                yield return SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 5f) + 10f;
                SetLightLevel(0.5f);
                yield return 0.2f;
                TurnOff();
                yield return SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 2f) + 0.5f;
                yield return FlickerOn();
            }
        }

        private IEnumerator FlickerOn()
        {
            TurnOn();
            SetLightLevel(0.0f);
            int flickerCount = ((ISourceRandomEnvironment)engine).Random.Next(3, 7);
            float flickerLength = SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 0.02f) + 0.01f;
            for (int i = 0; i < flickerCount; i++)
            {
                SetLightLevel(0.5f * (i + 1) / flickerCount);
                yield return flickerLength;
                SetLightLevel(1.0f * (i + 1) / flickerCount);
                yield return flickerLength;
            }
        }

        private IEnumerator FlickerOff()
        {
            TurnOn();
            SetLightLevel(1.0f);
            int flickerCount = ((ISourceRandomEnvironment)engine).Random.Next(3, 7);
            float flickerLength = SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, 0.02f) + 0.01f;
            for (int i = 0; i < flickerCount; i++)
            {
                SetLightLevel(0.5f * (1 - ((i + 1) / flickerCount)));
                yield return flickerLength;
                SetLightLevel(1.0f * (1 - ((i + 1) / flickerCount)));
                yield return flickerLength;
            }

            TurnOff();
        }

        private void TurnOn()
        {
            sprite.SetAnimationFrame(1);
            light.Visible = true;
            bloom.Visible = true;
        }

        private void TurnOff()
        {
            sprite.SetAnimationFrame(0);
            light.Visible = false;
            bloom.Visible = false;
        }

        private void SetLightLevel(float dimnes)
        {
            light.Alpha = dimnes;
            bloom.Alpha = dimnes / 2;
        }
    }
}
