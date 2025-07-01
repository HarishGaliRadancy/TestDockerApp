# ---------------------------------------------
# STAGE 1: Build
# ---------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Copy csproj and restore
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# ---------------------------------------------
# STAGE 2: Runtime
# ---------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:7.0 AS runtime
WORKDIR /app

COPY --from=build /out ./

# Logs will be written to stdout/stderr via Console
ENTRYPOINT ["dotnet", "TestDockerApp.dll"]
