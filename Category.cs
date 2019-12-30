using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json.Linq;

namespace threadmarks_thing
{
    public class Category
    {
        private readonly HttpClient client;
        private readonly Site site;
        private readonly Story story;

        public string Id { get; }
        public string Href { get; }
        public string Name { get; }

        public Category(string id, string href, string name, HttpClient client, Site site, Story story)
        {
            Id = id;
            Href = href;
            Name = name;
            this.client = client;
            this.site = site;
            this.story = story;
        }

        public async Task<IEnumerable<Post>> GetPostDetails()
        {
            var doc = await FetchCategoryPage();
            var csrfToken = (doc.GetElementById("XF") as IHtmlHtmlElement).Dataset["csrf"];

            int.TryParse(doc.QuerySelector(".dataList-cell--min").InnerHtml, out var markCount);
            Console.WriteLine($"Category {Id}: {Name} ({markCount} threadmarks)");

            var relevantLinks = doc.QuerySelectorAll(".structItem--threadmark a");
            var posts = await FetchPosts(relevantLinks, csrfToken, doc);
            
            return posts;
        }
        
        private async Task<IHtmlDocument> FetchCategoryPage()
        {
            var response = await client.GetAsync(Href);
            var content = await response.Content.ReadAsStringAsync();

            var p = new HtmlParser();
            var doc = await p.ParseDocumentAsync(content);
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
                .Select(l => new Post(l.Href, l.InnerHtml, this, story, site, client))
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
                var l = site.BaseUrl + fetchUrl;

                var form = site.GetHtmlPostData(story.BaseUrl, csrfToken);

                var fq = new HttpRequestMessage(HttpMethod.Post, l);
                fq.Content = form;
                fq.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");

                var fr = await client.SendAsync(fq);
                var fc = await fr.Content.ReadAsStringAsync();

                File.WriteAllText("dump.json", fc);

                var hhhh = (string)JObject.Parse(fc)["html"]["content"];

                var fp = new HtmlParser();
                var fdoc = await fp.ParseDocumentAsync(hhhh);
                fdoc.Location.Href = l;

                return await FetchPosts(fdoc.Links, csrfToken, fdoc);
            }

            return new Post[0];
        }
    }
}