using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Content;

namespace Celeste.Mods.FactoryHelper.Runtime.Graphics
{
    public sealed class FactorySpriteBank
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, FactorySpriteBank> banks = new();
        private readonly ISourceEngineContext engine;
        public SpriteBank Sprites { get; private set; }
        private FactorySpriteBank(ISourceEngineContext engine) => this.engine = engine;
        public static FactorySpriteBank For(ISourceEngineContext engine) => banks.GetValue(engine, value => new(value));

        public void LoadContent()
        {
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry
                   .Get("Graphics/FactoryHelper/Sprites").Stream) xml.Load(stream);
            Sprites = new SpriteBank(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, xml);
        }

        public static void Clear(ISourceEngineContext engine) => banks.Remove(engine);
    }
}
