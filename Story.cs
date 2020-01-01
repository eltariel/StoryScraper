using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace StoryScraper
{
    public class Story
    {
        private readonly Uri url;
        private readonly Site site;
        private readonly HttpClient client;

        public Story(Uri url, Site site, HttpClient client)
        {
            this.url = url;
            this.site = site;
            this.client = client;

            BaseUrl = url.ToString().Replace(site.BaseUrl.ToString(), "");
        }

        public string BaseUrl { get; }
        
        public string Title { get; private set; }

        public List<Post> Posts { get; } = new List<Post>();

        public IDictionary<string, Category> Categories { get; } = new Dictionary<string, Category>();


        public async Task GetPosts()
        {
            var page = await GetStoryPage();
            var categories = await ParseStory(page);

            foreach (var (id, cat) in categories)
            {
                Categories[id] = cat;
                var posts = await cat.GetPostDetails();
                Posts.AddRange(posts);
            }
        }

        public async Task<string> GetStoryPage()
        {
            return await site.GetAsync(url);
        }

        private async Task<IDictionary<string, Category>> ParseStory(string storyPage)
        {
            var p = new HtmlParser();
            var doc = await p.ParseDocumentAsync(storyPage);
            doc.Location.Href = url.ToString();

            var titleElem = doc.QuerySelector<IHtmlHeadingElement>("h1.threadmarkListingHeader-name");
            Title = (titleElem.TextContent ?? "Unknown").Trim();
            
            var categories = doc.Links.OfType<IHtmlAnchorElement>()
                .Where(l => l.Id?.Contains("threadmark-category") ?? false)
                .Select(l => new Category(l.Id, l.Href, name: l.InnerHtml, site, this))
                .ToDictionary(l => l.Id);

            return categories;
        }
    }
}
