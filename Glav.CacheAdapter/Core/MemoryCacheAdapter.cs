using System;
using System.Collections.Generic;
using System.Reflection;
using Glav.CacheAdapter.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Glav.CacheAdapter.Core
{
    /// <summary>
    /// In memory cache with no dependencies on the web cache, only runtime dependencies.
    /// ie. Can be used in any type of application, desktop, web, service or otherwise.
    /// </summary>
    internal class MemoryCacheAdapter : ICache
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheAdapter> _logger;
        private readonly PerRequestCacheHelper _requestCacheHelper = new PerRequestCacheHelper();

        public MemoryCacheAdapter(ILogger<MemoryCacheAdapter> logger, IMemoryCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public void Add(string cacheKey, DateTime expiry, object dataToAdd)
        {
            var policy = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = new DateTimeOffset(expiry)
            };

            if (dataToAdd != null)
            {
                _cache.Set(cacheKey, dataToAdd, policy);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Adding data to cache with cache key: {cacheKey}, expiry date {expiryDate}",
                        cacheKey, expiry.ToString("yyyy/MM/dd hh:mm:ss"));
                }
            }
        }

        public T Get<T>(string cacheKey) where T : class
        {
            // try per request cache first, but only if in a web context
            var requestCacheData = _requestCacheHelper.TryGetItemFromPerRequestCache<T>(cacheKey);
            if (requestCacheData != null)
            {
                return requestCacheData;
            }

            T data = _cache.Get(cacheKey) as T;
            return data;
        }

        public void InvalidateCacheItem(string cacheKey)
        {
            if (_cache.TryGetValue(cacheKey, out object _))
            {
                _cache.Remove(cacheKey);
            }
        }

        public void InvalidateCacheItems(IEnumerable<string> cacheKeys)
        {
            if (cacheKeys == null)
            {
                return;
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Invalidating a series of cache keys");
            }

            foreach (var cacheKey in cacheKeys)
            {
                _cache.Remove(cacheKey);
            }
        }

        public void Add(string cacheKey, TimeSpan slidingExpiryWindow, object dataToAdd)
        {
            if (dataToAdd != null)
            {
                var policy = new MemoryCacheEntryOptions() { SlidingExpiration = slidingExpiryWindow };
                _cache.Set(cacheKey, dataToAdd, policy);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Adding data to cache with cache key: {cacheKey}, sliding expiry window: {slidingExpiry} seconds",
                        cacheKey, slidingExpiryWindow.TotalSeconds);
                }
            }
        }

        public void AddToPerRequestCache(string cacheKey, object dataToAdd)
        {
            _requestCacheHelper.AddToPerRequestCache(cacheKey, dataToAdd);
        }

        public CacheSetting CacheType => CacheSetting.Memory;

        public void ClearAll()
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Clearing the cache");
            }

            // from https://stackoverflow.com/questions/34406737/how-to-remove-all-objects-reset-from-imemorycache-in-asp-net-core
            if (_cache is MemoryCache memCache)
            {
                memCache.Compact(1.0);
                return;
            }
            else
            {
                MethodInfo clearMethod = _cache.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
                if (clearMethod != null)
                {
                    clearMethod.Invoke(_cache, null);
                    return;
                }
                else
                {
                    PropertyInfo prop = _cache.GetType().GetProperty("EntriesCollection", BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Public);
                    if (prop != null)
                    {
                        object innerCache = prop.GetValue(_cache);
                        if (innerCache != null)
                        {
                            clearMethod = innerCache.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
                            if (clearMethod != null)
                            {
                                clearMethod.Invoke(innerCache, null);
                                return;
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException("Unable to clear memory cache instance of type " +
                _cache.GetType().FullName);
        }
    }
}
