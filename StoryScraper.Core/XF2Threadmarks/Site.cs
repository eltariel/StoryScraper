using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            return await Story.FromUrl(url, this);
        }
    }
}