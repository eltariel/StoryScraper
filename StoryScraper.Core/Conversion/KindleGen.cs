using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core.Conversion
{
    public class KindleGen
    {
        private readonly Config config;

        public KindleGen(Config config)
        {
            this.config = config;
        }

        public void ToMobi(Story story)
        {
            var basename = story.Title.ToValidPath();
            var args = $"\"{basename}.epub\" -o \"{basename}.mobi\"";
            var kgProcess = MakeKindleGenProcess(args);
            kgProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.Out.WriteLine($"  [kindlegen] {e.Data}");
                }
            };
            kgProcess.BeginOutputReadLine();
        }
        
        private Process MakeKindleGenProcess(string args)
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
                FileName = config.UseWsl ? "wsl" : config.KindleGenPath,
                Arguments = config.UseWsl ? $"{config.PandocPath} {args}" : args
            };

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
    }
}