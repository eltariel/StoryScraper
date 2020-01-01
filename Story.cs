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

        public async Task GetPosts()
        {
            var page = await GetStoryPage();
            var categories = await GetCategories(page);

            foreach (var (id, cat) in categories)
            {
                var posts = await cat.GetPostDetails();
            }
        }

        public async Task<string> GetStoryPage()
        {
            return await site.GetAsync(url);
        }

        private async Task<IDictionary<string, Category>> GetCategories(string storyPage)
        {
            var p = new HtmlParser();
            var doc = await p.ParseDocumentAsync(storyPage);
            doc.Location.Href = url.ToString();

            var categories = doc.Links.OfType<IHtmlAnchorElement>()
                .Where(l => l.Id?.Contains("threadmark-category") ?? false)
                .Select(l => new Category(l.Id, l.Href, name: l.InnerHtml, client, site, this))
                .ToDictionary(l => l.Id);

            return categories;
        }
    }
}
