# Telltale — Revit add-in

(Named on 2026-11-27; "Revit Volume & Cost" was its working name. This repo was `RevitVolumeCostAddin` until then.)

A Revit add-in (Revit 2022–2027) that reads the quantities of a structural model (concrete, steel and other
materials), element by element, after each Synchronize with Central and on a manual Sync. It writes them to an
outbox folder, and a small separate uploader sends them to the Telltale website. It also holds the snapshot classes the website's API compiles against.

Status: v0.1, phase 0 (the raw dump command). See `CLAUDE.md` and `BACKLOG.md`. Contributions: see `CONTRIBUTING.md`.

Author: Adnan Almulla @ Killa Design.
