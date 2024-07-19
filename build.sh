echo ".NET version installed:"
dotnet --version

DOTNET_VERSION=$(dotnet --version | cut -d. -f1)
if [ "$DOTNET_VERSION" -lt 7 ]; then
	echo "FF .NET SDK requires .NET 7 or later to build. Aborting"
	exit 1
fi

set -x

dotnet pack ff-netF48-server-sdk.csproj

# Install Tools needed for build
dotnet tool install --global coverlet.console --version 3.2.0
dotnet tool install --global dotnet-reportgenerator-globaltool
dotnet tool restore

# Install Libraries needed for build and build
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
