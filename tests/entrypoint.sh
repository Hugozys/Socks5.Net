#!/bin/sh

if [ "$CONFIG" = "debug" ]; then
   dotnet test --no-build --collect:"XPlat Code Coverage" --settings ./tests/coverlet.runsettings --results-directory ./results
else
   dotnet test --no-build -c "$CONFIG" .
fi
