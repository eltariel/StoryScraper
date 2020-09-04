using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StoryScraper.Core
{
    public interface IStory
    {
        public string StoryId { get; }
        public string BaseUrl { get; }
        
        public string Title { get; }
        public string Author { get; }
        public DateTime LastUpdate { get; }

        public List<ICategory> Categories { get; }
        Uri Url { get; }
        string Image { get; set; }

        public Task GetCategories();
    }
}
