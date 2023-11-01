FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
ARG config=debug
ENV CONFIG=${config}
WORKDIR /app

# Copy everything
COPY . ./
# Restore as distinct layers

RUN dotnet restore

RUN dotnet build -c ${config} .
ENTRYPOINT ["./tests/entrypoint.sh"]
