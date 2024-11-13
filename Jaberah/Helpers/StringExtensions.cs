using System.Text.RegularExpressions;

namespace Jaberah.Helpers
{
    public static class StringExtensions
    {
        public static bool ContainsArabicAndSpaces(this string input)
        {
            // Regular expression to match Arabic letters and spaces
            var arabicRegex = new Regex(@"^[\u0621-\u064A\u0660-\u0669\s]+$");
            return arabicRegex.IsMatch(input);
        }
        public static bool ContainsArabic(this string input)
        {
            // Regular expression to match Arabic letters, Arabic numbers, symbols, and spaces
            var arabicWithSymbolsRegex = new Regex(@"^[\u0621-\u064A\u0660-\u0669\s\p{P}\p{S}]+$");
            return arabicWithSymbolsRegex.IsMatch(input);
        }
    }
}
