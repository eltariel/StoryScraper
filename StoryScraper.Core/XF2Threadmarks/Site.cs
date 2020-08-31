using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Common;
using AngleSharp.Io;
using NLog;
using HttpMethod = System.Net.Http.HttpMethod;

namespace StoryScraper.Core.XF2Threadmarks
{
    public abstract class Site : BaseSite
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        private static readonly HttpMessageHandler clientHandler = new RateLimitHandler(
            new HttpClientHandler { CookieContainer = cookieContainer });
        private static readonly HttpClient client = new HttpClient(clientHandler);

        public IConfiguration AngleSharpConfig { get; }

        protected Site(string name, Uri baseUrl, IDictionary<string, int> categoryIds, Config config) : base(name, baseUrl, config)
        {
            CategoryIds = categoryIds;

            Directory.CreateDirectory(CachePath);
            
            AngleSharpConfig = Configuration.Default
                .WithRequesters(clientHandler)
                .WithPersistentCookies(Path.Combine(CachePath, "cookies.txt"))
                .WithDefaultLoader();
        }

        public IDictionary<string, int> CategoryIds { get; }
        
        public override async Task<string> GetAsync(Uri url)
        {
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        public override async Task<string> PostAsync(Uri url, HttpContent data)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url) {Content = data};
            req.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            
            var response = await client.SendAsync(req);
            return await response.Content.ReadAsStringAsync();
        }

        public override async Task<IStory> GetStory(Uri url)
        {
            var s = new Story(url, this, client, config);
            await s.GetCategories();
            return s;
        }

        public List<Category> GetCategoriesFor(Story story)
        {
            return CategoryIds
                .Where(a => !config.ExcludedCategories.Contains(a.Key))
                .Select(a =>
                    new Category(
                        $"{a.Value}",
                        a.Key,
                        story,
                        config))
                .ToList();
        }

        public IDictionary<string, string> GetCommonParams(string url, string csrfToken) =>
            new Dictionary<string, string> {
                {"_xfRequestUri", url},
                {"_xfWithData", "1"},
                {"_xfToken", csrfToken},
                {"_xfResponseType", "json"},
            };

        public FormUrlEncodedContent GetHtmlPostData(string url, string csrfToken) =>
            new FormUrlEncodedContent(GetCommonParams(url, csrfToken));
    }
}