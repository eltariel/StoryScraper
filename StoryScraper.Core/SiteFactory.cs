using System;
using System.Linq;

namespace StoryScraper.Core
{
    public class SiteFactory
    {        
        private static readonly Site[] sites = {
            new Site ("Sufficient Velocity", new Uri("https://forums.sufficientvelocity.com")),
            new Site ("Space Battles", new Uri("https://forums.spacebattles.com"))
        };

        public static Site GetSiteFor(Uri url)
        {
            return sites.FirstOrDefault(s => url.Host == s.BaseUrl.Host) ??
                   throw new Exception($"Can't find handler for site {url.Host}");
        }
    }
}
