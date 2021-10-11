# CacheAdapter

This project provides a simple, consistent API into a number of different cache mechanisms.
You can program your application agsinst the simple ICache or ICacheProvider interface, yet change the 
implementation underlying that interface via configuration to use either:
 - In Memory cache (config setting="memory")
 - Distributed Redis cache. (config setting="redis")

For example:
<pre>
"CacheAdapter": {
    "CacheToUse": "redis"
}
</pre>

Means the underlying cache mechanism uses redis and it expects to find redis server node(s) at the
address(es) listed in the 'DistributedCacheServers' configuration element in settings file.

This means you dont have to know how to program against these specific cache mechanisms, as this is all
handled by the various adapters within this project, and driven purely through configuration.

In addition, the use of an interface based approach means you can easily test any component using this
cache interface as a dependency by mocking this interface implementation using tools such as MoQ.

This library consists of 2 main interfaces:
<pre>
ICache
ICacheProvider
</pre>

ICache is what is implemented by each each cache mechanism such as memory, and redis. It contains the
raw Add/Get/Delete methods for each cache implementation. ICacheProvider is a more fluent API that makes
use of what ever ICache implementation is configured. You can use either ICache or ICacheProvider directly,
and it will use the underlying configured cache mechanism. ICacheProvider is simply provided to give a more
fluent API to cache usage.

In the config file, if you set the 'CacheToUse' setting to 'redis', then the 'Configuration' should be
a comma separated list of server IP addresses and port numbers that represent the cache servers in your 
cache farm (see Redis specifics below). For example:
<pre>
"CacheAdapter": {
    "CacheToUse": "redis",
},
 "RedisCacheOptions": {
    "Configuration": "localhost:6379;192.168.1.2:6379",
    "InstanceName": "",
    "DefaultDatabase": null
  }
</pre>
This configuration states that there are 2 cache servers in the farm. One at address localhost (127.0.0.1),
port 6379 and the other at address 192.168.1.2, port 6379.

Please note that each distributed cache mechanism has different default ports that they use if
none are specified. The following is the default ports for each implementation:
* redis = Port 6379

## Redis Remarks:
.NET's Redis app-settings entry is used and there is no specific settings handled by this library.
This is implemented in this way to allow other referenced libraries that use ..NET's Redis cache implementation
will be using the same settings by default. In .NET Core/5 and later, the app settings are as follows:
<pre>
 "RedisCacheOptions": {
    "Configuration": "localhost:6379;192.168.1.2:6379",
    "InstanceName": "",
    "DefaultDatabase": null
  }
</pre

## Disabling the cache globally
You can completely disable the use of any cache so that all GET attempts will result in a cache miss 
and execute the delegate when one is provided. You can do this by setting the configuration
setting "IsCacheEnabled" to false.
<pre>
"CacheAdapter": {
    "IsCacheEnabled": true,
}
</pre>

*Note:* This feature only works if you are using the CacheProvider method of access. If you access the 
InnerCache or ICache directly, you will still be able to access the cache itself and cache operations
will work as normal.

## Support of CacheDependencies
<pre>
"CacheAdapter": {
    "IsCacheDependencyManagementEnabled": true,
}
</pre>
Note:
* Enabling this feature when using the default dependency support, incurs some performance 
    hit due to more calls being made to the caching engine. This can result in a more "chatty"
    interface to the cache engine,and higher cache usage, therefore more memory and connections 
    to the cache.
* This feature (and all advanced features) are only available when using the CacheProvider
    interface implementation. This is by design. The ICache abstraction is a raw abstraction over
    the basic cache engine.
Includes a generic cache dependency mechanism which acts as a common base. Not the most efficient but
intent is to later introduce cache dependency managers which utilise specific features of the cache
engine to maximise performance.

The cache dependency implementation works as a parent/child mechanism.
You can register or associate one or more child cache keys to a parent item. The
cache key can actually be the key of an item in the cache but it doesn't have to be.
So the parent key can be an arbitrary name or the key of an item in the cache.If it
is an item in the cache, it will get invalidated when instructed as normal.
Additionally, a child key of a parent key, can itself act as the parent for other
cache keys. When the top level parent is invalidated, all its dependent children,
and any further nested dependent children will also be invalidated.
For example:
<pre>
// Gets some data from main store and implicitly adds it to cache with key 'ParentKey'
cacheProvider.Get<string>("ParentKey",DateTime.Now.AddDays(1),() => "Data");
// Gets some data from main store and implicitly adds it to cache with key 'FirstChildKey' 
// and as a dependency to parent item with key "ParentKey"
cacheProvider.Get<string>("FirstChildKey",DateTime.Now.AddDays(1),() => "Data","ParentKey");
// Gets some data from main store and implicitly adds it to cache with key 'ChildOfFirstChildKey' 
// and as a dependency to parent item with key "FirstChildKey" which itself is a dependency to item with key "ParentKey"
cacheProvider.Get<string>("ChildOfFirstChildKey",DateTime.Now.AddDays(1),() => "FirstChildKey");

// At this point, the cache item relationship looks like
// ParentKey
//    +-----> FirstChildKey
//                   +-------> ChildOfFirstChildKey

// Invalidate the top level Parent, which clears all depenedent keys, included nested items
cacheProvider.InvalidateCacheItem("ParentKey");
</pre>

Note: A Parent can have a child key(s) that are themselves parents of the top
level key causing recursion. This is fully supported by the code and no infinite loops
are created.All relevant cache keys are cleared/actioned as normal within the
collective set of dependent keys


Source from https://github.com/glav/Glav.CacheAdapter and converted from .NET Framework 4.x to .NET Standard 2.0
