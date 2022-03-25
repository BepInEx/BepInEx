# Building BepInEx

You can build BepInEx two ways: by using MSBuild-compatible IDE or the CakeBuild script.

## CakeBuild script

You can use the included [cakebuild](https://cakebuild.net/) script that allows you to automatically get dependencies, build and package everything.

**CakeBuild requires [.NET 6.0](https://dotnet.microsoft.com/download) or newer to be installed**

### Windows (Command Line)

Clone this repository via `git clone https://github.com/BepInEx/BepInEx.git`.  
After that, run in the repository directory

```bat
build.bat --task=Build
```

### Windows (PowerShell)

Clone this repository via `git clone https://github.com/BepInEx/BepInEx.git`.  
After that, run in the repository directory

```ps
./build.ps1 --task=Build
```

Make sure you have the execution policy set to enable running scripts.

### Linux (Bash)

Clone this repository via `git clone https://github.com/BepInEx/BepInEx.git`.  
After that, run in the repository directory

```sh
./build.sh --task=Build
```

### Additional build tasks

The build script provides the following build tasks (that you can pass via the `task` parameter)

| Task          | Description                                                              |
| ------------- | ------------------------------------------------------------------------ |
| `Build`       | Pulls dependencies and builds BepInEx                                    |
| `MakeDist`    | Runs `Build` and creates distributable packages into `bin/dist` folder   |
| `Pack`        | Runs `MakeDist` and zips everything into archives into `bin/dist` folder |

## MSBuild

Download and IDE (for example Visual Studio), open the solution file and build it.
