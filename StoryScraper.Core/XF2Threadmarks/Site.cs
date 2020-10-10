using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using StoryScraper.Core.Conversion;

namespace StoryScraper.Core.XF2Threadmarks
{
    public abstract class Site : BaseSite
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        protected Site(string name, Uri baseUrl, IDictionary<string, string> categoryIds, IConfig config, Pandoc pandoc)
            : base(name, baseUrl, config, pandoc)
        {
            CategoryIds = categoryIds
                .Where(a => !config.ExcludedCategories.Contains(a.Key))
                .ToDictionary(c => c.Key, c => c.Value);

            Directory.CreateDirectory(Cache.Root);
        }

        public IDictionary<string, string> CategoryIds { get; }

        public override async Task<IStory> GetStory(Uri url)
        {
            var cached = Story.GetCachedStory(this, GuessStoryIdFrom(url));
            return cached ?? await Story.FromUrl(url, this);
        }

        private static string GuessStoryIdFrom(Uri url)
        {
            var regex = new Regex(@"^/threads/.*\.(?<id>\d*)/?");
            var m = regex.Match(url.AbsolutePath);
            return m.Success ? m.Groups["id"].Value : null;
        }
    }
}