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
        
        private readonly Config config;

        public Pandoc(Config config)
        {
            this.config = config;
        }
        
        public void ToEpub(IStory story)
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
                return;
            }

            var epubFile = Path.Combine(config.OutDir, $"{story.Title.ToValidPath()}.epub");
            if(File.Exists(epubFile) &&
               File.GetLastWriteTimeUtc(epubFile) is {} epubTime &&
               posts.All(p => {
		  var pc = GetPostCachePath(p);
		  var ft = File.GetLastWriteTimeUtc(GetPostCachePath(p));
                  log.Trace($"Cache: {ft.ToLocalTime():O} < {epubTime.ToLocalTime():O} = {ft < epubTime} ({pc})");
		  return File.Exists(pc) && ft < epubTime;}))
            {
                log.Info($"EPUB [{epubFile}] up to date ({epubTime.ToLocalTime():O}), not rebuilding.");
                return;
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
                using var md = PostToMarkdown(post);
                md.CopyTo(pandocProcess.StandardInput.BaseStream);
                pandocProcess.StandardInput.WriteLine();
            }

            pandocProcess.StandardInput.Close();
            pandocProcess.WaitForExit();
            log.Debug($"Pandoc exit code: {pandocProcess.ExitCode}");
        }

        private Stream PostToMarkdown(IPost post)
        {
            var postCachePath = GetPostCachePath(post);
	    log.Trace($"\tcache for {post.Name} @ {postCachePath}");
            if (!File.Exists(postCachePath))
            {
                using var mdPandoc = MakePandocProcess($"--verbose -t markdown -f html -o \"{postCachePath}\"");

                var inputBuffer = Encoding.UTF8.GetBytes(post.AsHtml);
                mdPandoc.StandardInput.BaseStream.Write(inputBuffer, 0, inputBuffer.Length);
                mdPandoc.StandardInput.Close();
                mdPandoc.WaitForExit();
            }
            
            return File.OpenRead(postCachePath);
        }

        private static string GetPostCachePath(IPost post)
        {
            var pandocCachePath = Path.Combine(
                post.Site.Cache.Root,
                "pandoc",
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
