using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Glav.CacheAdapter.DependencyManagement;
using Glav.CacheAdapter.Features;
using Glav.CacheAdapter.Helpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glav.CacheAdapter.Core
{
    /// <summary>
    /// This class acts as a cache provider that will attempt to retrieve items from a cache, and if they do not exist,
    /// execute the passed in delegate to perform a data retrieval, then place the item into the cache before returning it.
    /// Subsequent accesses will get the data from the cache until it expires.
    /// </summary>
    internal class CacheProvider : ICacheProvider
    {
        private readonly ILogger<CacheProvider> _logger;

        public CacheProvider(ICache cache, ILogger<CacheProvider> logger, IOptions<CacheConfig> config,
            ICacheDependencyManager cacheDependencyManager, ICacheFeatureSupport featureSupport)
        {
            InnerCache = cache;
            _logger = logger;
            FeatureSupport = featureSupport;
            CacheConfiguration = config.Value;
            InnerDependencyManager = cacheDependencyManager;

            if (CacheConfiguration.IsCacheDependencyManagementEnabled && InnerDependencyManager != null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("CacheKey dependency management enabled, using {ManagerName}.", InnerDependencyManager.Name);
                }
            }
            else
            {
                InnerDependencyManager = null;  // Dependency Management is disabled
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("CacheKey dependency management not enabled.");
                }
            }
        }

        public ICache InnerCache { get; }

        public CacheConfig CacheConfiguration { get; }

        public T Get<T>(string cacheKey, DateTime expiryDate, Func<T> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return GetAndAddIfNecessary(cacheKey,
                data =>
                {
                    if (CacheConfiguration.IsCacheEnabled && data != null)
                    {
                        InnerCache.Add(cacheKey, expiryDate, data);
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Adding item [{cacheKey}] to cache with expiry date/time of [{expiryDate}].", cacheKey,
                                expiryDate.ToString("dd/MM/yyyy hh:mm:ss"));
                        }
                    }
                },
                getData,
                parentKey,
                actionForDependency
                );
        }

        public T Get<T>(string cacheKey, TimeSpan slidingExpiryWindow, Func<T> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return GetAndAddIfNecessary(cacheKey,
                data =>
                {
                    if (CacheConfiguration.IsCacheEnabled && data != null)
                    {
                        InnerCache.Add(cacheKey, slidingExpiryWindow, data);
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Adding item [{cacheKey}] to cache with sliding sliding expiry window in seconds [{slidingSec}].", cacheKey,
                                slidingExpiryWindow.TotalSeconds);
                        }
                    }
                },
                getData,
                parentKey,
                actionForDependency
                );
        }

        private T GetAndAddIfNecessary<T>(string cacheKey, Action<T> addData, Func<T> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            if (!CacheConfiguration.IsCacheEnabled)
            {
                return getData();
            }

            //Get data from cache
            T data = InnerCache.Get<T>(cacheKey);

            // check to see if we need to get data from the source
            if (data == null)
            {
                //get data from source
                data = getData();

                //only add non null data to the cache.
                if (data != null)
                {
                    addData(data);
                    ManageCacheDependenciesForCacheItem(data, cacheKey, parentKey, actionForDependency);
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Retrieving item [{cacheKey}] from cache.", cacheKey);
                }
            }

            return data;
        }

        public void InvalidateCacheItems(IEnumerable<string> cacheKeys)
        {
            if (cacheKeys == null)
            {
                return;
            }

            if (!CacheConfiguration.IsCacheEnabled)
            {
                return;
            }

            var distinctKeys = cacheKeys.Distinct();

            if (InnerDependencyManager == null)
            {
                InnerCache.InvalidateCacheItems(distinctKeys);
                return;
            }

            IEnumerable<string> allKeys = distinctKeys as string[] ?? distinctKeys.ToArray();
            foreach (var cacheKey in allKeys)
            {
                if (InnerDependencyManager.IsOkToActOnDependencyKeysForParent(cacheKey))
                {
                    try
                    {
                        InnerDependencyManager.PerformActionForDependenciesAssociatedWithParent(cacheKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error when trying to invalidate dependencies for [{cacheKey}]", cacheKey);
                    }
                }
            }

            try
            {
                InnerCache.InvalidateCacheItems(allKeys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when trying to invalidate a series of cache keys");
            }
        }

        public void InvalidateCacheItem(string cacheKey)
        {
            if (!CacheConfiguration.IsCacheEnabled)
            {
                return;
            }

            if (InnerDependencyManager == null)
            {
                InnerCache.InvalidateCacheItem(cacheKey);
                return;
            }

            if (InnerDependencyManager.IsOkToActOnDependencyKeysForParent(cacheKey))
            {
                try
                {
                    InnerDependencyManager.PerformActionForDependenciesAssociatedWithParent(cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when trying to invalidate dependencies for [{cacheKey}]", cacheKey);
                }
            }
            InnerCache.InvalidateCacheItem(cacheKey);
        }

        public void Add(string cacheKey, DateTime absoluteExpiryDate, object dataToAdd, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems)
        {
            if (CacheConfiguration.IsCacheEnabled)
            {
                InnerCache.Add(cacheKey, absoluteExpiryDate, dataToAdd);
                ManageCacheDependenciesForCacheItem(dataToAdd, cacheKey, parentKey, actionForDependency);
            }
        }

        public void Add(string cacheKey, TimeSpan slidingExpiryWindow, object dataToAdd, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems)
        {
            if (CacheConfiguration.IsCacheEnabled)
            {
                InnerCache.Add(cacheKey, slidingExpiryWindow, dataToAdd);
                ManageCacheDependenciesForCacheItem(dataToAdd, cacheKey, parentKey, actionForDependency);
            }
        }

        public void AddToPerRequestCache(string cacheKey, object dataToAdd)
        {
            if (CacheConfiguration.IsCacheEnabled)
            {
                InnerCache.AddToPerRequestCache(cacheKey, dataToAdd);
            }
        }

        public T Get<T>(DateTime absoluteExpiryDate, Func<T> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return Get(getData.GetCacheKey(), absoluteExpiryDate, getData, parentKey, actionForDependency);
        }

        public T Get<T>(TimeSpan slidingExpiryWindow, Func<T> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return Get(getData.GetCacheKey(), slidingExpiryWindow, getData, parentKey, actionForDependency);
        }

        public ICacheDependencyManager InnerDependencyManager { get; }

        private void ManageCacheDependenciesForCacheItem(object dataToAdd, string cacheKey, string parentKey, CacheDependencyAction action)
        {
            if (InnerDependencyManager == null)
            {
                return;
            }
            if (InnerDependencyManager.IsOkToActOnDependencyKeysForParent(parentKey) && dataToAdd != null)
            {
                InnerDependencyManager.AssociateDependentKeysToParent(parentKey, new[] { cacheKey }, action);
            }

        }

        public void InvalidateDependenciesForParent(string parentKey)
        {
            if (InnerDependencyManager == null)
            {
                return;
            }
            InnerDependencyManager.ForceActionForDependenciesAssociatedWithParent(parentKey, CacheDependencyAction.ClearDependentItems);
        }

        public void ClearAll()
        {
            InnerCache.ClearAll();
        }

        public ICacheFeatureSupport FeatureSupport { get; }

        public Task<T> GetAsync<T>(string cacheKey, DateTime expiryDate, Func<Task<T>> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return GetAndAddIfNecessaryAsync(cacheKey,
                data =>
                {
                    if (CacheConfiguration.IsCacheEnabled && data != null)
                    {
                        InnerCache.Add(cacheKey, expiryDate, data);
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Adding item [{cacheKey}] to cache with expiry date/time of [{expiryDate}].", cacheKey,
                                expiryDate.ToString("dd/MM/yyyy hh:mm:ss"));
                        }
                    }
                },
                getData,
                parentKey,
                actionForDependency
                );
        }

        public Task<T> GetAsync<T>(string cacheKey, TimeSpan slidingExpiryWindow, Func<Task<T>> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return GetAndAddIfNecessaryAsync(cacheKey,
                data =>
                {
                    if (CacheConfiguration.IsCacheEnabled && data != null)
                    {
                        InnerCache.Add(cacheKey, slidingExpiryWindow, data);
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Adding item [{cacheKey}] to cache with sliding sliding expiry window in seconds [{slidingSec}].", cacheKey,
                                slidingExpiryWindow.TotalSeconds);
                        }
                    }
                },
                getData,
                parentKey,
                actionForDependency
                );
        }

        public Task<T> GetAsync<T>(DateTime absoluteExpiryDate, Func<Task<T>> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return GetAsync(getData.GetCacheKey(), absoluteExpiryDate, getData, parentKey, actionForDependency);
        }

        public Task<T> GetAsync<T>(TimeSpan slidingExpiryWindow, Func<Task<T>> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            return GetAsync(getData.GetCacheKey(), slidingExpiryWindow, getData, parentKey, actionForDependency);
        }

        private async Task<T> GetAndAddIfNecessaryAsync<T>(string cacheKey, Action<T> addData, Func<Task<T>> getData, string parentKey = null, CacheDependencyAction actionForDependency = CacheDependencyAction.ClearDependentItems) where T : class
        {
            if (getData == null)
            {
                throw new ArgumentNullException(nameof(getData));
            }

            //Get data from cache
            T data = CacheConfiguration.IsCacheEnabled ? InnerCache.Get<T>(cacheKey) : null;

            // check to see if we need to get data from the source
            if (data == null)
            {
                //get data from source
                try
                {
                    data = await getData().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in getData()");
                }

                //only add non null data to the cache.
                if (CacheConfiguration.IsCacheEnabled && data != null)
                {
                    addData(data);
                    ManageCacheDependenciesForCacheItem(data, cacheKey, parentKey, actionForDependency);
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Retrieving item [{cacheKey}] from cache.", cacheKey);
                }
            }

            return data;
        }
    }
}
