﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using NLog;
using StoryScraper.Core.Conversion;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core
{
    public abstract class BaseSite
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        protected readonly IConfig config;
        
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        private static readonly HttpMessageHandler clientHandler = new RateLimitHandler(
            new HttpClientHandler { CookieContainer = cookieContainer });

        protected static readonly HttpClient client = new HttpClient(clientHandler);

        public IConfiguration AngleSharpConfig { get; }

        public BaseSite(string name, Uri baseUrl, IConfig config, Pandoc pandoc)
        {
            this.config = config;
            Name = name;
            BaseUrl = baseUrl;
            Cache = new Cache(this, config, pandoc);

            AngleSharpConfig = Configuration.Default
                .WithRequesters(clientHandler)
                .WithPersistentCookies(Path.Combine(Cache.Root, "cookies.txt"))
                .WithDefaultLoader();
            Context = BrowsingContext.New(AngleSharpConfig);
        }

        public Cache Cache { get; }

        public IBrowsingContext Context { get; }

        public Uri BaseUrl { get; }
        public string Name { get; }

        public abstract Task<IStory> GetStory(Uri url);
    }
}
