using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Newtonsoft.Json;
using NLog;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class Category : ICategory
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public Category(string categoryId, string name, Story story)
        {
            Story = story;
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
        public int PostCount => Posts.Count;

        [JsonIgnore]
        List<IPost> ICategory.Posts => Posts.Cast<IPost>().ToList();
        
        public List<Post> Posts { get; private set; } = new List<Post>();
                
        [JsonIgnore] public Site Site => Story.Site;

        private Uri BaseReaderLink => new Uri($"{Story.Url}{(CategoryId == "1" ? "" : $"{CategoryId}/")}reader/");

        internal Uri LastReaderPage =>
            Posts.Any(p => p.Refetch)
                ? BaseReaderLink
                : Posts
                      .OrderByDescending(p =>
                          p.ReaderUrl.LastIndexOf("page-", StringComparison.InvariantCulture) is {} dash && dash > 0
                              ? int.Parse(p.ReaderUrl.Substring(dash + 5))
                              : 1)
                      .Select(p => new Uri(p.ReaderUrl))
                      .FirstOrDefault()
                  ?? BaseReaderLink;

        public async Task<IEnumerable<IPost>> GetPosts()
        {
            var postDic = Posts.ToDictionary(p => p.PostId);
            foreach (var post in await GetReaderPage(Url.Convert(LastReaderPage)))
            {
                postDic[post.PostId] = post;
            }

            Posts = postDic.Values.ToList();
            return Posts;
        }

        private async Task<IEnumerable<Post>> GetReaderPage(Url readerUrl)
        {
            var doc = await Site.Context.OpenAsync(Url.Convert(readerUrl));

            var postArticles = doc.QuerySelectorAll("article.message.hasThreadmark");
            var posts = Enumerable.Empty<Post>();
            foreach (var article in postArticles)
            {
                posts = posts.Append(await Post.PostFromArticle(article, readerUrl, this));
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