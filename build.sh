dotnet pack

# Install Tools needed for build
dotnet tool install --global coverlet.console --version 3.2.0
dotnet tool install --global dotnet-reportgenerator-globaltool
dotnet tool restore

# Install Libraries needed for build and buld
dotnet add package JUnitTestLogger --version 1.1.0 
dotnet restore
dotnet build --no-restore


# Run tests
echo "Generating Test Report"
export MSBUILDDISABLENODEREUSE=1
dotnet test -v=n --blame-hang --logger:"junit;LogFilePath=junit.xml" -nodereuse:false
ls -l
echo "Done"
