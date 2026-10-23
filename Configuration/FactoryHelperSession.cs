// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Maps;
using Celeste.Everest.Modules.Persistence;
using Celeste.Simulation.Runtime.Session;

namespace Celeste.Mods.FactoryHelper.Configuration
{
    public class FactoryHelperSession : EverestModuleSession
    {
        public HashSet<MapEntityId> Batteries = new();
        public HashSet<MapEntityId> PermanentlyRemovedActivatorDashBlocks = new();
        public Vector2? SpecialBoxPosition;
        public ChapterCompletionSession OriginalSession;
        public string SpecialBoxLevel;
    }
}
