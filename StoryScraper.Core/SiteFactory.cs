using System;
using System.Linq;
using StoryScraper.Core.XF2Threadmarks;

namespace StoryScraper.Core
{
    public class SiteFactory
    {
        private readonly BaseSite[] sites;

        public SiteFactory(Config config)
        {
            sites = new BaseSite[]{
                new SufficientVelocity(config),
                new SpaceBattles(config)
            };
        }

        public BaseSite GetSiteFor(Uri url)
        {
            return sites.FirstOrDefault(s => url.Host == s.BaseUrl.Host) ??
                   throw new Exception($"Can't find handler for site {url.Host}");
        }
    }
}
