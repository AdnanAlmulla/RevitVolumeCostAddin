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
folders seeded with a 0.6.0 install (36 checks in `tests/Installer/Test-MoveFormerInstall.ps1`: a 0.6.0 PC, the new name
already present, `-WhatIf`, a second run, a fresh PC, old folders held open so they can't be renamed) and with a stand-in
process for the old uploader; a `-WhatIf` run of the packaged installer on the engineer's PC
went through. Technician sheets updated (`sync-instructions.md`, and `release-check.md` section A–B for this item).
Same batch: ribbon icons for both themes, a Help button, a failed Sync to website said plainly, the ribbon can't cost the
after-sync snapshot, the uploader's log bounded and unable to end it, refused snapshots' shapes stop retrying, sent
files kept 90 days (T-002), Raw dump in the panel's slide-out (T-003).
Still to do: the technician's release check on a PC with 0.6.0 (the acceptance above).

### [F-005] P2 — Parameter standard window in the add-in (built in 0.7.0, awaiting its Revit check)
<!-- type: feature | added: 2026-11-27 -->
**What:** A window in Revit that covers what the Excel parameter standard does (website repo,
`docs/Revit parameter standard.xlsx`): each shared parameter, its allowed values, categories, what to fill and what a blank
means, then the settings, rules and examples.
**Why:** Engineer, 2026-11-27, after agreeing the parameters with the technicians: "The excel is good, but the plugin needs to
carry the identity of the latest instance parameters." The same day he added Roof to Division_Shared and Upstand to
Wall Function_Shared.
**Where:** `src/Addin/Standard/ParameterStandard.cs` (the standard, pure, tested), `ParameterStandardWindow.cs` (WPF built in
code), `Commands/ParameterStandardCommand.cs`, the Parameter standard button in `App.cs`, icon "parameters".
**Acceptance:** In Revit the window opens from the ribbon with no model open and matches the Excel sheet word for word
(release check step 9).
**Progress (2026-11-27):** Built into 0.7.0: 7 tests on the standard (the five names, Roof, Upstand, blanks, no stray
spaces), compiles for Revit 2024 and 2026, the window rendered outside Revit and looked at. Not yet seen in Revit. Every
later change to the standard is a change here, in the Excel sheet and on the website.
**Progress (2026-11-27, later):** The window now shows the website's copy (fetched by the uploader, F-007) when complete,
else the built-in one, and says which; checked on .NET Framework with the Revit 2024 build.

### [F-006] P3 — Check the open model against the standard (proposed, engineer's call)
<!-- type: feature | added: 2026-11-27 -->
**What:** From the Parameter standard window, count in the open model, per parameter, the elements left blank and those
holding a value not on the list (a misspelling the website would show as "Unassigned"), and select them in Revit.
**Why:** The website matches values exactly; catching a misspelling in Revit, before the sync, saves a round trip.
**Where:** `src/Addin/Standard/` (a pure rule over values, tested; a thin Revit read reusing `SnapshotReader.ModelElements`).
**Acceptance:** On a test model with known blanks and misspellings the counts are right and Select highlights them.
**Notes:** Not asked for: offered on 2026-11-27. Read-only. Ships only in a batched release.

### [F-007] P1 — Updates through the uploader (built in 0.7.0, awaiting its first real update)
<!-- type: feature | added: 2026-11-27 -->
**What:** The uploader asks the website for the newest release Adnan has published, downloads and checks it, installs it
beside the running version and points each installed Revit year's manifest at it; Revit then shows "New version of
Telltale Addin ready, restart Revit please" once. The uploader updates itself, and fetches the parameter standard for
the Parameter standard window.
**Why:** Engineer, 2026-11-27: agreed to updates through the uploader so 0.7.0 is the last zip handed out, the parameter
window reads the standard from the site, and the technician sees that note (not a silent install).
**Where:** `src/Uploader/UpdateCheck.cs`, `AddinUpdates.cs`; `src/Addin/Updates/`; `Manifest.addin.template`
(`$Version$`); `installer/Install-Addin.ps1` (version folders, Run entry); `Build-Package.ps1 -Version`; website repo
for the endpoints and "Publish add-in.cmd". CLAUDE.md "Updates" has the design.
**Acceptance:** On a technician's PC with 0.7.0, a published test 0.7.1 is installed by itself, the note shows once in an
open Revit, the next Revit start runs 0.7.1, the uploader runs from `Uploader\0.7.1` and starts from there at sign-in
(release check section F).
**Progress (2026-11-27, afternoon):** A technician runs 0.7.0 in Revit (Help opened). Adnan published the test 0.7.1 at
11:19 UTC (checked: the download serves the same bytes). He then found the hourly wait too long when he wants a version
out now, so 0.7.2 adds Help's Check for updates (request and result files beside the outbox; the uploader wakes at once)
and a check right after each sent snapshot; the background check stays hourly (engineer). Trials: 0.7.0 to 0.7.2 (21
checks), 0.7.2 to a 0.7.3 test (25 checks, answers in under a second). Adnan published 0.7.2 at 11:47 UTC (SHA-256 412fdee1..., the build
committed as 24258e0); a PC still on 0.7.0 goes straight to 0.7.2. Don't rebuild artifacts\Telltale-0.7.2.zip: the
next build is 0.7.3.
**Progress (2026-11-27):** Built and committed. 23 new unit tests; `tests/Updates/Test-UpdateEndToEnd.ps1` passed (real
0.7.0 uploaders against a stand-in site offering a 0.7.1 test package, scratch folders, 21 checks: manifest repointed, old
version kept, Revit 2024 left alone when not installed, uploader staged, standard saved, a second run idle, a running
uploader handing over to 0.7.1 and ending, a corrupted download installing nothing, a site that is down changing nothing). The website side is committed (website repo e19227c,
976a4a1), not live until a site restart Adnan approves. Not checked: Revit itself, the Run entry (it acts on the real
registry), the real installer beyond `-WhatIf`, antivirus. Open, for the engineer: whether to sign releases (the hash
proves the download is whole, not who made it; the site is plain http).

### [T-005] P2 — Uploader: say more in its log, and remove the old Startup shortcut (next version)
<!-- type: tech debt | added: 2026-11-27 -->
**What:** Three small changes to the uploader, for the next batched version after 0.7.2:
1. A second copy that exits because one is already running writes a line ("another copy is running; this one exits")
   before it ends. Today it exits silently, before "Started".
2. Each update check that finds nothing newer writes one line ("update: up to date (x.y.z)"). Today it writes nothing,
   so the log can't show that a check happened.
3. At start (not in a scratch trial), delete `<Startup folder>\<product> uploader.lnk` when it exists. Builds of 0.7.0
   made before about 15:00 on 2026-11-27 started the uploader from that shortcut; later installs use the Run entry and
   delete the old flat `Uploader\` folder, so the shortcut points at a file that no longer exists.
**Why:** 2026-11-27, the technician's log after a relaunch showed no "Started" and no "update:" line. The code explains it
(nothing newer at the start check, so no line; a relaunch while a copy runs exits silently), but the log should say so.
The stale shortcut sits on that technician's PC (installed 14:08 from an early 0.7.0 build, reinstalled 15:15).
**Where:** `src/Uploader/Program.cs` (the mutex check; the start), `src/Uploader/UpdateCheck.cs` (the up-to-date return).
**Acceptance:** The trial (`tests/Updates/Test-UpdateEndToEnd.ps1`) sees the up-to-date line and the second-copy line; a
scratch Startup folder's old shortcut is removed (pass the Startup folder as an override, as the AppData folders are).

### [T-006] P3 — Dialog titles repeat the product name (next version)
<!-- type: tech debt | added: 2026-11-27 -->
**What:** The "restart Revit please" note's title bar reads "Telltale - Telltale" (engineer, 2026-11-27, on the
technician's PC: "Something to fix for the future"). Revit puts the add-in's name (the manifest's Name) in front of each
TaskDialog title, and `UpdateNotice` passes the product name as the title. Give each dialog a title of its own, without
the product name: the note "Update ready", Help "Help" (now "Telltale - Telltale help"), "Parameter standard" and "Check
for updates" windows are WPF (no prefix) and stay as they are. Sync to website, Raw dump and the failure messages already
have their own titles.
**Where:** `src/Addin/Updates/UpdateNotice.cs`, `src/Addin/Commands/HelpCommand.cs` (both TaskDialogs and its error box).
**Acceptance:** In Revit the note reads "Telltale - Update ready" and Help "Telltale - Help" (technician's check).
**Notes:** First sighting of the note in Revit: it appeared on the technician's PC on 2026-11-27 after the update from
0.7.0 (engineer, 12:01 UTC). Batch with T-005.

## Done

- [[F-002]](docs/backlog-archive.md#f-002-division-element-kind-and-concrete-grade-conventions) Conventions agreed: a parameter standard the technicians follow — 2026-11-26
- [[T-004]](docs/backlog-archive.md#t-004-one-copy-of-the-standard-for-the-add-in-and-the-website) Done another way: the website's tests compile `ParameterStandard.cs` and check its copy matches — 2026-11-27
- [[T-002]](docs/backlog-archive.md#t-002-how-long-outboxsent-keeps-sent-files) Sent files kept 90 days, then deleted (0.7.0) — 2026-11-27
- [[T-003]](docs/backlog-archive.md#t-003-raw-dump-out-of-sight-on-the-ribbon) Raw dump moved to the Quantities panel's slide-out (0.7.0) — 2026-11-27
- [[F-004]](docs/backlog-archive.md#f-004-raw-dump-of-a-real-tower) Raw dump of a real tower: ran, matched Revit's takeoff, snapshot classes v1 written from it — 2026-11-26
