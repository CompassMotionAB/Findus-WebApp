using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Findus.Helpers
{
    public static class FortnoxStringUtil
    {
        private static readonly Dictionary<string, string> _replacements =
            new()
            {
                { "|", "-" },
                { "–", "-" },
                { "~", "-" },
                { "{", "(" },
                { "}", ")" },
                { "[", "(" },
                { "]", ")" },
            };

        // Catch-all pattern:
        //private const string _pattern = @"[\’\\åäöéáœæøüÅÄÖÉÁÜŒÆØ–:\.`´’,;\^¤#%§£$€¢¥©™°&\/\(\)=\+\-\*_\!?²³®½\@\n\r]+";
        // Pattern that catches disallowed symbols in Fortnox: ^$¢¥|~{}[]
        private const string _pattern = @"[\^$¢¥\{\}\[\]]+";

        public static string SanitizeStringForFortnox(this string description)
        {
            string result = "";
            foreach (string to_replace in _replacements.Keys)
            {
                result = description.Replace(to_replace, _replacements[to_replace]);
            }
            return Regex.Replace(result, _pattern, "");
        }
    }
}
