﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StoryScraper.Core;
using StoryScraper.Web.Models;

namespace StoryScraper.Web.Controllers
{
    public class StoryController : Controller
    {
        // GET
        public async Task<IActionResult> Details(string storyUrl)
        {
            var url = new Uri(storyUrl);
            var c = new Config(null, null, null, null, false);
            var site = new SiteFactory(c).GetSiteFor(url);
            var story = await site.GetStory(url);
            
            var model = new StoryViewModel(url, story);
            return View(model);
        }
    }
}