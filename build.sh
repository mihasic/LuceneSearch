#!/bin/bash
if [[ "$TRAVIS_OS_NAME" == "osx" ]]; then
  # This is due to: https://github.com/NuGet/Home/issues/2163#issue-135917905
  echo "current ulimit is: `ulimit -n`..."
  ulimit -n 1024
  echo "new limit: `ulimit -n`"
fi

for f in src/**/*.csproj; do
    $(cd `dirname $f`; dotnet restore) || exit $?
done
for f in src/**/*.csproj; do
    $(cd `dirname $f`; dotnet build) || exit $?
done

for f in test/**/*.csproj; do
    $(cd `dirname $f`; dotnet restore && dotnet build;
     if [[ `basename $f` =~ Tests ]]; then
        dotnet test;
     fi ) || exit $?
done

for f in src/**/*.csproj; do
    $(cd `dirname $f`; dotnet pack) || exit $?
done
