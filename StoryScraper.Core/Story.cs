﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace StoryScraper.Core
{
    public class Story
    {
        private readonly Uri url;
        private readonly Site site;
        private readonly HttpClient client;
        private readonly Config config;

        public Story(Uri url, Site site, HttpClient client, Config config)
        {
            this.url = url;
            this.site = site;
            this.client = client;
            this.config = config;

            BaseUrl = url.ToString().Replace(site.BaseUrl.ToString(), "");
        }

        public string BaseUrl { get; }
        
        public string Title { get; private set; }
        public string Author { get; private set; }

        public IEnumerable<Post> Posts => Categories.SelectMany(c => c.Posts);

        public List<Category> Categories { get; } = new List<Category>();

        public async Task GetCategories()
        {
            var page = await GetStoryPage();
            var categories = await ParseStory(page);
            
            Categories.Clear();
            Categories.AddRange(categories);
        }

        private async Task<string> GetStoryPage()
        {
            return await site.GetAsync(url);
        }

        private async Task<List<Category>> ParseStory(string storyPage)
        {
            var p = new HtmlParser();

            var context = BrowsingContext.New(Configuration.Default);
            var doc = await context.OpenAsync(res => res.Content(storyPage).Address(url));

            var titleElem = doc.QuerySelector<IHtmlHeadingElement>("h1.p-title-value");
            Title = (titleElem.TextContent ?? "Unknown").Trim();
            var authorElem = doc.QuerySelector<IHtmlAnchorElement>(".username.u-concealed");
            Author = (authorElem.TextContent ?? "Unknown").Trim();

            var categories = doc.QuerySelectorAll<IHtmlAnchorElement>("a[data-categoryid]")
                .Select(a => (name: a.Text, id: a.Attributes["data-categoryid"].Value, href: a.Href))
                .Distinct()
                .Select(a => new Category(a.id, a.href, name: a.name, site, this, config))
                .ToList();

            foreach (var cat in categories)
            { 
                await cat.GetDetails();
            }

            return categories;
        }
    }
}
