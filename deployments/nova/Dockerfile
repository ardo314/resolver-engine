FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY deployments/nova/Deployments.Nova.csproj deployments/nova/
RUN dotnet restore deployments/nova/Deployments.Nova.csproj

COPY deployments/nova/ deployments/nova/
RUN dotnet publish deployments/nova/Deployments.Nova.csproj -c Release -o /app

# --- production image ---
FROM mcr.microsoft.com/dotnet/runtime:9.0

WORKDIR /app
COPY --from=build /app .

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

ENTRYPOINT ["dotnet", "Deployments.Nova.dll"]
