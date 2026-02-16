# =============================================================================
# Anduril AI Assistant — Multi-stage Docker build
# =============================================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Directory.Build.props .
COPY Anduril.slnx .
COPY src/Anduril.Core/Anduril.Core.csproj src/Anduril.Core/
COPY src/Anduril.AI/Anduril.AI.csproj src/Anduril.AI/
COPY src/Anduril.Skills/Anduril.Skills.csproj src/Anduril.Skills/
COPY src/Anduril.Integrations/Anduril.Integrations.csproj src/Anduril.Integrations/
COPY src/Anduril.Communication/Anduril.Communication.csproj src/Anduril.Communication/
COPY src/Anduril.Host/Anduril.Host.csproj src/Anduril.Host/

RUN dotnet restore Anduril.slnx

# Copy everything else and publish
COPY . .
RUN dotnet publish src/Anduril.Host/Anduril.Host.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r anduril && useradd -r -g anduril -s /bin/false anduril

COPY --from=build /app/publish .
COPY skills/ ./skills/

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

USER anduril
ENTRYPOINT ["dotnet", "Anduril.Host.dll"]

