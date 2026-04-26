# worktree

Small CLI to work with git bare repos and worktrees with easy-to-remember commands.

## Help Output

```bash
worktree

Usage
  worktree <command>

Repo Commands
  repo init                               Initialize a bare repo here and create main/
  repo create <folder>                    Create a new bare-layout repo in <folder>/
  repo clone <remote>                     Clone a remote into this bare-layout repo
  repo convert <folder>                   Convert <folder>/ into <folder>/.bak + bare-layout <folder>/
  repo set-remote <remote>                Set or replace origin and fetch refs
  repo info                               Alias for repo show
  repo show                               Show repo root, origin, and branch hints

Worktree Commands
  create <prefix> <description>           Create a new branch worktree
  create <prefix>/<description>           Same as above, slash form
  branch <branch>                         Reuse or create a worktree for an existing branch
  show                                    List registered worktrees
  prune-removed-folders                   Prune deleted worktree folders
  repair [<path>...]                      Repair broken worktree links

Notes
  create prefixes: feature, fix, chore, hotfix
  repo convert expects a clean normal repo and keeps <folder>.bak as backup
  repair needs moved worktree path(s) when linked folders were relocated manually

Examples
  worktree repo create EffectfulFlow
  worktree repo clone git@github.com:me/EffectfulFlow.git
  worktree repo convert EffectfulFlow
  worktree create feature add-thing
  worktree branch feature/add-thing
  worktree repair
  worktree repair ../mylibs/CodecMapper/main
```

## Build

There's currently no prebuilt binaries, build your own and put on path...

```bash
mise run build
mise run test
dotnet publish src/Worktree/Worktree.fsproj -c Release -r linux-x64
```

For native AOT (linux64):

```bash
mise run publish-aot
```

Build output is centralized under [`artifacts/`](./artifacts/) via the .NET SDK artifacts layout.

## Tooling

- `.NET 10` is pinned in [global.json](./global.json)
- `mise` manages the local SDK via [mise.toml](./mise.toml)

