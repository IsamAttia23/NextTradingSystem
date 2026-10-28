using System.Text;
using ImageMagick;
using NEXT.Application.Abstractions.Media;
using NEXT.Application.Exceptions;
using NEXT.Infrastructure.Media;
using Xunit;

namespace NEXT.Tests;

public sealed class ProfileImageTests
{
    private readonly ProfileImageProcessor processor = new();

    [Theory]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.Ico)]
    public void DecodesActualContentAndNormalizesToPng(MagickFormat format)
    {
        using var original = new MagickImage(MagickColors.Blue, 32, 24);
        var bytes = processor.Normalize(original.ToByteArray(format));
        using var decoded = new MagickImage(bytes);
        Assert.Equal(MagickFormat.Png, decoded.Format);
        Assert.Equal(32U, decoded.Width);
        Assert.Equal(24U, decoded.Height);
        Assert.Empty(decoded.ProfileNames);
    }

    [Fact]
    public void EnforcesExactSevenMebibyteBoundary()
    {
        using var image = new MagickImage(MagickColors.Green, 10, 10);
        var bytes = image.ToByteArray(MagickFormat.Png);
        var boundary = new byte[ProfileImageRules.MaxBytes];
        bytes.CopyTo(boundary, 0);
        Assert.NotEmpty(processor.Normalize(boundary));
        Assert.Throws<AppValidationException>(() => processor.Normalize(new byte[ProfileImageRules.MaxBytes + 1]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("This is not an image.")]
    [InlineData("%PDF-1.7 fake document")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><use href='file:///etc/passwd'/></svg>")]
    [InlineData("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg>&x;</svg>")]
    public void RejectsInvalidOrActiveContent(string input) =>
        Assert.Throws<AppValidationException>(() => processor.Normalize(Encoding.UTF8.GetBytes(input)));

    [Fact]
    public void SupportsSelfContainedSvgAndReducesLargeImages()
    {
        var svg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='1000' height='800'><rect width='1000' height='800' fill='blue'/></svg>");
        using var result = new MagickImage(processor.Normalize(svg));
        Assert.True(result.Width <= 512 && result.Height <= 512);
        Assert.Equal(MagickFormat.Png, result.Format);
    }
}
