namespace Worktree.Tests

open System
open System.IO
open Swensen.Unquote
open Worktree
open Xunit

type CliIntegrationTests() =
  let captureStdout f =
    let original = Console.Out
    use writer = new StringWriter()
    Console.SetOut(writer)

    try
      f () |> ignore
      writer.ToString().Replace("\r\n", "\n").Trim()
    finally
      Console.SetOut(original)

  [<Fact>]
  member _.``repo create creates bare root with main worktree``() =
    use temp = new TempDir()

    let exitCode = Cli.execute temp.Path (Command.Repo(RepoCommand.Create "Example"))
    let repoRoot = Path.Combine(temp.Path, "Example")
    let mainWorktree = TestHelpers.worktreePath repoRoot "main"

    test <@ exitCode = 0 @>
    test <@ Git.isBareRepoHere repoRoot @>
    test <@ Directory.Exists(mainWorktree) @>

  [<Fact>]
  member _.``repo show explains empty remote and unborn local branch``() =
    use temp = new TempDir()

    let remoteRoot = Path.Combine(temp.Path, "Remote.git")
    let repoRoot = Path.Combine(temp.Path, "Example")

    Directory.CreateDirectory(remoteRoot) |> ignore
    TestHelpers.runGit remoteRoot [ "init"; "--bare" ] |> ignore

    Cli.execute temp.Path (Command.Repo(RepoCommand.Create "Example")) |> ignore
    Cli.execute repoRoot (Command.Repo(RepoCommand.SetRemote(Path.GetFullPath remoteRoot))) |> ignore

    let output = captureStdout (fun () -> Cli.execute repoRoot (Command.Repo RepoCommand.Show))

    test <@ output.Contains("local-base-branch: main (no commits yet)") @>
    test <@ output.Contains("remote-default-branch: (remote has no default branch yet; push first commit from main)") @>

  [<Fact>]
  member _.``create creates a new branch worktree from main``() =
    use temp = new TempDir()

    let repoRoot = Path.Combine(temp.Path, "Example")
    let mainWorktree = TestHelpers.worktreePath repoRoot "main"
    let featureWorktree = TestHelpers.worktreePath repoRoot "feature/add-thing"

    Cli.execute temp.Path (Command.Repo(RepoCommand.Create "Example")) |> ignore
    TestHelpers.configureLocalUser mainWorktree
    TestHelpers.writeFile (Path.Combine(mainWorktree, "README.md")) "hello"
    TestHelpers.commitAll mainWorktree "Initial commit"

    let exitCode = Cli.execute repoRoot (Command.Create "feature/add-thing")

    test <@ exitCode = 0 @>
    test <@ Directory.Exists(featureWorktree) @>
    test <@ Git.tryFindWorktreePath repoRoot "feature/add-thing" = Some(Path.GetFullPath featureWorktree) @>

  [<Fact>]
  member _.``branch creates a worktree for an existing local branch``() =
    use temp = new TempDir()

    let repoRoot = Path.Combine(temp.Path, "Example")
    let mainWorktree = TestHelpers.worktreePath repoRoot "main"
    let branchWorktree = TestHelpers.worktreePath repoRoot "fix/existing"

    Cli.execute temp.Path (Command.Repo(RepoCommand.Create "Example")) |> ignore
    TestHelpers.configureLocalUser mainWorktree
    TestHelpers.writeFile (Path.Combine(mainWorktree, "README.md")) "hello"
    TestHelpers.commitAll mainWorktree "Initial commit"
    TestHelpers.runGit mainWorktree [ "branch"; "fix/existing" ] |> ignore

    let exitCode = Cli.execute repoRoot (Command.Branch "fix/existing")

    test <@ exitCode = 0 @>
    test <@ Directory.Exists(branchWorktree) @>
    test <@ Git.tryFindWorktreePath repoRoot "fix/existing" = Some(Path.GetFullPath branchWorktree) @>

  [<Fact>]
  member _.``repo convert preserves origin and creates backup bare layout``() =
    use temp = new TempDir()

    let remoteRoot = Path.Combine(temp.Path, "Remote.git")
    let sourceRoot = Path.Combine(temp.Path, "CodecMapper")
    let convertedRoot = sourceRoot
    let backupRoot = Path.Combine(temp.Path, "CodecMapper.bak")
    let mainWorktree = TestHelpers.worktreePath convertedRoot "main"

    Directory.CreateDirectory(remoteRoot) |> ignore
    TestHelpers.runGit remoteRoot [ "init"; "--bare" ] |> ignore

    TestHelpers.createCommittedNormalRepo sourceRoot "main"
    TestHelpers.runGit sourceRoot [ "remote"; "add"; "origin"; remoteRoot ] |> ignore
    TestHelpers.runGit sourceRoot [ "push"; "-u"; "origin"; "main" ] |> ignore

    let exitCode = Cli.execute temp.Path (Command.Repo(RepoCommand.Convert "CodecMapper"))

    test <@ exitCode = 0 @>
    test <@ Directory.Exists(backupRoot) @>
    test <@ Git.isBareRepoHere convertedRoot @>
    test <@ Directory.Exists(mainWorktree) @>
    test <@ Git.tryGetOriginRemote convertedRoot = Some(Path.GetFullPath remoteRoot) @>

  [<Fact>]
  member _.``repo convert fails for dirty normal repo``() =
    use temp = new TempDir()

    let sourceRoot = Path.Combine(temp.Path, "CodecMapper")

    TestHelpers.createCommittedNormalRepo sourceRoot "main"
    TestHelpers.writeFile (Path.Combine(sourceRoot, "dirty.txt")) "uncommitted"

    raisesWith<exn>
      <@ Cli.execute temp.Path (Command.Repo(RepoCommand.Convert "CodecMapper")) @>
      (fun ex -> <@ ex.Message = "repo convert requires a clean working tree" @>)

  [<Fact>]
  member _.``prune removed folders stops tracking deleted worktrees``() =
    use temp = new TempDir()

    let repoRoot = Path.Combine(temp.Path, "Example")
    let mainWorktree = TestHelpers.worktreePath repoRoot "main"
    let featureWorktree = TestHelpers.worktreePath repoRoot "feature/add-thing"

    Cli.execute temp.Path (Command.Repo(RepoCommand.Create "Example")) |> ignore
    TestHelpers.configureLocalUser mainWorktree
    TestHelpers.writeFile (Path.Combine(mainWorktree, "README.md")) "hello"
    TestHelpers.commitAll mainWorktree "Initial commit"
    Cli.execute repoRoot (Command.Create "feature/add-thing") |> ignore

    Directory.Delete(featureWorktree, true)

    let exitCode = Cli.execute repoRoot Command.PruneRemovedFolders

    test <@ exitCode = 0 @>
    test <@ Git.tryFindWorktreePath repoRoot "feature/add-thing" = None @>

  [<Fact>]
  member _.``repair updates moved linked worktree paths``() =
    use temp = new TempDir()

    let repoRoot = Path.Combine(temp.Path, "Example")
    let mainWorktree = TestHelpers.worktreePath repoRoot "main"
    let originalWorktree = TestHelpers.worktreePath repoRoot "feature/add-thing"
    let movedRoot = Path.Combine(temp.Path, "Moved")
    let movedWorktree = Path.Combine(movedRoot, "feature/add-thing")

    Cli.execute temp.Path (Command.Repo(RepoCommand.Create "Example")) |> ignore
    TestHelpers.configureLocalUser mainWorktree
    TestHelpers.writeFile (Path.Combine(mainWorktree, "README.md")) "hello"
    TestHelpers.commitAll mainWorktree "Initial commit"
    Cli.execute repoRoot (Command.Create "feature/add-thing") |> ignore

    Directory.CreateDirectory(Path.GetDirectoryName(movedWorktree)) |> ignore
    Directory.Move(originalWorktree, movedWorktree)

    let exitCode = Cli.execute repoRoot (Command.Repair [ movedWorktree ])

    test <@ exitCode = 0 @>
    test <@ Git.tryFindWorktreePath repoRoot "feature/add-thing" = Some(Path.GetFullPath movedWorktree) @>
