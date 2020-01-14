using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace StoryScraper
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var url = new Uri("https://forums.sufficientvelocity.com/threads/mauling-snarks-worm.41471/");

            var site = SiteFactory.GetSiteFor(url);
            var story = await site.GetStory(url);
            // await story.GetCategories();

            Console.WriteLine($"Found {story.Categories.Count} categories:");
            foreach (var cat in story.Categories)
            {
                Console.WriteLine($"\t- {cat.Name} ({cat.PostCount} posts)");
            }

            var interestingCategories = story
                .Categories
                .Where(c => !new[] {"Staff Post", "Media"}.Contains(c.Name))
            .ToList();

            foreach (var cat in interestingCategories)
            {
                await cat.GetPosts();
            }

            var orderedPosts = story
                .Posts
                .Where(p => interestingCategories.Contains(p.Category))
                .OrderBy(p => p.Timestamp)
                .ToList();

            var postList = string.Join("\n",
                orderedPosts.Select(p => $"<li><a href='posts/post-{p.PostId}.html'>{p.Title}</a></li>"));

            var indexPage = $"<html><body><h1>{story.Title}</h1><ul>{postList}</ul></body></html>";

            Console.WriteLine(indexPage);
            await File.WriteAllTextAsync("toc.html", indexPage);

            var outPath = $"out/{story.Title}";
            Directory.CreateDirectory(outPath);
            using var f = new StreamWriter($"{outPath}/{story.Title}.html");
            f.Write($"<html><head><title>{story.Title}</title></head><body><h1>{story.Title}</h1><h2>Contents</h2><ul>");
            foreach (var post in orderedPosts)
            {
                var postFile = $"post-{post.PostId}.html";
                f.Write($"<li><a href=\"{postFile}\">{post.Category.Name}: {post.Title}</a></li>");
                await File.WriteAllTextAsync(Path.Combine(outPath, postFile),
                    $"<html><head><title>{story.Title}</title></head>" +
                    $"<body><h2>{post.Category.Name}: {post.Title}</h2>{post.Content}</body></html>");
            }
            f.Write("</ul></body></html>");
        }
    }
}
