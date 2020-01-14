using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryScraper.Web.Models
{
    public class StoryViewModel
    {
        private readonly Story story;

        public StoryViewModel(Uri storyUrl, Story story)
        {
            this.story = story;
            StoryUrl = storyUrl;
            Categories = story.Categories.Select(c => new CategoryViewModel(c)).ToList();
        }

        public Uri StoryUrl { get; }

        public string Title => story.Title;

        public string BaseUrl => story.BaseUrl;
        
        public List<CategoryViewModel> Categories { get; }
    }

    public class CategoryViewModel
    {
        private readonly Category category;

        public CategoryViewModel(Category category)
        {
            this.category = category;
        }

        public string Id => category.Id;

        public string Name => category.Name;
    }
}