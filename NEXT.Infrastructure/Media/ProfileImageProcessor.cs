using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ImageMagick;
using ImageMagick.Configuration;
using NEXT.Application.Abstractions.Media;
using NEXT.Application.Exceptions;

namespace NEXT.Infrastructure.Media;

public sealed class ProfileImageProcessor : IProfileImageProcessor
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> SvgElements = new(StringComparer.OrdinalIgnoreCase)
    { "svg", "g", "defs", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
      "linearGradient", "radialGradient", "stop", "clipPath", "title", "desc", "use" };

    static ProfileImageProcessor()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "next-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        MagickNET.SetTempDirectory(temporaryDirectory);
        var temporaryPattern = System.Security.SecurityElement.Escape("{" +
            temporaryDirectory.Replace('\\', '/') + "/*," +
            temporaryDirectory.Replace("\\", "\\\\") + "\\\\*}");
        var configuration = ConfigurationFiles.Default;
        configuration.Policy.Data = """
            <policymap>
              <policy domain="delegate" rights="none" pattern="*" />
              <policy domain="filter" rights="none" pattern="*" />
              <policy domain="path" rights="none" pattern="*" />
              <policy domain="path" rights="read|write" pattern="@TEMP@" />
              <policy domain="coder" rights="none" pattern="*" />
              <policy domain="coder" rights="read|write" pattern="{PNG,PNG32,JPEG,JPG,GIF,WEBP,BMP,BMP2,BMP3,TIFF,ICO,CUR,HEIC,AVIF,JXL,JP2,J2K,PSD,TGA,PNM,PBM,PGM,PPM,QOI,MSVG,SVG,MVG,XC}" />
            </policymap>
            """.Replace("@TEMP@", temporaryPattern);
        MagickNET.Initialize(configuration);
        ResourceLimits.Memory = 256 * 1024 * 1024;
        ResourceLimits.Disk = 0;
        ResourceLimits.Width = ProfileImageRules.MaxDimension;
        ResourceLimits.Height = ProfileImageRules.MaxDimension;
        ResourceLimits.Thread = 2;
    }

    public byte[] Normalize(byte[] data)
    {
        if (data.Length is <= 0 or > ProfileImageRules.MaxBytes) throw Invalid("ImageSizeError");
        // Bound native allocations across concurrent uploads, not just within a single request.
        lock (Gate)
        {
            try
            {
                var settings = new MagickReadSettings { FrameIndex = 0, FrameCount = 1 };
                if (LooksLikeXml(data))
                {
                    data = ValidateSvg(data);
                    settings.Format = MagickFormat.Msvg;
                }
                if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[3] == 0 && data[2] is 1 or 2)
                    settings.Format = data[2] == 1 ? MagickFormat.Ico : MagickFormat.Cur;
                var info = new MagickImageInfo(data, settings);
                // MVG is needed internally by the sanitized SVG renderer, never as an upload format.
                if (info.Format == MagickFormat.Mvg) throw Invalid("ImageInvalidError");
                if (settings.Format != MagickFormat.Msvg && info.Format is MagickFormat.Svg or MagickFormat.Msvg)
                    throw Invalid("ImageInvalidError");
                if (info.Width == 0 || info.Height == 0 || (long)info.Width * info.Height > ProfileImageRules.MaxPixels)
                    throw Invalid("ImageDimensionsError");
                using var image = new MagickImage();
                var timer = Stopwatch.StartNew();
                image.Progress += (_, progress) => { if (timer.Elapsed > TimeSpan.FromSeconds(10)) progress.Cancel = true; };
                image.Read(data, settings);
                image.AutoOrient();
                if (image.Width > 512 || image.Height > 512) image.Thumbnail(512, 512);
                image.Strip();
                return image.ToByteArray(MagickFormat.Png32);
            }
            catch (Exception exception) when (exception is MagickException or XmlException or InvalidOperationException or ArgumentException)
            {
                throw Invalid("ImageInvalidError");
            }
        }
    }

    private static bool LooksLikeXml(byte[] bytes)
    {
        var prefix = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 256))).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        return prefix.StartsWith('<');
    }

    private static byte[] ValidateSvg(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = ProfileImageRules.MaxBytes });
        var document = XDocument.Load(reader);
        if (document.Root?.Name.LocalName != "svg") throw Invalid("ImageInvalidError");
        foreach (var element in document.Root.DescendantsAndSelf())
        {
            if (!SvgElements.Contains(element.Name.LocalName)) throw Invalid("ImageInvalidError");
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration) continue;
                var name = attribute.Name.LocalName;
                var value = attribute.Value.Trim();
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || name == "style" ||
                    (name is "href" or "src" && !value.StartsWith('#')) ||
                    (value.Contains("url(", StringComparison.OrdinalIgnoreCase) &&
                     !System.Text.RegularExpressions.Regex.IsMatch(value, @"^url\(#[a-zA-Z_][a-zA-Z0-9_.-]*\)$")))
                    throw Invalid("ImageInvalidError");
            }
        }
        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    private static AppValidationException Invalid(string key) => new(new Dictionary<string, string[]> { ["Avatar"] = [key] });
}
