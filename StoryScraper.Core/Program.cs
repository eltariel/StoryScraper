using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
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
            string cachePath = null;
            var urls = new List<Uri>();
            var excludedCategories = new List<string>();
            string pandocPath = null;
            string kindlegenPath = null;
            var useWsl = false;
            var showHelp = false;

            urls.AddRange(new[]
            {
                new Uri("https://forums.sufficientvelocity.com/threads/mauling-snarks-worm.41471/"),
                new Uri("https://forums.spacebattles.com/threads/going-for-a-walk-worm-hellsing-ultimate-abridged.812348/"),
                new Uri("https://forums.sufficientvelocity.com/threads/taylor-varga-worm-luna-varga.32119/"),
            });
            
            var options = new OptionSet
            {
                {"c|cache-path=", "Location for cache", v=> cachePath = v},
                {"u|url=", "URL for story. Can be specified multiple times.", v => urls.Add(new Uri(v))},
                {"x|exclude-category=", "Category to exclude from the generated ebook. Can be specified multiple times. Defaults to 'Staff Post' and 'Media' if nothing specified.", v => excludedCategories.Add(v)},
                {"p|pandoc-path=", "Path to pandoc (html -> epub)", v => pandocPath = v},
                {"k|kindlegen-path=", "Path to KindleGen (epub -> mobi)", v => kindlegenPath = v},
                {"w|use-wsl", "Use pandoc in WSL rather than native.", v => useWsl = v != null},
                {"h|help", "Show help", v => showHelp = v != null}
            };
            
            try {
                var extra = options.Parse (args);
                urls.AddRange(extra.Select(x => new Uri(x)));
            } catch (OptionException e) {
                Console.Write("Commandline failed to parse: ");
                Console.WriteLine(e.Message);
                Console.WriteLine($"Try '{args[0]} --help' for more information.");
                return;
            }

            if (showHelp)
            {
                Console.WriteLine("Story Scraper - forum to ebook\n");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }
            
            var config = new Config(excludedCategories, cachePath, pandocPath, kindlegenPath, useWsl);

            var siteFactory = new SiteFactory(config);
            foreach (var url in urls)
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

                var pandoc = new Pandoc(config);
                var kindlegen = new KindleGen(config);
                pandoc.ToEpub(story);
                kindlegen.ToMobi(story);
            }
        }
    }

    public class Config
    {
        public Config(List<string> excludedCategories, string cachePath, string pandocPath, string kindleGenPath, bool useWsl)
        {
            ExcludedCategories = excludedCategories;
            if (excludedCategories.Count == 0)
            {
                excludedCategories.AddRange(new [] {"Staff Post", "Media"});
            }
            
            CachePath = cachePath ?? "cache";
            PandocPath = pandocPath ?? "pandoc";
            KindleGenPath = kindleGenPath ?? "kindlegen";
            UseWsl = useWsl;
            
            Console.WriteLine("Options:");
            Console.WriteLine($"  Excluded Categories = {string.Join(',', ExcludedCategories)}");
            Console.WriteLine($"  Cache Path =          {Path.GetFullPath(CachePath)}");
            Console.WriteLine($"  Pandoc path =         {PandocPath}");
            Console.WriteLine($"  KindleGen path =      {KindleGenPath}");
            Console.WriteLine($"  Use WSL =             {UseWsl}");
        }

        public List<string> ExcludedCategories { get; }
        public string CachePath { get; }
        public string PandocPath { get; }
        public string KindleGenPath { get; }
        public bool UseWsl { get; }
    }
}
