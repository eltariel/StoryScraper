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
        
        public void ToEpub(Story story)
        {
            var posts = story
                .Categories
                .Where(c => !config.ExcludedCategories.Contains(c.Name))
                .SelectMany(p => p.Posts)
                .OrderBy(p => p.Timestamp)
                .ToList();

            PostsToEpub(posts, story);
        }

        private void PostToMarkdown(Post post, StreamWriter parentStdin)
        {
            //Console.WriteLine($"[Pandoc] Post '{post.Title}' to markdown");
            var mdPandoc = MakePandocProcess("--verbose -t markdown -f html");

            mdPandoc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    (parentStdin ?? Console.Out).WriteLine(e.Data);
                }
            };
            mdPandoc.BeginOutputReadLine();

            var inputBuffer = Encoding.UTF8.GetBytes(post.AsHtml);
            mdPandoc.StandardInput.BaseStream.Write(inputBuffer, 0, inputBuffer.Length);
            mdPandoc.StandardInput.Close();
            mdPandoc.WaitForExit();
            
            parentStdin?.WriteLine("\n");
        }

        private void PostsToEpub(IEnumerable<Post> posts, Story story)
        {
            Console.WriteLine($"Posts Markdown to EPUB");

            var pandocArgs = $"--verbose --toc -o \"{story.Title.ToValidPath()}.epub\" -f markdown";
            var pandocProcess = MakePandocProcess(pandocArgs);
            
            pandocProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine(e.Data);
                }
            };
            pandocProcess.BeginOutputReadLine();

            pandocProcess.StandardInput.WriteLine(GetEpubMetadata(story));
            foreach (var post in posts)
            {
                PostToMarkdown(post, pandocProcess.StandardInput);
            }

            pandocProcess.StandardInput.Close();
            pandocProcess.WaitForExit();
            Console.WriteLine($"Pandoc exit code: {pandocProcess.ExitCode}");
        }

        private static string GetEpubMetadata(Story story) => "---\n" +
                                                              $"title: {story.Title}\n" +
                                                              $"author: {story.Author}\n" +
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
                if (!string.IsNullOrEmpty(e.Data))
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