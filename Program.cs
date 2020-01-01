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

namespace threadmarks_thing
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var f = File.Open("dump.html", FileMode.OpenOrCreate, FileAccess.Write))
            {
                var url = new Uri("https://forums.sufficientvelocity.com/threads/mauling-snarks-worm.41471/");

                var site = SiteFactory.GetSiteFor(url);
                var story = site.GetStory(url);
                await story.GetPosts();

                //await f.WriteAsync(Encoding.UTF8.GetBytes(storyPage));
            }
        }
    }
}
