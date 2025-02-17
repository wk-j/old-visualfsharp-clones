// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Build

open System
open System.Text
open System.Diagnostics.CodeAnalysis
open System.Reflection
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Internal.Utilities

[<assembly: System.Runtime.InteropServices.ComVisible(false)>]
[<assembly: System.CLSCompliant(true)>]

[<assembly: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope="type", Target="Microsoft.FSharp.Build.Fsc", MessageId="Fsc")>]
do()


type FscCommandLineBuilder() =
    // In addition to generating a command-line that will be handed to cmd.exe, we also generate
    // an array of individual arguments.  The former needs to be quoted (and cmd.exe will strip the
    // quotes while parsing), whereas the latter is not.  See bug 4357 for background; this helper
    // class gets us out of the business of unparsing-then-reparsing arguments.

    let builder = new CommandLineBuilder()
    let mutable args = []  // in reverse order
    let mutable srcs = []  // in reverse order
    let mutable alreadyCalledWithFilenames = false
    /// Return a list of the arguments (with no quoting for the cmd.exe shell)
    member x.CapturedArguments() =
        assert(not alreadyCalledWithFilenames)
        List.rev args
    /// Return a list of the sources (with no quoting for the cmd.exe shell)
    member x.CapturedFilenames() =
        assert(alreadyCalledWithFilenames)
        List.rev srcs
    /// Return a full command line (with quoting for the cmd.exe shell)
    override x.ToString() =
        builder.ToString()

    member x.AppendFileNamesIfNotNull(filenames:ITaskItem array, sep:string) =
        builder.AppendFileNamesIfNotNull(filenames, sep)
        // do not update "args", not used
        for item in filenames do
            let tmp = new CommandLineBuilder()
            tmp.AppendSwitchUnquotedIfNotNull("", item.ItemSpec)  // we don't want to quote the filename, this is a way to get that
            let s = tmp.ToString()
            if s <> String.Empty then
                srcs <- tmp.ToString() :: srcs
        alreadyCalledWithFilenames <- true

    member x.AppendSwitchIfNotNull(switch:string, values:string array, sep:string) =
        builder.AppendSwitchIfNotNull(switch, values, sep)
        let tmp = new CommandLineBuilder()
        tmp.AppendSwitchUnquotedIfNotNull(switch, values, sep)
        let s = tmp.ToString()
        if s <> String.Empty then
            args <- s :: args

    member x.AppendSwitchIfNotNull(switch:string, value:string) =
        builder.AppendSwitchIfNotNull(switch, value)
        let tmp = new CommandLineBuilder()
        tmp.AppendSwitchUnquotedIfNotNull(switch, value)
        let s = tmp.ToString()
        if s <> String.Empty then
            args <- s :: args

    member x.AppendSwitchUnquotedIfNotNull(switch:string, value:string) =
        assert(switch = "")  // we only call this method for "OtherFlags"
        // Unfortunately we still need to mimic what cmd.exe does, but only for "OtherFlags".
        let ParseCommandLineArgs(commandLine:string) = // returns list in reverse order
            let mutable args = []
            let mutable i = 0 // index into commandLine
            let len = commandLine.Length
            while i < len do
                // skip whitespace
                while i < len && System.Char.IsWhiteSpace(commandLine, i) do
                    i <- i + 1
                if i < len then
                    // parse an argument
                    let sb = new StringBuilder()
                    let mutable finished = false
                    let mutable insideQuote = false
                    while i < len && not finished do
                        match commandLine.[i] with
                        | '"' -> insideQuote <- not insideQuote; i <- i + 1
                        | c when not insideQuote && System.Char.IsWhiteSpace(c) -> finished <- true
                        | c -> sb.Append(c) |> ignore; i <- i + 1
                    args <- sb.ToString() :: args
            args
        builder.AppendSwitchUnquotedIfNotNull(switch, value)
        let tmp = new CommandLineBuilder()
        tmp.AppendSwitchUnquotedIfNotNull(switch, value)
        let s = tmp.ToString()
        if s <> String.Empty then
            args <- ParseCommandLineArgs(s) @ args

    member x.AppendSwitch(switch:string) =
        builder.AppendSwitch(switch)
        args <- switch :: args

//There are a lot of flags on fsc.exe.
//For now, not all of them are represented in the "Fsc class" object model.
//The goal is to have the most common/important flags available via the Fsc class, and the
//rest can be "backdoored" through the .OtherFlags property.

type [<Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")>] Fsc() as this = 
    inherit ToolTask()
    let mutable baseAddress : string = null
    let mutable codePage : string = null
    let mutable debugSymbols = false
    let mutable debugType : string = null
    let mutable defineConstants : ITaskItem[] = [||]
    let mutable disabledWarnings : string = null
    let mutable documentationFile : string = null
    let mutable generateInterfaceFile : string = null
    let mutable keyFile : string = null
    let mutable noFramework = false
    let mutable optimize  : bool = true
    let mutable tailcalls : bool = true
    let mutable otherFlags : string = null
    let mutable outputAssembly : string = null 
    let mutable pdbFile : string = null
    let mutable platform : string = null
    let mutable prefer32bit : bool = false
    let mutable references : ITaskItem[] = [||]
    let mutable referencePath : string = null
    let mutable resources : ITaskItem[] = [||]
    let mutable sources : ITaskItem[] = [||]
    let mutable targetType : string = null 
#if FX_ATLEAST_35   
#else 
    let mutable toolExe : string = "fsc.exe"
#endif    
    let mutable warningLevel : string = null
    let mutable treatWarningsAsErrors : bool = false
    let mutable warningsAsErrors : string = null
    let mutable toolPath : string = 
        match FSharpEnvironment.BinFolderOfDefaultFSharpCompiler with
        | Some s -> s
        | None -> ""
    let mutable versionFile : string = null
    let mutable win32res : string = null
    let mutable win32manifest : string = null
    let mutable vserrors : bool = false
    let mutable validateTypeProviders : bool = false
    let mutable vslcid : string = null
    let mutable utf8output : bool = false
    let mutable subsystemVersion : string = null
    let mutable highEntropyVA : bool = false
    let mutable targetProfile : string = null
    let mutable sqmSessionGuid : string = null

    let mutable capturedArguments : string list = []  // list of individual args, to pass to HostObject Compile()
    let mutable capturedFilenames : string list = []  // list of individual source filenames, to pass to HostObject Compile()

    do
        this.YieldDuringToolExecution <- true  // See bug 6483; this makes parallel build faster, and is fine to set unconditionally
    // --baseaddress
    member fsc.BaseAddress
        with get() = baseAddress 
        and set(s) = baseAddress <- s        
    // --codepage <int>: Specify the codepage to use when opening source files
    member fsc.CodePage
        with get() = codePage
        and set(s) = codePage <- s
    // -g: Produce debug file. Disables optimizations if a -O flag is not given.
    member fsc.DebugSymbols
        with get() = debugSymbols
        and set(b) = debugSymbols <- b
    // --debug <none/pdbonly/full>: Emit debugging information
    member fsc.DebugType
        with get() = debugType
        and set(s) = debugType <- s
    // --nowarn <string>: Do not report the given specific warning.
    member fsc.DisabledWarnings
        with get() = disabledWarnings
        and set(a) = disabledWarnings <- a        
    // --define <string>: Define the given conditional compilation symbol.
    member fsc.DefineConstants
        with get() = defineConstants
        and set(a) = defineConstants <- a
    // --doc <string>: Write the xmldoc of the assembly to the given file.
    member fsc.DocumentationFile
        with get() = documentationFile
        and set(s) = documentationFile <- s
    // --generate-interface-file <string>: 
    //     Print the inferred interface of the
    //     assembly to a file.
    member fsc.GenerateInterfaceFile
        with get() = generateInterfaceFile
        and set(s) = generateInterfaceFile <- s  
    // --keyfile <string>: 
    //     Sign the assembly the given keypair file, as produced
    //     by the .NET Framework SDK 'sn.exe' tool. This produces
    //     an assembly with a strong name. This is only relevant if producing
    //     an assembly to be shared amongst programs from different
    //     directories, e.g. to be installed in the Global Assembly Cache.
    member fsc.KeyFile
        with get() = keyFile
        and set(s) = keyFile <- s
    // --noframework
    member fsc.NoFramework
        with get() = noFramework 
        and set(b) = noFramework <- b        
    // --optimize
    member fsc.Optimize
        with get() = optimize
        and set(p) = optimize <- p
    // --tailcalls
    member fsc.Tailcalls
        with get() = tailcalls
        and set(p) = tailcalls <- p
    // REVIEW: decide whether to keep this, for now is handy way to deal with as-yet-unimplemented features
    member fsc.OtherFlags
        with get() = otherFlags
        and set(s) = otherFlags <- s
    // -o <string>: Name the output file.
    member fsc.OutputAssembly
        with get() = outputAssembly
        and set(s) = outputAssembly <- s
    // --pdb <string>: 
    //     Name the debug output file.
    member fsc.PdbFile
        with get() = pdbFile
        and set(s) = pdbFile <- s
    // --platform <string>: Limit which platforms this code can run on:
    //            x86
    //            x64
    //            Itanium
    //            anycpu
    //            anycpu32bitpreferred
    member fsc.Platform
        with get() = platform 
        and set(s) = platform <- s 
    // indicator whether anycpu32bitpreferred is applicable or not
    member fsc.Prefer32Bit
        with get() = prefer32bit 
        and set(s) = prefer32bit <- s 
    // -r <string>: Reference an F# or .NET assembly.
    member fsc.References 
        with get() = references 
        and set(a) = references <- a
    // --lib    
    member fsc.ReferencePath
        with get() = referencePath
        and set(s) = referencePath <- s
    // --resource <string>: Embed the specified managed resources (.resource).
    //   Produce .resource files from .resx files using resgen.exe or resxc.exe.
    member fsc.Resources
        with get() = resources
        and set(a) = resources <- a
    // source files 
    member fsc.Sources  
        with get() = sources 
        and set(a) = sources <- a
    // --target exe: Produce an executable with a console
    // --target winexe: Produce an executable which does not have a
    //      stdin/stdout/stderr
    // --target library: Produce a DLL
    // --target module: Produce a module that can be added to another assembly
    member fsc.TargetType
        with get() = targetType
        and set(s) = targetType <- s

    // --version-file <string>: 
    member fsc.VersionFile
        with get() = versionFile
        and set(s) = versionFile <- s

#if FX_ATLEAST_35
#else
    // Allow overriding to the executable name "fsc.exe"
    member fsc.ToolExe
        with get() = toolExe
        and set(s) = toolExe<- s
#endif

    // For targeting other folders for "fsc.exe" (or ToolExe if different)
    member fsc.ToolPath
        with get() = toolPath
        and set(s) = toolPath <- s
    
    // For specifying a win32 native resource file (.res)     
    member fsc.Win32ResourceFile
        with get() = win32res
        and set(s) = win32res <- s
        
    // For specifying a win32 manifest file
    member fsc.Win32ManifestFile
        with get() = win32manifest
        and set(m) = win32manifest <- m
        
    // For specifying the warning level (0-4)    
    member fsc.WarningLevel
        with get() = warningLevel
        and set(s) = warningLevel <- s
        
    member fsc.TreatWarningsAsErrors
        with get() = treatWarningsAsErrors
        and set(p) = treatWarningsAsErrors <- p
        
    member fsc.WarningsAsErrors 
        with get() = warningsAsErrors
        and set(s) = warningsAsErrors <- s
    
    member fsc.VisualStudioStyleErrors
        with get() = vserrors
        and set(p) = vserrors <- p

    member fsc.ValidateTypeProviders
        with get() = validateTypeProviders
        and set(p) = validateTypeProviders <- p

    member fsc.LCID
        with get() = vslcid
        and set(p) = vslcid <- p

    member fsc.Utf8Output
        with get() = utf8output
        and set(p) = utf8output <- p

    member fsc.SubsystemVersion
        with get() = subsystemVersion
        and set(p) = subsystemVersion <- p

    member fsc.HighEntropyVA
        with get() = highEntropyVA
        and set(p) = highEntropyVA <- p

    member fsc.TargetProfile
        with get() = targetProfile
        and set(p) = targetProfile <- p

    member fsc.SqmSessionGuid
        with get() = sqmSessionGuid
        and set(p) = sqmSessionGuid <- p
        
    // ToolTask methods
    override fsc.ToolName = "fsc.exe" 
    override fsc.StandardErrorEncoding = if utf8output then System.Text.Encoding.UTF8 else base.StandardErrorEncoding
    override fsc.StandardOutputEncoding = if utf8output then System.Text.Encoding.UTF8 else base.StandardOutputEncoding
    override fsc.GenerateFullPathToTool() = 
        if toolPath = "" then
            raise (new System.InvalidOperationException(FSBuild.SR.toolpathUnknown()))
        System.IO.Path.Combine(toolPath, fsc.ToolExe)
    member internal fsc.InternalGenerateFullPathToTool() = fsc.GenerateFullPathToTool()  // expose for unit testing
    member internal fsc.BaseExecuteTool(pathToTool, responseFileCommands, commandLineCommands) =  // F# does not allow protected members to be captured by lambdas, this is the standard workaround
        base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands)
    /// Intercept the call to ExecuteTool to handle the host compile case.
    override fsc.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands) =
        let ho = box fsc.HostObject
        match ho with
        | null ->
            base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands)
        | _ ->
            let sources = sources|>Array.map(fun i->i.ItemSpec)
            let baseCall = fun (dummy : int) -> fsc.BaseExecuteTool(pathToTool, responseFileCommands, commandLineCommands)
            // We are using a Converter<int,int> rather than a "unit->int" because it is too hard to
            // figure out how to pass an F# function object via reflection.  
            let baseCallDelegate = new System.Converter<int,int>(baseCall)
            try 
                let ret = 
                    (ho.GetType()).InvokeMember("Compile", BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.InvokeMethod ||| BindingFlags.Instance, null, ho, 
                                                [| box baseCallDelegate; box (capturedArguments |> List.toArray); box (capturedFilenames |> List.toArray) |],
                                                System.Globalization.CultureInfo.InvariantCulture)
                unbox ret
            with 
            | :? System.Reflection.TargetInvocationException as tie when (match tie.InnerException with | :? Microsoft.Build.Exceptions.BuildAbortedException -> true | _ -> false) ->
                fsc.Log.LogError(tie.InnerException.Message, [| |])
                -1  // ok, this is what happens when VS IDE cancels the build, no need to assert, just log the build-canceled error and return -1 to denote task failed
            | e ->
                System.Diagnostics.Debug.Assert(false, "HostObject received by Fsc task did not have a Compile method or the compile method threw an exception. "+(e.ToString()))
                reraise()
           
    override fsc.GenerateCommandLineCommands() =
        let builder = new FscCommandLineBuilder()
        
        // OutputAssembly
        builder.AppendSwitchIfNotNull("-o:", outputAssembly)
        // CodePage
        builder.AppendSwitchIfNotNull("--codepage:", codePage)
        // Debug
        if debugSymbols then
            builder.AppendSwitch("-g")
        // DebugType
        builder.AppendSwitchIfNotNull("--debug:", 
            if debugType = null then null else
                match debugType.ToUpperInvariant() with
                | "NONE"     -> null
                | "PDBONLY"  -> "pdbonly"
                | "FULL"     -> "full"
                | _         -> null)
        // NoFramework
        if noFramework then 
            builder.AppendSwitch("--noframework") 
        // BaseAddress
        builder.AppendSwitchIfNotNull("--baseaddress:", baseAddress)
        // DefineConstants
        for item in defineConstants do
            builder.AppendSwitchIfNotNull("--define:", item.ItemSpec)          
        // DocumentationFile
        builder.AppendSwitchIfNotNull("--doc:", documentationFile)
        // GenerateInterfaceFile
        builder.AppendSwitchIfNotNull("--sig:", generateInterfaceFile)
        // KeyFile
        builder.AppendSwitchIfNotNull("--keyfile:", keyFile)
        // Optimize
        if optimize then
            builder.AppendSwitch("--optimize+")
        else
            builder.AppendSwitch("--optimize-")
        if not tailcalls then
            builder.AppendSwitch("--tailcalls-")
        // PdbFile
        builder.AppendSwitchIfNotNull("--pdb:", pdbFile)
        // Platform
        builder.AppendSwitchIfNotNull("--platform:", 
            let ToUpperInvariant (s:string) = if s = null then null else s.ToUpperInvariant()
            match ToUpperInvariant(platform), prefer32bit, ToUpperInvariant(targetType) with
                | "ANYCPU", true, "EXE"
                | "ANYCPU", true, "WINEXE" -> "anycpu32bitpreferred"
                | "ANYCPU",  _, _  -> "anycpu"
                | "X86"   ,  _, _  -> "x86"
                | "X64"   ,  _, _  -> "x64"
                | "ITANIUM", _, _  -> "Itanium"
                | _         -> null)
        // Resources
        for item in resources do
            builder.AppendSwitchIfNotNull("--resource:", item.ItemSpec)
        // VersionFile
        builder.AppendSwitchIfNotNull("--versionfile:", versionFile)
        // References
        for item in references do
            builder.AppendSwitchIfNotNull("-r:", item.ItemSpec)
        // ReferencePath
        let referencePathArray = // create a array of strings
            match referencePath with
            | null -> null
            | _ -> referencePath.Split([|';'; ','|], StringSplitOptions.RemoveEmptyEntries)
                  
        builder.AppendSwitchIfNotNull("--lib:", referencePathArray, ",")   
        // TargetType
        builder.AppendSwitchIfNotNull("--target:", 
            if targetType = null then null else
                match targetType.ToUpperInvariant() with
                | "LIBRARY" -> "library"
                | "EXE" -> "exe"
                | "WINEXE" -> "winexe" 
                | "MODULE" -> "module"
                | _ -> null)
        
        // NoWarn
        match disabledWarnings with
        | null -> ()
        | _ -> builder.AppendSwitchIfNotNull("--nowarn:", disabledWarnings.Split([|' '; ';'; ','; '\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries), ",")
        
        // WarningLevel
        builder.AppendSwitchIfNotNull("--warn:", warningLevel)
        
        // TreatWarningsAsErrors
        if treatWarningsAsErrors then
            builder.AppendSwitch("--warnaserror")
            
        // WarningsAsErrors
        // Change warning 76, HashReferenceNotAllowedInNonScript/HashDirectiveNotAllowedInNonScript/HashIncludeNotAllowedInNonScript, into an error
        // REVIEW: why is this logic here? In any case these are errors already by default!
        let warningsAsErrorsArray =
            match warningsAsErrors with
            | null -> [|"76"|]
            | _ -> (warningsAsErrors + " 76 ").Split([|' '; ';'; ','|], StringSplitOptions.RemoveEmptyEntries)                        

        builder.AppendSwitchIfNotNull("--warnaserror:", warningsAsErrorsArray, ",")            
     
            
        // Win32ResourceFile
        builder.AppendSwitchIfNotNull("--win32res:", win32res)
        
        // Win32ManifestFile
        builder.AppendSwitchIfNotNull("--win32manifest:", win32manifest)
        
        // VisualStudioStyleErrors 
        if vserrors then
            builder.AppendSwitch("--vserrors")      

        // ValidateTypeProviders 
        if validateTypeProviders then
            builder.AppendSwitch("--validate-type-providers")           

        builder.AppendSwitchIfNotNull("--LCID:", vslcid)
        
        if utf8output then
            builder.AppendSwitch("--utf8output")
            
        // When building using the fsc task, always emit the "fullpaths" flag to make the output easier
        // for the user to parse
        builder.AppendSwitch("--fullpaths")
        
        // When building using the fsc task, also emit "flaterrors" to ensure that multi-line error messages
        // aren't trimmed
        builder.AppendSwitch("--flaterrors")

        builder.AppendSwitchIfNotNull("--subsystemversion:", subsystemVersion)
        if highEntropyVA then
            builder.AppendSwitch("--highentropyva+")
        else
            builder.AppendSwitch("--highentropyva-")

        builder.AppendSwitchIfNotNull("--sqmsessionguid:", sqmSessionGuid)

        builder.AppendSwitchIfNotNull("--targetprofile:", targetProfile)

        // OtherFlags - must be second-to-last
        builder.AppendSwitchUnquotedIfNotNull("", otherFlags)
        capturedArguments <- builder.CapturedArguments()
        
        // Sources - these have to go last
        builder.AppendFileNamesIfNotNull(sources, " ")
        capturedFilenames <- builder.CapturedFilenames()
        let s = builder.ToString()
        s
    // expose this to internal components (for nunit testing)
    member internal fsc.InternalGenerateCommandLineCommands() =
        fsc.GenerateCommandLineCommands()
    member internal fsc.InternalExecuteTool(pathToTool, responseFileCommands, commandLineCommands) =
        fsc.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands)
    member internal fsc.GetCapturedArguments() = 
        [|
            yield! capturedArguments
            yield! capturedFilenames
        |]

module Attributes =
    //[<assembly: System.Security.SecurityTransparent>]
    do()
