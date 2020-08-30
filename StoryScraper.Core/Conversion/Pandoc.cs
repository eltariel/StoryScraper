using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                .OrderBy(p => p.Timestamp)
                .ToList();

            var epubFile = Path.Combine(config.OutDir, $"{story.Title.ToValidPath()}.epub");
            if(File.Exists(epubFile) &&
               File.GetLastWriteTime(epubFile) >= story.LastUpdate &&
               posts.All(p => p.FromCache))
            {
                log.Debug($"{epubFile} exists, no new posts. Skipping ebook generation.");
                return;
            }

            var pandocArgs = $"--verbose --shift-heading-level-by=-1 -o \"{epubFile}\" -f markdown"
                             + (string.IsNullOrWhiteSpace(story.CachedImage)
                                 ? ""
                                 : $" --epub-cover-image=\"{story.CachedImage}\"");
            var pandocProcess = MakePandocProcess(pandocArgs);
            
            pandocProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    log.Trace(e.Data);
                }
            };
            pandocProcess.BeginOutputReadLine();

            var meta = GetEpubMetadata(story);
            pandocProcess.StandardInput.WriteLine(meta);
            foreach (var post in (IEnumerable<IPost>) posts)
            {
                PostToMarkdown(post, pandocProcess.StandardInput);
            }

            pandocProcess.StandardInput.Close();
            pandocProcess.WaitForExit();
            log.Debug($"Pandoc exit code: {pandocProcess.ExitCode}");
        }

        private void PostToMarkdown(IPost post, StreamWriter parentStdin)
        {
            var mdPandoc = MakePandocProcess("--verbose -t markdown -f html");

            mdPandoc.OutputDataReceived += (s, e) =>
            {
                if(e.Data != null && parentStdin != null)
                {
                    parentStdin.WriteLine(e.Data);
                    log.Trace($"  > {e.Data}");
                }
            };
            mdPandoc.BeginOutputReadLine();

            var inputBuffer = Encoding.UTF8.GetBytes(post.AsHtml);
            mdPandoc.StandardInput.BaseStream.Write(inputBuffer, 0, inputBuffer.Length);
            mdPandoc.StandardInput.Close();
            mdPandoc.WaitForExit();
            
            parentStdin?.WriteLine("\n");
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
