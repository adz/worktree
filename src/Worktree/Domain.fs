namespace Worktree

open System

type RepoCommand =
  | Init
  | Create of folderName: string
  | Clone of remote: string
  | Convert of folderName: string
  | SetRemote of remote: string
  | Show

type CompletionShell =
  | Bash
  | Zsh
  | Fish
  | PowerShell

type Command =
  | Help
  | Version
  | Repo of RepoCommand
  | Create of branch: string
  | Branch of branch: string
  | Show
  | PruneRemovedFolders
  | Repair of paths: string list
  | Completions of shell: CompletionShell
  | Complete of index: int * words: string list

module Domain =
  let allowedPrefixes = set [ "feature"; "fix"; "chore"; "hotfix" ]

  let preferredConvertBranch hasMain hasMaster =
    if hasMain then
      "main"
    elif hasMaster then
      "master"
    else
      failwith "repo convert requires a local 'main' or 'master' branch"

  let normalizeCreateBranch (args: string list) =
    let validate (branch: string) =
      let parts = branch.Split('/', 2, StringSplitOptions.None)

      if parts.Length <> 2 then
        invalidArg "args" "worktree create expects <prefix> <description> or <prefix>/<description>"

      let prefix = parts[0]
      let description = parts[1]

      if not (allowedPrefixes.Contains prefix) then
        invalidArg "args" "prefix must be one of: feature, fix, chore, hotfix"

      if String.IsNullOrWhiteSpace description then
        invalidArg "args" "branch description cannot be empty"

      branch

    match args with
    | [ branch ] -> validate branch
    | [ prefix; description ] -> validate (sprintf "%s/%s" prefix description)
    | _ -> invalidArg "args" "worktree create expects <prefix> <description> or <prefix>/<description>"
