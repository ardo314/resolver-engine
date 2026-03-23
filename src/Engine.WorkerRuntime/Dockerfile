# ---------- WorkerRuntime base image ----------
# Builds the generic worker host that discovers modules at startup.
#
# At runtime the host scans  <app>/modules/*.dll  for classes that derive
# from ComponentWorker<T> and registers them automatically.
#
# To ship your own worker, create a derived image:
#
#   FROM engine-worker-runtime:latest
#   COPY --from=build /out/ /app/modules/
#
# where /out/ contains the published output of your worker project.
# The modules/ directory will be created by the COPY if it doesn't exist.
# ---------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Engine.Core/Engine.Core.csproj src/Engine.Core/
COPY src/Engine.Client/Engine.Client.csproj src/Engine.Client/
COPY src/Engine.Worker/Engine.Worker.csproj src/Engine.Worker/
COPY src/Engine.WorkerRuntime/Engine.WorkerRuntime.csproj src/Engine.WorkerRuntime/
RUN dotnet restore src/Engine.WorkerRuntime/Engine.WorkerRuntime.csproj

COPY src/Engine.Core/ src/Engine.Core/
COPY src/Engine.Client/ src/Engine.Client/
COPY src/Engine.Worker/ src/Engine.Worker/
COPY src/Engine.WorkerRuntime/ src/Engine.WorkerRuntime/
RUN dotnet publish src/Engine.WorkerRuntime/Engine.WorkerRuntime.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0-bookworm-slim
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./Engine.WorkerRuntime"]
