// SPDX-License-Identifier: MIT

using Celeste.Mods.IsaGrabBag.Runtime.ZipLines;

namespace Celeste.Mods.IsaGrabBag.Modules
{
    public sealed partial class IsaGrabBagModule
    {
        private ZipLineRuntime zipLines;

        private void LoadZipLines()
        {
            zipLines = ZipLineRuntime.For(SourceEngine);
            zipLines.Load();
        }

        private void UnloadZipLines()
        {
            zipLines?.Dispose();
            zipLines = null;
        }

        public override void LoadContent(bool firstLoad) => ZipLineRuntime.For(SourceEngine).LoadContent();
    }
}
