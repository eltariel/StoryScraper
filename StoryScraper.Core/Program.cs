using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Common;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //var url = new Uri("https://forums.sufficientvelocity.com/threads/mauling-snarks-worm.41471/");
            //var url = new Uri("https://forums.spacebattles.com/threads/going-for-a-walk-worm-hellsing-ultimate-abridged.812348/");
            var url = new Uri("https://forums.sufficientvelocity.com/threads/taylor-varga-worm-luna-varga.32119/");

            var excludedCategories = new[] {"Staff Post", "Media"};
            
            var site = SiteFactory.GetSiteFor(url);
            var story = await site.GetStory(url);

            Console.WriteLine($"Found {story.Categories.Count} categories:");
            foreach (var cat in story.Categories)
            {
                Console.WriteLine($"\t- {cat.Name} ({cat.PostCount} posts)");
            }

            var interestingCategories = story
                .Categories
                .Where(c => !excludedCategories.Contains(c.Name))
                .ToList();

            foreach (var cat in interestingCategories)
            {
                await cat.GetPosts();
            }

            // var orderedPosts = story
            //     .Posts
            //     .Where(p => interestingCategories.Contains(p.Category))
            //     .OrderBy(p => p.Timestamp)
            //     .ToList();

            var title = story.Title.ToValidPath();
            // var outPath = $"out/{title}";
            // var tocPath = $"{outPath}/{title}.html";
            //
            // Directory.CreateDirectory(outPath);
            // await using var f = new StreamWriter(tocPath);
            // await f.WriteAsync($"<html><head><title>{story.Title}</title></head><body><h1>{story.Title}</h1><h2>Contents</h2><ul>");
            // foreach (var post in orderedPosts)
            // {
            //     var postFile = $"post-{post.PostId}.html";
            //     var postPath = $"{outPath}/{postFile}";
            //     
            //     await f.WriteAsync($"<li><a href=\"{postFile}\">{post.Category.Name}: {post.Title}</a></li>");
            //     await File.WriteAllTextAsync(postPath,
            //         $"<html><head><title>{story.Title}</title></head>" +
            //         $"<body><h2>{post.Category.Name}: {post.Title}</h2>{post.Content}</body></html>");
            // }
            // await f.WriteAsync("</ul></body></html>");

            var pandoc = new Pandoc(useWsl: false);
            pandoc.ToEpub(title, story, excludedCategories);
        }
    }
}
