using System;
using System.Collections.Generic;
using System.Linq;

using Glav.CacheAdapter.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glav.CacheAdapter.DependencyManagement
{
    internal abstract class BaseCacheDependencyManager : ICacheDependencyManager
    {
        protected BaseCacheDependencyManager(ICache cache, ILogger logger, IOptions<CacheConfig> config)
        {
            Cache = cache;
            Logger = logger;
            Config = config.Value;
        }

        public CacheConfig Config { get; }

        public ICache Cache { get; }

        public ILogger Logger { get; }

        public abstract void RegisterParentDependencyDefinition(string parentKey, CacheDependencyAction actionToPerform = CacheDependencyAction.ClearDependentItems);

        public abstract void RemoveParentDependencyDefinition(string parentKey);

        public abstract void AssociateDependentKeysToParent(string parentKey, IEnumerable<string> dependentCacheKeys, CacheDependencyAction actionToPerform = CacheDependencyAction.ClearDependentItems);

        public abstract IEnumerable<DependencyItem> GetDependentCacheKeysForParent(string parentKey, bool includeParentNode = false);

        public abstract string Name { get; }

        public virtual void PerformActionForDependenciesAssociatedWithParent(string parentKey)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Performing required actions on associated dependency cache keys for parent key:[{parentKey}]", parentKey);
            }

            ExecuteDefaultOrSuppliedActionForParentKeyDependencies(parentKey);
        }

        public virtual void ForceActionForDependenciesAssociatedWithParent(string parentKey, CacheDependencyAction forcedAction)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Forcing action:[{forceAction}] on dependency cache keys for parent key:[{parentKey}]", forcedAction, parentKey);
            }

            ExecuteDefaultOrSuppliedActionForParentKeyDependencies(parentKey, forcedAction);
        }

        protected virtual void ExecuteDefaultOrSuppliedActionForParentKeyDependencies(string parentKey, CacheDependencyAction? forcedAction = null)
        {
            if (!IsOkToActOnDependencyKeysForParent(parentKey))
            {
                return;
            }
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("executing action on dependency cache keys for parent key:[{parentKey}]", parentKey);
            }

            var alreadyProcessedKeys = new List<string>();

            var itemsToAction = GetCacheKeysToActionForParentKeyDependencies(parentKey, alreadyProcessedKeys);
            var itemsToClear = new List<string>();
            itemsToAction.ForEach(item =>
            {
                var cacheItemAction = item.Action;
                if (forcedAction.HasValue)
                {
                    cacheItemAction = forcedAction.Value;
                }
                switch (cacheItemAction)
                {
                    case CacheDependencyAction.ClearDependentItems:
                        itemsToClear.Add(item.CacheKey);
                        break;
                    default:
                        throw new NotSupportedException($"Action [{cacheItemAction}] not supported at this time");
                }
            });
            if (itemsToClear.Count > 0)
            {
                Cache.InvalidateCacheItems(itemsToClear);
            }
        }

        protected virtual List<DependencyItem> GetCacheKeysToActionForParentKeyDependencies(string parentKey, List<string> alreadyProcessedKeys = null)
        {
            var cacheKeysToAction = new List<DependencyItem>();

            if (!IsOkToActOnDependencyKeysForParent(parentKey))
            {
                return cacheKeysToAction;
            }

            if (alreadyProcessedKeys == null)
            {
                alreadyProcessedKeys = new List<string>();
            }

            var items = GetDependentCacheKeysForParent(parentKey);
            var dependencyItems = items as DependencyItem[] ?? items.ToArray();
            var numItems = items != null ? dependencyItems.Count() : 0;
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Number of dependencies found for master cache key [{parentKey}] is: {count}", parentKey, numItems);
            }

            if (numItems > 0)
            {
                foreach (var item in dependencyItems)
                {
                    // Dont allow recursion
                    if (item.CacheKey == parentKey)
                    {
                        continue;
                    }
                    if (alreadyProcessedKeys.Contains(item.CacheKey))
                    {
                        continue;
                    }
                    cacheKeysToAction.Add(item);
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("--> Child cache key [{cacheKey}] added for processing of parent key [{parentKey}]", item.CacheKey, parentKey);
                    }

                    alreadyProcessedKeys.Add(item.CacheKey);
                    cacheKeysToAction.AddRange(GetCacheKeysToActionForParentKeyDependencies(item.CacheKey, alreadyProcessedKeys));
                }
            }
            return cacheKeysToAction;
        }

        public bool IsOkToActOnDependencyKeysForParent(string parentKey)
        {
            if (!Config.IsCacheEnabled)
            {
                return false;
            }

            if (!Config.IsCacheDependencyManagementEnabled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parentKey))
            {
                return false;
            }
            return true;
        }
    }
}
