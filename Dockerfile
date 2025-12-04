FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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

ENTRYPOINT ["dotnet", "FileBlobEmulator.dll"]
