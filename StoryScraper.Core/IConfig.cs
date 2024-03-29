﻿using System;
using System.Collections.Generic;
using NLog;

namespace StoryScraper.Core
{
    public interface IConfig
    {
        List<string> ExcludedCategories { get; }
        Dictionary<string, List<Uri>> Urls { get; }
        string CachePath { get; }
        string PandocPath { get; }
        string KindleGenPath { get; }
        string KepubifyPath { get; }
        bool UseWsl { get; }
        bool SkipMobi { get; }
        bool SkipKepub { get; }
        int Verbosity { get; }
        LogLevel LogLevel { get; }
        string OutDir { get; }
    }
}
