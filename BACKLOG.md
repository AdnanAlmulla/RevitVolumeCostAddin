# Backlog

Persistent list of work for the Telltale add-in (working name until 2026-11-27: Revit Volume & Cost). Items are implementable
cold: read each item's **Where** and **Acceptance** before starting, and follow the repo `CLAUDE.md`
conventions.

IDs are permanent: `F-` feature, `B-` bug, `T-` tech debt. Never reused. This repo's IDs are its own;
the website repo keeps a separate list.

This file holds only outstanding work. A landed item's full record moves to
[docs/backlog-archive.md](docs/backlog-archive.md); `## Done` keeps a one-line index
linking to it. Conventions the shipped code follows are documented in `CLAUDE.md` —
that, not the archive, is the spec.

Out of scope (engineer, 2026-11-25): whether the model's volumes are right (overlaps, joins) is the
Revit technician's job, so there are no items for it.

## Active

### [F-001] P1 — One Revit add-in, Revit 2024 and 2026 first, then 2022 to 2027
<!-- type: feature | added: 2026-11-25 -->
**What:** The add-in project: one csproj with configurations R22 … R27 (`net48`, `net8.0-windows`, expected
`net10.0-windows` for 2027), one `.addin` manifest per year, and `src/Contracts` (`netstandard2.0`). It reads
one row per element of the host model, converts ft³ / ft² to m³ / m² in one place, and writes a snapshot to an
outbox folder after a successful Synchronize with Central and on a manual Sync (not on Save). The add-in carries no
third-party files. A small separate uploader, built once, signs in with its own MSAL wrapper and sends the outbox,
retrying until each file lands. WPF UI. Every structural material is read, not only concrete.
**Why:** The add-in is the website's only source of data.
**Where:** `src/Addin/`, `src/Contracts/`, `tests/` (planned layout in `CLAUDE.md`).
**Acceptance:** It builds for Revit 2024 and 2026 first, other years as projects need them; the BIM technician confirms it loads and syncs in at least the oldest and
newest Revit installed in the office, returning a snapshot JSON and a schedule CSV whose totals an automated test
matches; the unit conversion has unit tests against hand-checked element volumes; a sync is not slowed
down measurably.
**Notes:** A snapshot identifies its model by its ACC project id and model id (cloud models have no file path).
Confirm the 2027 runtime against Autodesk's 2027 SDK. Element tagging waits for F-002. The uploader's target
needs the website's API and Entra registration (website repo); until then, test against a local stub.
Moved here from the website repo's F-003 on 2026-11-25.
Sign-in is deferred (engineer, 2026-11-26): build the uploader so the token step can be added later; the upload
address is a per-PC setting.
**Progress (2026-11-26, v0.2.0):** Snapshot classes v1 (`src/Contracts`, facts only), the ribbon tab (Sync to website,
Raw dump), the snapshot after each successful Synchronize with Central of a cloud model (read-only, logged and
timed; switched off per PC until the manual snapshot's time is known; it does not yet skip non-structural models), the outbox (`%LocalAppData%\<product>\Outbox`) and the uploader (`src/Uploader`: Sent / Rejected / retry,
checked end to end against a test site). **First run in Revit (2026-11-26):** Sync to website on a real tower read
6,002 elements in 10.9 s (first) and 3.7 s (second), none unreadable; per-material volumes equal the raw dump's on
all 11 materials (68,802.0 m³). Version 0.3.0 (2026-11-26) takes the snapshot after every Synchronize with Central
by default, as the engineer ruled. Still to run in Revit: that automatic snapshot on a real cloud sync, the uploader on
a modeller's PC, Revit 2024.
**Progress (2026-11-26, v0.4.0):** The site is on the office network behind a site password (website T-006); the
uploader sends it from `uploader.json`, ships in the package, is installed and started at sign-in by `Install.cmd`, and
was checked end to end against a test copy of the site (refused password retried, then sent; a second copy exits).
Still to do: the first run on a modeller's PC; Microsoft sign-in in place of the password (website T-004).
**Progress (2026-11-26, v0.1):** The project exists with configurations R24 and R26, the product-name property, the
build-time `.addin` manifest, the installer, CI (`.github/workflows/build.yml`, not yet run: nothing pushed) and the
ft³ / ft² to m³ / m² conversion (`src/Addin/Units/RevitInternalUnits.cs`) with tests against hand-checked beam,
column and slab volumes. Still to do: `src/Contracts`, the snapshot, the Sync triggers, the outbox, the uploader.

### [F-003] P3 — Time the 3D shape export on the largest tower (deferred)
<!-- type: feature | added: 2026-11-25 -->
**What:** For the website's 3D viewer (decided 2026-11-25: three.js on the site), the add-in will export per-element shapes keyed by Revit
element id. First step only: time that export on the largest tower model.
**Why:** The export must not slow down a Synchronize with Central. The viewer approach is decided; only its timing is open.
**Where:** `src/Addin/Commands/ShapeExportCommand.cs` and `src/Addin/Shapes/` (a "3D shapes" button: the technician
runs it, so it ships in the add-in rather than under `_harnesses/`).
**Acceptance:** The export time on the largest tower is measured and put to the engineer.
**Notes:** Deferred by the engineer (2026-11-25). The viewer side is the website repo's F-006.
The viewer shows the whole structural model (concrete highlighted, the rest grey), so the export covers every
structural element, steel included (engineer, 2026-11-25).
**Progress (2026-11-26, v0.5.0):** Started at the engineer's word ("We can proceed with the 3D view"). The button reads
the shapes of the elements a snapshot holds, twice: Medium detail with the coarsest triangles, written to
`Documents\<product>\Shapes\*.shapes.json.gz` (format "shapes-1": element id, UniqueId, category, corners in whole mm,
triangles; provisional, kept out of `src/Contracts`), then Fine detail with finer triangles, counted only. It logs
the time spent asking Revit for geometry, cutting faces into triangles and writing, per pass, with counts per
category (elements with no shape, triangles), to `addin.log`, and progress every 1,000 elements. Not run in Revit
yet; not part of a sync (the engineer rules on that with the times). Sheet: `docs/technician/sync-instructions.md`
section 3.
**First run (2026-11-26, Revit 2026, the tower moved to ACC as a test model, 6,002 elements):** written setting
(Medium, coarsest) 1.4 s the first time and 0.9 s the second (geometry 0.5-0.6 s, triangles 0.3-0.6 s, writing 0.1-0.2
s), 279,527 triangles, 1.2 MB; finer setting (Fine, 0.5) 2.3 s and 1.1 s, 1,100,729 triangles (floors 750k of them).
159 elements failed at the written setting and none at the finer one, so they were missing from the file; v0.5.1 reads
a failed element again at the finer setting and logs each failure's reason. Same run: the snapshot after Synchronize
with Central fired on the cloud model (read 9.0 s), and the uploader delivered all four snapshots once its password
was corrected. Ruled the same day: shapes ride on every sync (0.6.0, built and checked through the uploader against
a test copy of the site; the Revit side not yet run).

### [T-001] P3 — Rename the add-in to Telltale, in the next batched release (held)
<!-- type: tech debt | added: 2026-11-27 -->
**What:** Set `ProductName` to `Telltale` (`Directory.Build.props`), so the ribbon tab, DLL, `.addin` manifest,
`%LocalAppData%\<product>\` (Outbox, Logs, Uploader), `%AppData%\<product>\` (`uploader.json`, `addin.json`), the
Startup shortcut, the uploader's mutex and the package name all follow. The installer must also clear the old name, for
each Revit year: delete `RevitVolumeCost.addin` and its folder; stop the old uploader and delete its Startup shortcut
and folder; carry `uploader.json` and `addin.json` across (so the technician is not asked for the password again); move
unsent files from the old Outbox into the new one. Update the technician sheets to the new tab, files and folders.
**Why:** The engineer named the product Telltale (2026-11-27). Changing the name beside an installed copy loads two
add-ins (two snapshots per sync), runs two uploaders and leaves the old outbox unsent.
**Where:** `Directory.Build.props`, `installer/Install-Addin.ps1`, `docs/technician/*`, `Build-Package.ps1` (reads the name).
**Acceptance:** On a PC with the old version installed and an unsent snapshot, the installer leaves one add-in, one
uploader (running, with the old settings) and the snapshot sent; a fresh PC installs as before.
**Notes:** Held, not a reason for a release by itself (engineer: no new add-in version "unless absolutely necessary";
batch changes). The website does not care about the add-in's name: a snapshot carries no product name, and its files
are found by extension. Website repo T-002 tracks the rest of the rename.
**Progress (2026-11-27, 0.7.0):** Built and committed with the batch below, not handed out. `ProductName` is `Telltale`;
`installer/Move-FormerInstall.ps1` (run by `Install.cmd` before anything else) stops the old uploader, deletes its
Startup shortcut and the old manifest and folder from every `Addins\<year>`, and renames `%AppData%\RevitVolumeCost` and
`%LocalAppData%\RevitVolumeCost` (without the old uploader program) to the new name; when the new name exists already, it
moves only the settings files and the complete unsent outbox files it lacks, never overwriting. Checked against scratch
folders seeded with a 0.6.0 install (35 checks: a 0.6.0 PC, the new name already present, `-WhatIf`, a second run, a fresh
PC) and with a stand-in process for the old uploader; a `-WhatIf` run of the packaged installer on the engineer's PC
went through. Technician sheets updated (`sync-instructions.md`, and `release-check.md` section A–B for this item).
Same batch: ribbon icons for both themes, a Help button, a failed Sync to website said plainly, the ribbon can't cost the
after-sync snapshot, the uploader's log bounded and unable to end it, refused snapshots' shapes stop retrying.
Still to do: the technician's release check on a PC with 0.6.0 (the acceptance above).

### [T-002] P3 — Decide how long Outbox\Sent keeps sent files (proposed, engineer's call)
<!-- type: tech debt | added: 2026-11-27 -->
**What:** The uploader moves every sent snapshot and shapes file to `%LocalAppData%\<product>\Outbox\Sent` and never
deletes them. Proposal: delete files there older than a set age (90 days suggested) at the start of each pass.
**Why:** About 1.5 MB per sync (0.3–0.6 MB snapshot, about 1.2 MB shapes, 6,000-element tower). At 10 syncs a day that is
about 15 MB a day, roughly 4 GB a year per modeller's PC. Against it: while the website has no backups, Sent is the only
second copy of the data, so deleting it is a real loss.
**Where:** `src/Uploader/Program.cs` (a pruning step in `SendOutboxAsync`), the rule pure and tested in `UploadRules.cs`.
**Acceptance:** The engineer picks an age, or rules to keep everything; files older than it are gone after the next pass
and younger ones are untouched (tested with a temporary folder).
**Notes:** Not built: deleting data on technicians' PCs is the engineer's decision. Ships only in a batched release.

### [T-003] P3 — Whether Raw dump stays on the ribbon (proposed, engineer's call)
<!-- type: tech debt | added: 2026-11-27 -->
**What:** Raw dump (F-004) was a one-off for the parameter standard, and it sits beside Sync to website on every
modeller's ribbon. Options: keep it; move it to the Quantities panel's slide-out (`RibbonPanel.AddSlideOut()`), out of
sight until asked for; or remove it.
**Why:** A button that is not for daily use invites clicks, and a raw dump of a large tower holds Revit for minutes.
**Where:** `src/Addin/App.cs` (`AddRibbon`), the technician sheets.
**Acceptance:** The engineer's choice, in the next batched release.

## Done

- [[F-002]](docs/backlog-archive.md#f-002-division-element-kind-and-concrete-grade-conventions) Conventions agreed: a parameter standard the technicians follow — 2026-11-26
- [[F-004]](docs/backlog-archive.md#f-004-raw-dump-of-a-real-tower) Raw dump of a real tower: ran, matched Revit's takeoff, snapshot classes v1 written from it — 2026-11-26
