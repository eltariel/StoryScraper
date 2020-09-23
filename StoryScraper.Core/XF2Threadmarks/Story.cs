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
        private static readonly JsonSerializer jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        });

        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public Story(Uri url, Site site)
        {
            Url = url;
            var regex = new Regex(@"(?:\.(\d*))");
            Site = site;
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

        private Story(Uri url, string storyId, string title, string author, string image, List<string> categoryIds, Site site)
        {
            Url = url;
            StoryId = storyId;
            Title = title;
            Author = author;
            Image = image;
            Categories = site.CategoryIds
                .Where(c => categoryIds.Contains(c.Value))
                .Select(c => new Category(c.Value, c.Key, this))
                .ToList();
            Site = site;
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
            foreach (var cat in Categories)
            {
                await cat.GetPosts();
            }
            CacheStoryMetadata();
        }

        internal static async Task<Story> FromUrl(Uri url, Site site)
        {
            var doc = await site.Context.OpenAsync(url.ToString());

            var storyId = doc.QuerySelector<IHtmlHtmlElement>("html")
                ?.Dataset["content-key"]
                ?.Substring("thread-".Length) ?? throw new ArgumentException("Can't parse story ID from url");

            url = doc.QuerySelector<IHtmlLinkElement>("[rel='canonical']")?.Href is {} canonicalUrl
                ? new Uri(canonicalUrl)
                : url;

            var titleElem = doc.QuerySelector<IHtmlHeadingElement>("h1.p-title-value");
            var authorElem = doc.QuerySelector<IHtmlAnchorElement>(".username.u-concealed");

            var categoryIds = doc
                .QuerySelectorAll<IHtmlAnchorElement>(".block-tabHeader--threadmarkCategoryTabs a")
                .Select(tab => tab.Id.Replace("threadmark-category-", ""))
                .ToList();

            var title = (titleElem.TextContent ?? "Unknown").Trim();
            var author = (authorElem.TextContent ?? "Unknown").Trim();
            var authorUrl = authorElem.Href;

            var img = await GetStoryImage(doc, authorUrl, site);

            return new Story(url, storyId, title, author, img, categoryIds, site);
        }

        private static async Task<string> GetStoryImage(IDocument doc, string authorUrl, Site site)
        {
            var tlhImgElem = doc.QuerySelector<IHtmlImageElement>(".threadmarkListingHeader-icon img");
            var imgUrl = tlhImgElem?.Source;

            if (string.IsNullOrWhiteSpace(imgUrl))
            {
                log.Debug("Story has no image, trying author avatar instead.");
                var authorPage = await site.Context.OpenAsync(authorUrl);
                var authorFullAvatar = authorPage.QuerySelector<IHtmlAnchorElement>("a.avatar");
                imgUrl = authorFullAvatar?.Href;
                
                if (string.IsNullOrWhiteSpace(imgUrl))
                {
                    var defaultImage = authorPage.QuerySelector<IHtmlMetaElement>("[property='og:image']");
                    imgUrl = defaultImage?.Content;
                }
            }

            if (string.IsNullOrWhiteSpace(imgUrl))
            {
                var defaultImage = doc.QuerySelector<IHtmlMetaElement>("[property='og:image']");
                imgUrl = defaultImage?.Content;
            }

            return string.IsNullOrWhiteSpace(imgUrl)
                ? await site.Cache.CacheImage(imgUrl)
                : "";
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
