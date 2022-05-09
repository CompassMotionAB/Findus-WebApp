using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// TODO: Move this into a separate utility function class
namespace Findus.Helpers
{
    public static class StringExtension
    {
        // String replace from Dictionary
        // https://stackoverflow.com/a/1321366
        private static readonly Dictionary<string, string> _replacements = new();
        private const string _pattern = @"[\’\\åäöéáœæøüÅÄÖÉÁÜŒÆØ–:\.`´’,;\^¤#%§£$€¢¥©™°&\/\(\)=\+\-\*_\!?²³®½\@\n\r]+";

        static StringExtension()
        {
            _replacements["|"] = "-";
            _replacements["–"] = "-";
            _replacements["~"] = "-";
            _replacements["{"] = "(";
            _replacements["}"] = ")";
            _replacements["["] = "(";
            _replacements["]"] = ")";

        }

        public static string SanitizeStringForFortnox(this string description)
        {
            /*
            string result = "";
            foreach (string to_replace in _replacements.Keys)
            {
                result = description.Replace(to_replace, _replacements[to_replace]);
            }
            return Regex.Replace(result, _pattern, "");
            */
            return Regex.Replace(description, _pattern, "");
        }
    }
}
