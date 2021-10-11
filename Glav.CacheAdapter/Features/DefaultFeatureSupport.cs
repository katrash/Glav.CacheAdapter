namespace Glav.CacheAdapter.Features
{
    internal class DefaultFeatureSupport : ICacheFeatureSupport
    {

        public bool SupportsClearingCacheContents()
        {
            return false;
        }

    }
}
