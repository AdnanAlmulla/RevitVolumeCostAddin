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

### [F-004] P1 — Raw dump of a real tower (the technician's first job)
<!-- type: feature | added: 2026-11-25 -->
**What:** A one-off dump command for Revit 2026. It writes one file: every structural element of the host model
with its `UniqueId`, element id, category, family, type, levels, materials with volumes, and every parameter name
and value. The BIM technician runs it once on a real tower and also exports Revit's quantity schedule as CSV.
**Why:** It shows which parameters exist and what they are called, how many elements a tower has, how long reading
them takes, and whether material names carry the grade, before any rule or snapshot class is fixed.
**Where:** `_harnesses/` or `src/Addin/`; the file comes back to the engineer.
**Acceptance:** The dump and the CSV are in hand; F-002's open points are answered from them; snapshot classes v1
are written from them.
**Notes:** Adopted from the plan review (engineer, 2026-11-25).
**Progress (2026-11-26):** Built for Revit 2024 and 2026 (a cloud model can't move to a newer Revit without an
upgrade for everyone, so the dump runs in the model's own year): `src/Addin/Commands/RawDumpCommand.cs` and
`src/Addin/RawDump/RawDumpWriter.cs`, format "raw-dump-1" (gzipped JSON in `Documents\<product>\Raw dumps\`),
read-only transaction mode. The hand-off is `Build-Package.ps1`'s zip plus `docs/technician/raw-dump-instructions.md`,
which also asks for a multi-category material takeoff (category, material, m³ to 3 places, count) exported as text.
**Not yet run inside Revit**: Revit's unsigned add-in prompt needs a click, so the technician's run is the first.
Next, when the files are back: summarise the parameter names and fill rates (F-002), compare the dump's per-category
and per-material volumes with the takeoff, then write snapshot classes v1. Never commit the files (public repo).
**Result (2026-11-26):** The technician ran it in Revit 2026.5 on a detached copy of a real tower: 7,667 elements read in
18.5 s, none unreadable. Its volume per material matches Revit's material takeoff to 0.001 m³ on 10 of 11 materials;
the 11th differs by 0.02% (two elements the takeoff left out; a re-export with Category will trace them). The
findings and the engineer's open decisions are in the private website repo (`docs/raw-dump-findings.md`). Still to
do before closing: the engineer's rulings (F-002), then snapshot classes v1.

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
**Progress (2026-11-26):** The project exists with configurations R24 and R26, the product-name property, the
build-time `.addin` manifest, the installer, CI (`.github/workflows/build.yml`, not yet run: nothing pushed) and the
ft³ / ft² to m³ / m² conversion (`src/Addin/Units/RevitInternalUnits.cs`) with tests against hand-checked beam,
column and slab volumes. Still to do: `src/Contracts`, the snapshot, the Sync triggers, the outbox, the uploader.

### [F-002] P1 — Division, element-kind and concrete-grade conventions
<!-- type: feature | added: 2026-11-25 -->
**What:** Find out how the structural models mark the divisions (piles, foundation, basement, ground floor,
podium, tower; set per project), RC or PT, element kind (retaining vs shear wall; pile vs raft vs pile cap), pile type /
diameter and concrete grade. See the reference table in `CLAUDE.md` "What a snapshot carries".
**Why:** Every filter on the website beyond category depends on these tags.
**Notes (2026-11-25):** The engineer has ruled that division and concrete grade are **instance parameters**.
Also since 2026-11-25: **RC or PT** is another instance parameter, read like division and grade.
Open: their names; shared project parameters bound to the structural categories (suggested, not decided);
how element kind and pile type are marked; a per-project level-to-division default with the parameter as an
override (suggested, not decided).
**Where:** The engineer, with the BIM technician; the raw dump (F-004) answers most of it.
**Acceptance:** The convention is written into `CLAUDE.md`, and the add-in's tagging follows it.
**Notes:** Moved here from the website repo's F-002 on 2026-11-25.
**Found in the raw dump (2026-11-26):** concrete grade is already a shared instance parameter on most elements. Nothing
marks division, RC or PT, or retaining against shear walls. Element kind and pile type can be read from family and
type names, and pile diameter and length from type parameters. Many walls and columns run through several storeys
and across division boundaries, so the snapshot needs each element's base and top level. Proposals and the
engineer's five decisions: website repo `docs/raw-dump-findings.md`. Parameter names stay a per-office setting,
never written into this public repo.

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
