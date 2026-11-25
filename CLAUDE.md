# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**The Revit add-in of Revit Volume & Cost** (repo `RevitVolumeCostAddin`): a Revit add-in for Revit
2022–2027 that reads the quantities of a structural model (concrete, steel and other materials) and sends them, as a snapshot, to the
project's internal website. It also holds the **snapshot classes** (`src/Contracts`) that the website's API
compiles against.

The website (API, database, site) lives in a separate private repo, `RevitVolumeCost`. This repo was split
off on 2026-11-25 so that Revit-side contributors work here only (engineer's ruling).

**The name is a placeholder** (engineer, 2026-11-25): "Revit Volume & Cost", `RevitVolumeCost` and
`RevitVolumeCostAddin` are working names; the product's real name is not decided. Keep it cheap to rename:
put the product name in one MSBuild property (`Directory.Build.props`) that assembly names and root
namespaces derive from, and nowhere else in code.

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

Status (2026-11-25): **v0.1 planning.** No code yet.

## How work lands here

- **Every contributor's change arrives as a pull request into `master`, and the engineer (Adnan Almulla)
  reviews and merges it.** Contributors never push to `master`. There is no branch rule (the account stays on
  GitHub's free plan): the repo relies on trust, "unless proven otherwise" (engineer, 2026-11-25). See
  `CONTRIBUTING.md`.
- **`src/Contracts` is shared with the website.** The website repo builds against a clone of this repo
  placed beside it, so a change to a snapshot class changes what the API receives. Keep changes additive
  (new optional fields); renaming or removing a field needs the engineer's say-so in the pull request.

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
  CSi plugins: "this is not a CSi plugin").
- **No cost in the add-in.** Rates live on the website. The add-in sends quantities only.
- **Our data never depends on a third-party service.** Autodesk's cloud may later serve a 3D viewer only.

## One code base for Revit 2022–2027

| Revit | Runtime | Target |
|---|---|---|
| 2022, 2023, 2024 | .NET Framework 4.8 | `net48` |
| 2025, 2026 | .NET 8 | `net8.0-windows` |
| 2027 | expected .NET 10: confirm against Autodesk's 2027 SDK before relying on it | `net10.0-windows` |

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

- **Snapshot:** time, who synced, Revit version, the model's ACC project id and model id.
- **Element row:** element id, category, family/type, level, material, volume (m³), area (m²), count, and
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

**Not only concrete** (engineer, 2026-11-25). Every structural element is sent whatever its material: steel and
other materials are quantified, costed and given carbon on the website (mass = volume × a density typed in on
the site). Still open (F-002): how a steel element's grade or section is marked. Ask before adding fields.

Whether the volumes in the model are right (overlaps, joins) is the Revit technician's job, not the
add-in's.

## Planned layout

Nothing below exists yet except `docs/`. Create each part when its work starts.

```
src/Contracts/   netstandard2.0 snapshot classes, also compiled by the website's API
src/Addin/       Revit add-in (WPF), configurations R22–R27
tests/           xUnit, pure source compiled in by path
_harnesses/      throwaway probes, never part of a build
docs/            backlog-archive.md
```

## Working here

- **Work tracking:** `BACKLOG.md` + `docs/backlog-archive.md` (IDs `F-` feature, `B-` bug, `T-` tech
  debt, never reused). This repo's IDs are its own; the website repo has a separate list.
- **AGENTS.md** mirrors this file for Codex. Apply every edit to both.
- Remote: https://github.com/AdnanAlmulla/RevitVolumeCostAddin (private).
- Author: Adnan Almulla @ Killa Design.
