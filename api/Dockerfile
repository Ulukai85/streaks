# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY Api/Api.csproj Api/
RUN dotnet restore Api/Api.csproj
COPY Api/ Api/
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS runtime
WORKDIR /app
# /keys backs AddDataProtection().PersistKeysToFileSystem() (see
# Api/Infrastructure/DataProtectionServiceCollectionExtensions.cs) - created and owned by the
# built-in non-root $APP_UID user before switching to it, since the named volume mounted here
# needs to be writable by whoever the process actually runs as.
RUN mkdir -p /keys && chown $APP_UID /keys
COPY --from=build --chown=$APP_UID /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
