using System;
using System.Linq;

namespace StoryScraper
{
    public class SiteFactory
    {        
        private static readonly Site[] sites = {
            new Site ("Sufficient Velocity", new Uri("https://forums.sufficientvelocity.com"))
        };

        public static Site GetSiteFor(Uri url)
        {
            return sites.First(s => url.Host == s.BaseUrl.Host);
        }
    }
}
