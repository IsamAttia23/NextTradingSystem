namespace NEXT.web.Services;

/// <summary>Trusted, embedded document assets shared by preview, print and server-side PDF.</summary>
public sealed class InvoiceDocumentAssets
{
    private readonly Lazy<string> styles;

    public InvoiceDocumentAssets(IWebHostEnvironment environment)
    {
        styles = new(() =>
        {
            var root = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
            string Font(string file, int weight) => "@font-face{font-family:Tajawal;font-weight:" + weight +
                ";font-style:normal;src:url(data:font/ttf;base64," + Convert.ToBase64String(
                    File.ReadAllBytes(Path.Combine(root, "fonts", "tajawal", file))) + ") format('truetype');}";
            return Font("Tajawal-Regular.ttf", 400) + Font("Tajawal-Medium.ttf", 500) +
                Font("Tajawal-Bold.ttf", 700) + File.ReadAllText(Path.Combine(root, "css", "invoice-document.css")) +
                File.ReadAllText(Path.Combine(root, "css", "product-report.css")) +
                File.ReadAllText(Path.Combine(root, "css", "debt-book.css")) +
                File.ReadAllText(Path.Combine(root, "css", "store-profile.css"));
        });
    }

    public string Styles => styles.Value;
}
