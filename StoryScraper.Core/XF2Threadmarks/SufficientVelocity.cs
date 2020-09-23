using System;
using System.Collections.Generic;
using AngleSharp;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class SufficientVelocity : Site
    {
        public SufficientVelocity(IConfig config)
            : base("Sufficient Velocity",
                new Uri("https://forums.sufficientvelocity.com"),
                new Dictionary<string, string>
                {
                    {"Threadmarks", "1"},
                    {"Staff Post", "2"},
                    {"Media", "3"},
                    {"Apocrypha", "4"},
                    {"Sidestory", "5"},
                    {"Informational", "6"},
                },
                config)
        {
            Context.SetCookie(Url.Convert(BaseUrl), "xen_user=38778%2CZd66NE1Wd8Khz2hBk_6hBZGVjVrfTYibRi7FGvdp");
        }
    }
}