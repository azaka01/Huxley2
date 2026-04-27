using System.Text.RegularExpressions;

namespace Huxley2.Utils
{
    public static class PostcodeValidator
    {
        private static readonly Regex PostcodePattern = new Regex(
            @"^[A-Z]{1,2}\d[A-Z\d]?\s?\d[A-Z]{2}$",
            RegexOptions.Compiled);

        /// <summary>
        /// Normalises a postcode by trimming whitespace, removing internal spaces, and converting to uppercase.
        /// </summary>
        public static string NormalisePostcode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return input.Trim().Replace(" ", "").ToUpperInvariant();
        }

        /// <summary>
        /// Returns true if the string is a valid UK postcode after normalisation.
        /// </summary>
        public static bool IsValidUkPostcode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var normalised = NormalisePostcode(input);
            return PostcodePattern.IsMatch(normalised);
        }
    }
}
