using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json.Linq;

namespace StoryScraper.Core
{
    public class Post
    {
        public Post(string href, string name, Category category, Story story, Site site)
        {
            Href = href;
            Name = name;
            Category = category;
            Story = story;
            Site = site;
            PostId = Href.Substring(Href.LastIndexOf("post-", StringComparison.InvariantCulture) + 5);
        }

        public string Href { get; }
        public string PostId { get; }
        public string Name { get; }
        public Category Category { get; }
        public Story Story { get; }
        public Site Site { get; }

        public DateTime Timestamp { get; private set; }
        public string Author { get; private set; }
        public string Title { get; private set; }
        public string Content { get; private set; }

        public async Task FetchContent(string csrfToken)
        {
            Console.WriteLine($"Fetching content for '{Name}'");

            var queryParams = Site.GetCommonParams(Story.BaseUrl, csrfToken);
            var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
            var url = new Uri(Site.BaseUrl, $"/posts/{PostId}/preview-threadmark?{queryString}");

            var jsonCacheFile = $"{Site.CachePath}/posts/json/post-{PostId}.json";
            var readFromCache = File.Exists(jsonCacheFile);
            var json = !readFromCache
                ? await Site.GetAsync(url)
                : await File.ReadAllTextAsync(jsonCacheFile);
            
            Directory.CreateDirectory($"{Site.CachePath}/posts/json");
            await File.WriteAllTextAsync(jsonCacheFile, json);

            var html = (string)JObject.Parse(json)["html"]["content"];
            await File.WriteAllTextAsync($"{Site.CachePath}/posts/post-{PostId}.html", html);

            await ParseContent(html);
        }

        private async Task ParseContent(string html)
        {
            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(html);

            var titleElement = doc.QuerySelector(".threadmarkLabel") as IHtmlSpanElement;
            var bodyElement = doc.QuerySelector(".bbWrapper") as IHtmlDivElement;
            var timestampElement = doc.QuerySelector(".u-dt") as IHtmlTimeElement;
            var authorElement = doc.QuerySelector(".username") as IHtmlAnchorElement;

            Title = titleElement?.TextContent;
            Content = bodyElement?.InnerHtml;
            Timestamp = DateTime.Parse(timestampElement?.DateTime ?? "1901-01-01");
            Author = authorElement?.TextContent;
        }
    }
}
