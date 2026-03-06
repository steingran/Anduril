# =============================================================================
# Anduril AI Assistant — Multi-stage Docker build
# =============================================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/Anduril.Core/Anduril.Core.csproj src/Anduril.Core/
COPY src/Anduril.AI/Anduril.AI.csproj src/Anduril.AI/
COPY src/Anduril.Skills/Anduril.Skills.csproj src/Anduril.Skills/
COPY src/Anduril.Integrations/Anduril.Integrations.csproj src/Anduril.Integrations/
COPY src/Anduril.Communication/Anduril.Communication.csproj src/Anduril.Communication/
COPY src/Anduril.Host/Anduril.Host.csproj src/Anduril.Host/
COPY src/Anduril.Setup/Anduril.Setup.csproj src/Anduril.Setup/

RUN dotnet restore src/Anduril.Host/Anduril.Host.csproj
RUN dotnet restore src/Anduril.Setup/Anduril.Setup.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish src/Anduril.Host/Anduril.Host.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --self-contained false
RUN dotnet publish src/Anduril.Setup/Anduril.Setup.csproj \
    -c Release \
    -o /app/setup \
    --no-restore \
    --self-contained false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r anduril && useradd -r -g anduril -s /bin/false anduril

COPY --from=build /app/publish .
COPY --from=build /app/setup/ ./
COPY skills/ ./skills/

# Ensure runtime-writable data directories exist for the non-root user
RUN mkdir -p /app/sessions \
    && chown -R anduril:anduril /app/sessions

# Set environment
# Keep container defaults quiet; opt components back in with explicit env overrides.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    AI__OpenAI__Enabled=false \
    AI__Anthropic__Enabled=false \
    AI__Augment__Enabled=false \
    AI__AugmentChat__Enabled=false \
    AI__Ollama__Enabled=false \
    AI__LLamaSharp__Enabled=false \
    Communication__Cli__Enabled=false \
    Communication__Slack__Enabled=false \
    Communication__Teams__Enabled=false \
    Communication__Signal__Enabled=false

EXPOSE 8080

USER anduril
ENTRYPOINT ["dotnet", "Anduril.Host.dll"]

