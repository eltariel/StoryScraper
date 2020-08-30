using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StoryScraper.Core
{
    public interface ICategory
    {
        public string CategoryId { get; }
        
        public string Name { get; }
        
        public DateTime LastUpdate { get; }

        public int PostCount { get; }
        public List<IPost> Posts { get; }
        
        public Task GetDetails();
        public Task<IEnumerable<IPost>> GetPosts();
    }
}
