using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Io;
using Newtonsoft.Json;
using NLog;
using SixLabors.ImageSharp;

namespace StoryScraper.Core
{
    public class ImageCache
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private static readonly SHA256 sha = SHA256.Create();
        private readonly Cache cache;

        public ImageCache(Cache cache)
        {
            this.cache = cache;
        }

        public async Task<string> CacheImage(string source)
        {
            var meta = FromCache(cache, source);
            if (meta == null)
            {
                log.Trace($"Downloading image from {source}");
                var download = cache.Site.Context
                    .GetService<IDocumentLoader>()
                    .FetchAsync(new DocumentRequest(new Url(source)));

                using var response = await download.Task;

                meta = await FromResponse(response, source);
                log.Trace($"Image written to {GetImagePath(meta)}");
            }

            return GetImagePath(meta);
        }

        private static ImageCacheMetadata FromCache(Cache cache, string source)
        {
            try
            {
                var imageCachePath = MakeImageCachePath(cache, source);
                var cacheMetadataPath = GetMetaPath(imageCachePath);

                var meta = JsonConvert.DeserializeObject<ImageCacheMetadata>(File.ReadAllText(cacheMetadataPath));
                return meta;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<ImageCacheMetadata> FromResponse(IResponse response, string source)
        {
            if (!(200 <= (int) response.StatusCode && (int) response.StatusCode < 300))
            {
                throw new FileNotFoundException($"HTTP Response failed: {response.StatusCode} ({(int)response.StatusCode})");
            }
            
            var buf = new byte[response.Content.Length];
            await using (var ms = new MemoryStream(buf))
            {
                await response.Content.CopyToAsync(ms);
            }

            var format = Image.DetectFormat(buf);
            if (format == null)
            {
                throw new UnknownImageFormatException($"Unknown image format.");
            }
            
            response.Headers.TryGetValue("Last-Modified", out var timestamp);
            var meta = new ImageCacheMetadata(
                format.FileExtensions.FirstOrDefault(),
                timestamp ?? $"{DateTime.Now}",
                source);

            await File.WriteAllBytesAsync(GetImagePath(meta), buf);
            ToCache(meta);
            
            return meta;
        }

        private void ToCache(ImageCacheMetadata meta)
        {
            var metaPath = GetMetaPath(MakeImageCachePath(cache, meta.Source));
            File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta));
        }
        
        private string GetImagePath(ImageCacheMetadata meta) =>
            $"{MakeImageCachePath(cache, meta.Source)}.{meta.Extension}";

        private static string MakeImageCachePath(Cache cache, string imgSource)
        {
            var imgCacheDir = Path.Combine(cache.Root, "images");
            Directory.CreateDirectory(imgCacheDir);
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(imgSource));
            var fileName = string.Join("", hash.Select(b => $"{b:x2}"));
            return Path.Combine(imgCacheDir, $"{fileName}");
        }
 
        private static string GetMetaPath(string baseFilename) => $"{baseFilename}-meta.json";

        private class ImageCacheMetadata
        {
            public ImageCacheMetadata(string extension, string timestamp, string source)
            {
                Extension = extension;
                Timestamp = timestamp;
                Source = source;
            }

            public string Extension {get; }

            public string Timestamp { get; }
        
            public string Source { get; }
        }
    }
}