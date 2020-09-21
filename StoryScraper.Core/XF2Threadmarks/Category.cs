using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class Category : ICategory
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        private readonly Config config;

        public Category(string categoryId, string name, Story story, Config config, int postCount = 0)
        {
            this.config = config;
            Story = story;
            PostCount = postCount;
            CategoryId = categoryId;
            Name = name;
        }

        public string CategoryId { get; }
        public string Name { get; }

        public DateTime LastUpdate => Posts.Select(p => p.UpdatedAt)
            .OrderByDescending(p => p)
            .FirstOrDefault();
 
        [JsonIgnore]
        public Story Story { get; set; }
        
        [JsonIgnore]
        public Site Site => Story.Site;
        
        public int PostCount { get; private set; }

        [JsonIgnore]
        List<IPost> ICategory.Posts => Posts.Cast<IPost>().ToList();
        
        public List<Post> Posts { get; } = new List<Post>();

        private Uri BaseReaderLink => new Uri($"{Story.Url}{(CategoryId == "1" ? "" : $"{CategoryId}/")}reader/");

        public async Task GetDetails()
        {
            Uri readerUrl = BaseReaderLink;
            var doc = await Site.Context.OpenAsync(Url.Convert(readerUrl));
            IDocument temp = doc;
        }

        public async Task<IEnumerable<IPost>> GetPosts()
        {
            Posts.Clear();

            var posts = await GetReaderPage(Url.Convert(BaseReaderLink));

            Posts.AddRange(posts);
            PostCount = Posts.Count;

            return Posts;
        }

        private async Task<IEnumerable<Post>> GetReaderPage(Url readerUrl)
        {
            var doc = await Site.Context.OpenAsync(Url.Convert(readerUrl));

            var postArticles = doc.QuerySelectorAll("article.message.hasThreadmark");
            var posts = Enumerable.Empty<Post>();
            foreach (var article in postArticles)
            {
                posts = posts.Append(await Post.PostFromArticle(article, this, config));
            }
            
            var next = doc.QuerySelector<IHtmlLinkElement>("link[rel=next]");
            if (next != null)
            {
                posts = posts.Concat(await GetReaderPage(new Url(next.Href)));
            }

            return posts;
        }
    }
}