// SPDX-License-Identifier: MIT

using Celeste.Mods.FactoryHelper.Entities.Conveyors;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Physics.Collision;
using System;

namespace Celeste.Mods.FactoryHelper.Components.Conveyors
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("FactoryHelper.Components.ConveyorMover")]
    public class ConveyorMover : SourceComponentState
    {
        public Action<float> OnMove;
        public bool IsOnConveyor = false;

        public ConveyorMover()
            : base(true, true)
        {
        }

        public void Move(float amount)
        {
            OnMove?.Invoke(amount);
        }

        public override void Update()
        {
            base.Update();
            bool foundConveyor = false;
            foreach (Conveyor conveyor in ((ISourceScene)((SceneEntityState)Entity).Scene).SourceScene.Entities
                     .GetTrackedEntities(typeof(Conveyor)))
            {
                if (!conveyor.IsBrokenDown && SourceEntityCollision.CheckAt(conveyor, (SceneEntityState)Entity,
                        conveyor.Position - Vector2.UnitY))
                {
                    foundConveyor = true;
                    Move(conveyor.IsMovingLeft ? -Conveyor.ConveyorMoveSpeed : Conveyor.ConveyorMoveSpeed);
                }
            }

            IsOnConveyor = foundConveyor;
        }
    }
}
