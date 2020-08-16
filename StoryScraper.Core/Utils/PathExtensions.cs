using System.IO;
using System.Text.RegularExpressions;

namespace StoryScraper.Core.Utils
{
    public static class PathExtensions
    {
        public static string ToValidPath(this string name)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace( name, invalidRegStr, "_" );
        }
    }
}