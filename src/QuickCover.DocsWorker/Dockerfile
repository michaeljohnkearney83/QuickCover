# Build from the repo root:  docker build -f src/QuickCover.DocsWorker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/QuickCover.Domain/QuickCover.Domain.csproj src/QuickCover.Domain/
COPY src/QuickCover.Contracts/QuickCover.Contracts.csproj src/QuickCover.Contracts/
COPY src/QuickCover.Application/QuickCover.Application.csproj src/QuickCover.Application/
COPY src/QuickCover.Infrastructure/QuickCover.Infrastructure.csproj src/QuickCover.Infrastructure/
COPY src/QuickCover.DocsWorker/QuickCover.DocsWorker.csproj src/QuickCover.DocsWorker/
RUN dotnet restore src/QuickCover.DocsWorker/QuickCover.DocsWorker.csproj

COPY src/ src/
RUN dotnet publish src/QuickCover.DocsWorker/QuickCover.DocsWorker.csproj -c Release -o /app/publish --no-restore

# Debian-based runtime image: QuestPDF ships its native Skia library for linux-x64/arm64 glibc.
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "QuickCover.DocsWorker.dll"]
