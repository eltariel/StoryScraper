using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io.Dom;
using Newtonsoft.Json;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class Story : IStory
    {
        private readonly HttpClient client;
        private readonly Config config;

        private static readonly JsonSerializer jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        });

        public Story(Uri url, Site site, HttpClient client, Config config)
        {
            Url = url;
            var regex = new Regex(@"(?:\.(\d*))");
            Xf2Site = site;
            this.client = client;
            this.config = config;
            StoryId = regex.Match(url.AbsolutePath).Groups[1].Value;
            Context = BrowsingContext.New(Xf2Site.AngleSharpConfig);
        }

        [JsonConstructor]
        public Story(Uri url, string storyId, string title, string author, Uri image, string cachedImage, List<Category> xf2Categories)
        {
            Url = url;
            StoryId = storyId;
            Title = title;
            Author = author;
            Image = image;
            CachedImage = cachedImage;
            Xf2Categories = xf2Categories;
        }
        
        public string BaseUrl => Url.ToString().Replace(Xf2Site.BaseUrl.ToString(), "");
        public Uri Url { get; private set; }

        public string StoryId { get; private set; }
        public string Title { get; private set; }
        public string Author { get; private set; }
        
        public Uri Image { get; private set; }

        [JsonIgnore]
        public List<ICategory> Categories => Xf2Categories.Cast<ICategory>().ToList();
        public List<Category> Xf2Categories { get; private set; }

        [JsonIgnore]
        public IBrowsingContext Context { get; }

        public DateTime LastUpdate => Categories.Aggregate(DateTime.MinValue, (d, c) => d > c.LastUpdate ? d : c.LastUpdate);

        public async Task GetCategories()
        {
            if (!await ReadCache())
            {
                await ParseStory();

                var c = Xf2Site.GetCategoriesFor(this);
                foreach (var cat in c)
                { 
                    await cat.GetPosts();
                }

                Xf2Categories = c;

                CacheStoryMetadata();
            }
        }

        private async Task<bool> ReadCache()
        {
            try
            {
                using var streamReader = new StreamReader(MetadataCachePath);
                var jtr = new JsonTextReader(streamReader);
                var cachedStory = jsonSerializer.Deserialize<Story>(jtr);
                
                Url = cachedStory.Url;
                StoryId = cachedStory.StoryId;
                Title = cachedStory.Title;
                Author = cachedStory.Author;
                Image = cachedStory.Image;
                Xf2Categories = cachedStory.Xf2Categories;
                foreach (var cat in Xf2Categories)
                {
                    cat.Xf2Story = this;
                    foreach (var post in cat.Xf2Posts)
                    {
                        post.Xf2Category = cat;
                        await post.FetchContent("Assume it's cached too");
                    }
                }

                // Check for updates
                var rss = Xf2Categories.First().RssLink;
                var req = new HttpRequestMessage(HttpMethod.Head, rss);
                var head = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                Console.WriteLine($"Getting headers for [{rss}]: response status = {head.StatusCode} ({(int)head.StatusCode})");
                Console.WriteLine($"Last Update for [{rss}]: {head.Content.Headers.LastModified}");
                
                Console.WriteLine($"Last cache date = {LastUpdate}, last modified date = {head.Content.Headers.LastModified}.");
                if (LastUpdate < head.Content.Headers.LastModified)
                {
                    Console.WriteLine("New posts, re-fetch categories");
                    return false; // TODO: Don't just invalidate the whole cache here...
                }
                else
                {
                    Console.WriteLine("No new posts.");
                }
                
                return true;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Cache not found.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception loading story cache: {ex}");
            }
            
            Console.WriteLine("Attempting to load from URL");
            return false;
        }

        private async Task ParseStory()
        {
            var doc = await Context.OpenAsync(Url.ToString());

            StoryId = doc.QuerySelector<IHtmlHtmlElement>("html")
                ?.Dataset["content-key"]
                ?.Substring("thread-".Length) ?? StoryId;

            if (doc.QuerySelector<IHtmlLinkElement>("[rel='canonical']")?.Href is {} canonicalUrl)
            {
                Url = new Uri(canonicalUrl);
            }

            if (doc.QuerySelector<IHtmlMetaElement>("[property='og:image']")?.Content is {} imgString)
            {
                Image = new Uri(imgString);
                CachedImage = CoverImagePath(Path.GetExtension(Image.AbsolutePath));
                
                var img = await client.GetByteArrayAsync(Image);
                await File.WriteAllBytesAsync(CachedImage, img);
            }

            var titleElem = doc.QuerySelector<IHtmlHeadingElement>("h1.p-title-value");
            Title = (titleElem.TextContent ?? "Unknown").Trim();
            var authorElem = doc.QuerySelector<IHtmlAnchorElement>(".username.u-concealed");
            Author = (authorElem.TextContent ?? "Unknown").Trim();
        }

        public string CachedImage { get; set; }

        private void CacheStoryMetadata()
        {
            using var sw = new StreamWriter(MetadataCachePath);
            var jtw = new JsonTextWriter(sw);
            jsonSerializer.Serialize(jtw, this);
            Console.WriteLine($"Cache written to '{MetadataCachePath}'");
        }

        private string MetadataCachePath =>
            Path.Combine(Xf2Site.CachePath, $"story-{StoryId}.json".ToValidPath());
        
        private string CoverImagePath(string ext) =>
            Path.Combine(Xf2Site.CachePath, $"cover-{StoryId}{ext}".ToValidPath());

        [JsonIgnore]
        public Site Xf2Site { get; }
    }
}
