using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json.Linq;

namespace StoryScraper
{
    public class Category
    {
        public string Id { get; }
        public string Href { get; }
        public string Name { get; }

        public Site Site {get;}
        public Story Story {get;}

        public List<Post> Posts { get; } = new List<Post>();
        public int PostCount { get; private set; }

        public Category(string id, string href, string name, Site site, Story story)
        {
            Id = id;
            Href = href;
            Name = name;
            Site = site;
            Story = story;
        }

        public async Task GetDetails()
        {
            await FetchCategoryPage();
        }

        public async Task<IEnumerable<Post>> GetPosts()
        {
            Posts.Clear();

            var doc = await FetchCategoryPage();

            var csrfToken = (doc.GetElementById("XF") as IHtmlHtmlElement)?.Dataset["csrf"];

            var relevantLinks = doc.QuerySelectorAll(".structItem--threadmark a");
            var ps = await FetchPosts(relevantLinks, csrfToken, doc);
            
            Posts.AddRange(ps.OrderBy(p => p.Timestamp));
            return Posts;
        }

        private async Task<IHtmlDocument> FetchCategoryPage()
        {
            var content = await Site.GetAsync(new Uri(Href));

            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(content);
            doc.Location.Href = Href;

            // Postmark counting is wrong - this is *per user*
            int.TryParse(doc.QuerySelector(".dataList-cell--min").InnerHtml, out var markCount);
            PostCount = markCount;
            return doc;
        }

        private async Task<IEnumerable<Post>> FetchPosts(
            IEnumerable<IElement> relevantLinks,
            string csrfToken,
            IHtmlDocument doc)
        {
            var fetchedPosts = relevantLinks
                .OfType<IHtmlAnchorElement>()
                .Where(l => l.ChildElementCount == 0)
                .Select(l => new Post(l.Href, l.InnerHtml, this, Story, Site))
                .ToList();

            foreach (var p in fetchedPosts)
            {
                Console.WriteLine($"Found post: {p.Name} @ {p.Href}");
                await p.FetchContent(csrfToken);
            }

            if (doc.QuerySelector("[data-xf-click=\"threadmark-fetcher\"]") is IHtmlDivElement fetcherDiv)
            {
                var fetchUrl = fetcherDiv.Dataset["fetchurl"];

                var ret = await ParseAdditionalPosts(csrfToken, fetchUrl);
                fetchedPosts.AddRange(ret);
            }

            return fetchedPosts;
        }

        private async Task<IEnumerable<Post>> ParseAdditionalPosts(string csrfToken, string fetchUrl)
        {
            Console.WriteLine($"Fetch url: {fetchUrl}");
            var url = new Uri(Site.BaseUrl, fetchUrl);
            var form = Site.GetHtmlPostData(Story.BaseUrl, csrfToken);

            var json = await Site.PostAsync(url, form);

            var content = (string) JObject.Parse(json)["html"]["content"];
            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(content);
            doc.Location.Href = url.ToString();

            return await FetchPosts(doc.Links, csrfToken, doc);
        }
    }
}