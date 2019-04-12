# Overview

paket.install is a dotnet cli tool to ease [paket](https://fsprojects.github.io/Paket/) installation for netcore projects.

# Installation

Install it as a global tool using the following command:

```
dotnet tool install paket.install -g
```

you can also install it locally to a specific directory:

```
dotnet tool install paket.install --tool-path ./tools
```


# Usage

```
USAGE: paket.install.exe [--help] [--version <string>] [--feed <string>] [--paket-path <string>] [--enalbe-scripts]
                         [--skip-gitignore] [--silent] [--verbose]

OPTIONS:

    --version, -v <string>
                          specify paket version to install
    --feed, -f <string>   specify nuget feed used to download paket bootstrapper
    --paket-path, -p <string>
                          the paket directory path
    --enalbe-scripts, -s  enable script generation in paket.dependencies
    --skip-gitignore      skip .gitignore generation/modification
    --silent              silent mode
    --verbose             verbose output
    --help                display this list of options.
```

The tool installs .paket/paket.bootstrapper.proj and .paket/paket.bootstrapper.props files 
that manage paket bootstrapping for dotnet restore. Once setup, no need to add .paket/paket.exe 
to git repository, since it will be loaded and installed if missing. Use --version to fix the 
paket.bootstrapper version to use, and --feed to use a different nuget feed. The default is pointing 
to [Enrico Sada](https://github.com/enricosada) paket-netcore-as-tool myget feed.

The tool then creates an empty paket.dependencies files for dotnet core, restricting framework, disabling
storage. It is possible to request load script generation using the --enable-scripts flag.

The tool also adds new entries in the .gitignore file to a void committing paket files accidentally. This
step can be skipped using the --skip-gitignore flag.

# Credits

This tool is based on [Enrico Sada](https://github.com/enricosada) [paket-netcore-testing-as-tool](https://github.com/enricosada/paket-netcore-testing-as-tool) work. 

Thank you to [Steffen Forkmann](https://github.com/forki/) for make [paket](https://fsprojects.github.io/Paket/) awesome!
