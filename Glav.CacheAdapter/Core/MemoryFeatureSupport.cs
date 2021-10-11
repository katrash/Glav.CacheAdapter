using Glav.CacheAdapter.Features;

namespace Glav.CacheAdapter.Core
{
    internal class MemoryFeatureSupport : ICacheFeatureSupport
    {
        public bool SupportsClearingCacheContents()
        {
            return true;
        }

    }
}
