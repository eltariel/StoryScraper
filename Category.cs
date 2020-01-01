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
        private readonly List<Post> posts = new List<Post>();

        public string Id { get; }
        public string Href { get; }
        public string Name { get; }

        public Site Site {get;}
        public Story Story {get;}

        public IReadOnlyCollection<Post> Posts => posts;

        public Category(string id, string href, string name, Site site, Story story)
        {
            Id = id;
            Href = href;
            Name = name;
            Site = site;
            Story = story;
        }

        public async Task<IEnumerable<Post>>  GetPostDetails()
        {
            posts.Clear();

            var doc = await FetchCategoryPage();
            var csrfToken = (doc.GetElementById("XF") as IHtmlHtmlElement).Dataset["csrf"];

            int.TryParse(doc.QuerySelector(".dataList-cell--min").InnerHtml, out var markCount);
            Console.WriteLine($"Category {Id}: {Name} ({markCount} threadmarks)");

            var relevantLinks = doc.QuerySelectorAll(".structItem--threadmark a");
            var ps = await FetchPosts(relevantLinks, csrfToken, doc);
            
            posts.AddRange(ps.OrderBy(p => p.Timestamp));
            return posts;
        }
        
        private async Task<IHtmlDocument> FetchCategoryPage()
        {
            var content = await Site.GetAsync(new Uri(Href));

            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(content);
            doc.Location.Href = Href;

            return doc;
        }

        private async Task<IEnumerable<Post>> FetchPosts(
            IHtmlCollection<IElement> relevantLinks,
            string csrfToken,
            IHtmlDocument doc)
        {
            var posts = relevantLinks
                .OfType<IHtmlAnchorElement>()
                .Where(l => l.ChildElementCount == 0)
                .Select(l => new Post(l.Href, l.InnerHtml, this, Story, Site))
                .ToList();

            foreach (var p in posts)
            {
                Console.WriteLine($"Found post: {p.Name} @ {p.Href}");
                await p.FetchContent(csrfToken);
            }

            posts.AddRange(await FetchAdditional(csrfToken, doc));

            return posts;
        }

        private async Task<IEnumerable<Post>> FetchAdditional(string csrfToken, IHtmlDocument doc)
        {
            var fetcherDiv = doc.QuerySelector("[data-xf-click=\"threadmark-fetcher\"]") as IHtmlDivElement;
            if (fetcherDiv != null)
            {
                var fetchUrl = fetcherDiv.Dataset["fetchurl"];
                Console.WriteLine($"Fetch url: {fetchUrl}");

                var url = new Uri(Site.BaseUrl, fetchUrl);
                var form = Site.GetHtmlPostData(Story.BaseUrl, csrfToken);

                var json = await Site.PostAsync(url, form);

                File.WriteAllText("dump.json", json);

                var content = (string)JObject.Parse(json)["html"]["content"];

                var parser = new HtmlParser();
                var innerDoc = await parser.ParseDocumentAsync(content);
                innerDoc.Location.Href = url.ToString();

                return await FetchPosts(innerDoc.Links, csrfToken, innerDoc);
            }

            return new Post[0];
        }
    }
}