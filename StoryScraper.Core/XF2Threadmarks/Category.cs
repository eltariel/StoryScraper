using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class Category : ICategory
    {
        private readonly Config config;

        public Category(string categoryId, string name, Story xf2Story, Config config, int postCount = 0)
        {
            this.config = config;
            Xf2Story = xf2Story;
            PostCount = postCount;
            CategoryId = categoryId;
            Name = name;
        }

        public string CategoryId { get; }
        public string Name { get; }

        public DateTime LastUpdate => Posts.Select(p => p.Timestamp).OrderByDescending(p => p).FirstOrDefault();
                 

        [JsonIgnore]
        public IStory Story => Xf2Story;
        
        [JsonIgnore]
        public Story Xf2Story { get; set; }
        
        [JsonIgnore]
        public Site Xf2Site => Xf2Story.Xf2Site;
        
        public int PostCount { get; private set; }

        [JsonIgnore]
        public List<IPost> Posts => Xf2Posts.Cast<IPost>().ToList();
        
        public List<Post> Xf2Posts { get; } = new List<Post>();

        public Uri Href => new Uri($"{Xf2Story.Url}threadmarks?threadmark_category={CategoryId}");
        public Uri RssLink => new Uri($"{Xf2Story.Url}threadmarks.rss?threadmark_category_id={CategoryId}");

        public async Task GetDetails()
        {
            await FetchCategoryPage();
        }

        public async Task<IEnumerable<IPost>> GetPosts()
        {
            Posts.Clear();

            var doc = await FetchCategoryPage();

            var csrfToken = doc.QuerySelector<IHtmlHtmlElement>("#XF")?.Dataset["csrf"];
            var relevantLinks = doc.QuerySelectorAll<IHtmlDivElement>("div.structItem--threadmark");
            var ps = await FetchPosts(relevantLinks, csrfToken);
            
            Xf2Posts.AddRange(ps.OrderBy(p => p.Timestamp));
            return Posts;
        }

        private async Task<IDocument> FetchCategoryPage()
        {
            var doc = await Xf2Story.Context.OpenAsync(Href.ToString());
            
            var postCount = doc.QuerySelectorAll("td.dataList-cell--min");
            PostCount = postCount.Aggregate(0, (posts, element) =>
            {
                int.TryParse(element.InnerHtml, out var count);
                return posts + count;
            });
            
            return doc;
        }

        private async Task<IEnumerable<Post>> FetchPosts(
            IEnumerable<IHtmlDivElement> relevantLinks,
            string csrfToken)
        {
            var fetchedPosts = new List<Post>(PostCount);
            foreach (var div in relevantLinks)
            {
                if (div.QuerySelector<IHtmlDivElement>("div[data-xf-click='threadmark-fetcher']") is {} fetcher)
                {
                    var fetchUrl = fetcher.Dataset["fetchurl"];
                    var ret = await ParseAdditionalPosts(csrfToken, fetchUrl);
                    fetchedPosts.AddRange(ret);
                }
                else
                {
                    var titleLink = div.QuerySelector<IHtmlAnchorElement>(".structItem-title a");
                    var timestampElement = div.QuerySelector<IHtmlTimeElement>(".structItem-cell--latest time");
                    var author = div.Dataset["content-author"];
                    DateTime.TryParse(timestampElement.DateTime, out var timestamp);
                    var p = new Post(titleLink.Href, titleLink.InnerHtml, author, timestamp, this, config);
                    await p.FetchContent(csrfToken);
                    fetchedPosts.Add(p);
                }
            }

            return fetchedPosts;
        }

        private async Task<IEnumerable<Post>> ParseAdditionalPosts(string csrfToken, string fetchUrl)
        {
            var url = new Uri(Xf2Site.BaseUrl, fetchUrl);
            var form = Xf2Site.GetHtmlPostData(Story.BaseUrl, csrfToken);

            var json = await Xf2Site.PostAsync(url, form);

            if ((string) JObject.Parse(json)["html"]?["content"] is {} content)
            {
                var doc = await Xf2Story.Context.OpenAsync(res => res.Content(content).Address(url));
                return await FetchPosts(doc.QuerySelectorAll<IHtmlDivElement>("div.structItem--threadmark"), csrfToken);
            }

            return Enumerable.Empty<Post>();
        }
    }
}