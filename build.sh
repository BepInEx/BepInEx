#!/bin/bash

# Simple build script for BepInEx NuGet packages
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
CONFIGURATION="Release"
VERSION_SUFFIX=""
SKIP_TESTS=false
SKIP_PACK=false
OUTPUT_DIR="./artifacts"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --configuration|-c)
            CONFIGURATION="$2"
            shift 2
            ;;
        --version-suffix|-s)
            VERSION_SUFFIX="$2"
            shift 2
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        --skip-pack)
            SKIP_PACK=true
            shift
            ;;
        --output|-o)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --help|-h)
            echo "Usage: ./build.sh [options]"
            echo "Options:"
            echo "  -c, --configuration <CONFIG>  Build configuration (Debug/Release) [default: Release]"
            echo "  -s, --version-suffix <SUFFIX> Version suffix for pre-release versions"
            echo "  --skip-tests                  Skip running tests"
            echo "  --skip-pack                   Skip creating NuGet packages"
            echo "  -o, --output <DIR>            Output directory for NuGet packages [default: ./artifacts]"
            echo "  -h, --help                    Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

echo -e "${GREEN}Building BepInEx with configuration: $CONFIGURATION${NC}"

# Clean previous artifacts
if [ -d "$OUTPUT_DIR" ]; then
    echo -e "${YELLOW}Cleaning previous artifacts...${NC}"
    rm -rf "$OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"

# Restore dependencies
echo -e "${GREEN}Restoring dependencies...${NC}"
dotnet restore

# Build
echo -e "${GREEN}Building solution...${NC}"
if [ -n "$VERSION_SUFFIX" ]; then
    dotnet build --configuration "$CONFIGURATION" --no-restore -p:VersionSuffix="$VERSION_SUFFIX"
else
    dotnet build --configuration "$CONFIGURATION" --no-restore
fi

# Run tests if not skipped
if [ "$SKIP_TESTS" = false ]; then
    echo -e "${GREEN}Running tests...${NC}"
    if [ -d "tests" ] || [ -d "Tests" ]; then
        dotnet test --configuration "$CONFIGURATION" --no-build --verbosity normal
    else
        echo -e "${YELLOW}No tests directory found, skipping tests${NC}"
    fi
fi

# Pack NuGet packages if not skipped
if [ "$SKIP_PACK" = false ]; then
    echo -e "${GREEN}Creating NuGet packages...${NC}"
    if [ -n "$VERSION_SUFFIX" ]; then
        dotnet pack --configuration "$CONFIGURATION" --no-build -p:VersionSuffix="$VERSION_SUFFIX" --output "$OUTPUT_DIR"
    else
        dotnet pack --configuration "$CONFIGURATION" --no-build --output "$OUTPUT_DIR"
    fi
    
    echo -e "${GREEN}NuGet packages created in $OUTPUT_DIR:${NC}"
    ls -la "$OUTPUT_DIR"/*.nupkg
fi

echo -e "${GREEN}Build completed successfully!${NC}"