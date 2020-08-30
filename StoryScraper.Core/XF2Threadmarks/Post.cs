using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class Post : IPost
    {
        private readonly Config config;

        public Post(string url, string name, string author, DateTime timestamp, Category category, Config config)
        {
            this.config = config;
            Xf2Category = category;
            Url = url;
            Name = name;
            Author = author;
            Timestamp = timestamp;
            PostId = url.Substring(url.LastIndexOf("post-", StringComparison.InvariantCulture) + 5);
        }

        public string PostId { get; }
        public string Name { get; set; }
        public string Author { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Url { get; }

        [JsonIgnore]
        public Site Xf2Site => Xf2Story.Xf2Site;

        [JsonIgnore]
        public Story Xf2Story => Xf2Category.Xf2Story;
        
        [JsonIgnore]
        public Category Xf2Category { get; set; }

        [JsonIgnore]
        public ICategory Category => Xf2Category;
        
        [JsonIgnore]
        public IStory Story => Xf2Story;
        
        [JsonIgnore]
        public BaseSite Site => Xf2Site;

        [JsonIgnore]
        public bool FromCache { get; private set; } = false;
        
        [JsonIgnore]
        public string Content { get; private set; }
        
        [JsonIgnore]
        public string AsHtml { get; private set; }

        public async Task FetchContent(string csrfToken)
        {
            try
            {
                AsHtml = await File.ReadAllTextAsync(HtmlCacheFile);
                FromCache = true;
                return;
            }
            catch (Exception)
            {
                FromCache = false;
            }

            var directUrl = GetPostPreviewUrl(csrfToken);
            var json = await Site.GetAsync(directUrl);

            Directory.CreateDirectory(Path.GetDirectoryName(HtmlCacheFile));
            await File.WriteAllTextAsync(JsonCacheFile, json);
            
            if ((string)JObject.Parse(json)["html"]?["content"] is {} html)
            {
                await ParseContent(html);
                await File.WriteAllTextAsync(HtmlCacheFile, AsHtml);
            }
        }

        private Uri GetPostPreviewUrl(string csrfToken)
        {
            var queryParams = Xf2Site.GetCommonParams(Story.BaseUrl, csrfToken);
            var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
            return new Uri(Site.BaseUrl, $"/posts/{PostId}/preview-threadmark?{queryString}");
        }

        private string JsonCacheFile => $"{Site.CachePath}/posts/json/post-{PostId}.json";
        private string HtmlCacheFile => $"{Site.CachePath}/posts/json/post-{PostId}.html";

        private async Task ParseContent(string html)
        {
            var doc = await Xf2Story.Context.OpenAsync(res => res.Content(html).Address(Site.BaseUrl));

            var bodyElement = GetProperties(doc);
            FixImageSourceUrls(doc);
            ReformatQuotes(doc);
            ReformatSpoilers(doc);
            InsertPostTitle(doc);
            TrimMetadata(doc, bodyElement);

            AsHtml = doc.Prettify();
        }

        private IHtmlDivElement GetProperties(IDocument doc)
        {
            var titleElement = doc.QuerySelector<IHtmlSpanElement>(".threadmarkLabel");
            var bodyElement = doc.QuerySelector<IHtmlDivElement>(".bbWrapper");
            var timestampElement = doc.QuerySelector<IHtmlTimeElement>(".u-dt");
            var authorElement = doc.QuerySelector<IHtmlAnchorElement>(".username");

            Name = titleElement?.TextContent;
            Content = bodyElement?.InnerHtml;
            Timestamp = DateTime.Parse(timestampElement?.DateTime ?? "1901-01-01");
            Author = authorElement?.TextContent;
            return bodyElement;
        }

        private void FixImageSourceUrls(IDocument doc)
        {
            foreach (var img in doc.QuerySelectorAll<IHtmlImageElement>("img"))
            {
                // set image src element to full url by reassigning it to itself
                // relative urls will have doc.BaseUrl added, absolute urls will be left unchanged
                img.Source = img.Source;
            }
        }

        private void ReformatQuotes(IDocument doc)
        {
            foreach (var q in doc.QuerySelectorAll<IHtmlQuoteElement>("blockquote"))
            {
                if (q.QuerySelector<IHtmlDivElement>("div.bbCodeBlock-title") is {} t)
                {
                    var b = doc.CreateElement("b");
                    b.InnerHtml = t.InnerHtml;
                    t.Replace(b);
                }

                var c = q.QuerySelector<IHtmlDivElement>("div.bbCodeBlock-content");
                var xc = q.QuerySelector<IHtmlDivElement>("div.bbCodeBlock-expandContent");
                var p = doc.CreateElement("p");
                p.InnerHtml = xc.InnerHtml;

                c.Replace(p);
                p.After(doc.CreateElement<IHtmlHrElement>());
            }
        }

        private void ReformatSpoilers(IDocument doc)
        {
            foreach (var spoiler in doc.QuerySelectorAll<IHtmlDivElement>("div.bbCodeSpoiler"))
            {
                var spoilerContent = spoiler.QuerySelector<IHtmlDivElement>("div.bbCodeBlock-content");
                var spoilerTitle = spoiler.QuerySelector<IHtmlSpanElement>("span.bbCodeSpoiler-button-title");

                var spoilerHeader = doc.CreateElement("b");
                spoilerHeader.TextContent = "Spoiler: " + spoilerTitle?.TextContent ?? "";
                var q = doc.CreateElement("blockquote");
                q.Append(doc.CreateElement("hr"),
                    spoilerHeader,
                    spoilerContent,
                    doc.CreateElement("hr"));
                spoiler.Replace(q);
            }
        }

        private void InsertPostTitle(IDocument doc)
        {
            var te = doc.CreateElement<IHtmlTitleElement>();
            te.Text = Name;
            doc.Head.Append(te);

            var header = doc.CreateElement("h2");
            header.TextContent = $"{Category.Name}: {Name}" +
                                 (Story.Author == Author ? "" : $" (by {Author})");
            doc.Body.Prepend(header);
        }

        private void TrimMetadata(IDocument doc, IHtmlDivElement bodyElement)
        {
            var tp = doc.Body.QuerySelector<IHtmlDivElement>(".threadmarkPreview");
            tp.ReplaceWith(bodyElement);
        }
    }
}