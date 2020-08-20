using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryScraper.Core
{
    public class SiteFactory
    {
        private readonly Site[] sites;

        public SiteFactory(Config config)
        {
            sites = new[]{
                new Site ("Sufficient Velocity", new Uri("https://forums.sufficientvelocity.com"), config),
                new Site ("Space Battles", new Uri("https://forums.spacebattles.com"), config)
            };
        }

        public Site GetSiteFor(Uri url)
        {
            return sites.FirstOrDefault(s => url.Host == s.BaseUrl.Host) ??
                   throw new Exception($"Can't find handler for site {url.Host}");
        }
    }
}
