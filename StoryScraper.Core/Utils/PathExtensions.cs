using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StoryScraper.Core.Utils
{
    public static class PathExtensions
    {
        public static string ToValidPath(this string name)
        {
			var invalidChars = Path.GetInvalidFileNameChars()
				.Concat(@":/\?".ToCharArray()) // Windows exclusions when on linux, ugh.
				.Distinct()
				.ToArray();

            var invalidCharsStr = Regex.Escape(new string(invalidChars));
            var invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidCharsStr);

            return Regex.Replace( name, invalidRegStr, "_" );
        }
    }
}
