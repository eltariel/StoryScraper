using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Targets;
using StoryScraper.Core;
using StoryScraper.Core.Conversion;

namespace StoryScraper.Cli
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var cfg = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();
            
            var config = Config.ParseArgs(args, cfg);
            if (config == null) return;

            // Initial config
            UpdateNlogConfig(config);
            LogManager.ConfigurationReloaded += (sender, e) =>
            {
                // Redo config mods if config file is reloaded
                UpdateNlogConfig(config);
            };
            
            var pandoc = new Pandoc(config);
            var kindlegen = new KindleGen(config);
            var kepubify = new Kepubify(config);
            var siteFactory = new SiteFactory(config, pandoc);

            var runner = new Runner(pandoc, config, kindlegen, kepubify, siteFactory);
            await runner.FetchStoryDetails();
            await runner.FetchStoryContents();
            runner.ConvertStories();
        }

        private static void UpdateNlogConfig(IConfig config)
        {
            var cfg = LogManager.Configuration;
            var consoleRules = cfg.LoggingRules
                .Where(r => r.Targets.Any(t => t is ConsoleTarget || t is ColoredConsoleTarget));
            foreach (var rule in consoleRules)
            {
                rule.SetLoggingLevels(config.LogLevel, LogLevel.Fatal);
            }

            LogManager.Configuration = cfg;
        }
    }
}
