FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Engine.Core/Engine.Core.csproj src/Engine.Core/
COPY src/Engine.Backend/Engine.Backend.csproj src/Engine.Backend/
RUN dotnet restore src/Engine.Backend/Engine.Backend.csproj

COPY src/Engine.Core/ src/Engine.Core/
COPY src/Engine.Backend/ src/Engine.Backend/
RUN dotnet publish src/Engine.Backend/Engine.Backend.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0-bookworm-slim
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./Engine.Backend"]
