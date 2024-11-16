using System.Text.RegularExpressions;

namespace Jaberah.Helpers
{
    public static class StringExtensions
    {
        public static bool ContainsArabicAndSpaces(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            // Regular expression to match Arabic letters and spaces
            var arabicRegex = new Regex(@"^[\u0621-\u064A\u0660-\u0669\s]+$");
            return arabicRegex.IsMatch(input);
        }
        public static bool ContainsArabic(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            // Regular expression to match Arabic letters, Arabic numbers, symbols, and spaces
            var arabicWithSymbolsRegex = new Regex(@"^[\u0621-\u064A\u0660-\u0669\s\p{P}\p{S}]+$");
            return arabicWithSymbolsRegex.IsMatch(input);
        }
        public static bool IsPhoneNumberStartingWith7(this string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return false;

            // Check if the phone number starts with '7' and contains only digits
            var regex = new Regex(@"^7\d{8}$");  // Example: '7' followed by 8 digits
            return regex.IsMatch(phoneNumber);
        }
    }
}
