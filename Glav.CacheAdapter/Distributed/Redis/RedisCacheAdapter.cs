using System;
using System.Collections.Generic;
using System.Linq;

using Glav.CacheAdapter.Core;
using Glav.CacheAdapter.Helpers;
using Glav.CacheAdapter.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Glav.CacheAdapter.Distributed.Redis
{
    internal class RedisCacheAdapter : ICache
    {
        private readonly ILogger<RedisCacheAdapter> _logger;
        private readonly PerRequestCacheHelper _requestCacheHelper = new PerRequestCacheHelper();
        private static IConnectionMultiplexer _connection;
        //private readonly IDistributedCache _distributedCache;
        private readonly bool _cacheDependencyManagementEnabled;

        public RedisCacheAdapter(ILogger<RedisCacheAdapter> logger,
            IConfiguration configuration, IOptions<CacheConfig> cacheConfig)
        {
            _logger = logger;
            string connectionString = configuration["RedisCacheOptions:ConnectionString"];
            _connection = ConnectionMultiplexer.Connect(connectionString);
            _cacheDependencyManagementEnabled = cacheConfig.Value.IsCacheDependencyManagementEnabled;
        }

        public T Get<T>(string cacheKey) where T : class
        {
            try
            {
                var requestCacheData = _requestCacheHelper.TryGetItemFromPerRequestCache<T>(cacheKey);
                if (requestCacheData != null)
                {
                    return requestCacheData;
                }

                var data = new RedisValue();
                var rdb = RedisDatabase;
                if (_cacheDependencyManagementEnabled && rdb.KeyType(cacheKey) == RedisType.List)
                {
                    var cacheValue = rdb.ListGetByIndex(cacheKey, 0);
                    if (cacheValue.HasValue && cacheValue != string.Empty)
                    {
                        data = cacheValue;
                    }
                }
                else
                {
                    data = rdb.StringGet(cacheKey);
                }
                if (!data.IsNull && data.HasValue)
                {
                    var blobBytes = (byte[])data;
                    var deserialisedObject = blobBytes.Deserialize<T>();
                    return deserialisedObject;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in get from cache");
            }
            return null;
        }

        public void Add(string cacheKey, DateTime absoluteExpiry, object dataToAdd)
        {
            try
            {
                var binaryData = dataToAdd.Serialize();
                var expiry = absoluteExpiry - DateTime.Now;
                var success = RedisDatabase.StringSet(cacheKey, binaryData, expiry);
                if (!success)
                {
                    _logger.LogError("Unable to store item in cache. CacheKey:{cacheKey}", cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item");
            }
        }

        public void Add(string cacheKey, TimeSpan slidingExpiryWindow, object dataToAdd)
        {
            try
            {
                var binaryData = dataToAdd.Serialize();
                var success = RedisDatabase.StringSet(cacheKey, binaryData, slidingExpiryWindow);
                if (!success)
                {
                    _logger.LogError("Unable to store item in cache. CacheKey:{cacheKey}", cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error add item");
            }
        }

        public void InvalidateCacheItem(string cacheKey)
        {
            try
            {
                var success = RedisDatabase.KeyDelete(cacheKey);
                if (!success)
                {
                    _logger.LogError("Unable to remove item from cache. CacheKey:{cacheKey}", cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidate item");
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

            var distinctKeys = cacheKeys.Distinct();

            try
            {
                var redisKeyList = distinctKeys.Select(s => (RedisKey)s);
                RedisDatabase.KeyDelete(redisKeyList.ToArray(), CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidate item");
            }
        }

        public void AddToPerRequestCache(string cacheKey, object dataToAdd)
        {
            _requestCacheHelper.AddToPerRequestCache(cacheKey, dataToAdd);
        }

        public CacheSetting CacheType => CacheSetting.Redis;

        public void ClearAll()
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Clearing the cache");
            }

            var allEndpoints = _connection.GetEndPoints();
            if (allEndpoints != null && allEndpoints.Length > 0)
            {
                foreach (var endpoint in allEndpoints)
                {
                    var server = _connection.GetServer(endpoint);
                    try
                    {
                        server.FlushAllDatabases();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing the cache using `FlushAllDatabases` method (probably due to requiring admin privileges)");
                        _logger.LogError("Flushing the cache - attempting to delete all keys");
                        try
                        {
                            var allKeys = server.Keys();
                            RedisDatabase.KeyDelete(allKeys.ToArray(), CommandFlags.FireAndForget);
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogError(ex2, "Error flushing the cache using `KeyDelete` method");
                        }
                    }
                }
            }
        }

        public IDatabase RedisDatabase => _connection.GetDatabase();
    }
}
