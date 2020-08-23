using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json.Linq;

namespace StoryScraper.Core
{
    public class Post
    {
        private readonly Config config;

        public Post(string href, string name, Category category, Story story, Site site, Config config)
        {
            this.config = config;
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

		public bool FromCache { get; private set; } = false;

        public DateTime Timestamp { get; private set; }
        public string Author { get; private set; }
        public string Title { get; private set; }
        public string Content { get; private set; }
        public string AsHtml { get; private set; }

        public async Task FetchContent(string csrfToken)
        {
            var queryParams = Site.GetCommonParams(Story.BaseUrl, csrfToken);
            var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
            var url = new Uri(Site.BaseUrl, $"/posts/{PostId}/preview-threadmark?{queryString}");

            var jsonCacheFile = $"{Site.CachePath}/posts/json/post-{PostId}.json";
            FromCache = File.Exists(jsonCacheFile);
            var json = !FromCache
                ? await Site.GetAsync(url)
                : await File.ReadAllTextAsync(jsonCacheFile);
            
            Directory.CreateDirectory($"{Site.CachePath}/posts/json");
            await File.WriteAllTextAsync(jsonCacheFile, json);

            var html = (string)JObject.Parse(json)["html"]["content"];
            await ParseContent(html);
        }

        private async Task ParseContent(string html)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var doc = await context.OpenAsync(res => res.Content(html).Address(Site.BaseUrl));

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

            Title = titleElement?.TextContent;
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
            te.Text = Title;
            doc.Head.Append(te);

            var header = doc.CreateElement("h2");
            header.TextContent = $"{Category.Name}: {Title}" +
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
