FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /app

# Copy everything
COPY . ./
# Restore as distinct layers

RUN dotnet restore

RUN dotnet build .
ENTRYPOINT ["dotnet", "test", "--no-build", "--collect:\"XPlat Code Coverage\"", "--settings", "./tests/coverlet.runsettings", "--results-directory", "./results"]
