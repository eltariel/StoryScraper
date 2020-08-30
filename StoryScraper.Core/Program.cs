using System;
using System.Threading.Tasks;
using NLog;
using StoryScraper.Core.Conversion;

namespace StoryScraper.Core
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var config = Config.ParseArgs(args);
            if (config == null) return;

            var log = LogManager.GetCurrentClassLogger();
            
            var siteFactory = new SiteFactory(config);
            var pandoc = new Pandoc(config);
            var kindlegen = new KindleGen(config);
            
            foreach (var url in config.Urls)
            {
                try
                {
                    log.Info($"Attempting to fetch story from {url}");
                
                    var site = siteFactory.GetSiteFor(url);
                    var story = await site.GetStory(url);

                    log.Info($"Found {story.Categories.Count} categories:");
                    foreach (var cat in story.Categories)
                    {
                        log.Info($"\t- {cat.Name} ({cat.PostCount} posts)");
                    }

                    pandoc.ToEpub(story);

                    if (!config.SkipMobi)
                    {
                        kindlegen.ToMobi(story);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Failed url {url}: {ex}");
                }
            }
        }
    }
}
