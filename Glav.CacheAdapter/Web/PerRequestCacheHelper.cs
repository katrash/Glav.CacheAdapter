namespace Glav.CacheAdapter.Web
{
    internal class PerRequestCacheHelper
    {
        public void AddToPerRequestCache(string cacheKey, object dataToAdd)
        {
#if USE_SYSTEM_WEB
            // If not in a web context, do nothing
            if (InWebContext())
            {
                if (System.Web.HttpContext.Current.Items.Contains(cacheKey))
                {
                    System.Web.HttpContext.Current.Items.Remove(cacheKey);
                }
                System.Web.HttpContext.Current.Items.Add(cacheKey, dataToAdd);
            }
#endif
        }

        public T TryGetItemFromPerRequestCache<T>(string cacheKey) where T : class
        {
#if USE_SYSTEM_WEB
            // try per request cache first, but only if in a web context
            if (InWebContext())
            {
                if (System.Web.HttpContext.Current.Items.Contains(cacheKey))
                {
                    var data = System.Web.HttpContext.Current.Items[cacheKey];
                    var realData = data as T;
                    if (realData != null)
                    {
                        return realData;
                    }
                }
            }

#endif
            return default;
        }

        private static bool InWebContext()
        {
#if USE_SYSTEM_WEB
            return System.Web.HttpContext.Current != null;
#else
            return false;
#endif
        }
    }
}
