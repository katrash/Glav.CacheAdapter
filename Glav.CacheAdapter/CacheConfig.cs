namespace Glav.CacheAdapter
{
    public class CacheConfig
    {
        public string CacheToUse { get; set; }

        public bool IsCacheEnabled { get; set; }

        /// <summary>
        /// Enables support of cache dependencies using parent and child/associated cache keys
        /// </summary>
        /// <remarks>This can require extra calls to the cache engine and so can incur a
        /// performance degradation due to extra network cache calls</remarks>
        public bool IsCacheDependencyManagementEnabled { get; set; }
    }
}
