using Jaberah.Helpers;
using Xunit;

namespace Jaberah.Tests;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("محمد سعيد")]
    [InlineData("أحمد")]
    [InlineData("حلقة ١٢")]
    public void ContainsArabicAndSpaces_AcceptsArabicLettersDigitsAndSpaces(string input)
    {
        Assert.True(input.ContainsArabicAndSpaces());
    }

    [Theory]
    [InlineData("Mohamed")]
    [InlineData("محمد Saeed")]
    [InlineData("محمد-سعيد")]
    [InlineData("محمد 5")]
    [InlineData("")]
    [InlineData("   ")]
    public void ContainsArabicAndSpaces_RejectsAnythingElse(string input)
    {
        Assert.False(input.ContainsArabicAndSpaces());
    }

    [Theory]
    [InlineData("ملاحظة: الطالب متفوق!")]
    [InlineData("حلقة (أ)")]
    [InlineData("مراجعة ٣ + ٤")]
    public void ContainsArabic_AllowsPunctuationAndSymbols(string input)
    {
        Assert.True(input.ContainsArabic());
    }

    [Theory]
    [InlineData("note in english")]
    [InlineData("ملاحظة mixed")]
    [InlineData("")]
    public void ContainsArabic_RejectsLatinLetters(string input)
    {
        Assert.False(input.ContainsArabic());
    }

    [Theory]
    [InlineData("712345678")]
    [InlineData("799999999")]
    public void IsPhoneNumberStartingWith7_AcceptsNineDigitsStartingWithSeven(string input)
    {
        Assert.True(input.IsPhoneNumberStartingWith7());
    }

    [Theory]
    [InlineData("812345678")]   // wrong prefix
    [InlineData("71234567")]    // too short
    [InlineData("7123456789")]  // too long
    [InlineData("71234567a")]   // not all digits
    [InlineData("+712345678")]  // leading plus
    [InlineData("")]
    public void IsPhoneNumberStartingWith7_RejectsMalformedNumbers(string input)
    {
        Assert.False(input.IsPhoneNumberStartingWith7());
    }
}
