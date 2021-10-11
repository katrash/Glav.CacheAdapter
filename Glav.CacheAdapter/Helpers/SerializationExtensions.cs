using System.IO;
using System.Runtime.Serialization;

namespace Glav.CacheAdapter.Helpers
{
    public static class SerializationExtensions
    {
        public static byte[] Serialize(this object o)
        {
            if (o == null)
            {
                return null;
            }

            var srlzr = new DataContractSerializer(o.GetType());
            using (var memoryStream = new MemoryStream())
            {
                srlzr.WriteObject(memoryStream, o);
                var objectDataAsStream = memoryStream.ToArray();
                return objectDataAsStream;
            }
        }

        public static T Deserialize<T>(this byte[] stream)
        {
            if (stream == null)
            {
                return default;
            }

            var srlzr = new DataContractSerializer(typeof(T));
            using var memoryStream = new MemoryStream(stream);
            return (T)srlzr.ReadObject(memoryStream);
        }
    }
}
