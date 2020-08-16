using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        
        public async Task ToEpub(string outPath, string title, List<string> postPaths, Story story)
        {
            var postsMarkdown = new List<string>();
            foreach (var post in postPaths)
            {
                var postMarkdown = await PostHtmlToMarkdown(post);
                postsMarkdown.Add(postMarkdown);
            }

            var metadata = await GenerateEpubMetadata(outPath, story);

            await PostsToEpub(title, postsMarkdown, metadata);
        }

        private async Task PostsToEpub(string title, List<string> postsMarkdown, string metadata)
        {
            Console.WriteLine($"Posts Markdown to EPUB");

            var postFiles = string.Join("\" \"", postsMarkdown);
            var pandocArgs = $"--verbose --toc -o \"{title}.epub\" \"{metadata}\" \"{postFiles}\"";
            var pandocProcess = MakePandocProcess(pandocArgs);
            pandocProcess.Start();
            pandocProcess.WaitForExit();
            Console.WriteLine($"Pandoc exit code: {pandocProcess.ExitCode}");
            while (await pandocProcess.StandardError.ReadLineAsync() is {} l)
            {
                Console.WriteLine($"  [stderr] {l}");
            }
        }

        private async Task<string> PostHtmlToMarkdown(string post)
        {
            var postMarkdown = $"{post}.md";
            Console.WriteLine($"Post to markdown [{postMarkdown}]");
            var mdPandoc = MakePandocProcess($"--verbose -o \"{postMarkdown}\" \"{post}\"");
            mdPandoc.Start();
            mdPandoc.WaitForExit();
            while (await mdPandoc.StandardError.ReadLineAsync() is {} l)
            {
                Console.WriteLine($"  [stderr] {l}");
            }

            return postMarkdown;
        }

        private async Task<string> GenerateEpubMetadata(string outPath, Story story)
        {
            var metadata = "---\n" +
                           $"title: {story.Title}\n" +
                           $"author: {story.Posts.First().Author}\n" +
                           $"lang: en-us\n" +
                           "...";

            var metadataPath = $"{outPath}/_metadata.md";
            await File.WriteAllTextAsync(metadataPath, metadata);
            return metadataPath;
        }

        private Process MakePandocProcess(string pandocArgs)
        {
            var psi = new ProcessStartInfo
            {
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
                RedirectStandardInput = true,
                StandardInputEncoding = Encoding.UTF8,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = Environment.CurrentDirectory
            };

            if (useWsl)
            {
                psi.FileName = "wsl";
                psi.Arguments = $"pandoc {pandocArgs}";
            }
            else
            {
                psi.FileName = @"C:\Program Files\Pandoc\pandoc.exe";
                psi.Arguments = pandocArgs;
            }

            var pandocProcess = new Process {StartInfo = psi, EnableRaisingEvents = true};
            pandocProcess.OutputDataReceived += (s, e) => Console.Out.Write($"pout: {e.Data}");
            pandocProcess.ErrorDataReceived += (s, e) => Console.Out.Write($"perr: {e.Data}");
            return pandocProcess;
        }
    }
}