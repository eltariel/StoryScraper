using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using NLog;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core.Conversion
{
    public class Pandoc
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        private readonly IConfig config;

        public Pandoc(IConfig config)
        {
            this.config = config;
        }
        
        public string ToEpub(IStory story)
        {
            log.Trace("Posts Markdown to EPUB");

            var posts = story
                .Categories
                .SelectMany(p => p.Posts)
                .OrderBy(p => p.PostedAt)
                .ToList();

            if (posts.Count == 0)
            {
                log.Warn($"Story {story.Title} has no posts, aborting! ({story.Url})");
                return null;
            }

            var epubFile = GetEpubFileName(story);
            if (File.Exists(epubFile) &&
                File.GetLastWriteTimeUtc(epubFile) is {} epubTime &&
                posts.All(p =>
                {
                    var pc = GetPostCachePath(p);
                    return File.Exists(pc) && File.GetLastWriteTimeUtc(GetPostCachePath(p)) < epubTime;
                }))
            {
                log.Info($"EPUB [{epubFile}] up to date ({epubTime.ToLocalTime():O}), not rebuilding.");
                return epubFile;
            }

            var pandocArgs = $"--verbose --shift-heading-level-by=-1 -o \"{epubFile}\" -f markdown"
                             + (string.IsNullOrWhiteSpace(story.Image)
                                 ? ""
                                 : $" --epub-cover-image=\"{story.Image}\"");
            using var pandocProcess = MakePandocProcess(pandocArgs);
            pandocProcess.BeginOutputReadLine();

            var meta = GetEpubMetadata(story);
            pandocProcess.StandardInput.WriteLine(meta);
            foreach (var post in (IEnumerable<IPost>) posts)
            {
                try
                {
                    using var md = File.OpenRead(GetPostCachePath(post));
                    md.CopyTo(pandocProcess.StandardInput.BaseStream);
                    pandocProcess.StandardInput.WriteLine();
                }
                catch (Exception ex)
                {
                    log.Warn(ex, "Can't find cached post, aborting.");
                    pandocProcess.Kill();
                    pandocProcess.Close();
                    File.Delete(epubFile);
                    return null;
                }
            }

            pandocProcess.StandardInput.Close();
            pandocProcess.WaitForExit();

            return epubFile;
        }

        private string GetEpubFileName(IStory story)
        {
            var dir = Path.Combine(config.CachePath, "epubs");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"{story.Title.ToValidPath()}.epub");
        }

        public void PostToMarkdown(IPost post, string html)
        {
            var postCachePath = GetPostCachePath(post);
            if (!File.Exists(postCachePath))
            {
                log.Trace($"  Writing markdown for {post.Name} @ {postCachePath}");
                using var mdPandoc = MakePandocProcess($"--verbose -t markdown -f html -o \"{postCachePath}\"");

                var inputBuffer = Encoding.UTF8.GetBytes(html);
                mdPandoc.StandardInput.BaseStream.Write(inputBuffer, 0, inputBuffer.Length);
                mdPandoc.StandardInput.Close();
                mdPandoc.WaitForExit();
            }
        }

        internal static string GetPostCachePath(IPost post)
        {
            var pandocCachePath = Path.Combine(
                post.Site.Cache.Root,
                "posts",
                $"story-{post.Story.StoryId}".ToValidPath());
            
            Directory.CreateDirectory(pandocCachePath);

            return Path.Combine(
                pandocCachePath,
                $"post-{post.PostId}-{post.UpdatedAt:yyyyMMdd-HHmmss}.md".ToValidPath());
        }

        private static string GetEpubMetadata(IStory story) => "---\n" +
                                                               $"title: \"{story.Title}\"\n" +
                                                               $"author: \"{story.Author}\"\n" +
                                                               $"lang: en-us\n" +
                                                               "...\n\n";

        private Process MakePandocProcess(string args)
        {
            var psi = new ProcessStartInfo
            {
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
                RedirectStandardInput = true,
                StandardInputEncoding = Encoding.UTF8,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                FileName = config.UseWsl ? "wsl" : config.PandocPath,
                Arguments = config.UseWsl ? $"{config.PandocPath} {args}" : args
            };

            log.Trace($"  [Pandoc] \"{psi.FileName}\" {psi.Arguments}");
            var process = new Process {StartInfo = psi, EnableRaisingEvents = true};
            process.ErrorDataReceived += (s, e) => 
            {
                if (e.Data != null)
                {
                    log.Debug($"  [stderr] {e.Data}");
                }
            };
            
            process.Start();
            process.BeginErrorReadLine();
            return process;
        }
    }
}
