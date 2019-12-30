using System;
using System.Linq;

namespace threadmarks_thing
{
    public class SiteFactory
    {        
        private static readonly Site[] sites = new[] {
            new Site ("Sufficient Velocity", "https://forums.sufficientvelocity.com")
        };

        public static Site GetSiteFor(string url)
        {
            return sites.First(s => url.StartsWith(s.BaseUrl));
        }
    }
}
