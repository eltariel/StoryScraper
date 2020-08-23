using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Common;
using Mono.Options;
using StoryScraper.Core.Conversion;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var urls = new List<Uri>
            {
                // new Uri("https://forums.sufficientvelocity.com/threads/mauling-snarks-worm.41471/"),
                // new Uri("https://forums.spacebattles.com/threads/going-for-a-walk-worm-hellsing-ultimate-abridged.812348/"),
                // new Uri("https://forums.sufficientvelocity.com/threads/taylor-varga-worm-luna-varga.32119/"),
            };
            
            var config = Config.ParseArgs(args, urls);
            if (config == null) return;

            var siteFactory = new SiteFactory(config);
            var pandoc = new Pandoc(config);
            var kindlegen = new KindleGen(config);
            
            foreach (var url in config.Urls)
            {
                try
                {
                    Console.WriteLine($"Attempting to fetch story from {url}");
                
                    var site = siteFactory.GetSiteFor(url);
                    var story = await site.GetStory(url);

                    Console.WriteLine($"Found {story.Categories.Count} categories:");
                    foreach (var cat in story.Categories)
                    {
                        Console.WriteLine($"\t- {cat.Name} ({cat.PostCount} posts)");
                    }

                    var interestingCategories = story
                        .Categories
                        .Where(c => !config.ExcludedCategories.Contains(c.Name))
                        .ToList();

                    foreach (var cat in interestingCategories)
                    {
                        await cat.GetPosts();
                    }

                    pandoc.ToEpub(story);

                    if (!config.SkipMobi)
                    {
                        kindlegen.ToMobi(story);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed url: {ex}");
                }
            }
        }
    }
}
