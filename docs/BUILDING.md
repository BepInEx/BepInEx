# Building BepInEx

This fork of BepInEx uses a simplified build system focused on creating NuGet packages.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download) or newer
- Git (for version calculation in CI/CD)

## Building with the Build Scripts

### Windows (PowerShell)

```powershell
# Build and create NuGet packages
./build.ps1

# Build with custom configuration
./build.ps1 -Configuration Debug

# Build with version suffix
./build.ps1 -VersionSuffix preview.123

# See all options
./build.ps1 -Help
# Also supports: ./build.ps1 --help or ./build.ps1 -h
```

### Linux/macOS/WSL (Bash)

```bash
# Build and create NuGet packages
./build.sh

# Build with custom configuration
./build.sh --configuration Debug

# Build with version suffix
./build.sh --version-suffix preview.123

# See all options
./build.sh --help
# Also supports: ./build.sh -h
```

## Building with dotnet CLI

You can also build directly using the dotnet CLI:

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release

# Create NuGet packages
dotnet pack --configuration Release --output ./artifacts

# Build with version suffix
dotnet pack --configuration Release -p:VersionSuffix=preview.123 --output ./artifacts
```

## Build Options

Both build scripts support the following options:

| Option | Description | Default |
|--------|-------------|---------|
| Configuration | Build configuration (Debug/Release) | Release |
| VersionSuffix | Version suffix for pre-release packages | (none) |
| SkipTests | Skip running tests | false |
| SkipPack | Skip creating NuGet packages | false |
| OutputDir | Output directory for packages | ./artifacts |

## GitHub Actions Workflow

The GitHub Actions workflow (`.github/workflows/nuget-publish.yml`) automatically:

1. **Builds** the projects on every push and pull request
2. **Creates** NuGet packages with appropriate versioning:
   - Release versions for tags (e.g., `v6.0.0`)
   - Preview versions for master branch (e.g., `6.0.0-preview.123`)
   - Dev versions for other branches (e.g., `6.0.0-dev.feature-xyz.45`)
3. **Publishes** packages to GitHub Packages registry (for master branch and tags only)

## Package Versioning

Version is determined by:
- Base version from `Directory.Build.props` (VersionPrefix)
- Suffix based on branch/tag:
  - Tags (`v*`): No suffix (release version)
  - Master branch: `preview.{build_number}`
  - Other branches: `dev.{branch_name}.{build_number}`

## Publishing to GitHub Packages

Packages are automatically published by GitHub Actions when:
- Pushing to master branch
- Creating a release tag (v*)

The packages are published to:
```
https://nuget.pkg.github.com/{YourOrg}/index.json
```

## NuGet Package Output

The build creates the following NuGet packages:
- `BepInEx.Core` - Core BepInEx library
- `BepInEx.Preloader.Core` - Preloader functionality
- `BepInEx.NET.Common` - Common .NET components
- `BepInEx.NET.CoreCLR` - .NET Core runtime support

Packages are output to the `./artifacts` directory (or custom location if specified).

## Consuming the Packages

To use these packages in other projects, you have several options:

### Option 1: Project File Configuration (Recommended)
Add the package source directly in your `.csproj` file:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RestoreAdditionalProjectSources>
      https://nuget.pkg.github.com/{YourOrg}/index.json
    </RestoreAdditionalProjectSources>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="BepInEx.Core" Version="6.0.0-preview.1" />
    <PackageReference Include="BepInEx.Preloader.Core" Version="6.0.0-preview.1" />
  </ItemGroup>
</Project>
```

### Option 2: CLI Arguments
Use dotnet CLI with source arguments:
```bash
# Restore with additional source
dotnet restore --source https://api.nuget.org/v3/index.json --source https://nuget.pkg.github.com/{YourOrg}/index.json

# Add package with source
dotnet add package BepInEx.Core --source https://nuget.pkg.github.com/{YourOrg}/index.json
```

### Option 3: NuGet.config (For team-wide configuration)
Create a `NuGet.config` in your repository root:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/{YourOrg}/index.json" />
  </packageSources>
</configuration>
```

**Note**: If the packages are public, no authentication is needed. For private packages, set the `NUGET_AUTH_TOKEN` environment variable with your GitHub PAT.

## Migration from Cake Build

This fork simplifies the build significantly:
- **Removed Unity dependencies**: No Doorstop, Dobby, or bundled .NET runtime needed
- **Custom hooking**: Uses a different hooking method specific to this fork
- **Pure NuGet approach**: Focus on library distribution via NuGet packages

The new build system:
- ✅ Builds core BepInEx libraries
- ✅ Creates NuGet packages with proper versioning
- ✅ Publishes to GitHub Packages registry
- ✅ Removes complexity of external dependency management
- ✅ Uses standard dotnet CLI commands (no Cake required)