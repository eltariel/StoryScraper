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

        private Story(Uri url, string storyId, string title, string author, string image, Site site)
        {
            Url = url;
            StoryId = storyId;
            Title = title;
            Author = author;
            Image = image;
            Categories = new List<Category>();
            Site = site;
        }

        public string BaseUrl => Url.ToString().Replace(Site.BaseUrl.ToString(), "");
        public Uri Url { get; }

        public string StoryId { get; }
        public string Title { get; }
        public string Author { get; }

        public string Image { get; set; }

        [JsonIgnore]
        public Site Site { get; set; }

        [JsonIgnore]
        List<ICategory> IStory.Categories => Categories.Cast<ICategory>().ToList();
        public List<Category> Categories { get; }

        public DateTime LastUpdate => Categories.Aggregate(DateTime.MinValue, (d, c) => d > c.LastUpdate ? d : c.LastUpdate);

        public async Task GetPosts()
        {
            foreach (var cat in Categories)
            {
                await cat.GetPosts();
            }
            CacheStoryMetadata();
        }

        internal static async Task<Story> FromUrl(Uri url, Site site)
        {
            log.Trace($"Getting story from {url}");
            var doc = await site.Context.OpenAsync(url.ToString());

            var actualUrl = GetActualUrl(url, doc);
            if (!actualUrl.Equals(url))
            {
                log.Trace($"  --> URL is not canonical for story, trying {actualUrl}");
                return await FromUrl(actualUrl, site);
            }

            var storyId = doc.QuerySelector<IHtmlHtmlElement>("html")
                ?.Dataset["content-key"]
                ?.Substring("thread-".Length) ?? throw new ArgumentException("Can't parse story ID from url");

            var categoryIds = doc
                .QuerySelectorAll<IHtmlAnchorElement>("[data-categoryid]")
                .Select(a => a.Dataset["categoryid"])
                .Distinct()
                .ToList();
            
            var cachedStory = GetCachedStory(site, storyId);
            var story = cachedStory ?? await GetStoryFromPage(site, doc, actualUrl, storyId, categoryIds);
            story.UpdateCategories(doc);
            return story;
        }

        private static async Task<Story> GetStoryFromPage(Site site, IDocument doc, Uri actualUrl, string storyId, List<string> categoryIds)
        {
            var titleElem = doc.QuerySelector<IHtmlHeadingElement>("h1.p-title-value");
            var authorElem = doc.QuerySelector<IHtmlAnchorElement>(".username.u-concealed");

            var title = (titleElem.TextContent ?? "Unknown").Trim();
            var author = (authorElem.TextContent ?? "Unknown").Trim();
            var authorUrl = authorElem.Href;

            var img = await GetStoryImage(doc, authorUrl, site);

            return new Story(actualUrl, storyId, title, author, img, site);
        }

        private static Uri GetActualUrl(Uri url, IDocument doc)
        {
            var ogUrl = doc.QuerySelector<IHtmlMetaElement>("[property='og:url']")?.Content;
            var canonicalUrl = doc.QuerySelector<IHtmlLinkElement>("[rel='canonical']")?.Href;
            var actualUrl = !string.IsNullOrWhiteSpace(ogUrl)
                ? new Uri(ogUrl)
                : !string.IsNullOrWhiteSpace(canonicalUrl)
                    ? new Uri(canonicalUrl)
                    : url;
            return actualUrl;
        }

        internal static Story GetCachedStory(Site site, string storyId)
        {
            try
            {
                using var streamReader = new StreamReader(MakeCachePath(site, storyId));
                using var jsonReader = new JsonTextReader(streamReader);
                var cachedStory = jsonSerializer.Deserialize<Story>(jsonReader);
                if (cachedStory != null && cachedStory.Categories.Any())
                {
                    cachedStory.Site = site;
                    foreach (var category in cachedStory.Categories)
                    {
                        category.Story = cachedStory;
                        foreach (var post in category.Posts)
                        {
                            post.Category = category;
                            post.Refetch = !site.Cache.IsPostCached(post);
                        }
                    }

                    log.Debug("Loaded story from cache");
                    return cachedStory;
                }
            }
            catch (Exception ex)
            {
                log.Trace(ex, $"Can't load story {site.Name}:{storyId} from cache");
            }

            return null;
        }

        public void UpdateCategories(IDocument doc)
        {
            var catIds = doc
                .QuerySelectorAll<IHtmlAnchorElement>("[data-categoryid]")
                .Select(a => a.Dataset["categoryid"])
                .Distinct()
                .ToList();

            var missingCategories = catIds
                .Where(c => Site.CategoryIds.Values.Contains(c))
                .Except(Categories.Select(c => c.CategoryId));
            
            foreach (var catId in missingCategories)
            {
                Categories
                    .Add(new Category(
                        catId,
                        Site.CategoryIds.FirstOrDefault(c => c.Value == catId).Key ?? "",
                        this));
            }

            CacheStoryMetadata();
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

            return !string.IsNullOrWhiteSpace(imgUrl)
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

        private string MetadataCachePath => MakeCachePath(Site, StoryId);

        private static string MakeCachePath(Site site, string storyId)
        {
            return Path.Combine(site.Cache.Root, $"story-{storyId}.json".ToValidPath());
        }
    }
}
