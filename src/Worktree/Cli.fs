namespace Worktree

open System
open System.IO

module Cli =
  let version = "0.1.0"

  let private padRight width (value: string) =
    value.PadRight width

  let private commandLine width command summary =
    sprintf "  %s  %s" (padRight width command) summary

  let helpText =
    let commandWidth = 38

    String.concat Environment.NewLine [
      sprintf "worktree %s" version
      ""
      "Usage"
      "  worktree <command>"
      ""
      "Repo Commands"
      commandLine commandWidth "repo init" "Initialize a bare repo here and create main/"
      commandLine commandWidth "repo create <folder>" "Create a new bare-layout repo in <folder>/"
      commandLine commandWidth "repo clone <remote>" "Clone a remote into this bare-layout repo"
      commandLine commandWidth "repo convert <folder>" "Convert <folder>/ into <folder>/.bak + bare-layout <folder>/"
      commandLine commandWidth "repo set-remote <remote>" "Set or replace origin and fetch refs"
      commandLine commandWidth "repo info" "Alias for repo show"
      commandLine commandWidth "repo show" "Show repo root, origin, and branch hints"
      ""
      "Worktree Commands"
      commandLine commandWidth "create <prefix> <description>" "Create a new branch worktree"
      commandLine commandWidth "create <prefix>/<description>" "Same as above, slash form"
      commandLine commandWidth "branch <branch>" "Reuse or create a worktree for an existing branch"
      commandLine commandWidth "show" "List registered worktrees"
      commandLine commandWidth "prune-removed-folders" "Prune deleted worktree folders"
      commandLine commandWidth "repair [<path>...]" "Repair broken worktree links"
      ""
      "Workflows"
      commandLine commandWidth "completions <shell>" "Generate shell completion script"
      ""
      "Notes"
      "  create prefixes: feature, fix, chore, hotfix"
      "  repo convert expects a clean normal repo and keeps <folder>.bak as backup"
      "  repair needs moved worktree path(s) when linked folders were relocated manually"
      ""
      "Examples"
      "  worktree repo create EffectfulFlow"
      "  worktree repo clone git@github.com:me/EffectfulFlow.git"
      "  worktree repo convert EffectfulFlow"
      "  worktree create feature add-thing"
      "  worktree branch feature/add-thing"
      "  worktree repair"
      "  worktree repair ../mylibs/CodecMapper/main"
    ]

  let parse args =
    try
      match args with
      | []
      | [ "help" ]
      | [ "-h" ]
      | [ "--help" ] -> Ok Help
      | [ "version" ]
      | [ "-v" ]
      | [ "--version" ] -> Ok Command.Version
      | [ "completions"; "bash" ] -> Ok(Command.Completions CompletionShell.Bash)
      | [ "completions"; "zsh" ] -> Ok(Command.Completions CompletionShell.Zsh)
      | [ "completions"; "fish" ] -> Ok(Command.Completions CompletionShell.Fish)
      | [ "completions"; "powershell" ] -> Ok(Command.Completions CompletionShell.PowerShell)
      | "__complete" :: index :: words -> Ok(Command.Complete(int index, words))
      | [ "repo"; "init" ] -> Ok(Command.Repo RepoCommand.Init)
      | [ "repo"; "create"; folderName ] -> Ok(Command.Repo(RepoCommand.Create folderName))
      | [ "repo"; "clone"; remote ] -> Ok(Command.Repo(RepoCommand.Clone remote))
      | [ "repo"; "convert"; folderName ] -> Ok(Command.Repo(RepoCommand.Convert folderName))
      | [ "repo"; "set-remote"; remote ] -> Ok(Command.Repo(RepoCommand.SetRemote remote))
      | [ "repo"; "info" ] -> Ok(Command.Repo RepoCommand.Show)
      | [ "repo"; "show" ] -> Ok(Command.Repo RepoCommand.Show)
      | [ "repo"; subcommand ] -> Error(sprintf "unknown worktree repo command: %s" subcommand)
      | "create" :: createArgs -> Ok(Command.Create(Domain.normalizeCreateBranch createArgs))
      | [ "branch"; branch ] -> Ok(Command.Branch branch)
      | [ "show" ] -> Ok Command.Show
      | [ "prune-removed-folders" ] -> Ok Command.PruneRemovedFolders
      | "repair" :: paths -> Ok(Command.Repair paths)
      | command :: _ -> Error(sprintf "unknown worktree command: %s" command)
    with ex ->
      Error ex.Message

  let private complete cwd index (words: string list) =
    let context = words |> List.truncate index

    let completions =
      match context with
      | [ "worktree" ] -> [ "repo"; "create"; "branch"; "show"; "prune-removed-folders"; "repair"; "help"; "version" ]
      | [ "worktree"; "repo" ] -> [ "init"; "create"; "clone"; "convert"; "set-remote"; "info"; "show" ]
      | [ "worktree"; "create" ] -> Domain.allowedPrefixes |> Set.toList
      | [ "worktree"; "branch" ] -> if Git.isBareRepoHere cwd then Git.getBranches cwd else []
      | [ "worktree"; "repo"; ("create" | "convert") ]
      | [ "worktree"; "repair" ] ->
          Directory.GetDirectories(cwd) |> Array.toList |> List.map (fun d -> (Path.GetFileName d) + "/")
      | _ -> []

    completions |> List.iter (printfn "%s")

  let private generateCompletions shell =
    match shell with
    | CompletionShell.Bash ->
        printfn
          """_worktree_completions() {
    local cur prev words cword
    _get_comp_words_by_ref -n : cur prev words cword
    local suggestions=$(worktree __complete "$cword" "${words[@]}")
    COMPREPLY=( $(compgen -W "$suggestions" -- "$cur") )
}
complete -F _worktree_completions worktree"""
    | CompletionShell.Zsh ->
        printfn
          """#compdef worktree
_worktree() {
    local -a suggestions
    suggestions=(${(f)"$(worktree __complete $((CURRENT - 1)) "${words[@]}")"})
    _arguments "*: :($suggestions)"
}
_worktree "$@" """
    | CompletionShell.Fish ->
        printfn "complete -c worktree -f -a \"(worktree __complete (math (count (commandline -poc)) + 1) (commandline -poc))\""
    | CompletionShell.PowerShell ->
        printfn
          """Register-ArgumentCompleter -Native -CommandName worktree -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $words = $commandAst.Elements | ForEach-Object { $_.ToString() }
    $index = $words.IndexOf($wordToComplete)
    if ($index -lt 0) { $index = $words.Count }
    worktree __complete $index $words | Where-Object { $_ -like "$wordToComplete*" }
}"""

  let private initRepo repoRoot =
    Git.ensureNoGitDirExists repoRoot
    Git.runOrFail repoRoot [ "init"; "--bare"; "--initial-branch=main"; ".git" ] false |> ignore
    Git.runOrFail repoRoot [ "worktree"; "add"; "main"; "-b"; "main" ] false |> ignore
    Git.printFullPath (Path.Combine(repoRoot, "main"))

  let private createRepo cwd folderName =
    let repoRoot = Path.Combine(cwd, folderName)

    if Directory.Exists repoRoot || File.Exists repoRoot then
      failwithf "path already exists: %s" repoRoot

    Directory.CreateDirectory repoRoot |> ignore
    initRepo repoRoot

  let private cloneRepo cwd remote =
    Git.ensureNoGitDirExists cwd
    Git.runOrFail cwd [ "clone"; "--bare"; remote; ".git" ] false |> ignore
    Git.configureOriginFetch cwd
    Git.refreshOrigin cwd

    let defaultBranch =
      Git.detectRemoteDefaultBranch cwd
      |> Option.defaultWith (fun () -> failwith "could not determine the remote default branch")

    Git.runOrFail cwd [ "worktree"; "add"; defaultBranch; defaultBranch ] false |> ignore
    Git.printFullPath (Path.Combine(cwd, defaultBranch))

  let private convertRepo cwd folderName =
    let sourceRepoRoot = Path.Combine(cwd, folderName)
    let backupRepoRoot = Path.Combine(cwd, sprintf "%s.bak" folderName)

    if not (Directory.Exists sourceRepoRoot) then
      failwithf "repo folder not found: %s" sourceRepoRoot

    if File.Exists backupRepoRoot || Directory.Exists backupRepoRoot then
      failwithf "backup path already exists: %s" backupRepoRoot

    Git.ensureNormalRepoHere sourceRepoRoot
    Git.ensureCleanWorktree sourceRepoRoot

    let originalOrigin = Git.tryGetOriginRemote sourceRepoRoot

    let primaryBranch =
      Domain.preferredConvertBranch
        (Git.localBranchExists sourceRepoRoot "main")
        (Git.localBranchExists sourceRepoRoot "master")

    Directory.Move(sourceRepoRoot, backupRepoRoot)
    Directory.CreateDirectory(sourceRepoRoot) |> ignore

    Git.runOrFail sourceRepoRoot [ "clone"; "--bare"; backupRepoRoot; ".git" ] false |> ignore

    match originalOrigin with
    | Some origin ->
        Git.runOrFail sourceRepoRoot [ "remote"; "set-url"; "origin"; origin ] false |> ignore
        Git.configureOriginFetch sourceRepoRoot
    | None ->
        Git.runOrFail sourceRepoRoot [ "remote"; "remove"; "origin" ] false |> ignore

    Git.runOrFail sourceRepoRoot [ "worktree"; "add"; primaryBranch; primaryBranch ] false |> ignore

    printfn "converted-repo: %s" (Path.GetFullPath sourceRepoRoot)
    printfn "backup-repo: %s" (Path.GetFullPath backupRepoRoot)

  let private setRemote cwd remote =
    Git.ensureBareRepoHere cwd

    if Git.hasOriginRemote cwd then
      Git.runOrFail cwd [ "remote"; "set-url"; "origin"; remote ] false |> ignore
    else
      Git.runOrFail cwd [ "remote"; "add"; "origin"; remote ] false |> ignore

    Git.configureOriginFetch cwd
    Git.refreshOrigin cwd
    Git.showRepoSummary cwd

  let private createWorktree cwd branch =
    Git.ensureBareRepoHere cwd

    match Git.tryFindWorktreePath cwd branch with
    | Some path -> Git.printFullPath path
    | None ->
        Git.ensureParentDirectory cwd branch
        let startPoint = Git.startPointForNewBranch cwd
        Git.runOrFail cwd [ "worktree"; "add"; branch; "-b"; branch; startPoint ] false |> ignore
        Git.printFullPath (Path.Combine(cwd, branch))

  let private forBranch cwd branch =
    Git.ensureBareRepoHere cwd

    match Git.tryFindWorktreePath cwd branch with
    | Some path -> Git.printFullPath path
    | None ->
        Git.ensureParentDirectory cwd branch

        if Git.localBranchExists cwd branch then
          Git.runOrFail cwd [ "worktree"; "add"; branch; branch ] false |> ignore
        else
          Git.refreshOrigin cwd

          if Git.remoteBranchExists cwd branch then
            Git.runOrFail cwd [ "worktree"; "add"; "-b"; branch; branch; sprintf "origin/%s" branch ] false |> ignore
          else
            failwithf "branch not found locally or on origin: %s" branch

        Git.printFullPath (Path.Combine(cwd, branch))

  let execute cwd command =
    match command with
    | Command.Help ->
        printfn "%s" helpText
        0
    | Command.Version ->
        printfn "%s" version
        0
    | Command.Completions shell ->
        generateCompletions shell
        0
    | Command.Complete(index, words) ->
        complete cwd index words
        0
    | Command.Repo RepoCommand.Init ->
        initRepo cwd
        0
    | Command.Repo(RepoCommand.Create folderName) ->
        createRepo cwd folderName
        0
    | Command.Repo(RepoCommand.Clone remote) ->
        cloneRepo cwd remote
        0
    | Command.Repo(RepoCommand.Convert folderName) ->
        convertRepo cwd folderName
        0
    | Command.Repo(RepoCommand.SetRemote remote) ->
        setRemote cwd remote
        0
    | Command.Repo RepoCommand.Show ->
        Git.showRepoSummary cwd
        0
    | Command.Create branch ->
        createWorktree cwd branch
        0
    | Command.Branch branch ->
        forBranch cwd branch
        0
    | Command.Show ->
        Git.ensureBareRepoHere cwd
        Git.runOrFail cwd [ "worktree"; "list" ] false |> ignore
        0
    | Command.PruneRemovedFolders ->
        Git.ensureBareRepoHere cwd
        Git.runOrFail cwd [ "worktree"; "prune" ] false |> ignore
        Git.runOrFail cwd [ "worktree"; "list" ] false |> ignore
        0
    | Command.Repair paths ->
        Git.ensureBareRepoHere cwd
        Git.runOrFail cwd ([ "worktree"; "repair" ] @ paths) false |> ignore
        Git.runOrFail cwd [ "worktree"; "list" ] false |> ignore
        0
