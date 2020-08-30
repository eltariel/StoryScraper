using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Common;
using Mono.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using StoryScraper.Core.Conversion;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var config = Config.ParseArgs(args);
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
                    //
                    // foreach (var cat in story.Categories)
                    // {
                    //     await cat.GetPosts();
                    // }

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
