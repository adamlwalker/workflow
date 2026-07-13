# Language and Framework Recommendations
## PDF Proofer — Production Job Management & Proofing System
**Date:** July 13, 2026

---

## Top Recommendation: C# / .NET 9 (ASP.NET Core)

This is the strongest fit for this system's profile.

| Requirement | How .NET fits |
|---|---|
| Windows-first desktop app, minimal foreground footprint | Native Windows—tray process, native dialogs, no VM overhead (unlike Electron/Tauri) |
| File watching (Hot Folder) | `FileSystemWatcher` is mature, battle-tested, and handles UNC shares natively |
| PDF proof generation + thumbnails | **Pdfium.NET SDK** (commercial, BSD-equivalent), **PDFsharp/MigraDoc** (MIT-licensed—no AGPL contamination), or Aspose.PDF |
| PDF optimization without GPL/AGPL | Library-level constraint (CON-L-002) maps directly to .NET's commercial library ecosystem: PDFium.NET SDK, Aspose.PDF—all permissively or commercially licensed |
| Built-in web server (operator UI + customer portal) | ASP.NET Core Minimal APIs—lightweight, self-contained (`webListenAddresses`), no IIS needed (CON-T-002) |
| Embedded SQLite | `Microsoft.Data.Sqlite`—no external server (CON-T-001), auto-migrations via EF Core or Dapper |
| Optional PrintSmith integration | Independent HTTP clients with Polly for retry/backoff (FR-PS-008) |
| LAN read-only view, no login for LAN viewers | ASP.NET Core's host-based filtering (local = `POST/PUT`, LAN = read-only) maps cleanly |
| Cross-platform portability note (2.2) | ASP.NET Core runs on Linux/macOS already; only OS-specific bits: FileSystemWatcher API, URL reservation (`netsh urlacl`—CON-O-001) |

### Recommended stack
- **Language:** C# 13+ (.NET 9)
- **API layer:** ASP.NET Core Minimal APIs (lightweight, fits the REST endpoints in SRS Section 5)
- **Data:** EF Core (Code First with auto-migrations at startup — CON-O-004) or Dapper for raw speed
- **PDF:** PDFium.NET SDK (commercial, BSD-compatible) or PDFsharp 2.x (MIT license — directly satisfies CON-L-001/CON-L-002)
- **Thumbnails:** SkiaSharp (MIT, cross-platform rasterization of PDF pages)
- **File watching:** `Microsoft.Extensions.FileSystemGlobbing` + native `FileSystemWatcher`, with file-stable detection via open-file-handle polling (FR-FW-004)
- **Desktop shell:** Native WinForms/WPF tray app hosting the browser, or Tauri (Rust core + web frontend) for a thin shell
- **Background workers:** `IHostedService` / background tasks with Polly for retry/backoff
- **Config management:** appsettings.json (matches FR-CM-001)

---

## Alternatives (ranked)

### 2. Python — usable but licensing is the dealbreaker
- **Pros:** Fast prototyping, rich PDF ecosystem (`PyPDF2`/`pypdf`, `pdfplumber`)
- **Cons (critical):** The SRS explicitly calls out PyMuPDF as AGPL (Section 8.1, CON-L-003). Replacing it with permissive alternatives (PdfPig + SkiaSharp) essentially means you're already on the .NET stack — just with a Python wrapper. PDF optimization at scale is also slower than .NET/libharfbuzz.
- **If you still want Python:** FastAPI for the web server, `watchfiles` (not `watchdog`) for file watching with better performance.

### 3. Go — strong concurrent worker model
- **Pros:** Excellent for the async, concurrent processing pipeline (FR-PP-012), one binary deployment, no GC overhead for a tray process
- **Cons:** No mature Windows-native PDF library. `pdfcpu` exists but is community-maintained; commercial options are thin
- **If Go:** `pdfcpu` (Apache-2.0) for basic proof/thumbnails, commercial library for optimization

### 4. Node.js/TypeScript — weakest PDF story
- **Pros:** npm ecosystem, Express/Fastify for the API layer, Tauri for desktop shell
- **Cons:** `pdf-lib` can *modify* PDFs but cannot *render* them; headless Chrome for rendering is heavy and slow. No good open-source PDF optimization library exists at this scope

---

## Licensing note (Section 8) — applies regardless of language

The AGPL constraint is the single biggest architectural driver. Two concrete paths:

1. **MIT-permissive stack:** PDFsharp 2.x (PDF manipulation) + SkiaSharp (rendering thumbnails/proofs at configurable DPI) — both MIT, zero copyleft risk.
2. **Commercial stack:** PDFium.NET SDK + Aspose.PDF — paid but full-featured, explicitly supports the optimization pipeline without copyleft.

Avoid Ghostscript unless running as a separately-installed, non-distributed binary on the target machine (the SRS's own fallback in CON-L-002).

---

## Bottom line

C# / .NET 9 with ASP.NET Core + PDFsharp/SkiaSharp (or the commercial PDFium.NET SDK) hits every requirement in this SRS natively, respects all licensing constraints, and keeps the "Windows tray process with embedded browser" model lightweight. The customer portal (Section 4.8, feature-flagged) is just an additional ASP.NET Core route prefix behind HTTPS — no extra framework needed.
