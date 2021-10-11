namespace Glav.CacheAdapter.Core
{
    internal static class CacheTypes
    {
        public static string Memory => CacheSetting.Memory.ToString().ToLowerInvariant();
        public static string Redis => CacheSetting.Redis.ToString().ToLowerInvariant();
    }
}
