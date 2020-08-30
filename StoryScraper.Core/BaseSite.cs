using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core
{
    public abstract class BaseSite
    {
        protected readonly Config config;

        public BaseSite(string name, Uri baseUrl, Config config)
        {
            this.config = config;
            Name = name;
            BaseUrl = baseUrl;
        }

        public Uri BaseUrl { get; }
        public string Name { get; }

        public string CachePath => Path.Combine(config.CachePath, $"site-{Name.ToValidPath()}");
        
        public abstract Task<string> GetAsync(Uri url);

        public abstract Task<string> PostAsync(Uri url, HttpContent data);

        public abstract Task<IStory> GetStory(Uri url);
    }
}
