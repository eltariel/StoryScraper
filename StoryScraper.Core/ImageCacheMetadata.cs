using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AngleSharp.Io;
using Newtonsoft.Json;
using NLog;

namespace StoryScraper.Core
{
    public class ImageCacheMetadata
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private static readonly SHA256 sha = SHA256.Create();

        public ImageCacheMetadata(MimeType mimeType, string timestamp)
        {
            MimeType = mimeType;
            Timestamp = timestamp;
        }
            
        [JsonConstructor]
        public ImageCacheMetadata(string contentType, string timestamp, string source)
        {
            MimeType = new MimeType(contentType);
            Timestamp = timestamp;
            Source = source;
        }

        [JsonIgnore]
        public MimeType MimeType { get; }
        public string ContentType => MimeType.Content;
        
        public string Timestamp { get; }
        
        public string Source { get; private set; }

        [JsonIgnore]
        public Cache Cache { get; private set; }

        public void ToCache()
        {
            var metaPath = GetMetaPath(MakeImageCachePath(Cache, Source));
            File.WriteAllText(metaPath, JsonConvert.SerializeObject(this));
        }

        public static ImageCacheMetadata FromCache(Cache cache, string source)
        {
            try
            {
                var imageCachePath = MakeImageCachePath(cache, source);
                var cacheMetadataPath = GetMetaPath(imageCachePath);

                var meta = JsonConvert.DeserializeObject<ImageCacheMetadata>(File.ReadAllText(cacheMetadataPath));
                meta.Cache = cache;
                return meta;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static ImageCacheMetadata FromResponse(IResponse response, string source, Cache cache)
        {
            response.Headers.TryGetValue("Last-Modified", out var timestamp);
            return new ImageCacheMetadata(response.GetContentType(), timestamp ?? $"{DateTime.Now}")
            {
                Source = source,
                Cache = cache
            };
        }
        
        public string GetImagePath() => MakeImageCachePath(Cache, Source) + GetImageExtension(MimeType);

        private static string GetImageExtension(MimeType contentType) =>
            contentType.Content switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                _ => ".bin"
            };

        private static string MakeImageCachePath(Cache cache, string imgSource)
        {
            var imgCacheDir = Path.Combine(cache.Root, "images");
            Directory.CreateDirectory(imgCacheDir);
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(imgSource));
            var fileName = string.Join("", hash.Select(b => $"{b:x2}"));
            return Path.Combine(imgCacheDir, $"{fileName}");
        }
 
        private static string GetMetaPath(string baseFilename) => $"{baseFilename}-meta.json";
    }
}