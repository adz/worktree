open System
open System.IO
open Worktree

[<EntryPoint>]
let main argv =
  try
    let cwd = Directory.GetCurrentDirectory()

    match Cli.parse (List.ofArray argv) with
    | Ok command -> Cli.execute cwd command
    | Error message -> raise (Exception message)
    
  with ex ->
    eprintfn $"ERROR: %s{ex.Message}"
    1
