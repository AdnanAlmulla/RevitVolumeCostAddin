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

### [F-001] P1 — One Revit add-in building for Revit 2022 to 2027
<!-- type: feature | added: 2026-11-25 -->
**What:** The add-in project: one csproj with configurations R22 … R27 (`net48`, `net8.0-windows`, expected
`net10.0-windows` for 2027), one `.addin` manifest per year, and `src/Contracts` (`netstandard2.0`). It reads
one row per element of the host model, converts ft³ / ft² to m³ / m² in one place, and uploads a snapshot on
Save, on Synchronize with Central and on a manual Sync, in the background, queued and retried on failure. Its
own small MSAL wrapper gets the API token. WPF UI.
**Why:** The add-in is the website's only source of data.
**Where:** `src/Addin/`, `src/Contracts/`, `tests/` (planned layout in `CLAUDE.md`).
**Acceptance:** It builds for every year; the BIM technician confirms it loads and syncs in at least the oldest and
newest Revit installed in the office, returning a snapshot JSON and a schedule CSV whose totals an automated test
matches; the unit conversion has unit tests against hand-checked element volumes; a save is not slowed
down measurably.
**Notes:** A snapshot identifies its model by its ACC project id and model id (cloud models have no file path).
Confirm the 2027 runtime against Autodesk's 2027 SDK. Element tagging waits for F-002. The upload target
needs the website's API and Entra registration (website repo); until then, test against a local stub.
Moved here from the website repo's F-003 on 2026-11-25.

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
**Where:** The engineer, with the BIM technician.
**Acceptance:** The convention is written into `CLAUDE.md`, and the add-in's tagging follows it.
**Notes:** Moved here from the website repo's F-002 on 2026-11-25.

### [F-003] P3 — Time the 3D shape export on the largest tower (deferred)
<!-- type: feature | added: 2026-11-25 -->
**What:** For the website's 3D viewer (decided 2026-11-25: three.js on the site), the add-in will export per-element shapes keyed by Revit
element id. First step only: time that export on the largest tower model.
**Why:** The export must not slow down a Save. The viewer approach is decided; only its timing is open.
**Where:** A timing probe under `_harnesses/`.
**Acceptance:** The export time on the largest tower is measured and put to the engineer.
**Notes:** Deferred by the engineer (2026-11-25). The viewer side is the website repo's F-006.

## Done
