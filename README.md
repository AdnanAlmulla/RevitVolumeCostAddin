# Telltale — Revit add-in

(Named on 2026-11-27; "Revit Volume & Cost" was its working name. This repo was `RevitVolumeCostAddin` until then.)

A Revit add-in (Revit 2022–2027) that reads the quantities of a structural model (concrete, steel and other
materials), element by element, after each Synchronize with Central and on a manual Sync. It writes them to an
outbox folder, and a small separate uploader sends them to the Telltale website. It also holds the snapshot classes the website's API compiles against.

Status: version 0.7.0, built for Revit 2024 and 2026. See `CLAUDE.md` for how it works and why, `BACKLOG.md` for the
work in hand, and `CONTRIBUTING.md` before you change anything.

## What's where

| Folder | What it holds |
|---|---|
| `src/Addin` | The Revit add-in: the ribbon tab, the snapshot after each sync, Sync to website, Raw dump, Help |
| `src/Uploader` | The small program that sends the outbox to the website |
| `src/Contracts` | The snapshot classes, shared with the website: change them only as `CONTRIBUTING.md` says |
| `tests/Addin.Tests` | Automated tests of the parts that don't need Revit |
| `installer` | `Install.cmd`, which a technician double-clicks to install a package |
| `docs/technician` | The instruction sheets that go to the BIM technicians with each package |
| `tools` | `Draw-RibbonIcons.ps1`, which draws the ribbon icons |

## Building

You need Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Revit is not needed to build:
the add-in compiles against reference copies of the Revit API.

```powershell
dotnet test tests/Addin.Tests/Addin.Tests.csproj
dotnet build src/Addin/Addin.csproj -c R26
dotnet build src/Addin/Addin.csproj -c R24
```

`-c R26` builds for Revit 2026 and `-c R24` for Revit 2024; the output is in `src/Addin/bin/R26` (or `R24`).

## Trying a change in Revit

Build the whole package, then install it on your own PC as a technician would:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Package.ps1
```

It runs the tests, then writes `artifacts\Telltale-<version>\` and its `.zip` (`-Version 0.7.1` builds a test release
under another number, for trying the update path). Close Revit, double-click
`Install.cmd` in that folder, then start Revit. Each install replaces the previous one. Snapshots are written to
`%LocalAppData%\Telltale\Outbox`, and the add-in's log is `%LocalAppData%\Telltale\Logs\addin.log`.

Author: Adnan Almulla @ Killa Design.
