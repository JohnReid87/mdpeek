# mdpeek — CONTEXT

A lightweight Windows desktop app for reading local markdown files. Point it at a folder, browse the directory tree, and view rendered `.md` files side-by-side — no server, no setup, no editor required.

## Domain

mdpeek is a single-user, read-only desktop tool for browsing local markdown documentation. It targets developers and technical writers who keep notes, design docs, READMEs, and reference material as `.md` files on disk and want a frictionless way to *read* them — not edit them.

The primary problem it solves is the awkward gap between "open it in Notepad and read raw markdown" and "spin up MkDocs / Docusaurus / VS Code preview" for casual reading.

Core interactions: pick a root folder, navigate the directory tree, view rendered HTML.

## Tech stack

- **Language / runtime:** C# on .NET 10 (LTS).
- **UI framework:** WPF — mature, well-suited to a tree + content split-pane layout, no extra runtime to ship, good designer-time tooling.
- **Markdown rendering:** [Markdig](https://github.com/xoofx/markdig) for parsing markdown → HTML. CommonMark-compliant, extensible.
- **HTML display:** WPF `WebView2` control (Edge Chromium-based) to render the HTML output.
- **DI container:** `Microsoft.Extensions.DependencyInjection`.
- **MVVM helpers:** [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) for `[ObservableProperty]` / `[RelayCommand]` source generators.
- **Packaging:** single-file self-contained publish via `dotnet publish`, distributed via GitHub Releases. No installer. See [`docs/distribution.md`](docs/distribution.md) for the decision record.
- **Testing:** xUnit + FluentAssertions + NSubstitute.

## Architecture

Single Visual Studio solution `MdPeek.slnx` (the newer XML solution format), layered with one-way dependencies:

- **`MdPeek.Core`** — pure C# class library, no UI references. Owns the domain: markdown rendering (wraps Markdig), the directory-tree model, file-system abstractions (`IFileSystem`, `IMarkdownRenderer`).
- **`MdPeek.App`** — application layer / view-models. MVVM view-models, commands, navigation logic, settings management. No XAML or WPF types — only `System.ComponentModel` so view-models stay testable. References `Core`.
- **`MdPeek.UI`** — WPF executable. XAML views, WebView2 hosting, file dialogs, app startup / DI wiring. The only project that knows about WPF. References `App` and `Core`.
- **`MdPeek.Core.Tests`** — xUnit tests for Core.
- **`MdPeek.App.Tests`** — xUnit tests for view-models.

**Strict dependency direction:** `UI → App → Core`. Core depends on nothing in the solution. App never references UI.

**Layer modification order for any new feature:** Core first, then App, then UI.

**DI composition root:** `UI/App.xaml.cs`. Interfaces in Core, concrete implementations registered in UI.

## Conventions

- **Style.** `.editorconfig` at repo root. 4-space indent, file-scoped namespaces, `var` when type is obvious, `_camelCase` private fields, `PascalCase` everything else, `using` outside namespace, `System.*` first.
- **Nullability.** `<Nullable>enable</Nullable>` on every project. Nullability warnings as errors.
- **Async.** `Async` suffix on async methods. `ConfigureAwait(false)` in Core and App; not in UI.
- **MVVM.** CommunityToolkit.Mvvm source generators only — no hand-rolled `INotifyPropertyChanged`.
- **Testing.** xUnit + FluentAssertions + NSubstitute. AAA pattern with blank lines between sections. Naming: `MethodName_Scenario_ExpectedBehavior`. One assertion concept per test. `[Theory]` + `[InlineData]` to cut duplication.
- **Commits.** Conventional Commits (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`). Imperative mood. One logical change per commit.
- **Branches.** Solo project — commit directly to `main`. No feature branches, no PRs.
- **Comments.** Only when the *why* is non-obvious. XML doc comments only on public Core APIs.

## Out of scope

- **Editing.** Viewer only. No write operations on `.md` files.
- **Non-markdown formats.** No `.rst`, `.adoc`, `.txt`, `.org`. CommonMark + Markdig standard extensions only.
- **Live file-watching / hot reload.** v1 renders on selection. Re-click to refresh. May revisit later.
- **Multi-user / sync / cloud / telemetry.** Local files only.
- **Web hosting / server mode.** Desktop app only.
- **Cross-platform.** Windows-only by deliberate choice. Linux/macOS would mean rewriting the UI layer; explicitly deferred.
- **Installer / auto-update / code signing.** Single `.exe` from `dotnet publish`.
- **Plugin system.** No extensibility surface.
