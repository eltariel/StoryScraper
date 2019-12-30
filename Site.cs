using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace threadmarks_thing
{
    public class Site
    {
        private static readonly CookieContainer cookieContainer = new CookieContainer();
        private static readonly HttpClientHandler clientHandler = new HttpClientHandler() { CookieContainer = cookieContainer };
        private static readonly HttpClient client = new HttpClient(clientHandler);

        public Site(string name, string baseUrl)
        {
            Name = name;
            BaseUrl = baseUrl;
        }

        public string BaseUrl { get; }
        public string Name { get; }

        public Story GetStory(string url)
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
