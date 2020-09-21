using System;
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
        private readonly ImageCache imageCache;

        public Cache(BaseSite site, Config config)
        {
            this.config = config;
            Site = site;
            imageCache = new ImageCache(this);
        }
        
        public BaseSite Site { get; }
        
        public string Root => Path.Combine(config.CachePath, $"site-{Site.Name.ToValidPath()}");
        
        public async Task<string> CacheImage(string source)
        {
            try
            {
                return await imageCache.CacheImage(source);
            }
            catch (Exception ex)
            {
                log.Warn($"Can't cache image from {source}: {ex}");
                return string.Empty;
            }
        }
    }
}