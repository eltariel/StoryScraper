using System;
using System.Collections.Generic;
using System.Linq;
using StoryScraper.Core;

namespace StoryScraper.Web.Models
{
    public class StoryViewModel
    {
        private readonly IStory story;

        public StoryViewModel(Uri storyUrl, IStory story)
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
        private readonly ICategory category;

        public CategoryViewModel(ICategory category)
        {
            this.category = category;
        }

        public string Id => category.CategoryId;

        public string Name => category.Name;
    }
}