using System;
using System.Collections.Generic;
using System.Linq;

using Glav.CacheAdapter.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glav.CacheAdapter.DependencyManagement
{
    /// <summary>
    /// A generic cache dependency mechanism that utilizes no specific features
    /// of any cache engine and acts as overall support of rudimentary cache dependencies
    /// in light of the fact cache engines may not support advanced queries, dependencies
    /// and events
    /// </summary>
    internal class GenericDependencyManager : BaseCacheDependencyManager
    {
        public const string CacheKeyPrefix = "__DepMgr_"; // The root cache key prefix we use
        public const string CacheDependencyEntryPrefix = "DepEntry_"; // The additional prefix for master/child cache key dependency entries

        public GenericDependencyManager(ICache cache,
            ILogger<GenericDependencyManager> logger, IOptions<CacheConfig> config)
            : base(cache, logger, config)
        {
        }

        /// <summary>
        /// Associate the dependent cache keys to their parent or master-key so that when the parent is cleared, a list of dependent keys can also be cleared.
        /// IMPORTANT NOTE!!: This method is not thread safe, especially across a distributed system. If this is called concurrently by 2 different threads or processes
        /// and executes at the same time, there is a chance that the parent gets registered at the time meaning the last registration will work and one child/dependent cache
        /// key may not get associated with the parent key.
        /// </summary>
        /// <param name="parentKey"></param>
        /// <param name="dependentCacheKeys"></param>
        /// <param name="actionToPerform"></param>
        public override void AssociateDependentKeysToParent(string parentKey, IEnumerable<string> dependentCacheKeys, CacheDependencyAction actionToPerform = CacheDependencyAction.ClearDependentItems)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Associating list of cache keys to parent key:[{parentKey}]", parentKey);
            }

            var cacheKeyForDependency = GetParentItemCacheKey(parentKey);
            var currentDependencyItems = Cache.Get<DependencyItem[]>(cacheKeyForDependency);
            var tempList = new List<DependencyItem>();

            if (currentDependencyItems != null && currentDependencyItems.Length > 0)
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Found cache key dependency list for parent key:[{parentKey}]", parentKey);
                }

                tempList.AddRange(currentDependencyItems);
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("No dependency items were found for parent key [{parentKey}].", parentKey);
                }

                RegisterParentDependencyDefinition(parentKey, actionToPerform);
                var items = Cache.Get<DependencyItem[]>(cacheKeyForDependency);
                if (items != null)
                {
                    tempList.AddRange(items);
                }
            }

            var keysList = new List<string>(dependentCacheKeys);
            keysList.ForEach(d =>
            {
                if (tempList.All(c => c.CacheKey != d))
                {
                    tempList.Add(new DependencyItem { CacheKey = d, Action = actionToPerform });
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("Associating cache key [{cacheKey}] to its dependent parent key:[{parentKey}]", d, parentKey);
                    }
                }
            });
            Cache.InvalidateCacheItem(cacheKeyForDependency);
            Cache.Add(cacheKeyForDependency, GetMaxAge(), tempList.ToArray());
        }

        public override IEnumerable<DependencyItem> GetDependentCacheKeysForParent(string parentKey, bool includeParentNode = false)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Retrieving associated cache key dependency list parent key:[{parentKey}]", parentKey);
            }

            var cacheKeyForDependency = GetParentItemCacheKey(parentKey);
            var keyList = Cache.Get<DependencyItem[]>(cacheKeyForDependency);
            if (keyList == null)
            {
                RegisterParentDependencyDefinition(parentKey);
                return FilterDependencyListForParentNode(Cache.Get<DependencyItem[]>(cacheKeyForDependency), includeParentNode);
            }

            return FilterDependencyListForParentNode(keyList, includeParentNode);
        }

        private static IEnumerable<DependencyItem> FilterDependencyListForParentNode(DependencyItem[] dependencyList, bool includeParentNode)
        {
            var depList = new List<DependencyItem>();
            if (dependencyList != null)
            {
                depList.AddRange(dependencyList);
            }

            if (!includeParentNode)
            {
                var item = depList.FirstOrDefault(d => d.IsParentNode);
                if (item != null)
                {
                    depList.Remove(item);
                }
            }
            return depList.ToArray();
        }

        public override void RegisterParentDependencyDefinition(string parentKey, CacheDependencyAction actionToPerform = CacheDependencyAction.ClearDependentItems)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Registering parent item:[{parentKey}]", parentKey);
            }

            var cacheKeyForParent = GetParentItemCacheKey(parentKey);
            var item = new DependencyItem { CacheKey = parentKey, Action = actionToPerform, IsParentNode = true };
            var depList = new[] { item };
            Cache.InvalidateCacheItem(cacheKeyForParent);
            Cache.Add(cacheKeyForParent, GetMaxAge(), depList);
        }

        public override void RemoveParentDependencyDefinition(string parentKey)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Removing parent key:[{parentKey}]", parentKey);
            }

            var cacheKeyForParent = GetParentItemCacheKey(parentKey);
            Cache.InvalidateCacheItem(cacheKeyForParent);
        }

        public override string Name => "Generic/Default";

        private static DateTime GetMaxAge()
        {
            return DateTime.Now.AddYears(10);
        }

        private static string GetParentItemCacheKey(string parentKey)
        {
            var cacheKeyForParent = $"{CacheKeyPrefix}{CacheDependencyEntryPrefix}{parentKey}";
            return cacheKeyForParent;

        }
    }
}
