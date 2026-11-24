# Build from the repo root:  docker build -f src/QuickCover.PolicyWorker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/QuickCover.Domain/QuickCover.Domain.csproj src/QuickCover.Domain/
COPY src/QuickCover.Contracts/QuickCover.Contracts.csproj src/QuickCover.Contracts/
COPY src/QuickCover.Application/QuickCover.Application.csproj src/QuickCover.Application/
COPY src/QuickCover.Infrastructure/QuickCover.Infrastructure.csproj src/QuickCover.Infrastructure/
COPY src/QuickCover.PolicyWorker/QuickCover.PolicyWorker.csproj src/QuickCover.PolicyWorker/
RUN dotnet restore src/QuickCover.PolicyWorker/QuickCover.PolicyWorker.csproj

COPY src/ src/
RUN dotnet publish src/QuickCover.PolicyWorker/QuickCover.PolicyWorker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "QuickCover.PolicyWorker.dll"]
