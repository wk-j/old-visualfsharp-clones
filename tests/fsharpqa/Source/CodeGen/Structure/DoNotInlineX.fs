// #Regression #NoMT #CodeGen #Attributes

open System
open System.Diagnostics
open System.IO

let programFiles = 
    let pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)")
    if String.IsNullOrEmpty(pf86) then Environment.GetEnvironmentVariable("ProgramFiles") else pf86
let fsc =
    let overridePath = Environment.GetEnvironmentVariable("FSC")
    if not (String.IsNullOrEmpty(overridePath)) then 
        overridePath 
    else
        let fsc45 = programFiles + @"\Microsoft SDKs\F#\3.1\Framework\v4.0\fsc.exe"
        let fsc40 = programFiles + @"\Microsoft F#\v4.0\fsc.exe"
        let fsc20 = programFiles + @"\FSharp-2.0.0.0\bin\fsc.exe"
        
        match ([fsc45; fsc40; fsc20] |> List.tryFind(fun x -> File.Exists(x))) with
        | Some(path) -> path
        | None -> "fsc.exe"  // just use what's on the PATH


let start (p1 : string) = Process.Start(p1)

let CompileFile file args = 
    let p = Process.Start(fsc, file + " " + args)
    while not (p.HasExited) do ()

[<EntryPoint>]
let main (args : string[]) =
    if args.Length = 0 then 0 else
        let baseFlag, derivedFlag, expectedResult1, expectedResult2 = args.[0], args.[1], int args.[2], int args.[3]
        exit <|
            try
                CompileFile "BaseType.fs" ("-a --define:" + baseFlag)
                printfn "Compiled BaseType with %A" baseFlag
                CompileFile "DerivedType.fs" ("-r:BaseType.dll --define:" + derivedFlag)
                printfn "Compiled DerivedType with %A" derivedFlag

                let r1 = start "DerivedType.exe"
                while not r1.HasExited do ()
                printfn "Ran DerivedType.exe with result: %A" r1.ExitCode

                CompileFile "BaseType.fs" "-a"
                printfn "Compiled BaseType without %A" baseFlag
                let r2 = start "DerivedType.exe"
                while not r2.HasExited do ()
                printfn "Ran DerivedType.exe with result: %A" r2.ExitCode

                if r1.ExitCode = expectedResult1 && r2.ExitCode = expectedResult2 then 0 else 1
            with
                _ -> 1
    