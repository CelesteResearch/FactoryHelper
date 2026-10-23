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
    public partial class ElectrifiedWall
    {
        private static void DrawSimpleLightning(ref int index, ref SourceVertexPositionColor[] verts, uint seed,
            Vector2 pos, Vector2 a, Vector2 b, PackedColor color, float thickness = 1f)
        {
            seed = (uint)((int)seed + SourceSingleHashMath.GetVector2HashCodeInstalled(a) +
                          SourceSingleHashMath.GetVector2HashCodeInstalled(b));
            a += pos;
            b += pos;
            float num = (b - a).Length();
            Vector2 vector = (b - a) / num;
            Vector2 vector2 = new Vector2(-vector.Y, vector.X);
            a += vector2;
            b += vector2;
            Vector2 vector3 = a;
            int num2 = (PseudoRand(ref seed) % 2u != 0) ? 1 : (-1);
            float num3 = PseudoRandRange(ref seed, 0f, (float)Math.PI * 2f);
            float num4 = 0f;
            float num5 = index + ((((b - a).Length() / 4f) + 1f) * 6f);
            while (num5 >= verts.Length)
            {
                Array.Resize(ref verts, verts.Length * 2);
            }

            for (int i = index; i < num5; i++)
            {
                verts[i].Color = color;
            }

            do
            {
                float num6 = PseudoRandRange(ref seed, 0f, 4f);
                num3 += 0.1f;
                num4 += 4f + num6;
                Vector2 vector4 = a + (vector * num4);
                if (num4 < num)
                {
                    vector4 += (num2 * vector2 * num6) - vector2;
                }
                else
                {
                    vector4 = b;
                }

                verts[index++].Position = new Vector3(vector3 - (vector2 * thickness), 0f);
                verts[index++].Position = new Vector3(vector4 - (vector2 * thickness), 0f);
                verts[index++].Position = new Vector3(vector4 + (vector2 * thickness), 0f);
                verts[index++].Position = new Vector3(vector3 - (vector2 * thickness), 0f);
                verts[index++].Position = new Vector3(vector4 + (vector2 * thickness), 0f);
                verts[index++].Position = new Vector3(vector3, 0f);
                vector3 = vector4;
                num2 = -num2;
            } while (num4 < num);
        }

        private static uint PseudoRand(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            return seed;
        }

        public static float PseudoRandRange(ref uint seed, float min, float max)
        {
            return min + ((float)(double)(PseudoRand(ref seed) & 0x3FF) / 1024f * (max - min));
        }
    }
}
