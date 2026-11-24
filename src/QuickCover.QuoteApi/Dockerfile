# Build from the repo root:  docker build -f src/QuickCover.QuoteApi/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, using only project files, so this layer is cached until dependencies change.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/QuickCover.Domain/QuickCover.Domain.csproj src/QuickCover.Domain/
COPY src/QuickCover.Contracts/QuickCover.Contracts.csproj src/QuickCover.Contracts/
COPY src/QuickCover.Application/QuickCover.Application.csproj src/QuickCover.Application/
COPY src/QuickCover.Infrastructure/QuickCover.Infrastructure.csproj src/QuickCover.Infrastructure/
COPY src/QuickCover.QuoteApi/QuickCover.QuoteApi.csproj src/QuickCover.QuoteApi/
RUN dotnet restore src/QuickCover.QuoteApi/QuickCover.QuoteApi.csproj

COPY src/ src/
RUN dotnet publish src/QuickCover.QuoteApi/QuickCover.QuoteApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "QuickCover.QuoteApi.dll"]
