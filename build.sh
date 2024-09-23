#!/bin/bash

# Check .NET version
echo ".NET version installed:"
dotnet --version

# Initialize git submodules
git submodule update --init --recursive

# Get the major version of the installed .NET SDK
DOTNET_VERSION=$(dotnet --version | cut -d. -f1)

# Check if .NET 8.0 is installed, if not, install it
if [ "$DOTNET_VERSION" -lt 8 ]; then
    echo "FF .NET SDK requires .NET 8.0 or later to build. Attempting to install .NET 8.0..."

    # Detect the platform
    OS=$(uname)
    if [ "$OS" == "Linux" ]; then
        # For Ubuntu/Debian-based systems
        apt-get update
        apt-get install -y dotnet-sdk-8.0
    elif [ "$OS" == "Darwin" ]; then
        # For macOS
        brew install --cask dotnet-sdk
    elif [[ "$OS" =~ MINGW|MSYS|CYGWIN ]]; then
        # For Windows (Git Bash/Cygwin/WSL)
        echo "Please manually download and install the .NET 8.0 SDK from https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 1
    else
        echo "Unsupported OS. Please install .NET 8.0 manually."
        exit 1
    fi

    # Verify installation
    dotnet --version
    DOTNET_VERSION=$(dotnet --version | cut -d. -f1)

    if [ "$DOTNET_VERSION" -lt 8 ]; then
        echo ".NET 8.0 installation failed. Aborting."
        exit 1
    else
        echo ".NET 8.0 installed successfully."
    fi
else
    echo ".NET 8.0 or later is already installed."
fi

set -x

# Build the FF .NET SDK
dotnet pack ff-netF48-server-sdk.csproj

# Install Tools needed for the build
dotnet tool install --global coverlet.console --version 3.2.0
dotnet tool install --global dotnet-reportgenerator-globaltool
dotnet tool restore

# Restore and build libraries
dotnet restore ff-netF48-server-sdk.csproj
dotnet build ff-netF48-server-sdk.csproj --no-restore

# Run tests
echo "Generating Test Report"
export MSBUILDDISABLENODEREUSE=1
dotnet test tests/ff-server-sdk-test/ff-server-sdk-test.csproj -v=n --blame-hang --logger:"junit;LogFilePath=junit.xml" -nodereuse:false

# Capture exit code of the test command
TEST_EXIT_CODE=$?
if [ "$TEST_EXIT_CODE" -ne 0 ]; then
  echo "Tests failed. Aborting build."
  exit $TEST_EXIT_CODE
fi

ls -l
echo "Done"
