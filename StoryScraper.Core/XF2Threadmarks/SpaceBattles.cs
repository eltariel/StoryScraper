using System;
using System.Collections.Generic;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class SpaceBattles : Site
    {
        public SpaceBattles(IConfig config)
            : base("Space Battles",
                new Uri("https://forums.spacebattles.com"),
                new Dictionary<string, string>
                {
                    {"Threadmarks", "1"},
                    {"Staff Post", "7"},
                    {"Media", "10"},
                    {"Apocrypha", "13"},
                    {"Sidestory", "16"},
                    {"Informational", "19"},
                },
                config)
        {
        }
    }
}