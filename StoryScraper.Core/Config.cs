using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;

namespace StoryScraper.Core
{
    public class Config
    {
        public Config(List<Uri> urls,
            List<string> excludedCategories,
            string cachePath,
            string pandocPath,
            string kindleGenPath,
            bool useWsl,
            bool skipMobi)
        {
            ExcludedCategories = excludedCategories;
            Urls = urls;
            if (excludedCategories.Count == 0)
            {
                excludedCategories.AddRange(new [] {"Staff Post", "Media"});
            }
            
            CachePath = cachePath ?? "cache";
            PandocPath = pandocPath ?? "pandoc";
            KindleGenPath = kindleGenPath ?? "kindlegen";
            UseWsl = useWsl;
            SkipMobi = skipMobi;

            Console.WriteLine("Options:");
            Console.WriteLine($"  Excluded Categories = {string.Join(',', ExcludedCategories)}");
            Console.WriteLine($"  Cache Path =          {Path.GetFullPath(CachePath)}");
            Console.WriteLine($"  Pandoc path =         {PandocPath}");
            Console.WriteLine($"  KindleGen path =      {KindleGenPath}");
            Console.WriteLine($"  Use WSL =             {UseWsl}");
            Console.WriteLine($"  Skip .mobi creation = {SkipMobi}");
        }

        public List<string> ExcludedCategories { get; }
        public List<Uri> Urls { get; }
        public string CachePath { get; }
        public string PandocPath { get; }
        public string KindleGenPath { get; }
        public bool UseWsl { get; }
        public bool SkipMobi { get; }

        public static Config ParseArgs(string[] args, List<Uri> urls)
        {
            string cachePath = null;
			string urlFile = null;
            var excludedCategories = new List<string>();
            string pandocPath = null;
            string kindlegenPath = null;
            var useWsl = false;
            var skipMobi = false;
            var showHelp = false;

            var options = new OptionSet
            {
                {"c|cache-path=", "Location for cache", v => cachePath = v},
                {"u|url-file=", "File containing URLs to check, one per line. Can be combined with urls on the command line.", v => urlFile = v},
                {
                    "x|exclude-category=",
                    "Category to exclude from the generated ebook. Can be specified multiple times. Defaults to 'Staff Post' and 'Media' if nothing specified.",
                    v => excludedCategories.Add(v)
                },
                {"p|pandoc-path=", "Path to pandoc (html -> epub)", v => pandocPath = v},
                {"k|kindlegen-path=", "Path to KindleGen (epub -> mobi)", v => kindlegenPath = v},
                {"w|use-wsl", "Use pandoc in WSL rather than native.", v => useWsl = v != null},
                {"skip-mobi", "Skip creating .mobi with kindlegen", v => skipMobi = v != null},
                {"h|help", "Show help", v => showHelp = v != null}
            };

            try
            {
                var extra = options.Parse(args);
                urls = new List<Uri>(extra.Select(x => new Uri(x)));
            }
            catch (OptionException e)
            {
                Console.Write("Commandline failed to parse: ");
                Console.WriteLine(e.Message);
                Console.WriteLine($"Try '{args[0]} --help' for more information.");
                return null;
            }
			catch (Exception ex)
			{
				Console.WriteLine($"Something went wrong: {ex}");
				return null;
			}

			try
			{
				if(urlFile != null)
				{
					urls.AddRange(File.ReadAllLines(urlFile).Select(u => new Uri(u)));
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Can't read URL file: {ex}");
				return null;
			}

            if (showHelp)
            {
                Console.WriteLine("Story Scraper - forum to ebook\n");
                options.WriteOptionDescriptions(Console.Out);
                return null;
            }

            return new Config(urls, excludedCategories, cachePath, pandocPath, kindlegenPath, useWsl, skipMobi);
        }
    }
}
