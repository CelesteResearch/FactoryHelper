using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.FactoryHelper.Entities.Mechanisms.PressurePlates
{
    public partial class PressurePlate
    {
        [SceneTracked(false)]
        [RuntimeTypeAlias("FactoryHelper.Entities.PressurePlate+Button")]
        public class Button : SceneEntityState
        {
            public SpriteImage Image { get; private set; }

            public Button(ISourceEngineContext engine, Vector2 position)
                : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
            {
                Add(Image = new SpriteImage(((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                    "objects/FactoryHelper/pressurePlate/plate_button0"]));
                Collider = new HitboxColliderGeometry(14, 7, 1, 6);
                Image.Position.X -= 2;
                Image.Position.Y -= 2;
            }
        }
    }
}
