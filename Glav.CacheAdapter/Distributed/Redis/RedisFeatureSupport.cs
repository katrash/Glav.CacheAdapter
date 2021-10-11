using Glav.CacheAdapter.Features;

namespace Glav.CacheAdapter.Distributed.Redis
{
    internal class RedisFeatureSupport : ICacheFeatureSupport
    {
        public bool SupportsClearingCacheContents()
        {
            return true;
        }

    }
}
