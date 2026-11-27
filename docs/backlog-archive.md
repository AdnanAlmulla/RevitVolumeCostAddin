# Backlog archive

Full records of landed `BACKLOG.md` items, newest first.

## [T-004] One copy of the standard for the add-in and the website
<!-- type: tech debt | added: 2026-11-27 -->
**What:** Move `ParameterStandard.cs` into `src/Contracts`, which the website already compiles in, so the website reads its
parameter names and allowed values from the same file as the add-in's window.
**Why:** Today the standard lives in three places (the Excel sheet, this add-in, the website's settings and rules), so a
change made in one can be missed in another.
**Where:** `src/Contracts/`, and the website repo's classification rules and `addin-ref.txt` pin.
**Acceptance:** The website builds against the moved file and its rules use its lists; the add-in window is unchanged.
**Notes:** Touches `src/Contracts`, so it needs the engineer's say-so and a matching website change (CLAUDE.md "How work lands here").
**Result (2026-11-27):** Done another way, without touching `src/Contracts`: the website serves the standard to the uploader
(F-007), and its tests compile `ParameterStandard.cs` and `StandardContent.cs` by path and fail when the site's copy
differs from the add-in's (website thread). CLAUDE.md "How work lands here" says to keep both files in place.

## [T-002] How long Outbox\Sent keeps sent files
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
**Result (2026-11-27):** The engineer chose 90 days, once told the website keeps its own copy of each file ("Then I
agree: 90 days after it can be deleted"). Built into 0.7.0: `UploadRules.DeleteExpiredSentFiles` and `MarkAsSent` (the
uploader stamps the sending time as it moves a file into Sent), run at the start of each pass; tested with a temporary
folder (91 days deleted, 89 days and today kept). Not run on a modeller's PC.

## [T-003] Raw dump out of sight on the ribbon
<!-- type: tech debt | added: 2026-11-27 -->
**What:** Raw dump (F-004) was a one-off for the parameter standard, and it sits beside Sync to website on every
modeller's ribbon. Options: keep it; move it to the Quantities panel's slide-out (`RibbonPanel.AddSlideOut()`), out of
sight until asked for; or remove it.
**Why:** A button that is not for daily use invites clicks, and a raw dump of a large tower holds Revit for minutes.
**Where:** `src/Addin/App.cs` (`AddRibbon`), the technician sheets.
**Acceptance:** The engineer's choice, in the next batched release.
**Result (2026-11-27):** The engineer chose the slide-out ("move it out of sight into the panel's drop-down"). Built
into 0.7.0 (`RibbonPanel.AddSlideOut()` before Raw dump); compiles for 2024 and 2026, not yet seen in Revit (release check
step 6).

## [F-002] Division, element-kind and concrete-grade conventions
Landed 2026-11-26: the engineer approved a parameter standard (shared Text instance parameters for division, RC or PT,
wall function and concrete and steel grade, with exact allowed values). It lives in the private website repo
(`docs/Revit parameter standard.xlsx`), because parameter names stay out of this public repo. The add-in needs no
change: it sends every project and shared parameter, and the website reads them by name (a setting there).

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
