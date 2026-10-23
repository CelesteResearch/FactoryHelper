// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;

namespace Celeste.Mods.FactoryHelper.Components.Steam
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("FactoryHelper.Components.SteamCollider")]
    public sealed class SteamCollider : SourceComponentState
    {
        public Action<ISourceEntityCollider> OnCollide;
        public ColliderGeometry Collider;
        public bool OneShot;

        public SteamCollider(Action<ISourceEntityCollider> onCollide, bool oneShot = true)
            : base(false, false)
        {
            OnCollide = onCollide;
            Collider = null;
            OneShot = oneShot;
        }

        public bool ShouldDoChecks { get; private set; } = true;

        public void Check(ISourceEntityCollider steamWall)
        {
            if (OnCollide != null)
            {
                var entity = (SceneEntityState)Entity;
                ColliderGeometry collider = entity.Collider;
                if (Collider != null)
                {
                    entity.Collider = Collider;
                }

                if (SourceEntityCollision.Check(steamWall, entity))
                {
                    OnCollide(steamWall);
                    if (OneShot)
                    {
                        ShouldDoChecks = false;
                    }
                }

                entity.Collider = collider;
            }
        }
    }
}
