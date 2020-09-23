using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;
using NLog;
using NLog.Targets;
using StoryScraper.Core;

namespace StoryScraper.Cli
{
    public class Config : IConfig
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        private Config(List<Uri> urls,
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
            
            CachePath = cachePath ?? Path.Combine(Environment.CurrentDirectory, "cache");
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
            OutDir = outDir ?? Environment.CurrentDirectory;
            
            // On start of your program
            UpdateNlogConfig();
            LogManager.ConfigurationReloaded += (sender, e) =>
            {
                //Re apply if config reloaded
                UpdateNlogConfig();
            };

            log.Debug("Options:");
            log.Debug($"  Excluded Categories = {string.Join(',', ExcludedCategories)}");
            log.Debug($"  Output Path =         {Path.GetFullPath(OutDir)}");
            log.Debug($"  Cache Path =          {Path.GetFullPath(CachePath)}");
            log.Debug($"  Pandoc path =         {PandocPath}");
            log.Debug($"  KindleGen path =      {KindleGenPath}");
            log.Debug($"  Use WSL =             {UseWsl}");
            log.Debug($"  Verbosity           = {LogLevel} ({Verbosity})");
            log.Debug($"  Skip .mobi creation = {SkipMobi}");
        }

        public List<string> ExcludedCategories { get; }
        public List<Uri> Urls { get; }
        public string CachePath { get; }
        public string PandocPath { get; }
        public string KindleGenPath { get; }
        public bool UseWsl { get; }
        public bool SkipMobi { get; }
        public int Verbosity { get; }
        public LogLevel LogLevel { get; }
        public string OutDir { get; }

        private void UpdateNlogConfig()
        {
            var cfg = LogManager.Configuration;
            var consoleRules = cfg.LoggingRules
                .Where(r => r.Targets.Any(t => t is ConsoleTarget || t is ColoredConsoleTarget));
            foreach (var rule in consoleRules)
            {
                rule.SetLoggingLevels(LogLevel, LogLevel.Fatal);
            }
            LogManager.Configuration = cfg;
        }

        public static Config ParseArgs(string[] args)
        {
            List<Uri> urls;
            string cachePath = null;
            string urlFile = null;
            string outDir = null;
            var excludedCategories = new List<string>();
            string pandocPath = null;
            string kindlegenPath = null;
            var useWsl = false;
            var verbosity = 0;
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

            return new Config(urls, excludedCategories, cachePath, outDir, pandocPath, kindlegenPath, verbosity, useWsl,
                skipMobi);
        }
    }
}
