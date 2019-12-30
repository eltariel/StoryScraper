using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using Newtonsoft.Json.Linq;

namespace threadmarks_thing
{
    public class Post
    {
        private readonly HttpClient client;

        public Post(string href, string name, Category category, Story story, Site site, HttpClient client)
        {
            Href = href;
            Name = name;
            Category = category;
            Story = story;
            Site = site;
            this.client = client;
        }

        public string Href { get; }
        public string Name { get; }
        public Category Category { get; }
        public Story Story { get; }
        public Site Site { get; }
        public string Content { get; private set; }

        public async Task FetchContent(string csrfToken)
        {
            Console.WriteLine($"Fetching content for '{Name}'");
            var postId = Href.Substring(Href.LastIndexOf("post-") + 5);

            var queryParams = Site.GetCommonParams(Story.BaseUrl, csrfToken);
            var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
            var url = $"{Site.BaseUrl}/posts/{postId}/preview-threadmark?{queryString}";

            var fq = new HttpRequestMessage(HttpMethod.Get, url);
            fq.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");

            var fr = await client.SendAsync(fq);
            var fc = await fr.Content.ReadAsStringAsync();

            File.WriteAllText($"post-{postId}.json", fc);

            var hhhh = (string)JObject.Parse(fc)["html"]["content"];

            Content = hhhh;
            File.WriteAllText($"post-{postId}.html", hhhh);
//            return doc;
        }
    }
}
