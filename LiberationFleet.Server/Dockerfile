# Build the LiberationFleet API + Angular SPA image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Angular build — official Node image (distro nodejs packages often omit npm).
FROM node:20-bookworm AS client-build
WORKDIR /src/liberationfleet.client
COPY liberationfleet.client/package.json liberationfleet.client/package-lock.json ./
RUN npm ci
COPY liberationfleet.client/ ./
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["LiberationFleet.Server/LiberationFleet.Server.csproj", "LiberationFleet.Server/"]
COPY ["liberationfleet.client/liberationfleet.client.esproj", "liberationfleet.client/"]
RUN dotnet restore "./LiberationFleet.Server/LiberationFleet.Server.csproj" \
    /p:SkipSpaProjectReference=true
COPY LiberationFleet.Server/ ./LiberationFleet.Server/
WORKDIR /src/LiberationFleet.Server
RUN dotnet publish "./LiberationFleet.Server.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:SkipSpaProjectReference=true

RUN mkdir -p /app/publish/wwwroot
COPY --from=client-build /src/liberationfleet.client/dist/liberationfleet.client/browser/. /app/publish/wwwroot/

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/ || exit 1
ENTRYPOINT ["dotnet", "LiberationFleet.Server.dll"]
