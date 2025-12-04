FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Update Alpine packages to get latest security fixes for BusyBox
RUN apk update && apk upgrade --no-cache && \
    apk add --no-cache icu-libs && \
    rm -rf /var/cache/apk/*

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/services/FileBlobEmulator/FileBlobEmulator.csproj", "src/services/FileBlobEmulator/"]
RUN dotnet restore "src/services/FileBlobEmulator/FileBlobEmulator.csproj"
COPY . .
WORKDIR "/src/src/services/FileBlobEmulator"
RUN dotnet build "FileBlobEmulator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FileBlobEmulator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create blob-data directory
RUN mkdir -p /app/blob-data

ENV ASPNETCORE_URLS=http://+:8080
ENV BLOB_ROOT=/app/blob-data
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "FileBlobEmulator.dll"]
