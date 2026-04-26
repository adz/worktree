namespace Worktree

open System
open System.IO

type WorktreeRecord =
  { Path: string
    Branch: string option }

module Git =
  let combine cwd child = Path.Combine(cwd, child)

  let gitDirPath cwd = combine cwd ".git"

  let run cwd args capture =
    Process.run cwd "git" args capture

  let runOrFail cwd args capture =
    Process.runOrFail cwd "git" args capture

  let runLines cwd args =
    let result = runOrFail cwd args true

    if String.IsNullOrWhiteSpace result.StdOut then
      []
    else
      result.StdOut.Split('\n', StringSplitOptions.None) |> Array.toList

  let ensureDirectoryExists path =
    Directory.CreateDirectory path |> ignore

  let printFullPath path =
    printfn "%s" (Path.GetFullPath path)

  let isBareRepoHere cwd =
    Directory.Exists(gitDirPath cwd)
    &&
    let result = run cwd [ "--git-dir=.git"; "rev-parse"; "--is-bare-repository" ] true
    result.ExitCode = 0 && result.StdOut = "true"

  let ensureBareRepoHere cwd =
    if not (isBareRepoHere cwd) then
      failwith "run this from the repo root that contains the bare .git directory"

  let ensureNoGitDirExists cwd =
    if File.Exists(gitDirPath cwd) || Directory.Exists(gitDirPath cwd) then
      failwith ".git already exists here"

  let isGitRepoHere cwd =
    let result = run cwd [ "rev-parse"; "--show-toplevel" ] true
    result.ExitCode = 0

  let ensureNormalRepoHere cwd =
    if not (isGitRepoHere cwd) then
      failwith "expected a normal git repo here"

    let repoRoot =
      runOrFail cwd [ "rev-parse"; "--show-toplevel" ] true
      |> _.StdOut
      |> Path.GetFullPath

    let requestedPath = Path.GetFullPath cwd

    if repoRoot <> requestedPath then
      failwith "repo convert expects the folder itself to be the git repo root"

    let bareResult = runOrFail cwd [ "rev-parse"; "--is-bare-repository" ] true

    if bareResult.StdOut <> "false" then
      failwith "repo convert expects a normal non-bare git repo"

  let ensureCleanWorktree cwd =
    let status = runOrFail cwd [ "status"; "--porcelain" ] true

    if not (String.IsNullOrWhiteSpace status.StdOut) then
      failwith "repo convert requires a clean working tree"

  let hasOriginRemote cwd =
    run cwd [ "remote"; "get-url"; "origin" ] true
    |> fun result -> result.ExitCode = 0

  let tryGetOriginRemote cwd =
    let result = run cwd [ "remote"; "get-url"; "origin" ] true
    if result.ExitCode = 0 && not (String.IsNullOrWhiteSpace result.StdOut) then Some result.StdOut else None

  let localBranchExists cwd branch =
    run cwd [ "show-ref"; "--verify"; "--quiet"; sprintf "refs/heads/%s" branch ] true
    |> fun result -> result.ExitCode = 0

  let remoteBranchExists cwd branch =
    run cwd [ "show-ref"; "--verify"; "--quiet"; sprintf "refs/remotes/origin/%s" branch ] true
    |> fun result -> result.ExitCode = 0

  let getSymbolicRef cwd refName =
    let result = run cwd [ "symbolic-ref"; refName ] true
    if result.ExitCode = 0 && not (String.IsNullOrWhiteSpace result.StdOut) then Some result.StdOut else None

  let detectRemoteDefaultBranch cwd =
    getSymbolicRef cwd "refs/remotes/origin/HEAD"
    |> Option.bind (fun refName ->
      if refName.StartsWith "refs/remotes/origin/" then
        Some(refName.Substring("refs/remotes/origin/".Length))
      else
        None)
    |> Option.orElseWith (fun () ->
      if remoteBranchExists cwd "main" then Some "main" else None)
    |> Option.orElseWith (fun () ->
      if remoteBranchExists cwd "master" then Some "master" else None)

  let detectBaseBranch cwd =
    if localBranchExists cwd "main" then
      Some "main"
    elif localBranchExists cwd "master" then
      Some "master"
    else
      getSymbolicRef cwd "HEAD"
      |> Option.bind (fun refName ->
        if refName.StartsWith "refs/heads/" then
          Some(refName.Substring("refs/heads/".Length))
        else
          None)
      |> Option.orElseWith (fun () ->
        match runLines cwd [ "for-each-ref"; "--format=%(refname:short)"; "refs/heads" ] with
        | [ branch ] -> Some branch
        | _ -> None)

  let hasLocalBranchCommit cwd branch =
    localBranchExists cwd branch

  let formatLocalBaseBranch cwd =
    match detectBaseBranch cwd with
    | Some branch when hasLocalBranchCommit cwd branch -> branch
    | Some branch -> sprintf "%s (no commits yet)" branch
    | None -> "(unknown)"

  let formatRemoteDefaultBranch cwd =
    match detectRemoteDefaultBranch cwd, tryGetOriginRemote cwd, detectBaseBranch cwd with
    | Some branch, _, _ -> branch
    | None, Some _, Some branch when not (hasLocalBranchCommit cwd branch) ->
        sprintf "(remote has no default branch yet; push first commit from %s)" branch
    | None, Some _, _ -> "(remote has no default branch yet)"
    | None, None, _ -> "(unknown)"

  let refreshOrigin cwd =
    if hasOriginRemote cwd then
      runOrFail cwd [ "fetch"; "origin" ] false |> ignore

  let configureOriginFetch cwd =
    runOrFail cwd [ "config"; "remote.origin.fetch"; "+refs/heads/*:refs/remotes/origin/*" ] false |> ignore

  let getWorktrees cwd =
    let lines = runLines cwd [ "worktree"; "list"; "--porcelain" ]

    let finalize path branch acc =
      match path with
      | Some pathValue -> { Path = pathValue; Branch = branch } :: acc
      | None -> acc

    let rec loop (remaining: string list) currentPath currentBranch acc =
      match remaining with
      | [] -> finalize currentPath currentBranch acc |> List.rev
      | line :: tail when line.StartsWith "worktree " ->
          let nextAcc = finalize currentPath currentBranch acc
          loop tail (Some(line.Substring("worktree ".Length))) None nextAcc
      | line :: tail when line.StartsWith "branch refs/heads/" ->
          loop tail currentPath (Some(line.Substring("branch refs/heads/".Length))) acc
      | _ :: tail ->
          loop tail currentPath currentBranch acc

    loop lines None None []

  let tryFindWorktreePath cwd branch =
    getWorktrees cwd
    |> List.tryFind (fun worktree -> worktree.Branch = Some branch)
    |> Option.map _.Path

  let ensureParentDirectory cwd branch =
    let branchPath = combine cwd branch
    let parent = Path.GetDirectoryName branchPath
    if not (String.IsNullOrWhiteSpace parent) then
      ensureDirectoryExists parent

  let startPointForNewBranch cwd =
    let baseBranch =
      match detectBaseBranch cwd with
      | Some branchName -> branchName
      | None -> failwith "could not determine a base branch; expected main, master, HEAD, or a single local branch"

    refreshOrigin cwd

    if remoteBranchExists cwd baseBranch then
      sprintf "origin/%s" baseBranch
    elif localBranchExists cwd baseBranch then
      baseBranch
    else
      failwithf "base branch '%s' has no commits yet; make an initial commit there or connect a remote first" baseBranch

  let showRepoSummary cwd =
    ensureBareRepoHere cwd

    let origin =
      run cwd [ "remote"; "get-url"; "origin" ] true
      |> fun result -> if result.ExitCode = 0 then Some result.StdOut else None
      |> Option.defaultValue "(none)"

    let localBase = formatLocalBaseBranch cwd

    let remoteDefault = formatRemoteDefaultBranch cwd

    printfn "repo-root: %s" cwd
    printfn "origin: %s" origin
    printfn "local-base-branch: %s" localBase
    printfn "remote-default-branch: %s" remoteDefault
