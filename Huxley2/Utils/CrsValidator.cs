using System.Text.RegularExpressions;

namespace Huxley2.Utils
{
    public static class CrsValidator
    {
        private static readonly Regex CrsPattern = new Regex("^[A-Z]{3}$", RegexOptions.Compiled);

        /// <summary>
        /// Returns true if the string is a valid 3-letter uppercase alpha CRS code.
        /// </summary>
        public static bool IsValidCrsCode(string code)
        {
            return !string.IsNullOrEmpty(code) && CrsPattern.IsMatch(code);
        }
    }
}
