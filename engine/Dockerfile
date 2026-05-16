FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy csproj files and restore
COPY engine/Engine.csproj engine/
RUN dotnet restore engine/Engine.csproj

# Copy source and publish
COPY engine/ engine/
RUN dotnet publish engine/Engine.csproj -c Release -o /app

# --- production image ---
FROM mcr.microsoft.com/dotnet/runtime:9.0

WORKDIR /app
COPY --from=build /app .

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["dotnet", "Engine.dll"]
