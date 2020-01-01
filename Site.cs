using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace threadmarks_thing
{
    public class Site
    {
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        private static readonly HttpMessageHandler clientHandler = new RateLimitHandler(
            new HttpClientHandler() { CookieContainer = cookieContainer });
        private static readonly HttpClient client = new HttpClient(clientHandler);

        public Site(string name, Uri baseUrl)
        {
            Name = name;
            BaseUrl = baseUrl;
        }

        public Uri BaseUrl { get; }
        public string Name { get; }

        public async Task<string> GetAsync(Uri url)
        {
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PostAsync(Uri url, HttpContent data)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = data;
            req.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            
            var response = await client.SendAsync(req);
            return await response.Content.ReadAsStringAsync();
        }

        public Story GetStory(Uri url)
        {
            var s = new Story(url, this, client);
            return s;
        }

        public IDictionary<string, string> GetCommonParams(string url, string csrfToken) =>
            new Dictionary<string, string> {
                {"_xfRequestUri", url},
                {"_xfWithData", "1"},
                {"_xfToken", csrfToken},
                {"_xfResponseType", "json"},
            };

        public FormUrlEncodedContent GetHtmlPostData(string url, string csrfToken) =>
            new FormUrlEncodedContent(GetCommonParams(url, csrfToken)
        );
    }
}
