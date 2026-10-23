// SPDX-License-Identifier: MIT

using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;

namespace Celeste.Mods.FactoryHelper.Modules
{
    public sealed class FactoryHelperModule(ISourceEngineContext engine) : SourceEverestModule(engine)
    {
        public override System.Type SessionType =>
            typeof(Celeste.Mods.FactoryHelper.Configuration.FactoryHelperSession);

        public override void LoadContent(bool firstLoad)
        {
            base.LoadContent(firstLoad);
            Celeste.Mods.FactoryHelper.Runtime.Graphics.FactorySpriteBank.For(SourceEngine).LoadContent();
        }

        public override void Load()
        {
            Celeste.Mods.FactoryHelper.Runtime.Holdables.ThrowBoxRuntime.For(SourceEngine).Load(this);
        }

        public override void Unload()
        {
            Celeste.Mods.FactoryHelper.Runtime.Holdables.ThrowBoxRuntime.For(SourceEngine).Dispose();
            Celeste.Mods.FactoryHelper.Runtime.Graphics.FactorySpriteBank.Clear(SourceEngine);
        }
    }
}
