# Contributing

1. Work on a branch of your own (or your fork), never on `master`.
2. Open a pull request into `master`. Say what changed and how you checked it in Revit (which Revit
   version, which model).
3. Adnan Almulla reviews every pull request and merges it. Contributors' changes reach `master` no other
   way: `master` is protected so a pull request needs his approval. As the owner he can still commit to
   `master` himself (GitHub does not let an author approve their own pull request).

Read `CLAUDE.md` first. In particular:
- `src/Contracts` is compiled by the website too. Add fields; don't rename or remove one without
  Adnan's say-so in the pull request.
- Units go in every name (`volumeFt3`, `volumeM3`), and the ft³ to m³ conversion lives in one place,
  with tests.
- Pick up work from `BACKLOG.md`, and keep it up to date in the same pull request.
