using ResumeBuilder.Core.Services;
using Xunit;

namespace ResumeBuilder.Tests;

public class TextHelperTests
{
    [Theory]
    [InlineData("maria@gmail.com", true)]
    [InlineData("ivan.petrov@mail.by", true)]
    [InlineData("wrong@mail", false)]
    [InlineData("без собаки.ru", false)]
    public void IsValidEmail_Works(string email, bool expected)
    {
        Assert.Equal(expected, TextHelper.IsValidEmail(email));
    }

    [Theory]
    [InlineData("+375291234567")]
    [InlineData("8 029 1234567")]
    [InlineData("8 (029) 123-45-67")]
    [InlineData("+375 (29) 123-45-67")]
    public void NormalizePhone_MakesOneFormat(string phone)
    {
        Assert.Equal("+375 (29) 123-45-67", TextHelper.NormalizePhone(phone));
    }

    [Fact]
    public void IsValidPhone_WrongNumber_False()
    {
        Assert.False(TextHelper.IsValidPhone("12-34"));
    }

    [Fact]
    public void FindEmailsAndPhones_InText()
    {
        string text = "Пишите на maria@gmail.com или звоните 80291234567";
        Assert.Equal("maria@gmail.com", TextHelper.FindEmails(text).Single());
        Assert.Equal("+375 (29) 123-45-67", TextHelper.FindPhones(text).Single());
    }

    [Fact]
    public void DatesToIso_SwapsGroups()
    {
        Assert.Equal("с 2024-09-01", TextHelper.DatesToIso("с 01.09.2024"));
    }

    [Fact]
    public void MaskEmails_HidesName()
    {
        Assert.Equal("ma***@gmail.com", TextHelper.MaskEmails("maria.ivanova@gmail.com"));
    }
}
