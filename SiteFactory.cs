using System;
using System.Linq;

namespace threadmarks_thing
{
    public class SiteFactory
    {        
        private static readonly Site[] sites = new[] {
            new Site ("Sufficient Velocity", new Uri("https://forums.sufficientvelocity.com"))
        };

        public static Site GetSiteFor(Uri url)
        {
            return sites.First(s => url.Host == s.BaseUrl.Host);
        }
    }
}
