# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN dotnet new nugetconfig
COPY --from=nugets / /nugets/
RUN dotnet nuget add source /nugets --name local
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet restore src/PigeonPost/PigeonPost.csproj
RUN dotnet publish src/PigeonPost/PigeonPost.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update && apt-get install -y --no-install-recommends \
    iproute2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app /app
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh
ENTRYPOINT ["/docker-entrypoint.sh"]
