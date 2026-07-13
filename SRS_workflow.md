# Software Requirements Specification (SRS)
## PDF Proofer — Production Job Management & Proofing System
**Version:** 1.0  
**Date:** July 8, 2026  
**Status:** Draft  

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Overall Description](#2-overall-description)
3. [Stakeholders & User Roles](#3-stakeholders--user-roles)
4. [Functional Requirements](#4-functional-requirements)
   - 4.1 File Ingestion & Watching
   - 4.2 PDF Processing Pipeline
   - 4.3 Job Management
   - 4.4 Destination Dispatch
   - 4.5 Operator Web UI
   - 4.6 Configuration Management
   - 4.7 PrintSmith Integration *(planned)*
   - 4.8 Customer Proof Approval Portal *(planned)*
5. [API Specification](#5-api-specification)
6. [Data Models](#6-data-models)
7. [Non-Functional Requirements](#7-non-functional-requirements)
8. [Constraints](#8-constraints)
9. [Feature Flags & Configuration Schema](#9-feature-flags--configuration-schema)
10. [Glossary](#10-glossary)

---

## 1. Introduction

### 1.1 Purpose
This document specifies the functional and non-functional requirements for the **PDF Proofer** system — a production job management and PDF proofing platform for use in a commercial print shop environment. It is intended to serve as the canonical reference for development, testing, and future enhancement regardless of implementation language or framework.

### 1.2 Scope
The system automates the intake, proofing, optimization, dispatch, and tracking of PDF print jobs from the moment a file is dropped into a watched folder through to delivery at a production destination (printer, cutter, etc.). It optionally integrates with an existing sales/MIS system (PrintSmith) and optionally exposes a customer-facing URL for remote proof review and approval.

### 1.3 Definitions
See [Section 10 — Glossary](#10-glossary).

### 1.4 Document Conventions
- **SHALL** — Mandatory requirement
- **SHOULD** — Recommended but not mandatory
- **MAY** — Optional
- **[PLANNED]** — Not yet implemented; intended for a future release
- **[OPTIONAL / FEATURE FLAG]** — Behavior is controlled by a toggle in system configuration

---

## 2. Overall Description

### 2.1 Product Perspective
PDF Proofer is a self-contained local desktop application that runs on a Windows workstation in the production area. It exposes a lightweight built-in web server for:
- A local/LAN-accessible job management interface used by production operators
- A public-facing proof approval portal accessible by customers via a unique URL (optional)

It does **not** require a persistent internet connection for core functionality. External integrations (PrintSmith, customer portal) each have independent network dependencies that are individually toggleable.

### 2.2 Operating Environment
- **Host OS:** Windows (primary). The core system SHOULD be designed so that the file-watching, processing, and web server components could run on Linux/macOS with minimal changes.
- **Network:** LAN-connected. The production workstation runs the system. Other workstations on the same LAN may access the read-only job list view via a browser.
- **External Systems:** PrintSmith MIS (optional), outbound SMTP or messaging for customer notifications (optional).

### 2.3 User Classes
See [Section 3](#3-stakeholders--user-roles).

---

## 3. Stakeholders & User Roles

| Role | Access Method | Permissions |
|------|---------------|-------------|
| **Production Operator** | Local desktop app (embedded browser) | Full access: all job actions, state changes, dispatch |
| **LAN Viewer** (Graphics, Sales staff) | Browser on office network | Read-only: view job list, statuses, proofs |
| **Customer** | Public URL (unique per job) | Proof approval portal only: view proof, approve or reject |
| **Administrator / IT** | Local settings dialogs | Configure all paths, API URLs, feature flags |
| **PrintSmith / MIS** | HTTP API (inbound webhooks or polling) | Read job status, receive state-change callbacks [PLANNED] |

---

## 4. Functional Requirements

---

### 4.1 File Ingestion & Watching

**FR-FW-001** — The system SHALL monitor a configurable "Hot Folder" for incoming PDF files.

**FR-FW-002** — The system SHALL begin watching the Hot Folder automatically on application startup without requiring manual intervention.

**FR-FW-003** — The system SHALL detect files copied, moved, or created in the Hot Folder.

**FR-FW-004** — The system SHALL wait for a file to be fully written (i.e., not file-locked) before beginning processing. It SHALL retry this check at configurable intervals for a configurable maximum duration.

**FR-FW-005** — The system SHALL process any PDF files already present in the Hot Folder when the watcher starts (startup scan).

**FR-FW-006** — The system SHALL ignore non-PDF files in the Hot Folder.

**FR-FW-007** — The system SHALL prevent a single file from being processed more than once per drop event (deduplication guard).

**FR-FW-008** — The system SHALL allow the watcher to be manually started and stopped by an operator via the control surface.

**FR-FW-009** — The system SHALL support Hot Folder paths that are local directories or UNC network shares.

---

### 4.2 PDF Processing Pipeline

**FR-PP-001** — Upon detecting a valid PDF, the system SHALL parse a **job number** from the file name. The job number SHALL be the leading sequence of digits in the filename stem (e.g., `12345-jobname.pdf` → `12345`). If no leading digits are present, the full stem shall be used.

**FR-PP-002** — The system SHALL move the original PDF from the Hot Folder to a structured storage location: `<ActiveSharePath>/<JobNumber>/Original.pdf`. If a file already exists at that path, the system SHALL archive or overwrite it according to archive rules.

**FR-PP-003** — The system SHALL generate a **low-resolution proof PDF** from the original. The proof SHALL:
  - Be rendered at a configurable DPI (default: 100 DPI)
  - Display a prominent diagonal "PROOF" watermark in a clearly visible color

**FR-PP-004** — The proof PDF SHALL be saved to `<ActiveSharePath>/<JobNumber>/Proof.pdf`.

**FR-PP-005** — The system SHALL also copy the proof PDF to the flat `<ProofsSharePath>/<stem>_proof.pdf` directory so it can be referenced externally (customer portal, LAN share).

**FR-PP-006** — The system SHALL generate a **thumbnail image** (PNG) of the first page of the original PDF. The thumbnail SHALL be rendered at a configurable DPI (default: 72 DPI). It SHALL be saved as `<ActiveSharePath>/<JobNumber>/Thumb.png`.

**FR-PP-007** — The system SHALL generate an **optimized print-ready PDF** from the original using a prepress-grade optimization process. The optimization SHOULD produce a file suitable for professional printing with no quality loss to content. The optimized file SHALL be saved to `<ActiveSharePath>/<JobNumber>/Optimized/Original.pdf`.

> **Constraint Note (FR-PP-007A):** The PDF optimization tool used SHALL be licensed under a permissive or commercial license that does **not** impose copyleft obligations (e.g., AGPL, GPL) on software that calls it. Tools with AGPL licenses (e.g., Ghostscript) SHALL NOT be used unless the commercial/enterprise license is obtained. See [Section 8 — Constraints](#8-constraints).

**FR-PP-008** — The system SHALL record each file artifact (Proof, Thumb, Optimized, Original) as a versioned entry linked to the job in persistent storage.

**FR-PP-009** — If all processing steps succeed, the job's state SHALL be set to `Active`.

**FR-PP-010** — If any required processing step fails after the configured number of retries, the job's state SHALL be set to `Error`, and the original file SHALL be moved to `<ErrorDirPath>`.

**FR-PP-011** — The system SHALL retry failed processing steps up to a configurable number of times (default: 3) before declaring failure.

**FR-PP-012** — All processing SHALL occur asynchronously and SHALL not block the file watcher or UI.

**FR-PP-013** — If a job with the same job number is submitted again (re-drop), the system SHALL archive the previous version before processing the new one.

---

### 4.3 Job Management

**FR-JM-001** — The system SHALL maintain a persistent record of all jobs, including: Job ID, Job Number, Filename Stem, Current State, Current Location, Arrival Timestamp, Operator Notes, Sent-To Destination, and Sent File Path.

**FR-JM-002** — A job SHALL exist in one of the following states at any time:

| State | Description |
|-------|-------------|
| `Active` | Processed successfully; visible in operator queue |
| `Error` | Processing failed; visible in operator queue with error indicator |
| `Sent` | Dispatched to a destination; auto-hides when destination file is consumed |
| `Finished` | Manually marked complete or hidden by operator |

**FR-JM-003** — The system SHALL prevent backward state transitions to `Sent` (the initial processing state) once a job has moved past it.

**FR-JM-004** — An operator SHALL be able to manually mark a job as `Finished` (hide it from the active view).

**FR-JM-005** — An operator SHALL be able to add or edit a free-text note on any job. Notes SHALL be persisted and displayed in the job grid.

**FR-JM-006** — An operator SHALL be able to create a duplicate / copy of a job record (for reprint tracking purposes). The copy SHALL be a new DB entry linked to the same job number.

**FR-JM-007** — Jobs in the `Sent` state SHALL automatically disappear from the active view once the file at the destination path no longer exists (indicating the printer or downstream system has consumed it).

**FR-JM-008** — The system SHALL provide a mechanism to view all historical jobs (including `Finished`) in addition to the default active view.

---

### 4.4 Destination Dispatch

**FR-DD-001** — The system SHALL support a configurable list of **Destinations**. Each destination SHALL have: a unique ID, a display name, a target folder path, and a flag indicating whether to copy the optimized version (vs. original).

**FR-DD-002** — An operator SHALL be able to add, edit, and remove destinations via a management interface.

**FR-DD-003** — An operator SHALL be able to send any active job to any configured destination with a single action.

**FR-DD-004** — On dispatch, the system SHALL copy the optimized PDF (or original if no optimized version exists) to `<DestinationPath>/<JobNumber>/Original.pdf`.

**FR-DD-005** — On dispatch, the system SHALL update the job's `CurrentLocation` to the destination name and set the state to `Sent`.

**FR-DD-006** — Destination paths SHALL support local directories and UNC network shares.

**FR-DD-007** — Destinations MAY be assigned a display color for use in the job grid location indicator.

---

### 4.5 Operator Web UI

**FR-UI-001** — The system SHALL serve a web-based job management interface accessible at a configurable local port (default: 8765).

**FR-UI-002** — The interface SHALL display a sortable, filterable grid of active jobs with the following columns: Job Number, Filename, Status (color-coded), Current Location (badge), Operator Note, Thumbnail.

**FR-UI-003** — The interface SHALL support filtering between "Active Jobs Only" and "All Jobs (including Finished)".

**FR-UI-004** — The interface SHALL support a manual refresh action.

**FR-UI-005 [PLANNED]** — The interface SHOULD support automatic live refresh (e.g., via polling or server-sent events) so new jobs appear without manual action.

**FR-UI-006** — Right-clicking a job row SHALL display a context menu with available actions appropriate to the user's access level.

**FR-UI-007** — The interface SHALL detect whether it is running inside the local embedded desktop view or accessed via an external browser, and SHALL restrict operator-only actions accordingly:

| Action | Embedded (Operator) | External Browser (LAN) |
|--------|--------------------|-----------------------|
| View Versions | ✅ | ✅ |
| View Work Order | ✅ | ✅ |
| Mark Finished | ✅ | ❌ |
| Send to Destination | ✅ | ❌ |
| Add / Edit Note | ✅ | ❌ |
| Copy (Reprint Entry) | ✅ | ❌ |
| Hide from List | ✅ | ❌ |

**FR-UI-008** — "View Versions" SHALL open the proof, optimized, and original file versions in new browser tabs.

**FR-UI-009** — "View Work Order" SHALL open the associated PrintSmith (or MIS) work order using the configured Lookup URL and Reports URL.

**FR-UI-010** — The thumbnail for each job SHALL be loaded inline in the grid from the file server endpoint.

**FR-UI-011** — The interface SHALL function as a read-only view for LAN clients without any login requirement.

**FR-UI-012 [PLANNED]** — The interface SHOULD include a status indicator showing the current proof approval status from the customer portal for each job (if the portal feature is enabled).

---

### 4.6 Configuration Management

**FR-CM-001** — All configuration SHALL be stored in a human-readable file (e.g., JSON) adjacent to the application executable.

**FR-CM-002** — Safe default values SHALL be provided for all configuration options on first run.

**FR-CM-003** — The following paths SHALL be configurable: Hot Folder, Active Share, Proofs Share, Archive Root, Error Directory.

**FR-CM-004** — The following operational settings SHALL be configurable: PDF optimization command/tool path, processing retry count, web server port, remote server enable/disable toggle.

**FR-CM-005** — The following integration URLs SHALL be configurable: Work Order Lookup URL, Reports / Work Order Viewer URL.

**FR-CM-006** — A settings management interface SHALL allow an administrator to browse for folder paths and save settings without manually editing the configuration file.

**FR-CM-007** — Changes to settings SHALL take effect without requiring an application restart where technically feasible.

---

### 4.7 PrintSmith Integration [PLANNED]

> This section defines requirements for bidirectional integration with a PrintSmith (or compatible MIS) system. All behavior in this section is controlled by a feature flag and SHALL be independently disableable.

**FR-PS-001 [PLANNED, OPTIONAL]** — The system SHALL support a configurable "PrintSmith Base URL" from which all API calls are derived.

**FR-PS-002 [PLANNED, OPTIONAL]** — The system SHALL support a configurable API key or authentication token for authenticating with the PrintSmith API.

**FR-PS-003 [PLANNED, OPTIONAL]** — When a job is first ingested and set to `Active`, the system SHOULD notify PrintSmith that the job has entered prepress / proofing. The notification payload SHALL include the job number and a configurable status label (e.g., "In Proofing").

**FR-PS-004 [PLANNED, OPTIONAL]** — When a job is dispatched to a production destination (sent to printer, cutter, etc.), the system SHALL notify PrintSmith of the status change. The notification SHALL include: job number, destination name, and timestamp.

**FR-PS-005 [PLANNED, OPTIONAL]** — When a job is marked `Finished`, the system SHALL notify PrintSmith accordingly.

**FR-PS-006 [PLANNED, OPTIONAL]** — When a customer approves or rejects a proof via the customer portal, the system SHALL notify PrintSmith of the approval status. The specific PrintSmith field used for this update SHALL be configurable (placeholder until the target field is determined).

**FR-PS-007 [PLANNED, OPTIONAL]** — All PrintSmith API calls SHALL be non-blocking and SHALL not prevent the system from functioning if PrintSmith is unreachable. Failures SHALL be logged.

**FR-PS-008 [PLANNED, OPTIONAL]** — The system SHALL support retry logic for failed PrintSmith notifications with configurable retry count and backoff.

**FR-PS-009 [PLANNED, OPTIONAL]** — The system SHALL surface the current PrintSmith job status/stage for each job in the operator UI if available.

**PrintSmith State Mapping (Proposed):**

| PDF Proofer Event | Suggested PrintSmith Status |
|-------------------|-----------------------------|
| Job ingested (Active) | "In Proofing" |
| Proof approved by customer | "Proof Approved" |
| Proof rejected by customer | "Proof Rejected – Revision Required" |
| Sent to press destination | "On Press" |
| Sent to finishing destination | "In Finishing" |
| Marked Finished | "Shipped" / "Complete" |

> **Note:** Exact field names and status values SHALL be determined by the PrintSmith API documentation and confirmed with the print shop MIS administrator before implementation.

---

### 4.8 Customer Proof Approval Portal [PLANNED, OPTIONAL]

> All requirements in this section are gated by a feature flag (`EnableCustomerPortal`). When disabled, no public portal routes SHALL be served.

**FR-CP-001 [PLANNED, OPTIONAL]** — The system SHALL generate a unique, unguessable URL for each job that provides a customer with access to their proof. The URL SHALL be of the form:  
`https://<hostname>/proof/<unique-token>`  
where `<unique-token>` is a cryptographically random string of sufficient length (minimum 32 characters).

**FR-CP-002 [PLANNED, OPTIONAL]** — The portal URL SHALL be generated at job creation time and stored in the database alongside the job record.

**FR-CP-003 [PLANNED, OPTIONAL]** — The portal URL SHALL be accessible from the operator UI for easy copying and sharing with the customer (via the customer service representative).

**FR-CP-004 [PLANNED, OPTIONAL]** — The portal page SHALL display:
  - The proof PDF inline (embedded PDF viewer in the browser)
  - Job reference number and filename
  - Approve and Reject action buttons
  - A free-text field for the customer to provide a comment or revision note

**FR-CP-005 [PLANNED, OPTIONAL]** — The portal SHALL NOT require the customer to create an account or log in.

**FR-CP-006 [PLANNED, OPTIONAL]** — Each portal URL SHALL be single-use in the sense that once a decision (Approve or Reject) has been submitted, the page SHALL display the recorded decision and SHALL NOT allow resubmission without operator intervention.

**FR-CP-007 [PLANNED, OPTIONAL]** — Operators SHALL be able to reset/invalidate a proof approval and issue a new proof URL (e.g., when a revised proof is ready after a rejection).

**FR-CP-008 [PLANNED, OPTIONAL]** — Proof approval status SHALL be one of: `Pending`, `Approved`, `Rejected`.

**FR-CP-009 [PLANNED, OPTIONAL]** — When a customer submits an approval decision, the system SHALL:
  1. Record the decision, timestamp, and any comment in the database
  2. Update the job's approval status in the operator UI
  3. Notify the operator (via UI indicator; optionally via email/notification)
  4. Notify PrintSmith of the approval status [see FR-PS-006]

**FR-CP-010 [PLANNED, OPTIONAL]** — The portal SHALL be served over HTTPS. A valid TLS certificate SHALL be configured. HTTP SHALL redirect to HTTPS.

**FR-CP-011 [PLANNED, OPTIONAL]** — The portal hostname and port SHALL be configurable to support reverse proxy deployments (e.g., behind nginx or IIS).

**FR-CP-012 [PLANNED, OPTIONAL]** — The portal SHALL be rate-limited to prevent abuse (e.g., brute-force token guessing).

**FR-CP-013 [PLANNED, OPTIONAL]** — Portal tokens SHALL expire after a configurable time period (default: 30 days). Expired tokens SHALL display a friendly message directing the customer to contact their customer service representative.

**FR-CP-014 [PLANNED, OPTIONAL]** — The operator UI SHALL indicate, per job, the current approval status: Pending / Approved / Rejected, along with the timestamp and any customer comment.

---

## 5. API Specification

All API endpoints are served from the system's built-in web server. All response bodies are JSON. All modifying endpoints (`POST`, `PUT`, `DELETE`) SHALL only be accepted from the local host machine; requests from LAN clients SHALL receive `403 Forbidden`.

### 5.1 Core Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/` or `/index.html` | None | Serves the operator SPA |
| `GET` | `/main.js` | None | Serves the frontend script |

### 5.2 Jobs

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/jobs` | None | List active jobs. Query param: `?includeFinished=true` |
| `POST` | `/api/jobs/{id}/state` | Local only | Update job state. Body: `{ "state": "Finished" }` |
| `POST` | `/api/jobs/{id}/send` | Local only | Send to destination. Body: `{ "destinationId": 1 }` |
| `POST` | `/api/jobs/{id}/note` | Local only | Save note. Body: `{ "note": "text" }` |
| `POST` | `/api/jobs/{id}/copy` | Local only | Duplicate job entry (reprint) |
| `POST` | `/api/jobs/{id}/hide` | Local only | Mark as Finished/hidden |

**Job Object (Response):**
```json
{
  "jobId": 1,
  "jobNumber": "12345",
  "stem": "12345-myjob",
  "currentState": "Active",
  "location": "Active",
  "arrivedAt": "2026-07-08T03:00:00Z",
  "note": "Rush job",
  "approvalStatus": "Pending"
}
```

### 5.3 Versions

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/versions/{jobId}` | None | List file versions for a job |

**Version Object (Response):**
```json
{ "type": "Proof", "filePath": "/files/proof/1" }
```

### 5.4 File Serving

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/files/proof/{jobId}` | None | Serve proof PDF |
| `GET` | `/files/optimized/{jobId}` | None | Serve optimized PDF |
| `GET` | `/files/original/{jobId}` | None | Serve original PDF |
| `GET` | `/files/thumb/{jobId}` | None | Serve thumbnail PNG |

### 5.5 Destinations

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/destinations` | None | List all configured destinations |
| `POST` | `/api/destinations` | Local only | Create or update a destination |
| `DELETE` | `/api/destinations/{id}` | Local only | Remove a destination |

**Destination Object:**
```json
{
  "destinationId": 1,
  "name": "Main Press",
  "path": "\\\\printserver\\press1",
  "copyOptimized": true,
  "color": "#3498db"
}
```

### 5.6 Configuration

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/config` | None | Read current integration URLs |
| `POST` | `/api/config` | Local only | Update integration URLs |

### 5.7 PrintSmith Integration [PLANNED]

*(Outbound calls from this system to PrintSmith — not inbound endpoints)*

| Direction | Event | PrintSmith API Target | Notes |
|-----------|-------|-----------------------|-------|
| Outbound | Job ingested | `POST /jobs/{id}/status` | Set to "In Proofing" |
| Outbound | Proof approved | `POST /jobs/{id}/status` | Set to "Proof Approved" (field TBD) |
| Outbound | Proof rejected | `POST /jobs/{id}/status` | Set to "Proof Rejected" |
| Outbound | Sent to destination | `POST /jobs/{id}/status` | Set to "On Press" / "In Finishing" |
| Outbound | Marked Finished | `POST /jobs/{id}/status` | Set to "Complete" |

> Exact PrintSmith API shape TBD upon access to PrintSmith API documentation.

### 5.8 Customer Proof Portal [PLANNED]

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/proof/{token}` | Token in URL | Customer proof view page |
| `POST` | `/proof/{token}/decision` | Token in URL | Submit approve/reject decision |
| `GET` | `/api/jobs/{id}/portal-url` | Local only | Get or generate the proof portal URL for a job |
| `POST` | `/api/jobs/{id}/portal-reset` | Local only | Invalidate current token, generate new one |

**Decision Payload (POST /proof/{token}/decision):**
```json
{
  "decision": "Approved",
  "comment": "Looks great, please proceed."
}
```

---

## 6. Data Models

### 6.1 Job

| Field | Type | Notes |
|-------|------|-------|
| `JobId` | Integer | Primary key, auto-increment |
| `JobNumber` | String | Leading digits from filename |
| `Stem` | String | Full filename without extension |
| `CurrentState` | String | Enum: Active, Error, Sent, Finished |
| `CurrentLocation` | String | Display name of destination or "Active" |
| `ArrivedAt` | DateTime (ISO 8601) | When the file was first ingested |
| `Note` | String | Operator free-text note |
| `SentTo` | String | Destination name when sent |
| `SentPath` | String | Full file path at destination |
| `ApprovalStatus` | String | [PLANNED] Enum: Pending, Approved, Rejected |
| `ApprovalToken` | String | [PLANNED] Unique portal URL token |
| `ApprovalDecidedAt` | DateTime | [PLANNED] When customer submitted decision |
| `ApprovalComment` | String | [PLANNED] Customer's note |
| `PrintSmithLastNotified` | DateTime | [PLANNED] Last successful PS notification |

### 6.2 Version

| Field | Type | Notes |
|-------|------|-------|
| `VersionId` | Integer | Primary key |
| `JobId` | Integer | Foreign key → Job |
| `Type` | String | Enum: Proof, Optimized, Original, Thumb |
| `FilePath` | String | Absolute path to file |
| `CreatedAt` | DateTime | When version was created |

### 6.3 Destination

| Field | Type | Notes |
|-------|------|-------|
| `DestinationId` | Integer | Primary key |
| `Name` | String | Display name (e.g., "Main Press") |
| `Path` | String | Target folder or UNC path |
| `CopyOptimized` | Boolean | Copy optimized vs. original |
| `Color` | String | Hex color for UI badge |

### 6.4 Settings / Configuration

| Field | Type | Notes |
|-------|------|-------|
| `HotFolderPath` | String | Watched input directory |
| `ActiveSharePath` | String | Structured job storage root |
| `ProofsSharePath` | String | Flat proofs directory |
| `ArchiveRootPath` | String | Archive directory root |
| `ErrorDirPath` | String | Failed job destination |
| `OptimizationToolPath` | String | Path to optimizer exe/command |
| `Retries` | Integer | Max worker retries (default 3) |
| `WebServerPort` | Integer | HTTP server port (default 8765) |
| `EnableRemoteServer` | Boolean | Allow LAN access to job list |
| `WorkorderLookupUrl` | String | Template URL for job lookup |
| `ReportsServerUrl` | String | Template URL for work order viewer |
| `EnablePrintSmith` | Boolean | [PLANNED] Toggle PS integration |
| `PrintSmithBaseUrl` | String | [PLANNED] PrintSmith API base URL |
| `PrintSmithApiKey` | String | [PLANNED] Auth token/key |
| `PrintSmithApprovalField` | String | [PLANNED] PS field to write approval to (TBD) |
| `EnableCustomerPortal` | Boolean | [PLANNED] Toggle proof approval portal |
| `CustomerPortalBaseUrl` | String | [PLANNED] Public hostname for portal links |
| `PortalTokenExpiryDays` | Integer | [PLANNED] Token validity period (default 30) |

---

## 7. Non-Functional Requirements

### 7.1 Performance

**NFR-P-001** — The system SHALL begin processing a newly detected file within 5 seconds of the file becoming stable (fully written).

**NFR-P-002** — The web UI SHALL load and display the job list within 2 seconds under normal LAN conditions with up to 500 active jobs.

**NFR-P-003** — File serving (proof, original, thumbnail) SHALL begin streaming within 1 second for files up to 50 MB.

**NFR-P-004** — PDF proof generation SHOULD complete within 60 seconds for a typical single-page print job at 100 DPI.

**NFR-P-005** — Multiple jobs dropped simultaneously SHOULD be processed concurrently where system resources allow, without starving the UI or file watcher.

### 7.2 Reliability

**NFR-R-001** — The system SHALL continue functioning if PrintSmith is unreachable (graceful degradation).

**NFR-R-002** — The system SHALL continue functioning if the customer portal feature is disabled or its external hostname is unavailable.

**NFR-R-003** — The file watcher SHALL automatically recover from transient I/O errors without requiring an application restart.

**NFR-R-004** — All database write operations SHALL be atomic. A crash mid-operation SHALL not leave the database in a corrupt state.

**NFR-R-005** — The system SHOULD log all errors (processing failures, API errors, integration failures) to a persistent, reviewable log.

### 7.3 Security

**NFR-S-001** — All modifying API operations (state changes, dispatch, note saves, configuration updates) SHALL only be accepted from the local host. Requests from remote hosts SHALL be rejected with HTTP 403.

**NFR-S-002** — The customer portal SHALL be served over HTTPS with a valid certificate.

**NFR-S-003** — Customer portal tokens SHALL be cryptographically random and of sufficient entropy to be unguessable (minimum 128 bits of randomness).

**NFR-S-004** — The system SHALL NOT expose any internal file paths or database connection strings in API responses.

**NFR-S-005** — The system SHALL implement rate limiting on the customer portal endpoint to mitigate token enumeration attacks.

**NFR-S-006** — The PrintSmith API key SHALL be stored in the configuration file with filesystem permissions restricting access to the service account running the application.

**NFR-S-007** — No authentication credentials SHALL appear in log files.

### 7.4 Usability

**NFR-U-001** — The operator interface SHALL require no login or credentials.

**NFR-U-002** — A newly dropped job SHALL appear in the operator grid within 10 seconds of processing completing (accounting for UI refresh behavior).

**NFR-U-003** — The customer portal proof page SHALL be usable on mobile devices (responsive layout).

**NFR-U-004** — The customer portal proof approval experience SHALL require no plug-ins or downloads beyond a modern browser.

**NFR-U-005** — Error states SHALL be communicated clearly in the UI with an informative message, not a raw error code.

### 7.5 Maintainability

**NFR-M-001** — The PDF processing pipeline (proof, thumbnail, optimization) SHALL be abstracted behind a well-defined interface so that the underlying tool (worker) can be swapped without changes to the core job processor logic.

**NFR-M-002** — Integration modules (PrintSmith, customer portal) SHALL be independently loadable/unloadable via feature flags without affecting the core system.

**NFR-M-003** — All configuration SHALL be externalized. No hardcoded paths or environment-specific values SHALL appear in the application code.

---

## 8. Constraints

### 8.1 Legal / Licensing Constraints

**CON-L-001** — The system and all libraries it depends upon SHALL be licensed under terms that do **not** require the source code of this application to be made publicly available. Specifically:
  - Software licensed under the **GNU General Public License (GPL)** SHALL NOT be used unless isolated in a separately invoked process and not linked into the application.
  - Software licensed under the **GNU Affero General Public License (AGPL)** SHALL NOT be used under any deployment model, as AGPL applies copyleft to network-accessible software.
  - Preferred licenses: MIT, Apache 2.0, BSD (2- or 3-clause), commercial/proprietary.

**CON-L-002** — **Ghostscript** (licensed AGPL for the open-source version) SHALL NOT be used in this system. An alternative PDF optimization tool with a compatible license SHALL be identified and integrated. Options to evaluate include:
  - **pdfium** (BSD-licensed, Google's PDF library)
  - **iText** (AGPL for open source, but commercial license available)
  - **PDFium.NET SDK** (commercial)
  - **Aspose.PDF** (commercial)
  - **PDFSharp / MigraDoc** (MIT)
  - Custom Ghostscript usage **only** if the customer was able to install a non-included binary that was not distributed with this package.

**CON-L-003** — PyMuPDF (used for proof rendering and thumbnails) is licensed under **AGPL**. Its use SHALL be re-evaluated. Options include:
  - Obtain a commercial license from Artifex
  - Replace with a permissively licensed alternative (e.g., pdfium bindings, PdfPig + SkiaSharp)

> **Note:** Until a replacement is finalized and tested, the system MAY continue using the current Python worker as an interim measure with the understanding this creates a licensing obligation.

### 8.2 Operational Constraints

**CON-O-001** — The core application SHALL run on Windows without requiring administrator privileges for normal operation. (Initial URL reservation for the web server may require a one-time admin action via `netsh urlacl`.)

**CON-O-002** — The system SHALL function without a persistent internet connection for all core features. External features (PrintSmith, customer portal) MAY require connectivity.

**CON-O-003** — The system SHALL operate on a workstation shared by production operators. It SHALL consume minimal foreground resources and SHOULD run as a background/tray process.

**CON-O-004** — Database schema migrations SHALL be handled automatically at startup. Operators SHALL not be required to run manual DB upgrade scripts.

### 8.3 Technical Constraints

**CON-T-001** — The persistent store SHALL be a file-based embedded database (e.g., SQLite) requiring no external database server.

**CON-T-002** — The built-in web server SHALL NOT require IIS, Apache, nginx, or any external web server to be installed.

**CON-T-003** — The customer portal, if enabled, SHOULD be deployable behind a reverse proxy (the system SHALL respect `X-Forwarded-*` headers for URL generation).

---

## 9. Feature Flags & Configuration Schema

The following features MAY be independently enabled or disabled via the configuration file without code changes:

| Feature Flag | Default | Controls |
|---|---|---|
| `EnableRemoteServer` | `true` | Allow LAN clients to access the read-only job list |
| `EnablePrintSmith` | `false` | Enable bidirectional PrintSmith status sync |
| `EnableCustomerPortal` | `false` | Enable customer-facing proof approval portal |

When a feature flag is `false`, its associated configuration keys are ignored and its API routes are not served.

---

## 10. Glossary

| Term | Definition |
|------|-----------|
| **Hot Folder** | The monitored directory into which prepress staff drop incoming PDF files |
| **Proof PDF** | A low-resolution watermarked version of the original, generated for customer or operator review |
| **Optimized PDF** | A prepress-quality version of the original, produced for production printing |
| **Thumbnail** | A small raster image of the first page of the original, used for visual identification in the UI |
| **Stem** | The filename without its extension |
| **Job Number** | The leading digit sequence extracted from the filename stem |
| **Destination** | A configured target folder (local or network share) representing a production endpoint (press, cutter, etc.) |
| **Active Share** | The structured storage directory where processed job files are organized by job number |
| **Proofs Share** | A flat directory where proof PDFs are copied for external access (LAN share or portal) |
| **Operator** | A production staff member using the local desktop application |
| **LAN Viewer** | Any office staff member accessing the job list via a browser on the office network |
| **Customer** | An external or internal customer who receives a unique URL to review and approve/reject a proof |
| **PrintSmith** | The third-party MIS/ERP system used to manage sales orders and job tickets |
| **Portal Token** | A cryptographically random string embedded in the customer proof URL, used to identify the job without exposing internal IDs |
| **AGPL** | GNU Affero General Public License — a copyleft license that requires source code disclosure for software used over a network |
| **Copyleft** | A licensing principle that requires derived works to be distributed under the same license terms |
