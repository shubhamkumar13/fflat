module fflat.CompileFSharp

open System

let fscExtraArgs = [
    "--debug-"
    "--nocopyfsharpcore"
    "--noframework"
    "--optimize+"
    "--reflectionfree"
    "--tailcalls+"
    "--target:library"
    // --
    "--define:FFLAT"
    "--define:NET"
    "--define:NET5_0_OR_GREATER"
    "--define:NET6_0_OR_GREATER"
    "--define:NET7_0"
    "--define:NET7_0_OR_GREATER"
    "--define:NETCOREAPP1_0_OR_GREATER"
    "--define:NETCOREAPP1_1_OR_GREATER"
    "--define:NETCOREAPP2_0_OR_GREATER"
    "--define:NETCOREAPP2_1_OR_GREATER"
    "--define:NETCOREAPP2_2_OR_GREATER"
    "--define:NETCOREAPP3_0_OR_GREATER"
    "--define:NETCOREAPP3_1_OR_GREATER"
    "--define:NETCOREAPP"
    "--define:RELEASE"
    "--highentropyva+"
    "--targetprofile:netcore"
    // "--crossoptimize+" // deprecated
]

open System.Reflection
open FSharp.Compiler.CodeAnalysis
open System.IO
open FSharp.Compiler.Text


module References =

    let bflatExclusions =
        set [
            "netstandard.dll"
            "System.Core.dll"
            "mscorlib.dll"
            "System.Private.CoreLib.dll"
        ]


let tryCompileToDll (outputDllPath: string) fsxFilePath =
    let checker = FSharpChecker.Create()

    let sourceText =
        File.ReadAllText(fsxFilePath)
        |> SourceText.ofString

    async {
        let! projOpts, _ =
            checker.GetProjectOptionsFromScript(
                fsxFilePath,
                sourceText,
                assumeDotNetFramework = false,
                useFsiAuxLib = true,
                useSdkRefs = true
            )

        let temporaryDllFile = outputDllPath

        let nugetCachePath =
            let userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            Path.Combine(userFolder, ".packagemanagement", "nuget")

        let filteredSourceFiles =
            projOpts.SourceFiles
            |> Seq.where (fun v ->
                not (v.EndsWith(".fsproj.fsx"))
                && not (v.StartsWith(nugetCachePath))
            )

        // let fsharpCoreIndex =
        //     projOpts.OtherOptions
        //     |> Seq.findIndex (fun v -> v.EndsWith("FSharp.Core.dll"))

        // projOpts.OtherOptions[fsharpCoreIndex]
        //     <- "-r:/home/ian/.nuget/packages/fsharp.core/7.0.300-dev/lib/netstandard2.1/FSharp.Core.dll"

        let! compileResult, exitCode =
            checker.Compile(
                [|
                    yield! projOpts.OtherOptions
                    yield! fscExtraArgs
                    $"--out:{temporaryDllFile}"
                    yield!
                        (projOpts.ReferencedProjects
                         |> Array.map (fun v -> v.OutputFile))

                    yield! filteredSourceFiles
                |]
            )

        match exitCode with
        | 0 -> return projOpts
        | _ ->
            compileResult
            |> Array.iter (fun v -> stdout.WriteLine $"%A{v}")

            return exit 1
    }
    |> Async.RunSynchronously
