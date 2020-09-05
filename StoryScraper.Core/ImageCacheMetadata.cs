using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Io;
using Newtonsoft.Json;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace StoryScraper.Core
{
    public class ImageCacheMetadata
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private static readonly SHA256 sha = SHA256.Create();
        private static readonly ImageFormatManager imageFormatManager = Configuration.Default.ImageFormatsManager;

        public ImageCacheMetadata(IImageFormat format, string timestamp)
        {
            Format = format;
            Timestamp = timestamp;
        }
            
        [JsonConstructor]
        public ImageCacheMetadata(string extension, string timestamp, string source)
        {
            Format = imageFormatManager.FindFormatByFileExtension(extension);
            Timestamp = timestamp;
            Source = source;
        }

        [JsonIgnore]
        public IImageFormat Format { get; }

        public string Extension => Format.FileExtensions.FirstOrDefault();

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

        public static async Task<ImageCacheMetadata> FromResponse(IResponse response, string source, Cache cache)
        {
            var buf = new byte[response.Content.Length];
            await using (var ms = new MemoryStream(buf))
            {
                await response.Content.CopyToAsync(ms);
            }

            var format = Image.DetectFormat(buf);
            
            response.Headers.TryGetValue("Last-Modified", out var timestamp);
            var meta = new ImageCacheMetadata(format, timestamp ?? $"{DateTime.Now}")
            {
                Source = source,
                Cache = cache
            };

            await File.WriteAllBytesAsync(meta.GetImagePath(), buf);
            meta.ToCache();
            
            return meta;
        }
        
        public string GetImagePath() => $"{MakeImageCachePath(Cache, Source)}.{Extension}";

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