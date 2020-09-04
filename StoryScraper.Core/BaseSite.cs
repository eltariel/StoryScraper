using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Io;
using NLog;
using StoryScraper.Core.Utils;
using StoryScraper.Core.XF2Threadmarks;

namespace StoryScraper.Core
{
    public abstract class BaseSite
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private static readonly SHA256 sha = SHA256.Create();
        protected readonly Config config;
        
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        private static readonly HttpMessageHandler clientHandler = new RateLimitHandler(
            new HttpClientHandler { CookieContainer = cookieContainer });

        protected static readonly HttpClient client = new HttpClient(clientHandler);

        public IConfiguration AngleSharpConfig { get; }

        public BaseSite(string name, Uri baseUrl, Config config)
        {
            this.config = config;
            Name = name;
            BaseUrl = baseUrl;

            AngleSharpConfig = Configuration.Default
                .WithRequesters(clientHandler)
                .WithPersistentCookies(Path.Combine(CachePath, "cookies.txt"))
                .WithDefaultLoader();
            Context = BrowsingContext.New(AngleSharpConfig);
        }

        public IBrowsingContext Context { get; }

        public Uri BaseUrl { get; }
        public string Name { get; }

        public string CachePath => Path.Combine(config.CachePath, $"site-{Name.ToValidPath()}");
        
        public abstract Task<string> GetAsync(Uri url);

        public abstract Task<string> PostAsync(Uri url, HttpContent data);

        public abstract Task<IStory> GetStory(Uri url);

        public async Task<string> CacheImage(string source)
        {
            var imageCachePath = MakeImageCachePath(source);
            if (!File.Exists(imageCachePath))
            {
                log.Debug($"Downloading image from {source}");
                var download = Context
                    .GetService<IDocumentLoader>()
                    .FetchAsync(new DocumentRequest(new Url(source)));

                using var response = await download.Task;
                await using var target = File.OpenWrite(imageCachePath);
                await response.Content.CopyToAsync(target);
                log.Debug($"Image written to {imageCachePath}");
            }

            return imageCachePath;
        }

        private string MakeImageCachePath(string imgSource)
        {
            var imgCacheDir = Path.Combine(CachePath, "images");
            Directory.CreateDirectory(imgCacheDir);
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(imgSource));
            var fileName = string.Join("", hash.Select(b=> $"{b:x2}"));
            return Path.Combine(imgCacheDir, $"{fileName}");
        }
    }
}
