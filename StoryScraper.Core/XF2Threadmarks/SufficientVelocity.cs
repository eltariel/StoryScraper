using System;
using System.Collections.Generic;

namespace StoryScraper.Core.XF2Threadmarks
{
    public class SufficientVelocity : Site
    {
        public SufficientVelocity(Config config)
            : base("Sufficient Velocity",
                new Uri("https://forums.sufficientvelocity.com"),
                new Dictionary<string, int>
                {
                    {"Threadmarks", 1},
                    {"Staff Post", 2},
                    {"Media", 3},
                    {"Apocrypha", 4},
                    {"Sidestory", 5},
                    {"Informational", 6},
                },
                config)
        {
        }
    }
}