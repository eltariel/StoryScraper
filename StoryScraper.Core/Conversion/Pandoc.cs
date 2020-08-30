using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core.Conversion
{
    public class Pandoc
    {
        private readonly Config config;

        public Pandoc(Config config)
        {
            this.config = config;
        }
        
        public void ToEpub(IStory story)
        {
            var posts = story
                .Categories
                .SelectMany(p => p.Posts)
                .OrderBy(p => p.Timestamp)
                .ToList();

            Console.WriteLine("Posts Markdown to EPUB");

            var epubFile = Path.Combine(config.OutDir, $"{story.Title.ToValidPath()}.epub");
            if(File.Exists(epubFile) &&
               File.GetLastWriteTime(epubFile) >= story.LastUpdate &&
               posts.All(p => p.FromCache))
            {
                Console.WriteLine($"{epubFile} exists, no new posts. Skipping ebook generation.");
                return;
            }

            var pandocArgs = $"--verbose --shift-heading-level-by=-1 -o \"{epubFile}\" --epub-cover-image=\"{story.CachedImage}\" -f markdown";
            var pandocProcess = MakePandocProcess(pandocArgs);
            
            pandocProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    Console.WriteLine(e.Data);
                }
            };
            pandocProcess.BeginOutputReadLine();

            var meta = GetEpubMetadata(story);
            Console.WriteLine($"epub metadata: {meta}");
            pandocProcess.StandardInput.WriteLine(meta);
            foreach (var post in (IEnumerable<IPost>) posts)
            {
                PostToMarkdown(post, pandocProcess.StandardInput);
            }

            pandocProcess.StandardInput.Close();
            pandocProcess.WaitForExit();
            Console.WriteLine($"Pandoc exit code: {pandocProcess.ExitCode}");
        }

        private void PostToMarkdown(IPost post, StreamWriter parentStdin)
        {
            //var md = new StringBuilder();
            var mdPandoc = MakePandocProcess("--verbose -t markdown -f html");

            mdPandoc.OutputDataReceived += (s, e) =>
            {
                if(e.Data != null)
                {
                    (parentStdin ?? Console.Out).WriteLine(e.Data);
                    //md.AppendLine(e.Data);
                }
            };
            mdPandoc.BeginOutputReadLine();

            var inputBuffer = Encoding.UTF8.GetBytes(post.AsHtml);
            mdPandoc.StandardInput.BaseStream.Write(inputBuffer, 0, inputBuffer.Length);
            mdPandoc.StandardInput.Close();
            mdPandoc.WaitForExit();
            
            //File.WriteAllText($"{post.Story.Title} - {post.PostId} {post.Title}.md".ToValidPath(), md.ToString());
            parentStdin?.WriteLine("\n");
        }

        private static string GetEpubMetadata(IStory story) => "---\n" +
                                                               $"title: \"{story.Title}\"\n" +
                                                               $"author: \"{story.Author}\"\n" +
                                                               //(!string.IsNullOrWhiteSpace(story.CachedImage)
                                                               //    ? $"cover-image: \"{story.CachedImage.Replace("\\", "/")}\"\n"
                                                               //    : "") +
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

            //Console.WriteLine($"  [Pandoc] \"{psi.FileName}\" {psi.Arguments}");
            var process = new Process {StartInfo = psi, EnableRaisingEvents = true};
            process.ErrorDataReceived += (s, e) => 
            {
                if (e.Data != null)
                {
                    Console.Out.WriteLine($"  [stderr] {e.Data}");
                }
            };
            
            process.Start();
            process.BeginErrorReadLine();
            return process;
        }
    }
}
