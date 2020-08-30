using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StoryScraper.Core.Utils
{
    public static class PathExtensions
    {
        // This is the output from Path.GetInvalidFileNameChars() on Win32, because on linux it just contains '/'
        public static char[] Win32InvalidPathChars = new[]
        {
            (char)0x0022, (char)0x003C, (char)0x003E, (char)0x007C,
            (char)0x0000, (char)0x0001, (char)0x0002, (char)0x0003,
            (char)0x0004, (char)0x0005, (char)0x0006, (char)0x0007,
            (char)0x0008, (char)0x0009, (char)0x000A, (char)0x000B,
            (char)0x000C, (char)0x000D, (char)0x000E, (char)0x000F,
            (char)0x0010, (char)0x0011, (char)0x0012, (char)0x0013,
            (char)0x0014, (char)0x0015, (char)0x0016, (char)0x0017,
            (char)0x0018, (char)0x0019, (char)0x001A, (char)0x001B,
            (char)0x001C, (char)0x001D, (char)0x001E, (char)0x001F,
            (char)0x003A, (char)0x002A, (char)0x003F, (char)0x005C,
            (char)0x002F
        };

        public static string ToValidPath(this string name)
        {
            var invalidCharsStr = Regex.Escape(new string(Win32InvalidPathChars));
            var invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidCharsStr);

            return Regex.Replace( name, invalidRegStr, "_" );
        }
    }
}
