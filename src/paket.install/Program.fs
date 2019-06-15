// Learn more about F# at http://fsharp.org

#if INTERACTIVE
#r "System.Xml.Linq"
#endif
open System
open Argu

let (</>) parent child = IO.Path.Combine(parent, child)


type LogLevel =
    | Silent
    | Verbose
    | Info
    | Warning

module Log =
    let mutable outLevel = Info
    let defaultColor = Console.ForegroundColor

    let reset() = Console.ForegroundColor <- defaultColor

    let color = function 
        | Silent -> ConsoleColor.Black
        | Verbose -> ConsoleColor.Gray
        | Info -> defaultColor
        | Warning -> ConsoleColor.Yellow

    
    let log level text =
        if outLevel <= level then
            Console.ForegroundColor <- color level 
            printfn "%s" text

    let logfn level fmt = Printf.kprintf (log level) fmt

module OS =
    open System.Runtime.InteropServices

    let isWindows = RuntimeInformation.IsOSPlatform OSPlatform.Windows

    let withExeExt p = if isWindows then p + ".exe" else p

module Xml =
    open System.Xml.Linq
    let xn = XName.op_Implicit
    let xdoc root = XDocument(root: XObject)
    let xe name attributes (children: XNode list) =  
        
        XElement(xn name, 
            [| for n,v in attributes do
                yield XAttribute(xn n, v) :> XObject
               for e in children do
                yield e :> XObject |]) :> XNode
    let str text = XText(text: string)
    let comment text = XComment(text: string) :> XNode

    let save path (xml: XDocument) =
        xml.Save(path: string)
        
type Props = {
    Version: string option
    Feed: string option
    EnableScripts: bool
    SkipGitIgnore: bool
    Frameworks: string
}

module Props =
    open Xml

    let defaultFeed = "https://www.myget.org/F/paket-netcore-as-tool/api/v2"

    let property name value =
        match value with
        | Some version -> xe name [] [ str version ] 
        | None -> comment (xe name [] [ str "version" ] |> string)

        
    let view props =

        xe "Project" ["ToolVersion", "16.0"] [
            xe "PropertyGroup" [] [
                property "PaketBootstrapperVersion" props.Version
                property "PaketBootstrapperFeed" props.Feed

            ]
        ]
        |> xdoc

module Path =
    let paket = ".paket"
    let props = "paket.bootstrapper.props"
    let proj = "paket.bootstrapper.proj"
    let dependencies = "paket.dependencies"
    let gitignore = ".gitignore"

module File =
    let exists path =
        IO.File.Exists path

    let write path content =
        IO.File.WriteAllText(path, content, Text.Encoding.UTF8)

    let writeLines path (content: string seq) =
        IO.File.WriteAllLines(path, content, Text.Encoding.UTF8)

    let readLines path =
        IO.File.ReadAllLines(path) |> Array.toList

module Directory =
    let create path =
        if IO.Directory.Exists path then
            Log.logfn LogLevel.Verbose "Directory %s already exists" path
        else
            Log.logfn LogLevel.Verbose "Creating directory %s" path
            IO.Directory.CreateDirectory path |> ignore


module Resources =
    let load name =
        let stream = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("paket.install.targets." + name)
        use reader = new IO.StreamReader(stream)
        reader.ReadToEnd()

module DotNet =
    open System.Diagnostics

    let exec args =
        let info =
            ProcessStartInfo(
                "dotnet",
                String.concat " " args)
        let p = Diagnostics.Process.Start(info)
        p.WaitForExit()

    let tool name path =
        Log.logfn Verbose "exec dotnet tool install"
        exec ["tool"; "install"; name; "--tool-path"; path ]

module Paket =
    let exe = OS.withExeExt "paket"

    let isInstalled exePath =
        IO.File.Exists exePath


    let install path =
        let exePath = path </> exe

        if isInstalled exePath then
            Log.logfn Info "paket dotnet tool is already installed"
        else
            Log.logfn Info "Install paket dotnet tool"
            DotNet.tool "paket" Path.paket

    let dependencies scripts frameworks =
        [ yield "frameworks: " + frameworks
          yield "storage: none"
          if scripts then yield "generate_load_scripts: true"
          yield "source https://api.nuget.org/v3/index.json" ]


    let createDependencies path props =
        let deps = path </> ".." </> Path.dependencies 
        if File.exists deps then
            Log.logfn Warning "paket.dependencies file already exists"
        else
            Log.logfn Info "Creating paket.dependencies file"
            dependencies props.EnableScripts props.Frameworks
            |> File.writeLines deps



module GitIgnore =
    let load path =
        if File.exists path then
            Log.logfn Verbose ".gitignore file already exists. It will be modified if needed"
            File.readLines path
        else
            Log.logfn Verbose ".gitignore doesn't exist. It will be created"
            []

    let save path lines = 
        Log.logfn Verbose "Saving .gitignore file"
    
        File.writeLines path lines

    let contains glob lines =
        lines
        |> List.exists (fun (l: string) -> l.Trim() = glob)

    let add glob lines =
        if contains glob lines then
            lines
        else
            List.append lines [ glob ]

    let install path =
        Log.logfn Info "Adding paket files to .gitignore"

        let path = path </> ".." </> Path.gitignore
        load path
        |> add ".paket/*"
        |> add "!.paket/Paket.Restore.targets"
        |> add "!.paket/paket.bootstrapper.proj"
        |> add "!.paket/paket.bootstrapper.props"
        |> add "paket-files/"
        |> add "**/[Oo]bj/"
        |> add "**/[Bb]in/"
        |> save path


module Install =
    let writeResource directory name =
        Log.logfn Verbose "Copy %s file to %s" name directory
        Resources.load name
        |> File.write (directory </> name)

    let run path props =

        Directory.create path

        Log.logfn Info "Install build targets"
        writeResource path Path.proj
        Props.view props
        |> Xml.save (path </> Path.props)

        Paket.install path

        Paket.createDependencies path props

        if not props.SkipGitIgnore then
            GitIgnore.install path 



type Arguments =
| Version of string
| Feed of string
| [<AltCommandLine("-fw")>] Frameworks of string
| [<AltCommandLine("-p")>] Paket_Path of string
| [<AltCommandLine("-es")>] Enable_Scripts
| Skip_GitIgnore
| [<AltCommandLine("-s")>]Silent
| [<AltCommandLine("-v")>]Verbose
with 
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Silent -> "silent mode"
            | Verbose -> "verbose output"
            | Version _ -> "specify paket version to install"
            | Feed _ -> "specify nuget feed used to download paket bootstrapper"
            | Frameworks _ -> "specify target .NET framework(s)"
            | Enable_Scripts -> "enable script generation in paket.dependencies"
            | Skip_GitIgnore -> "skip .gitignore generation/modification"
            | Paket_Path _ -> "the paket directory path"

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = OS.withExeExt "paket.install")

    try
        try
            let result = parser.ParseCommandLine(argv, raiseOnUsage = true)

            let path = result.GetResult(Paket_Path, Path.paket)

            Log.outLevel <-
                if result.Contains Silent then
                     LogLevel.Silent
                elif result.Contains Verbose then
                    LogLevel.Verbose
                else
                    LogLevel.Info

            let props =
                { Version = result.TryGetResult(Version)
                  Feed = result.GetResult(Feed, Props.defaultFeed ) |> Some
                  EnableScripts = result.Contains Enable_Scripts
                  SkipGitIgnore = result.Contains Skip_GitIgnore
                  Frameworks = result.GetResult(Frameworks, "netstandard2.0, netcoreapp2.2")
                }
            Install.run path props

        finally
            Log.reset()
    with
    | ex -> printfn "%s" ex.Message 



    0 // return an integer exit code
