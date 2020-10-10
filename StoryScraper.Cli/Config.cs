using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Mono.Options;
using NLog;
using StoryScraper.Core;

namespace StoryScraper.Cli
{
    public class Config : IConfig
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        private Config(Dictionary<string, List<Uri>> urls,
            List<string> excludedCategories,
            string cachePath,
            string outDir,
            string pandocPath,
            string kindleGenPath,
            int verbosity,
            bool useWsl,
            bool skipMobi)
        {
            ExcludedCategories = excludedCategories;
            Urls = urls;
            if (excludedCategories.Count == 0)
            {
                excludedCategories.AddRange(new [] {"Staff Post", "Media"});
            }
            
            CachePath = Path.GetFullPath(cachePath ?? Path.Combine(Environment.CurrentDirectory, "cache"));
            PandocPath = pandocPath ?? "pandoc";
            KindleGenPath = kindleGenPath ?? "kindlegen";
            Verbosity = verbosity;
            LogLevel = Verbosity switch
            {
                0 => LogLevel.Info,
                1 => LogLevel.Debug,
                _ => LogLevel.Trace
            };
            UseWsl = useWsl;
            SkipMobi = skipMobi;
            OutDir = Path.GetFullPath(outDir ?? Environment.CurrentDirectory);

            log.Debug("Options:");
            log.Debug($"  Excluded Categories = {string.Join(',', ExcludedCategories)}");
            log.Debug($"  Output Path =         {OutDir}");
            log.Debug($"  Cache Path =          {CachePath}");
            log.Debug($"  Pandoc path =         {PandocPath}");
            log.Debug($"  KindleGen path =      {KindleGenPath}");
            log.Debug($"  Use WSL =             {UseWsl}");
            log.Debug($"  Verbosity           = {LogLevel} ({Verbosity})");
            log.Debug($"  Skip .mobi creation = {SkipMobi}");
        }

        public List<string> ExcludedCategories { get; }
        public Dictionary<string, List<Uri>> Urls { get; }
        public string CachePath { get; }
        public string PandocPath { get; }
        public string KindleGenPath { get; }
        public bool UseWsl { get; }
        public bool SkipMobi { get; }
        public int Verbosity { get; }
        public LogLevel LogLevel { get; }
        public string OutDir { get; }

        public static Config ParseArgs(string[] args, IConfigurationRoot cfg)
        {
            var cachePath = cfg["paths:cache"];
            var outDir = cfg["paths:out"];
            var pandocPath = cfg["paths:pandoc"];
            var kindlegenPath = cfg["paths:kindlegen"];
            
            var useWsl = cfg.GetValue<bool>("behaviour:use_wsl");
            var verbosity = cfg.GetValue<int>("behaviour:verbosity");
            var skipMobi = cfg.GetValue<bool>("behaviour:skip_mobi");
            var excludedCategories = cfg.GetSection("behaviour:skip_categories").Get<string[]>()?.ToList() ??
                                     new List<string>();
            var showHelp = false;

            var urls = new Dictionary<string, List<Uri>>
                {{"_", cfg.GetSection("input:urls").Get<Uri[]>()?.ToList() ?? new List<Uri>()}};
            var urlFiles = cfg.GetSection("input:files").Get<string[]>()?.ToList() ?? new List<string>();

            var options = new OptionSet
            {
                {"c|cache-path=", "Location for cache", v => cachePath = v},
                {"u|url-file=", "File containing URLs to check, one per line. Can be combined with urls on the command line.", v => urlFiles.Add(v)},
                {
                    "x|exclude-category=",
                    "Category to exclude from the generated ebook. Can be specified multiple times. Defaults to 'Staff Post' and 'Media' if nothing specified.",
                    v => excludedCategories.Add(v)
                },
                {"o|out-dir=", "epub output path", v => outDir = v},
                {"p|pandoc-path=", "Path to pandoc (html -> epub)", v => pandocPath = v},
                {"k|kindlegen-path=", "Path to KindleGen (epub -> mobi)", v => kindlegenPath = v},
                {"w|use-wsl", "Use pandoc in WSL rather than native.", v => useWsl = v != null},
                {"v|verbose", "Increase Verbosity.", v => { if (v != null) ++verbosity; }},
                {"skip-mobi", "Skip creating .mobi with kindlegen", v => skipMobi = v != null},
                {"h|help", "Show help", v => showHelp = v != null}
            };

            try
            {
                var extra = options.Parse(args);
                urls["_"] = extra.Select(u => new Uri(u)).ToList();
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
                foreach (var file in urlFiles)
                {
                    var baseName = Path.GetFileNameWithoutExtension(file);
                    urls[baseName] = File.ReadAllLines(file)
                        .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var url) ? url : null)
                        .Where(u => u != null)
                        .ToList();
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

            return new Config(urls, excludedCategories, cachePath, outDir, pandocPath, kindlegenPath, verbosity, useWsl,
                skipMobi);
        }
    }
}
