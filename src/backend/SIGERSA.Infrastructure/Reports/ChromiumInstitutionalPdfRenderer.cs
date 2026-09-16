using System.Reflection;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Infrastructure.Reports;

public sealed class ChromiumInstitutionalPdfRenderer(IOptions<ReportOptions> options)
    : IInstitutionalPdfRenderer
{
    private readonly ReportOptions _options = options.Value;

    public async Task<byte[]> RenderAsync(
        ReportGenerationData data,
        bool official,
        CancellationToken cancellationToken = default)
    {
        var executablePath = ResolveBrowserExecutable();
        var assets = new InstitutionalReportAssets(
            DataUri("SIGERSA.Infrastructure.Reports.Assets.sigersa-logo.png", "image/png"),
            DataUri("SIGERSA.Infrastructure.Reports.Assets.digemaps-logo.png", "image/png"),
            DataUri("SIGERSA.Infrastructure.Reports.Assets.Poppins-Regular.ttf", "font/ttf"),
            DataUri("SIGERSA.Infrastructure.Reports.Assets.Poppins-SemiBold.ttf", "font/ttf"));
        var html = InstitutionalReportHtmlBuilder.Build(data, official, assets);

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = executablePath,
            Args = ["--disable-dev-shm-usage", "--no-sandbox"]
        });
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
        await page.EvaluateExpressionAsync("document.fonts.ready");
        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.Letter,
            PrintBackground = true,
            PreferCSSPageSize = true,
            Tagged = true,
            Outline = true,
            MarginOptions = new MarginOptions { Top = "0", Right = "0", Bottom = "0", Left = "0" }
        });
    }

    private string ResolveBrowserExecutable()
    {
        var configured = _options.BrowserExecutablePath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("CHROME_PATH"),
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            "/usr/bin/google-chrome",
            "/usr/bin/google-chrome-stable",
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser"
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            ?? throw new InvalidOperationException(
                "No se encontró Chrome o Chromium. Configure Reports:BrowserExecutablePath o CHROME_PATH.");
    }

    private static string DataUri(string resourceName, string mimeType)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se encontró el recurso institucional {resourceName}.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return $"data:{mimeType};base64,{Convert.ToBase64String(buffer.ToArray())}";
    }
}
