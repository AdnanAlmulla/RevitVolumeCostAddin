# Backlog

Persistent list of work for the Revit Volume & Cost add-in (working name). Items are implementable
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
Automatic upload from modellers' PCs waits for the site to be opened to the network and for sign-in (website T-006,
T-004).
**Progress (2026-11-26, v0.1):** The project exists with configurations R24 and R26, the product-name property, the
build-time `.addin` manifest, the installer, CI (`.github/workflows/build.yml`, not yet run: nothing pushed) and the
ft³ / ft² to m³ / m² conversion (`src/Addin/Units/RevitInternalUnits.cs`) with tests against hand-checked beam,
column and slab volumes. Still to do: `src/Contracts`, the snapshot, the Sync triggers, the outbox, the uploader.

### [F-003] P3 — Time the 3D shape export on the largest tower (deferred)
<!-- type: feature | added: 2026-11-25 -->
**What:** For the website's 3D viewer (decided 2026-11-25: three.js on the site), the add-in will export per-element shapes keyed by Revit
element id. First step only: time that export on the largest tower model.
**Why:** The export must not slow down a Synchronize with Central. The viewer approach is decided; only its timing is open.
**Where:** A timing probe under `_harnesses/`.
**Acceptance:** The export time on the largest tower is measured and put to the engineer.
**Notes:** Deferred by the engineer (2026-11-25). The viewer side is the website repo's F-006.
The viewer shows the whole structural model (concrete highlighted, the rest grey), so the export covers every
structural element, steel included (engineer, 2026-11-25).

## Done

- [[F-002]](docs/backlog-archive.md#f-002-division-element-kind-and-concrete-grade-conventions) Conventions agreed: a parameter standard the technicians follow — 2026-11-26
- [[F-004]](docs/backlog-archive.md#f-004-raw-dump-of-a-real-tower) Raw dump of a real tower: ran, matched Revit's takeoff, snapshot classes v1 written from it — 2026-11-26
