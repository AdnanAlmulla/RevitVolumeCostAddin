# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**The Revit add-in of Telltale** (repo `TelltaleAddin`): a Revit add-in for Revit
2022–2027 that reads the quantities of a structural model (concrete, steel and other materials) and sends them, as a snapshot, to the
project's internal website. It also holds the **snapshot classes** (`src/Contracts`) that the website's API
compiles against.

The website (API, database, site) lives in a separate private repo, `Telltale`. This repo was split
off on 2026-11-25 so that Revit-side contributors work here only (engineer's ruling).

**The name is Telltale** (engineer, 2026-11-27). "Revit Volume & Cost", `RevitVolumeCost` and
`RevitVolumeCostAddin` were working names. The product name lives in one MSBuild property
(`Directory.Build.props`, `ProductName`) and nowhere else in code. The assembly name, the add-in's folder and its `.addin` manifest (written at build time from
`src/Addin/Manifest.addin.template`) derive from it, and the code reads it back from the assembly name. C#
namespaces are written in source, so they stay neutral (`Addin.*`; `Contracts.*` when it exists) and never
carry the name. **`ProductName` is `Telltale` from 0.7.0** (`RevitVolumeCost` up to 0.6.0; backlog T-001): it also
names the ribbon tab, the DLL and manifest, the outbox, the settings and log folders, the uploader's folder, shortcut
and mutex. So an old copy left beside a renamed one would load two add-ins and strand the old outbox:
`installer/Move-FormerInstall.ps1`, run by `Install.cmd` first, clears a copy installed under a former name away and
carries its settings, outbox and logs over. Its list of former names is the one place a product name is written as
text outside `Directory.Build.props`; add the current name to it before ever renaming again. The `AddInId` in the
manifest template stays the same across names.

**Who runs what in Revit** (engineer, 2026-11-25): the engineer does not open Revit. The BIM technician
installs the add-in, fills the parameters, runs Export / Sync and does the per-release check. Every Revit step
ends in a hand-off the engineer can use without Revit: an installer plus a one-page instruction sheet going to
the technician; a snapshot JSON plus Revit's quantity schedule exported as CSV coming back. An automated test
compares the two. Build plan: https://claude.ai/artifact/1N5ebV79ww98qsfvBJwv2a

**This is not a CSi plugin.** If this repo is cloned under the engineer's `github repositories` folder,
Claude Code also loads the folder-level `../CLAUDE.md`, written for the ETABS/SAFE plugin family. Those rules
do **not** apply here: `cPlugin.Main`, `Finish(0)`, `cSapModel`, Avalonia hosting, the CSi API references,
the shared-components inventory and the plugin release tool. These **do** apply: the readability rules
(units in names, named and cited constants, one step per line), xUnit tests that compile pure source in by
path, and the `BACKLOG.md` convention.

Status (2026-11-26): **version 0.2.0 has run in Revit.** The technician's Sync to website on a real tower (Revit
2026.5, 6,002 elements) read in 10.9 s the first time and 3.7 s the second, none unreadable, and its volume per
material equals the raw dump's exactly. Version 0.3.0 takes the snapshot after every Synchronize with Central by
default (engineer, 2026-11-26). Version 0.4.0 ships the uploader in the hand-off package: installed by `Install.cmd`,
started at Windows sign-in, sending to the website on the office network with its site password (checked against a
test copy of the site; not yet on a modeller's PC). Version 0.5.0's **3D shapes** button timed the shape export on
the tower (0.9 to 1.4 s, F-003). Version 0.6.0 writes the shapes beside every snapshot instead (engineer, 2026-11-26:
"Yes, the sync should send the shape. I wouldn't want them to click on the 3D button each time"); the button is gone.

**Where things stand (end of 2026-11-26):** 0.6.0 is with the engineer and **not yet run in Revit**. He first agrees the
parameters with the technicians (2026-11-27; `Telltale/docs/Revit parameter standard.xlsx`, names in the
website's `appsettings.json` "Classification"), then has 0.6 tested on a tagged model. Its first sync proves the shapes
after a sync and their upload; the website side is live. **He does not want to re-issue versions lightly: hold add-in
changes and batch them into one version.** Models are assumed to be on ACC (engineer, 2026-11-26: the technicians are
"centralizing to ACC"); the after-sync snapshot skips models not in ACC, as ruled on 2026-11-25. No changes are held
at the moment beyond 0.7.0.

**Where things stand (2026-11-27):** **0.7.0 is built and committed, not handed out, not run in Revit.** It batches
0.6.0's untested shapes-after-sync with the rename to Telltale (T-001), ribbon icons for Revit's light and dark themes,
a Help button, a Parameter standard window, **updates through the uploader** (below), Raw dump moved out of sight, sent
files kept 90 days, and robustness fixes (below). **0.7.0 is meant to be the last zip handed out** (engineer, 2026-11-27):
that holds only once a real update has run on a technician's PC. **Adnan published a test 0.7.1 on the website on
2026-11-27** (11:19 UTC, before anyone had 0.7.0), so a PC that installs 0.7.0 moves to 0.7.1 within minutes (release
check section B); the "restart Revit" message needs Revit open when an update lands, so it is checked with the next
release (section F). The next real release is 0.7.2 or later. One technician runs `docs/technician/release-check.md` (shipped in the
package as "Release check.txt") on a PC with 0.6.0 before it goes to everyone. The shared parameters being agreed with
the technicians never break the snapshot: it names no parameter and sends them all. The **Parameter standard** window
shows the website's copy of the standard when the uploader has fetched one, else the copy built in
(`src/Addin/Standard/ParameterStandard.cs`, as agreed on 27 Nov 2026; engineer: "the plugin needs to carry the identity
of the latest instance parameters"), and says which. So a change to the standard reaches the technicians through the
website; the built-in copy and the Excel sheet should follow it.

## How work lands here

- **Every contributor's change arrives as a pull request into `master`, and the engineer (Adnan Almulla)
  reviews and merges it.** Contributors never push to `master`. The repo is **public** (engineer, 2026-11-25) so
  that a branch ruleset protecting `master` is available on GitHub's free plan; the engineer creates and owns
  that ruleset. See `CONTRIBUTING.md`.
- **`src/Contracts` is shared with the website.** The website repo builds against a clone of this repo
  placed beside it, so a change to a snapshot class changes what the API receives. Keep changes additive
  (new optional fields); renaming or removing a field needs the engineer's say-so in the pull request.
- **The website's tests also compile `src/Addin/Standard/ParameterStandard.cs` and `StandardContent.cs` by path**
  (website thread, 2026-11-27): one test checks the site's standard equals `ParameterStandard`'s `AGREED_ON`,
  `PARAMETER_SETTINGS`, `RULES`, `EXAMPLES` and `PARAMETERS` word for word, another that `ParameterStandardFile.Read`
  accepts the site's JSON. Keep both files pure (no Revit, no WPF), keep those names, and don't move them: otherwise the
  website's whole test project stops compiling. A change to the built-in standard must match the website's.

## What the add-in does (engineer's rulings, 2026-11-25)

```
Revit 2022–2027 + this add-in --> outbox folder --> uploader --(snapshot, Entra token)--> website API (other repo)
```

- **It pushes snapshots.** There is no live link from the website to Revit. The add-in sends one row per
  element of the **host model** (no linked models; structural models only) after a successful Synchronize with
  Central and on a manual Sync button, **not on Save** (engineer, 2026-11-25: on a shared ACC model a Save holds
  one modeller's unfinished local copy). Ignore family files, models not on ACC and detached copies.
- **The add-in writes, the uploader sends** (engineer, 2026-11-25). The add-in writes each snapshot to an outbox
  folder on the PC and carries **no third-party files** (no library clash with other add-ins in the same Revit).
  A small separate uploader program, built once for all Revit years, signs in and sends the outbox, retrying
  until each file lands. Nothing slows a sync.
- **Model identity:** the models are ACC cloud models with no file path, so a snapshot names its model by
  its **ACC project id and model id**, never by a path or file name.
- **Sign-in:** Microsoft 365 (Entra ID), in the uploader only, never inside Revit. The uploader has its
  **own** small MSAL wrapper that gets a token for the website API's upload scope. It does **not** use `KillaPluginAuthentication` (that belongs to the
  CSi plugins: "this is not a CSi plugin"). **Sign-in is deferred** (engineer, 2026-11-26). The upload address is a
  setting on each PC, never committed here.
- **No cost in the add-in.** Rates live on the website. The add-in sends quantities only.
- **Our data never depends on a third-party service.** Autodesk's cloud may later serve a 3D viewer only.

## One code base for Revit 2022–2027

| Revit | Runtime | Target |
|---|---|---|
| 2022, 2023, 2024 | .NET Framework 4.8 | `net48` |
| 2025, 2026 | .NET 8 | `net8.0-windows` |
| 2027 | .NET 10 (confirmed by Autodesk's 2027 SDK, April 2026) | `net10.0-windows` |

**Start with two years: Revit 2026 and Revit 2024** (engineer, 2026-11-25: "start with the latest models"), the
newest the office uses on each runtime. Add other years when a live project needs them; build 2027 once the office
has it. Revit 2025 and 2026 may move to .NET 10 by update; keep building them for .NET 8 (which loads on .NET 10)
and have the technician note Revit's exact build in every check.

- One csproj with one build configuration per Revit year (R22 … R27), each referencing that year's
  Revit API. One `.addin` manifest per year, installed to `%AppData%\Autodesk\Revit\Addins\<year>\`.
- Keep the Revit-touching layer thin. Snapshot classes live in `src/Contracts` (`netstandard2.0`) so
  net48, net8, net10 and the website's API all reference the same code.
- `ElementId` became 64-bit in Revit 2024. Handle that in one place with conditional compilation.
- The add-in UI (Sync status, settings) is **WPF**, not Avalonia: Revit is WPF-hosted, and another
  add-in's Avalonia version in the same Revit process could collide with ours.
- Revit API calls run on Revit's thread, inside Revit's API context. Only the upload leaves it.

## Units (engineer's ruling, 2026-11-25: be specific, and make it testable)

- Revit's internal volume unit is **cubic feet** (and square feet for area). Convert to m³ / m² in exactly
  one place, with the unit in every name (`volumeFt3`, `volumeM3`) and the factor as a named constant
  (1 ft = 0.3048 m exactly, so 1 ft³ = 0.028316846592 m³ and 1 ft² = 0.09290304 m²).
- That conversion has unit tests against hand-checked element volumes.
- A snapshot carries m³ and m² only.

## What a snapshot carries

- **Snapshot:** a snapshot id made by the add-in, UTC time, who synced, add-in version, Revit's exact build,
  whether the model is shared, which trigger fired, the model's ACC project id and model id.
- **Facts only** (engineer, 2026-11-25): the add-in sends category, family, type, level, material and the raw
  value of each named parameter. It never classifies (retaining or shear wall, which division): the website holds
  those rules per project. One row per element and material; area is optional (beams and columns have none).
- **As built, schema 1 (2026-11-26; `src/Contracts/Snapshot.cs`):** facts only. Every model element with a material
  volume (shoring walls and a stair's runs and landings included: the website's rules leave them out), its materials
  (name, class, m³, m²), its level, base and top level names, bounding-box bottom and top (m), and every project and
  shared parameter with a value, on the element and, once per type, on its type. Lengths, areas and volumes in
  metric. No parameter name is written in code: the website picks them by name (a per-project setting there).
  About 0.3–0.6 MB gzipped for a 6,000-element tower.
- **Element row:** Revit's `UniqueId` (the key) and element id (64-bit, for display), category, family/type, level, material, volume (m³), area (m²), count, and
  the tags below.

Rows are per element and never pre-aggregated: the website filters any combination of tags (for example
podium and tower only, beams and columns only, one concrete grade).

**Reference: the quantity table the office asks for today** (engineer, 2026-11-25). Columns: Ref,
Division, Element, Type / Size, Concrete Grade, Qty (nos), Volume (m³), Remarks.

| Division | Elements |
|---|---|
| 1. Piles | Pile, by type and diameter (mm) |
| 2. Foundation | Raft, Pile Cap |
| 3. Basement | Retaining Wall, Shear Wall, Column, Slab, Beam |
| 4. Ground Floor | Shear Wall, Column, Slab, Beam |
| 5. Podium | Shear Wall, Column, Slab, Beam |
| 6. Tower | Shear Wall, Column, Slab, Beam |

So each row needs:
- **Division** (divisions are set per project, not a fixed list). Substructure / podium / superstructure are
  groupings of divisions.
- **Element kind**, finer than Revit's category: walls split into retaining and shear walls; structural
  foundations into piles, rafts and pile caps.
- **Type / size** (pile diameter), **concrete grade** and a **count**.
- **RC or PT**: another instance parameter like division and grade (engineer, 2026-11-25), read and sent as
  one more tag; blanks reported.
- The **material** as now; the website derives mass (volume × density) and embodied carbon from it.
- Elements outside the table (stairs, ramps, upstands…) are still sent; the website files them under
  "Other / Misc".

**Division and concrete grade are instance parameters** on the elements (engineer, 2026-11-25). Still open
(backlog F-002): the parameters' exact names, whether they become shared project parameters bound to the
structural categories (a stable GUID across projects and Revit versions; suggested, not decided), how element
kind and pile type are marked, and whether division can default from a per-project level mapping with the
parameter as an override. The add-in reports elements whose parameter is blank; it never guesses a value.
The BIM technician's first job is a one-off **raw dump** of a real tower (F-004): every structural element with
its category, family, type, levels, materials with volumes, and every parameter name and value. It answers most of
F-002 by evidence. Until then pick no parameter names; once named, they are a setting the add-in reads, bound as
shared parameters.

**Not only concrete** (engineer, 2026-11-25). Every structural element is sent whatever its material: steel and
other materials are quantified, costed and given carbon on the website (mass = volume × a density typed in on
the site). Still open (F-002): how a steel element's grade or section is marked. Ask before adding fields.

Whether the volumes in the model are right (overlaps, joins) is the Revit technician's job, not the
add-in's.

## Layout

```
src/Addin/            Revit add-in, configurations R24 and R26 (others when needed)
  App.cs              starts with Revit: the snapshot after each Synchronize with Central, then the ribbon tab
  Commands/           SyncCommand (Sync to website), ParameterStandardCommand, RawDumpCommand, HelpCommand; all read-only
  Standard/           ParameterStandard, StandardContent (pure, tested; the website's tests compile both); ParameterStandardWindow (WPF, in code)
  Updates/            ReleaseVersions (pure, tested; the uploader compiles it too); UpdateNotice: the "restart Revit" note
  Ribbon/, Resources/ RibbonIcons loads the icons embedded from Resources/Icons (light and dark, 32 and 16 px)
  UploaderSettingsFile.cs, AddinSettings.cs   pure: the website's address for Help; the after-sync switch
  Shapes/             ShapeOutbox and ShapeReader (Revit); ShapeMesh and ShapeFile (pure, tested): the "shapes-1" file
  Snapshots/          SnapshotReader (Revit), SnapshotJson and SnapshotOutbox (pure, tested)
  RawDump/            RawDumpWriter: the F-004 dump, format "raw-dump-1"
  Json/, Units/       pure code (no Revit API), compiled into the tests by path
  RevitApi/           ElementIds: the one place an ElementId becomes a number
tests/Addin.Tests/    xUnit, pure source compiled in by path
tests/Installer/      Test-MoveFormerInstall.ps1: the installer's migration, against scratch folders (never the real ones)
installer/            Install.cmd + Install-Addin.ps1 + Move-FormerInstall.ps1, shipped in the hand-off package
docs/technician/      sync-instructions.md (the sheet: READ ME FIRST, and Help opens it) and release-check.md
tools/                Draw-RibbonIcons.ps1: draws the icon PNGs from outlines; -Preview shows them on both themes
.github/              CI, and the pull request form contributors fill in
Build-Package.ps1     builds the hand-off package into artifacts/ (not committed)
src/Contracts/        snapshot classes v1 (netstandard2.0); compiled into the add-in and the website by path
src/Uploader/         sends the outbox to the website (.NET 8, no packages); --once, --site, --outbox, --local-app-data,
                      --roaming-app-data; UpdateCheck (network) and AddinUpdates (file work, tested) install updates
tests/Updates/        Test-UpdateEndToEnd.ps1 + stub_site.py: a real uploader updates scratch folders from a stand-in site
_harnesses/           throwaway probes, never part of a build
```

## Building

- `dotnet build src/Addin/Addin.csproj -c R26` (or `-c R24`); `dotnet test tests/Addin.Tests/Addin.Tests.csproj`.
  `global.json` pins the .NET 8 SDK. CI (`.github/workflows/build.yml`) runs the same three commands.
- The Revit API comes from Nice3point's reference packages (`Nice3point.Revit.Api.RevitAPI`/`RevitAPIUI`),
  **compile-time only** (`ExcludeAssets="runtime"`), so CI builds without Revit and nothing of theirs ships.
  `Build-Package.ps1` refuses to package if any DLL but ours is in the build output.
- **The hand-off package** (`powershell -ExecutionPolicy Bypass -File .\Build-Package.ps1`): tests, then
  `artifacts\<product>-<version>\` and its `.zip`, holding one folder per Revit year (`<product>.addin` +
  `<product>\<version>\<product>.dll`), the uploader, `Install.cmd` and the instruction sheets. The installer copies each year's files
  to `%AppData%\Autodesk\Revit\Addins\<year>\` for each Revit installed, removes Windows' "downloaded" mark
  (Revit 2024 can refuse a marked DLL), refuses while Revit is open, and never runs the add-in from a network share (the ETABS crash of 2026-11-23).
- **3D shapes ride with every snapshot** (0.6.0): `ShapeOutbox` writes `<snapshot name>.shapes.json.gz` beside it in
  the outbox ("shapes-1": the snapshot's elements, found again by UniqueId; corners in whole mm; Medium detail, coarsest
  triangles, a failed element read again at Fine). About 1 s and 1.2 MB on the 6,002-element tower. A failure costs
  the 3D view an update, never the snapshot. The uploader sends shapes after snapshots, to `/api/shapes`; the website
  answers 409 while it has no snapshot of that model, and the uploader tries again.
- **Snapshots** go to `%LocalAppData%\<product>\Outbox\` (outside OneDrive), written under a temporary name and
  renamed when complete. The add-in logs each one, with its read time, to `%LocalAppData%\<product>\Logs\addin.log`.
  The snapshot after Synchronize with Central is **on by default** (engineer, 2026-11-26: "Let us do the automatic
  snapshot after every synchronize with central. Those added seconds are fine"); a PC switches it off with
  `%AppData%\<product>\addin.json` = `{ "snapshotAfterSync": false }` (`AddinSettings`). It holds Revit for its read
  (3.7 to 10.9 s on a 6,000-element tower), skips family files, detached copies and models not in ACC, never shows a
  dialog and can't fail the sync. It does not tell structural models from others, so the add-in goes only on
  structural modellers' PCs. The Sync button works on any project model.
- **The uploader** reads the site's address and the **site password** from `%AppData%\<product>\uploader.json` (per-PC
  settings, never committed; read again at every pass), posts each snapshot oldest first with the password in the
  `X-Site-Password` header (the website's gate, 2026-11-26), moves it to `Sent` or (refused) `Rejected` with the reason,
  and retries anything else every minute, a refused password (401, 403) included: the setting is wrong, not the file.
  It is a windowless program (`WinExe`) logging to `%LocalAppData%\<product>\Logs\uploader.log`, one copy per user (a
  named mutex); a pass that fails (a mistyped setting) is logged and tried again. `Install.cmd` copies it to `%LocalAppData%\<product>\Uploader\<version>\`, asks the technician once for the
  address and password (Adnan gives them; never in the package), sets the user's Run entry `<product> uploader` and
  starts it. It needs .NET 8 (Revit 2025 and 2026 install it); the installer says so when it is missing. Microsoft
  sign-in replaces the password later. **Not yet run on a modeller's PC**, and the installer's uploader steps were
  not run here (they write to the real Startup folder). Its log starts again past 5 MB (keeping
  `uploader.previous.log`), and writing it can never end the uploader. Shapes whose snapshot was refused follow it to
  `Rejected` (the website would answer 409 to them for ever). `Outbox\Sent` keeps a file 90 days after it was sent
  (engineer, 2026-11-27; the website keeps its own copy of every file in `%LocalAppData%\Telltale\Snapshots` and
  `Shapes` on the host PC); the uploader stamps the sending time on the file and deletes older ones at each pass.
  `Rejected` is never emptied.
- **The ribbon** (0.7.0): tab `<product>`, panels Quantities (Sync to website, Parameter standard; Raw dump in the panel's
  slide-out, out of sight, engineer 2026-11-27) and Help. **Parameter standard** works with no model open and shows the
  Excel sheet's content from `ParameterStandard`: one row per shared parameter (allowed values, categories, what to fill,
  what a blank means), then settings, rules and examples; names and values can be copied. It reads nothing from the model. The after-sync
  handler is hooked up before the ribbon is built, and the ribbon is built inside a try, so nothing on it can cost a
  snapshot. Icons: one flat colour per theme, chosen from `UIThemeManager.CurrentTheme` and swapped on `ThemeChanged`
  (both in the 2024 and 2026 API; called from `NoInlining` methods inside a try). **Help** works with no model open
  (`AlwaysAvailable`): version, the after-sync switch, snapshots still waiting in the outbox, and links to the website
  (`siteAddress` from `uploader.json`; the add-in never reads the password), the instruction sheet installed beside the
  DLL as `<product> instructions.txt`, the outbox and the logs. A failed Sync to website is logged and said in a plain
  dialog.
- **Updates** (engineer, 2026-11-27: "new version of Telltale Addin ready, restart Revit please"; Adnan decides when a
  version is released by publishing its zip on the website, website repo "Publish add-in.cmd"). Each version lives in
  its own folder, `Addins\<year>\<product>\<version>\` and `%LocalAppData%\<product>\Uploader\<version>\`, and the
  manifest's Assembly path names it (`Manifest.addin.template`, `$Version$`). The uploader (`UpdateCheck`) asks
  `GET /api/addin/latest` ({version, sha256, size, releasedAt}; 404 when none) at start and hourly, with the site
  password; for a newer version it downloads `/api/addin/download/{version}` (the zip exactly as `Build-Package.ps1`
  writes it), checks size and SHA-256, unpacks it under a temporary name, copies the version folder beside the running
  one for each Revit year whose manifest exists (never adds a year), then replaces the manifest in one step: Revit reads
  it only at start, so the next start loads the new version even while this session holds the old DLL. The version
  before is kept; older ones are deleted when nothing holds them. The uploader updates itself the same way: it starts its
  successor with `--after <its process id>`, ends only if the successor is still up 10 s later (else carries on and skips
  that version until it restarts), and the successor repoints the user's Run entry `<product> uploader` (set by the
  installer; a missing entry is left missing) after its first full pass. In Revit, `UpdateNotice` (on Idling, at most
  every 5 minutes, in a try) shows the note once per session when the manifest points past the running version. It also
  saves `GET /api/parameter-standard` to `%LocalAppData%\<product>\parameter-standard.json`; the window reads it with
  `DataContractJsonSerializer` (part of .NET Framework and .NET 8: no file shipped) and shows it only when complete
  (version, date, each parameter with a name, values and categories), else the built-in copy. `Build-Package.ps1
  -Version x.y.z` builds a test release. The folder overrides never touch the registry; `--once` never hands over. The
  update check runs after the outbox is sent, so a bad release can't hold snapshots back. **Checked:** unit tests, and
  `tests/Updates/Test-UpdateEndToEnd.ps1` (real 0.7.0 uploaders against a stand-in site offering a 0.7.1 test package,
  scratch folders: 21 checks, including a running uploader handing over to 0.7.1, a corrupted download and a site that is
  down). **Not checked:** anything in Revit (the note, a version-folder manifest path, loading the new version), the Run
  entry (it acts on the real registry), the real installer beyond `-WhatIf`, and antivirus. The hash proves the download is whole, not who made it: the site is plain http and the
  files are unsigned. A PC without .NET 8 has no uploader, so no updates.
- The DLL is **unsigned**, so Revit asks once ("Load Once" / "Always Load"). Code signing is not set up.
- A cloud model can't be opened in a newer Revit without upgrading it for everyone, so a Revit step always
  runs in the model's own Revit year; the instruction sheets say so.

## Working here

- **Work tracking:** `BACKLOG.md` + `docs/backlog-archive.md` (IDs `F-` feature, `B-` bug, `T-` tech
  debt, never reused). This repo's IDs are its own; the website repo has a separate list.
- **AGENTS.md** mirrors this file for Codex. Apply every edit to both.
- Remote: https://github.com/AdnanAlmulla/TelltaleAddin (**public**, 2026-11-25; renamed from `RevitVolumeCostAddin` on 2026-11-27, GitHub forwards the old address). The website builds against a clone of it in a folder named `TelltaleAddin` beside the website clone.
- **No licence** (engineer, 2026-11-25): the repo carries no open-source licence, so no one is granted the right
  to copy or reuse the code. Don't add a `LICENSE` file or licence text. Because it is public, never commit
  secrets, tenant or client ids, internal paths or company cost data here. **Never commit a dump, snapshot or
  schedule of a real model** either (the build plan's `samples/*.json` would be one): real model files stay on
  the engineer's PC or in the private website repo.
- **Claude commits, never pushes** (engineer, 2026-11-25): the engineer pushes when he chooses. Contributors
  still follow `CONTRIBUTING.md`.
- Author: Adnan Almulla @ Killa Design.
