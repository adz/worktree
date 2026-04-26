namespace Worktree

open System
open System.Diagnostics

type ProcessResult =
  { ExitCode: int
    StdOut: string
    StdErr: string }

module Process =
  let private joinLines (text: string) =
    text.Replace("\r\n", "\n").TrimEnd('\n', '\r')

  let run (workingDir: string) (fileName: string) (args: string list) (capture: bool) =
    let info = ProcessStartInfo()
    info.FileName <- fileName
    info.WorkingDirectory <- workingDir
    info.UseShellExecute <- false
    info.RedirectStandardOutput <- capture
    info.RedirectStandardError <- capture

    for arg in args do
      info.ArgumentList.Add arg

    use proc = new Process()
    proc.StartInfo <- info

    if not (proc.Start()) then
      failwithf "failed to start process: %s" fileName

    let stdOut =
      if capture then proc.StandardOutput.ReadToEnd() else ""

    let stdErr =
      if capture then proc.StandardError.ReadToEnd() else ""

    proc.WaitForExit()

    { ExitCode = proc.ExitCode
      StdOut = joinLines stdOut
      StdErr = joinLines stdErr }

  let runOrFail workingDir fileName args capture =
    let result = run workingDir fileName args capture

    if result.ExitCode <> 0 then
      let detail =
        [ result.StdOut; result.StdErr ]
        |> List.filter (String.IsNullOrWhiteSpace >> not)
        |> String.concat "\n"

      if String.IsNullOrWhiteSpace detail then
        failwithf "%s failed with exit code %d" fileName result.ExitCode
      else
        failwith detail

    result
