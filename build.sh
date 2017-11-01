#!/bin/bash
set -E
trap '[ "$?" = 0 ] || exit $?' ERR

if [[ "$TRAVIS_OS_NAME" == "osx" ]]; then
  # This is due to: https://github.com/NuGet/Home/issues/2163#issue-135917905
  echo "current ulimit is: `ulimit -n`..."
  ulimit -n 1024
  echo "new limit: `ulimit -n`"
fi

dotnet restore
dotnet build -c Release

for f in test/**/*.Tests.csproj; do
    dotnet test $f -c Release
done

for f in src/**/*.csproj; do
    dotnet pack $f --no-build -c Release
done
