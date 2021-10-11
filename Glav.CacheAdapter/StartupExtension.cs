using Glav.CacheAdapter.Core;
using Glav.CacheAdapter.DependencyManagement;
using Glav.CacheAdapter.Distributed.Redis;
using Glav.CacheAdapter.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Glav.CacheAdapter
{
    public static class StartupExtension
    {
        public static IServiceCollection RegisterCacheService(
            this IServiceCollection services, IConfiguration configuration)
        {
            string cacheToUse = (configuration["CacheAdapter:CacheToUse"] ?? CacheTypes.Memory)
                .ToLowerInvariant();
            if (CacheTypes.Redis.Equals(cacheToUse))
            {
                services.AddSingleton<ICache, RedisCacheAdapter>();
                services.AddSingleton<ICacheDependencyManager, RedisDependencyManager>();
                services.AddSingleton<ICacheFeatureSupport, RedisFeatureSupport>();
                services.AddSingleton<IDatabase>(s =>
                   (s.GetService<ICache>() as RedisCacheAdapter)?.RedisDatabase);
            }
            else
            {
                services.AddMemoryCache();
                services.AddSingleton<ICache, MemoryCacheAdapter>();
                services.AddSingleton<ICacheDependencyManager, GenericDependencyManager>();
                services.AddSingleton<ICacheFeatureSupport, DefaultFeatureSupport>();
            }

            services.Configure<CacheConfig>(instance =>
                configuration.GetSection("CacheAdapter").Bind(instance));
            services.AddSingleton<ICacheProvider, CacheProvider>();
            return services;
        }
    }
}
