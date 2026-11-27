# Contributing

1. Work on a branch of your own (or your fork), never on `master`.
2. Open a pull request into `master`. Say what changed and how you checked it in Revit (which Revit
   version and build, which model); the pull request form asks for both.
3. Adnan Almulla reviews every pull request and merges it. Never push to `master` yourself; a branch
   ruleset on `master` enforces this.

Read `CLAUDE.md` first, and `README.md` for how to build and try a change in Revit. In particular:
- `src/Contracts` is compiled by the website too. Add fields; don't rename or remove one without
  Adnan's say-so in the pull request.
- Units go in every name (`volumeFt3`, `volumeM3`), and the ft³ to m³ conversion lives in one place,
  with tests.
- Pick up work from `BACKLOG.md`, and keep it up to date in the same pull request.
- A new version goes to every technician's PC, so changes are batched into one version: don't change
  the version number in `Directory.Build.props`; Adnan does that when he builds a release.

**This repository is public.** Never commit, in a file or a commit message:
- a snapshot, raw dump, shapes file, schedule or screenshot of a real model, or a project or client name;
- the website's address, the site password or any other password, key or id;
- internal paths or server names, or company cost data.

Real model files stay on Adnan's PC. If one is committed by mistake, tell Adnan at once: deleting it in a later commit
leaves it in the history.
