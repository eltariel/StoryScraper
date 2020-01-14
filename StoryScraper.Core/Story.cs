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

        public IEnumerable<Post> Posts => Categories.SelectMany(c => c.Posts);

        public List<Category> Categories { get; } = new List<Category>();

        public async Task GetCategories()
        {
            var page = await GetStoryPage();
            var categories = await ParseStory(page);
            
            Categories.Clear();
            Categories.AddRange(categories);
        }

        public async Task GetPosts()
        {
            foreach (var cat in Categories)
            {
                var posts = await cat.GetPosts();
            }
        }

        private async Task<string> GetStoryPage()
        {
            return await site.GetAsync(url);
        }

        private async Task<List<Category>> ParseStory(string storyPage)
        {
            var p = new HtmlParser();
            var doc = await p.ParseDocumentAsync(storyPage);
            doc.Location.Href = url.ToString();

            var titleElem = doc.QuerySelector<IHtmlHeadingElement>("h1.threadmarkListingHeader-name");
            Title = (titleElem.TextContent ?? "Unknown").Trim();
            
            var categories = doc.Links.OfType<IHtmlAnchorElement>()
                .Where(l => l.Id?.Contains("threadmark-category") ?? false)
                .Select(l => new Category(l.Id, l.Href, name: l.InnerHtml, site, this))
                .ToList();

            foreach (var cat in categories)
            { 
                await cat.GetDetails();
            }

            return categories;
        }
    }
}
