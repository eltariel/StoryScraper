using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using StoryScraper.Core;
using StoryScraper.Core.Conversion;

namespace StoryScraper.Cli
{
    public class Runner
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        public Runner(Pandoc pandoc, Config config, KindleGen kindlegen, SiteFactory siteFactory)
        {
            var groupedUrls  = new Dictionary<Uri, List<string>>();
            var stories = new Dictionary<Uri, IStory>();


            GroupedUrls = groupedUrls;
            Stories = stories;
            Log = log;
            Pandoc = pandoc;
            Config = config;
            Kindlegen = kindlegen;
            SiteFactory = siteFactory;
        }

        public Dictionary<Uri, List<string>> GroupedUrls { get; }
        public Dictionary<Uri, IStory> Stories { get; }
        public Logger Log { get; }
        public Pandoc Pandoc { get; }
        public Config Config { get; }
        public KindleGen Kindlegen { get; }

        public SiteFactory SiteFactory { get; }

        public async Task FetchStoryDetails()
        {
            foreach (var (source, urls) in Config.Urls)
            {
                foreach (var url in urls)
                {
                    try
                    {
                        Log.Info($"Attempting to fetch story from {url}");

                        var site = SiteFactory.GetSiteFor(url);
                        var story = await site.GetStory(url);

                        Stories[story.Url] = story;

                        if (!GroupedUrls.TryGetValue(story.Url, out var sources))
                        {
                            sources = new List<string>();
                            GroupedUrls[story.Url] = sources;
                        }

                        sources.Add(source);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(ex, $"Exception fetching story from {url}");
                    }
                }
            }
        }

        public async Task FetchStoryContents()
        {
            foreach (var (_, story) in Stories)
            {
                Log.Info($"Fetching posts for \"{story.Title}\" [{story.Url}]");
                await story.GetPosts();

                Log.Info($"Found {story.Categories.Count} categories:");
                foreach (var cat in story.Categories)
                {
                    Log.Info($"\t- {cat.Name} ({cat.PostCount} posts)");
                }
            }
        }

        public void ConvertStories()
        {
            foreach (var (url, sources) in GroupedUrls)
            {
                try
                {
                    var story = Stories[url];
                    Log.Info($"Generating EPUB for for \"{story.Title}\" [{story.Url}]");
                    var epubPath = Pandoc.ToEpub(story);
                    var epubFileName = Path.GetFileName(epubPath);
                    foreach (var dir in sources.Select(source => Path.Combine(Config.OutDir, source)))
                    {
                        Log.Debug($"Copying epub from cache {epubPath} to {dir}");
                        Directory.CreateDirectory(dir);
                        var dest = Path.Combine(dir, epubFileName);
                        try
                        {
                            File.Copy(epubPath, dest, false);
                        }
                        catch (IOException)
                        {
                            if (File.GetLastWriteTimeUtc(epubPath) > File.GetLastWriteTimeUtc(dest))
                            {
                                File.Copy(epubPath, dest, true);
                            }
                        }
                    }

                    Kindlegen.ToMobi(story);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed url {url}: {ex}");
                }
            }
        }
    }
}