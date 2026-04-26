# worktree

Small CLI to work with git bare repos and worktrees with easy-to-remember commands.

## Tooling

- `.NET 10` is pinned in [global.json](./global.json)
- `mise` manages the local SDK via [mise.toml](./mise.toml)

## Common Commands

```bash
worktree repo init
worktree repo create EffectfulFlow
worktree repo clone git@github.com:me/EffectfulFlow.git
worktree repo convert EffectfulFlow
worktree repo set-remote git@github.com:me/EffectfulFlow.git
worktree repo show

worktree create feature add-thing
worktree create fix/one-liner
worktree branch feature/add-thing
worktree show
worktree prune-removed-folders
worktree repair ../mylibs/CodecMapper/main
```

## Build

```bash
mise run build
mise run test
dotnet publish src/Worktree/Worktree.fsproj -c Release -r linux-x64
```

For native AOT:

```bash
dotnet publish src/Worktree/Worktree.fsproj -c Release -r linux-x64 -p:PublishAot=true
```

Build output is centralized under [`artifacts/`](./artifacts/) via the .NET SDK artifacts layout.
