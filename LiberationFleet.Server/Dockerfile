# Build the LiberationFleet API + Angular SPA image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS with-node
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

FROM with-node AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY liberationfleet.client/package.json liberationfleet.client/package-lock.json liberationfleet.client/
WORKDIR /src/liberationfleet.client
RUN npm ci
COPY liberationfleet.client/ ./
RUN npm run build -- --configuration production

WORKDIR /src
COPY ["LiberationFleet.Server/LiberationFleet.Server.csproj", "LiberationFleet.Server/"]
COPY ["liberationfleet.client/liberationfleet.client.esproj", "liberationfleet.client/"]
RUN dotnet restore "./LiberationFleet.Server/LiberationFleet.Server.csproj"
COPY . .
WORKDIR /src/LiberationFleet.Server
RUN dotnet publish "./LiberationFleet.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
RUN mkdir -p /app/publish/wwwroot \
    && cp -r /src/liberationfleet.client/dist/liberationfleet.client/browser/. /app/publish/wwwroot/

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/ || exit 1
ENTRYPOINT ["dotnet", "LiberationFleet.Server.dll"]
