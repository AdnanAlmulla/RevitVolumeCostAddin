# Backlog archive

Full records of landed `BACKLOG.md` items, newest first.

## [F-004] Raw dump of a real tower
Landed 2026-11-26: the snapshot classes v1 are written (`src/Contracts`), so its acceptance is met; F-002's three open
decisions continue there.

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
