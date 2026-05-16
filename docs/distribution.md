# Distribution

How a copy of mdpeek gets onto another Windows machine.

## Decision

**Single-file self-contained `dotnet publish` for win-x64, distributed via GitHub Releases.**

The end-user path is: download one `.exe` from the repo's Releases page, double-click, app runs. No installer, no .NET runtime install, no unzip step.

## Publish command

```powershell
dotnet publish src/MdPeek.UI -c Release -r win-x64 `
  --self-contained `
  -p:PublishSingleFile=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

Flag-by-flag:

- `-r win-x64` — Windows x64 is the only supported architecture for v1. ARM64 users can run x64 under emulation.
- `--self-contained` — bundle the .NET 10 runtime into the exe so the user doesn't need to install it separately.
- `PublishSingleFile=true` — collapse the published output into a single executable.
- `IncludeAllContentForSelfExtract=true` — required for WPF. WPF needs native and managed resources extracted at runtime; `IncludeNativeLibrariesForSelfExtract` alone isn't enough. On first run, the exe extracts to `%TEMP%\.net\<app>\<hash>\` and caches the extract for subsequent launches.
- `EnableCompressionInSingleFile=true` — drops the artifact from ~150 MB to ~70 MB. Costs ~1-2 s on first launch (one-time decompress); subsequent launches use the cached extract and pay no penalty.

## Runtime dependency: WebView2

The app uses the WPF `WebView2` control to render rendered HTML. WebView2 is **not** bundled — it relies on the WebView2 Evergreen Runtime being present on the machine.

- **Windows 11:** preinstalled.
- **Windows 10:** distributed via Windows Update; effectively always present on supported builds (1809+).

If the runtime is missing the app must surface a clear modal dialog with a link to https://developer.microsoft.com/microsoft-edge/webview2/ rather than throwing an unhandled exception. (See follow-up issue.)

## Distribution channel

GitHub Releases. Each release is cut from a `v<semver>` tag (`v0.1.0`, `v0.2.0`, …) and attaches the published exe plus a SHA256 hash for download verification. Releases give a stable URL the README can link to without paying for hosting or invalidating cached links.

A GitHub Actions workflow on tag push runs the test suite and publishes the exe automatically. (See follow-up issue.)

## Rejected alternatives

- **Framework-dependent publish.** Drops the exe to ~5 MB but requires the user to install the .NET 10 Desktop Runtime themselves. Fails the "double-click and it works" bar.
- **`PublishTrimmed`.** Not supported with WPF — Microsoft explicitly excludes WPF from trimming. Would silently break XAML resource lookups.
- **MSI / MSIX installer.** Out of scope per `CONTEXT.md` — installer infrastructure isn't worth the cost for a single-developer read-only viewer.
- **Code signing.** Out of scope per `CONTEXT.md`. Users will see a SmartScreen warning on first run; documented and accepted.
- **Auto-update.** Out of scope per `CONTEXT.md`. Users grab new releases manually from the Releases page.

## Repeatability

The publish command is wrapped in `scripts/publish.ps1` so cutting a release is one command locally and one tag push in CI. (See follow-up issue.)
