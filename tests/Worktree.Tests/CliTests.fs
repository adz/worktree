namespace Worktree.Tests

open Swensen.Unquote
open Worktree
open Xunit

type CliTests() =
  [<Fact>]
  member _.``preferred convert branch prefers main``() =
    test <@ Domain.preferredConvertBranch true true = "main" @>

  [<Fact>]
  member _.``preferred convert branch falls back to master``() =
    test <@ Domain.preferredConvertBranch false true = "master" @>

  [<Fact>]
  member _.``preferred convert branch fails without main or master``() =
    raisesWith<exn>
      <@ Domain.preferredConvertBranch false false @>
      (fun ex -> <@ ex.Message = "repo convert requires a local 'main' or 'master' branch" @>)

  [<Fact>]
  member _.``create accepts split prefix and description``() =
    test <@ Domain.normalizeCreateBranch [ "feature"; "add-thing" ] = "feature/add-thing" @>

  [<Fact>]
  member _.``create accepts slash format``() =
    test <@ Domain.normalizeCreateBranch [ "fix/one-liner" ] = "fix/one-liner" @>

  [<Fact>]
  member _.``parse repo info command``() =
    match Cli.parse [ "repo"; "info" ] with
    | Ok (Command.Repo RepoCommand.Show) ->
        test <@ true @>
    | other ->
        failwithf "unexpected parse result: %A" other

  [<Fact>]
  member _.``parse repo create command``() =
    match Cli.parse [ "repo"; "create"; "EffectfulFlow" ] with
    | Ok (Command.Repo (RepoCommand.Create folderName)) ->
        test <@ folderName = "EffectfulFlow" @>
    | other ->
        failwithf "unexpected parse result: %A" other

  [<Fact>]
  member _.``parse repo convert command``() =
    match Cli.parse [ "repo"; "convert"; "EffectfulFlow" ] with
    | Ok (Command.Repo (RepoCommand.Convert folderName)) ->
        test <@ folderName = "EffectfulFlow" @>
    | other ->
        failwithf "unexpected parse result: %A" other

  [<Fact>]
  member _.``parse branch command``() =
    match Cli.parse [ "branch"; "feature/add-thing" ] with
    | Ok (Command.Branch branch) ->
        test <@ branch = "feature/add-thing" @>
    | other ->
        failwithf "unexpected parse result: %A" other

  [<Fact>]
  member _.``parse prune removed folders command``() =
    match Cli.parse [ "prune-removed-folders" ] with
    | Ok Command.PruneRemovedFolders ->
        test <@ true @>
    | other ->
        failwithf "unexpected parse result: %A" other

  [<Fact>]
  member _.``parse repair command``() =
    match Cli.parse [ "repair" ] with
    | Ok (Command.Repair paths) ->
        test <@ paths = [] @>
    | other ->
        failwithf "unexpected parse result: %A" other

  [<Fact>]
  member _.``parse repair command with paths``() =
    match Cli.parse [ "repair"; "../mylibs/CodecMapper/main"; "../mylibs/CodecMapper/feature/add-thing" ] with
    | Ok (Command.Repair paths) ->
        test <@ paths = [ "../mylibs/CodecMapper/main"; "../mylibs/CodecMapper/feature/add-thing" ] @>
    | other ->
        failwithf "unexpected parse result: %A" other

  [<Fact>]
  member _.``unknown repo subcommand reports repo-specific error``() =
    match Cli.parse [ "repo"; "wat" ] with
    | Error message ->
        test <@ message = "unknown worktree repo command: wat" @>
    | other ->
        failwithf "unexpected parse result: %A" other
