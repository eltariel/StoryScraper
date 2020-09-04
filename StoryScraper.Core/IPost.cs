using System;
using Newtonsoft.Json;

namespace StoryScraper.Core
{
    public interface IPost
    {
        public string PostId { get; }
        public string Name { get; }
        public string Author { get; }

        public DateTime PostedAt { get; }
        public DateTime UpdatedAt { get; }
        
        [JsonIgnore]
        public bool FromCache { get; }

        [JsonIgnore]
        public string Content { get; }

        [JsonIgnore]
        public string AsHtml { get; }
        
        [JsonIgnore]
        public ICategory Category { get; }
        
        [JsonIgnore]
        public IStory Story { get; }
        
        [JsonIgnore]
        public BaseSite Site { get; }
    }
}
