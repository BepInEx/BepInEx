# Building BepInEx

You can build BepInEx two ways: by using `dotnet` or by using the automated build project.

## CakeBuild script

You can use the included `build` projected based on [cakebuild](https://cakebuild.net/) that allows you to automatically get dependencies, build and package everything.

**CakeBuild requires [.NET 6.0](https://dotnet.microsoft.com/download) or newer to be installed**

### Windows (Command Line)

Clone this repository via `git clone https://github.com/BepInEx/BepInEx.git`.  
After that, run in the repository directory

```bat
build.cmd --target Compile
```

### Windows (PowerShell)

Clone this repository via `git clone https://github.com/BepInEx/BepInEx.git`.  
After that, run in the repository directory

```ps
./build.ps1 --target Compile
```

Make sure you have the execution policy set to enable running scripts.

### Linux (Bash)

Clone this repository via `git clone https://github.com/BepInEx/BepInEx.git`.  
After that, run in the repository directory

```sh
./build.sh --target Compile
```

### Additional build targets

The build script provides the following build targets (that you can pass via the `target` parameter)

| Target     | Description                                                                                        |
|------------|----------------------------------------------------------------------------------------------------|
| `Compile`  | Pulls dependencies and builds BepInEx binaries                                                     |
| `MakeDist` | Runs `Compile` and creates distributable package for each distribution target to `bin/dist` folder |
| `Publish`  | Runs `MakeDist` and zips everything into archives into `bin/dist` folder                           |
