using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class Post : IPost
    {

        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly Config config;

        public Post(string url, string name, string author, DateTime postedAt, DateTime updatedAt, Category category,
            Config config)
        {
            this.config = config;
            Category = category;
            Url = url;
            Name = name;
            Author = author;
            PostedAt = postedAt;
            UpdatedAt = updatedAt;
            PostId = url.Substring(url.LastIndexOf("post-", StringComparison.InvariantCulture) + 5);
        }

        public string PostId { get; }
        public string Name { get; }
        public string Author { get; }
        public DateTime PostedAt { get; }
        public DateTime UpdatedAt { get; }
        public string Url { get; }

        [JsonIgnore]
        public Site Site => Story.Site;

        [JsonIgnore]
        public Story Story => Category.Story;
        
        [JsonIgnore]
        public Category Category { get; }

        [JsonIgnore]
        ICategory IPost.Category => Category;
        
        [JsonIgnore] 
        IStory IPost.Story => Story;
        
        [JsonIgnore] 
        BaseSite IPost.Site => Site;

        [JsonIgnore]
        public bool FromCache { get; } = false;
        
        [JsonIgnore]
        public string Content { get; private set; }
        
        [JsonIgnore]
        public string AsHtml { get; private set; }

        private async Task ParseContent()
        {
            var doc = await Site.Context.OpenAsync(r => r.Content(Content).Address(Site.BaseUrl));
            await FixImageSourceUrls(doc);
            ReformatQuotes(doc);
            ReformatSpoilers(doc);
            InsertPostTitle(doc);

            // TODO: Verify that this solves links like sv:liason-worm.3419 and doesn't break formatting elsewhere.
            foreach (var span in doc.QuerySelectorAll<IHtmlSpanElement>("span").Where(s=> !s.ClassList.Contains("fixed-color")).ToList())
            {
                span.ReplaceWith(span.ChildNodes.ToArray());
            }

            AsHtml = doc.Prettify();
        }

        private async Task FixImageSourceUrls(IDocument doc)
        {
            foreach (var img in doc.QuerySelectorAll<IHtmlImageElement>("img"))
            {
                var imageCachePath = await Site.Cache.CacheImage(img.Source);

                img.SetAttribute("src", imageCachePath);
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
                spoilerHeader.TextContent = $"Spoiler: {spoilerTitle?.TextContent ?? ""}";
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

        public static async Task<Post> PostFromArticle(IElement article, Category category, Config config)
        {
            var idSpan = article.QuerySelector<IHtmlSpanElement>("span.u-anchorTarget");
            var postId = idSpan.Id.Substring("post-".Length);

            var linkElem = article.QuerySelector<IHtmlAnchorElement>("a.threadmark-control");
            var postUrl = linkElem.Href;

            var threadmarkElem = article.QuerySelector<IHtmlSpanElement>("span.threadmarkLabel");
            var title = threadmarkElem.TextContent;

            var usernameElem = article.QuerySelector<IHtmlAnchorElement>("a.username");
            var author = usernameElem.TextContent;

            var postTimeElem = article.QuerySelector<IHtmlTimeElement>("header.message-attribution time");
            var lastEditElem = article.QuerySelector<IHtmlTimeElement>("div.message-lastEdit time");
            var postTimeStr = lastEditElem?.DateTime ?? postTimeElem?.DateTime;
            DateTime.TryParse(postTimeElem?.DateTime, out var timestamp);
            DateTime.TryParse(postTimeStr, out var updated);
            
            var bodyElement = article.QuerySelector<IHtmlDivElement>(".bbWrapper");

            log.Debug($"New post {postId}: {title} by {author} at {timestamp}, updated at {updated}");
            
            var p = new Post(postUrl, title, author, timestamp, updated, category, config);
            p.Content = bodyElement?.InnerHtml;
            await p.ParseContent();

            return p;
        }
    }
}