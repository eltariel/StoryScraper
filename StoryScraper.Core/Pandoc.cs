using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
//using System.Linq.AsyncEnumerable;
using System.Text;
using System.Threading.Tasks;

namespace StoryScraper.Core
{
    public class Pandoc
    {
        private readonly bool useWsl;

        public Pandoc(bool useWsl)
        {
            this.useWsl = useWsl;
        }
        
        public void ToEpub(string title, Story story, IEnumerable<string> excludedCategories)
        {
            var posts = story
                .Categories
                .Where(c => !excludedCategories.Contains(c.Name))
                .SelectMany(p => p.Posts)
                .OrderBy(p => p.Timestamp)
                .ToList();

            PostsToEpub(title, posts, story);
        }

        private void PostToMarkdown(Post post, StreamWriter parentStdin)
        {
            Console.WriteLine($"[Pandoc] Post '{post.Title}' to markdown");
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

        private void PostsToEpub(string title, List<Post> posts, Story story)
        {
            Console.WriteLine($"Posts Markdown to EPUB");

            var pandocArgs = $"--verbose --toc -o \"{title}.epub\" -f markdown";
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
            Directory.CreateDirectory("temptemp");
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
                                                              $"author: {story.Posts.First().Author}\n" +
                                                              $"lang: en-us\n" +
                                                              "...\n\n";

        private Process MakePandocProcess(string pandocArgs)
        {
            var psi = DefaultProcessStartInfo;
            psi.Arguments = useWsl ? $"pandoc {pandocArgs}" : pandocArgs;

            Console.WriteLine($"  [Pandoc] \"{psi.FileName}\" {psi.Arguments}");
            var pandocProcess = new Process {StartInfo = psi, EnableRaisingEvents = true};
            pandocProcess.ErrorDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.Out.WriteLine($"  [stderr] {e.Data}");
                }
            };
            
            pandocProcess.Start();
            pandocProcess.BeginErrorReadLine();
            return pandocProcess;
        }

        private ProcessStartInfo DefaultProcessStartInfo =>
            new ProcessStartInfo
            {
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
                RedirectStandardInput = true,
                StandardInputEncoding = Encoding.UTF8,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                FileName = useWsl ? "wsl" : @"C:\Program Files\Pandoc\pandoc.exe"
            };
    }
}