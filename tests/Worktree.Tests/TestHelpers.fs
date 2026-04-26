namespace Worktree.Tests

open System
open System.IO
open Worktree

type TempDir() =
  let path =
    Path.Combine(Path.GetTempPath(), "worktree-tests", Guid.NewGuid().ToString("N"))

  do
    Directory.CreateDirectory(path) |> ignore

  member _.Path = path

  interface IDisposable with
    member _.Dispose() =
      if Directory.Exists(path) then
        Directory.Delete(path, true)

module TestHelpers =
  let runGit cwd args =
    Git.runOrFail cwd args true

  let configureLocalUser cwd =
    runGit cwd [ "config"; "user.name"; "Worktree Tests" ] |> ignore
    runGit cwd [ "config"; "user.email"; "worktree-tests@example.com" ] |> ignore

  let writeFile (path: string) (contents: string) =
    let parent = Path.GetDirectoryName(path)

    if not (String.IsNullOrWhiteSpace parent) then
      Directory.CreateDirectory(parent) |> ignore

    File.WriteAllText(path, contents)

  let commitAll cwd message =
    runGit cwd [ "add"; "." ] |> ignore
    runGit cwd [ "commit"; "-m"; message ] |> ignore

  let initNormalRepo path branchName =
    Directory.CreateDirectory(path) |> ignore
    runGit path [ "init"; sprintf "--initial-branch=%s" branchName ] |> ignore
    configureLocalUser path

  let createCommittedNormalRepo path branchName =
    initNormalRepo path branchName
    writeFile (Path.Combine(path, "README.md")) "# test"
    commitAll path "Initial commit"

  let bareGitDirPath repoRoot =
    Path.Combine(repoRoot, ".git")

  let worktreePath repoRoot branch =
    Path.Combine(repoRoot, branch)
