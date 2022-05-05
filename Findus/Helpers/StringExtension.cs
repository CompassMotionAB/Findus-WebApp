using System.Collections.Generic;
using System.Text;

namespace Findus.Helpers
{
    public static class StringExtension
    {
        // String replace from Dictionary
        // https://stackoverflow.com/a/1321366
        private static readonly Dictionary<string, string> _replacements = new();

        static StringExtension()
        {
            _replacements["|"] = "-";
            _replacements["–"] = "-";
            _replacements["~"] = "-";
            _replacements["{"] = "(";
            _replacements["}"] = ")";
            _replacements["["] = "(";
            _replacements["]"] = ")";
            _replacements["^"] = " ";
        }

        public static string SanitizeDescriptionForFortnoxArticle(this string description)
        {
            foreach (string to_replace in _replacements.Keys)
            {
                description = description.Replace(to_replace, _replacements[to_replace]);
            }
            return description;
        }
    }
}
