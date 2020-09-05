using System.IO;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Io;
using NLog;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core
{
    public class Cache
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        private readonly Config config;

        public Cache(BaseSite site, Config config)
        {
            this.config = config;
            Site = site;
        }
        
        public BaseSite Site { get; }
        
        public string Root => Path.Combine(config.CachePath, $"site-{Site.Name.ToValidPath()}");
        
        public async Task<string> CacheImage(string source)
        {
            var meta = ImageCacheMetadata.FromCache(this, source);
            if (meta == null)
            {
                log.Debug($"Downloading image from {source}");
                var download = Site.Context
                    .GetService<IDocumentLoader>()
                    .FetchAsync(new DocumentRequest(new Url(source)));

                using var response = await download.Task;

                meta = await ImageCacheMetadata.FromResponse(response, source, this);
                log.Debug($"Image written to {meta.GetImagePath()}");
            }

            return meta.GetImagePath();
        }
    }
}