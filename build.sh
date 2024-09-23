#!/bin/bash

# Check .NET version installed
echo ".NET version installed:"
dotnet --version

# Initialize git submodules
git submodule update --init --recursive

# Get the major version of the installed .NET SDK
DOTNET_VERSION=$(dotnet --version | cut -d. -f1)

# Check if .NET 8.0 is installed, if not, install it locally
if [ "$DOTNET_VERSION" -lt 8 ]; then
    echo "FF .NET SDK requires .NET 8.0 or later to build. Attempting to install .NET 8.0 locally..."

    # Download the official .NET install script
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh

    # Install .NET 8.0 SDK locally for the user
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --channel 8.0

    # Set DOTNET_ROOT and update PATH
    export DOTNET_ROOT=$HOME/.dotnet
    export PATH=$HOME/.dotnet:$PATH

    # Verify the installation
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

# Continue with build process
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
