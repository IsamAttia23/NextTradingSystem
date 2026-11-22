# Invoice PDF runtime

The invoice preview and print page share the same Razor document and bundled Tajawal fonts.
PDF export uses Microsoft.Playwright and a local headless Chromium process. Invoice data is
rendered locally; remote resource requests are blocked during PDF generation.

After building NEXT.web, install the matching Chromium runtime under the account that runs
the application. The installer puts Chromium under `App_Data/playwright` in the build
or publish output directory, which is the application's default lookup path:

```powershell
pwsh NEXT.web/scripts/install-pdf-browser.ps1
```

For a published application, pass its absolute output directory:

```powershell
pwsh NEXT.web/scripts/install-pdf-browser.ps1 -OutputDirectory C:/deploy/NEXT.web
```

On Linux, run the installer with `-WithDependencies` and the published output directory.
Install system dependencies while building the image;
the application itself does not need administrator privileges. Include Chromium in the image
and make its cache accessible to the runtime account. For a shared directory, pass
`-BrowsersPath` to the installer and set `InvoicePdf:BrowsersPath` in configuration or
`PLAYWRIGHT_BROWSERS_PATH` in the environment to that same path at runtime.

Repeat browser installation when upgrading Microsoft.Playwright. The application starts
Chromium only on the first PDF request, limits concurrent exports to two, and uses an isolated
browser context per document. Export has a 45-second queue/render deadline. No database
migration is needed.
