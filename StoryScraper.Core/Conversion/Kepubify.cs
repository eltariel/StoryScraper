using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using NLog;
using StoryScraper.Core.Utils;

namespace StoryScraper.Core.Conversion
{
    public class Kepubify
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly IConfig config;

        public Kepubify(IConfig config)
        {
            this.config = config;
        }

        public void ToKepub(IStory story, string epubPath)
        {
            if (config.SkipKepub)
            {
                log.Trace("Skipping kepub creation.");
                return;
            }
            
            var outPath = $"{config.OutDir}/kepubs";
            Directory.CreateDirectory(outPath);
            
            var args = $"-o \"{outPath}\" \"{epubPath}\"";
            var p = MakeKepubifyProcess(args);
            p.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    log.Debug($"  {e.Data}");
                }
            };
            p.BeginOutputReadLine();
			p.WaitForExit();
        }
        
        private Process MakeKepubifyProcess(string args)
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
                FileName = config.UseWsl ? "wsl" : config.KepubifyPath,
                Arguments = config.UseWsl ? $"{config.KepubifyPath} {args}" : args
            };

            var p = new Process {StartInfo = psi, EnableRaisingEvents = true};
            p.ErrorDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    log.Debug($"  [stderr] {e.Data}");
                }
            };
            
            p.Start();
            p.BeginErrorReadLine();
            return p;
        }
    }
}
