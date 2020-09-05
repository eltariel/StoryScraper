using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json;
using NLog;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class Story : IStory
    {
        private readonly Config config;
        
        private static readonly JsonSerializer jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        });

        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public Story(Uri url, Site site, Config config)
        {
            Url = url;
            var regex = new Regex(@"(?:\.(\d*))");
            Site = site;
            this.config = config;
            StoryId = regex.Match(url.AbsolutePath).Groups[1].Value;
        }

        [JsonConstructor]
        public Story(Uri url, string storyId, string title, string author, string image, List<Category> categories)
        {
            Url = url;
            StoryId = storyId;
            Title = title;
            Author = author;
            Image = image;
            Categories = categories;
        }
        
        public string BaseUrl => Url.ToString().Replace(Site.BaseUrl.ToString(), "");
        public Uri Url { get; private set; }

        public string StoryId { get; private set; }
        public string Title { get; private set; }
        public string Author { get; private set; }

        public string Image { get; set; }

        [JsonIgnore]
        public Site Site { get; }

        [JsonIgnore]
        List<ICategory> IStory.Categories => Categories.Cast<ICategory>().ToList();
        public List<Category> Categories { get; private set; }

        public DateTime LastUpdate => Categories.Aggregate(DateTime.MinValue, (d, c) => d > c.LastUpdate ? d : c.LastUpdate);

        public async Task GetCategories()
        {
            await ParseStory();

            var c = Site.GetCategoriesFor(this);
            foreach (var cat in c)
            {
                await cat.GetPosts();
            }

            Categories = c;

            CacheStoryMetadata();
        }

        private async Task ParseStory()
        {
            var doc = await Site.Context.OpenAsync(Url.ToString());

            StoryId = doc.QuerySelector<IHtmlHtmlElement>("html")
                ?.Dataset["content-key"]
                ?.Substring("thread-".Length) ?? StoryId;

            if (doc.QuerySelector<IHtmlLinkElement>("[rel='canonical']")?.Href is {} canonicalUrl)
            {
                Url = new Uri(canonicalUrl);
            }

            var titleElem = doc.QuerySelector<IHtmlHeadingElement>("h1.p-title-value");
            var authorElem = doc.QuerySelector<IHtmlAnchorElement>(".username.u-concealed");
            var authorUrl = authorElem.Href;

            Title = (titleElem.TextContent ?? "Unknown").Trim();
            Author = (authorElem.TextContent ?? "Unknown").Trim();

            var tlhImgElem = doc.QuerySelector<IHtmlImageElement>(".threadmarkListingHeader-icon img");

            var authorPage = await Site.Context.OpenAsync(authorUrl);
            var authorFullAvatar = authorPage.QuerySelector<IHtmlMetaElement>("[property='og:image']");
            if ((tlhImgElem?.Source ?? authorFullAvatar?.Content) is {} imgString)
            {
                Image = await Site.Cache.CacheImage(imgString);
            }
        }

        private void CacheStoryMetadata()
        {
            using var sw = new StreamWriter(MetadataCachePath);
            var jtw = new JsonTextWriter(sw);
            jsonSerializer.Serialize(jtw, this);
            log.Trace($"Cache written to '{MetadataCachePath}'");
        }

        private string MetadataCachePath =>
            Path.Combine(Site.Cache.Root, $"story-{StoryId}.json".ToValidPath());
    }
}
