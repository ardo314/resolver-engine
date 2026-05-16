FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy csproj files and restore
COPY engine/Engine.csproj engine/
COPY clients/csharp/Client.csproj clients/csharp/
COPY modules/core/Modules.Core.csproj modules/core/
COPY modules/nova/Modules.Nova.csproj modules/nova/
COPY providers/nova/Providers.Nova.csproj providers/nova/
RUN dotnet restore providers/nova/Providers.Nova.csproj

# Copy source and publish
COPY engine/ engine/
COPY clients/csharp/ clients/csharp/
COPY modules/core/ modules/core/
COPY modules/nova/ modules/nova/
COPY providers/nova/ providers/nova/
RUN dotnet publish providers/nova/Providers.Nova.csproj -c Release -o /app

# --- production image ---
FROM mcr.microsoft.com/dotnet/runtime:9.0

WORKDIR /app
COPY --from=build /app .

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["dotnet", "Providers.Nova.dll"]
