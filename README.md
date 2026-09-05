# CMISPilot

> 🇩🇪 **Deutsche Version:** Liegt hier als [README.de.md](README.de.md) vor.

**A modern, free CMIS client and repository explorer for Windows.**

Browsing, querying, and debugging a [CMIS](https://www.oasis-open.org/committees/tc_home.php?wg_abbrev=cmis) (Content Management Interoperability Services) repository shouldn't mean fighting the aging, Eclipse-based Apache Chemistry CMIS Workbench. **CMISPilot** is a fast, native Windows desktop CMIS browser and CMIS connector: connect to any CMIS 1.0/1.1-compliant repository over the JSON Browser Binding or the XML AtomPub Binding, explore its folder tree, inspect and edit object properties, run CMISQL queries, and see the raw HTTP protocol when something goes wrong — all in one ribbon-and-docking UI that never blocks while talking to the server.

![Platform](https://img.shields.io/badge/platform-Windows-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![Language](https://img.shields.io/badge/language-C%23-239120)
![CMIS](https://img.shields.io/badge/CMIS-1.0%20%7C%201.1-informational)
![Bindings](https://img.shields.io/badge/bindings-Browser%20(JSON)%20%7C%20AtomPub%20(XML)-informational)

---

## What is CMISPilot?

CMISPilot is a **CMIS repository explorer** and **CMIS client** for developers, integrators, and administrators who work with content management systems that implement the Content Management Interoperability Services standard. Think of it as an **Apache Chemistry CMIS Workbench alternative** built from scratch for modern Windows:

- **Modern UI, not a relic.** A Fluent Ribbon + AvalonDock docking shell instead of an old Eclipse RCP window — tabs for the explorer, query console, and type browser, plus dockable tool windows for output, errors, and diagnostics.
- **Never blocks.** Every server call runs asynchronously; the UI stays responsive even against a slow or flaky repository.
- **Built for troubleshooting.** A raw HTTP protocol log (method, URL, status code, duration, content type/length) is built in, so you can see exactly what CMISPilot sent and what the repository answered — without a separate proxy tool.
- **Extensible without bloat.** Company- or project-specific functionality can ship as a plugin that hooks into the ribbon and workspace, instead of being baked into the core application.
- **A real CMIS client, not just a viewer.** Full CRUD on documents and folders, versioning-aware document creation, and a CMISQL query console — not just read-only browsing.

## Supported CMIS Standards

CMISPilot talks to repositories using [PortCMIS](https://chemistry.apache.org/dotnet/portcmis.html), the .NET client library for Apache Chemistry, over two standard CMIS bindings:

| Binding | Format | CMIS Version | Status |
|---|---|---|---|
| **Browser Binding** | JSON | CMIS 1.1 | ✅ Supported (default) |
| **AtomPub Binding** | XML | CMIS 1.0 / 1.1 | ✅ Supported |
| Web Services Binding | SOAP | CMIS 1.0 | ❌ Not supported |

Each connection profile picks the binding explicitly, so you can point CMISPilot at either a modern CMIS 1.1 endpoint or an older AtomPub-only repository.

## Key Features

- **Explorer** — lazy-loading folder tree, object properties panel, extended/custom properties, document content access.
- **Full CRUD** — create, update, and delete documents and folders; versioning state (`Major`/`None`) is derived automatically from the object type definition, so you don't have to guess.
- **Types Browser** — inspect object type definitions and property definitions of the connected repository.
- **CMISQL Query Console** — write and run CMISQL queries directly against the repository, results shown in a data grid.
- **Diagnostics** — raw HTTP request/response logging for the active binding, purpose-built for debugging repository connectivity issues.
- **Flexible authentication** — no authentication, HTTP Basic (username/password), or OAuth 2.0 Bearer token.
- **Connection profiles** — save and reuse repository connections (URL, binding, credentials-at-runtime-only) instead of retyping them.
- **Plugin system** — first-party plugins can add their own ribbon tabs and document tabs without touching the CMISPilot core (see [`samples/SamplePlugin`](samples/SamplePlugin)).
- **Persisted layout** — window position, docking layout, and settings are restored automatically between sessions.

## Screenshots

**Explorer** — folder tree, document list, and the object properties panel:

![CMISPilot main window with the folder explorer and object properties](docs/images/screenshot_mainwindow.png)

**Connect dialog** — saved connection profiles on the left, connection settings on the right:

![CMISPilot connect dialog with saved connection profiles](docs/images/screenshot_connectiondialog.png)

## Compatible Repositories

CMISPilot speaks the open CMIS standard rather than a vendor-specific API, so it is designed to work with **any CMIS 1.0/1.1-compliant repository** — including:

- **Alfresco**
- **Nuxeo**
- **OpenText Content Server / Documentum**
- **IBM FileNet Content Manager**
- **Microsoft SharePoint** (via a CMIS connector)
- **Apache Chemistry OpenCMIS**-based servers

It is actively developed and tested against an OpenCMIS in-memory test server; compatibility with other repositories follows directly from their CMIS conformance rather than repository-specific integration testing.

## Quick Start

CMISPilot depends on two sibling projects — [`APX.PortCMIS`](https://github.com/apdata/APX.PortCMIS) (the CMIS client library) and [`APX.Wpf.Shell`](https://github.com/apdata/APX.Wpf.Shell) (the shared WPF shell) — which must be cloned next to it:

```bash
# 1. Clone CMISPilot and its two dependencies into a common folder
git clone https://github.com/apdata/CMISPilot.git
git clone https://github.com/apdata/APX.PortCMIS.git
git clone https://github.com/apdata/APX.Wpf.Shell.git

# 2. Build
cd CMISPilot
dotnet build src/CMISPilot.sln -c Release

# 3. Run
dotnet run --project src/CMISPilot.Desktop -c Release
```

Requires the **.NET 10 SDK** and **Windows** (CMISPilot is a WPF desktop application).

## Usage

1. **Connect** — open the connect dialog, choose Browser or AtomPub binding, enter the repository URL and credentials (or an OAuth 2.0 bearer token), and pick a repository.
2. **Explore** — the folder tree loads lazily; click a folder to open it as an Explorer tab and inspect or edit object properties.
3. **Query** — open the Query tab and run CMISQL against the connected repository.
4. **Troubleshoot** — open the Diagnostics tool window to see the raw HTTP traffic for the active binding.

Connection profiles let you save a repository connection (name, URL, binding, authentication type) so you don't have to fill in the connect dialog every time.

## Contributing

Company- or project-specific functionality belongs in a plugin, not in the CMISPilot core — see [`samples/SamplePlugin`](samples/SamplePlugin) for a minimal, documented starting point for the plugin contribution model (ribbon tabs, document tabs).

Issues and pull requests are welcome.

## Support

CMISPilot is free, open-source, and built in spare time alongside a day job. If it has saved you time or frustration wrestling with a CMIS repository, consider [buying me a coffee](https://buymeacoffee.com/apdata) — it helps keep the project maintained and moving forward.

## License

No license has been published for this repository yet. Until a license file is added, all rights are reserved by the copyright holder and no reuse, modification, or redistribution rights are granted.
