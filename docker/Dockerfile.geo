# API Geo (deploy autônomo). Banco isolado read-mostly com PostGIS (uniplus_geo,
# ADR-0090/0091). Não entra na imagem do monólito (Dockerfile.host).

# ---- Restore + Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
WORKDIR /src

# Configuração da solution. .editorconfig é necessário porque políticas por glob
# vivem nele — sem ele o build no container reverte às severidades default.
COPY Directory.Build.props Directory.Packages.props global.json UniPlus.slnx .editorconfig ./

# Apenas as árvores que a API Geo referencia (shared + geo).
COPY src/shared/ src/shared/
COPY src/geo/ src/geo/

RUN dotnet restore src/geo/Unifesspa.UniPlus.Geo.API/Unifesspa.UniPlus.Geo.API.csproj

RUN dotnet publish src/geo/Unifesspa.UniPlus.Geo.API/Unifesspa.UniPlus.Geo.API.csproj \
    -c Release \
    -o /app/publish

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl para health checks + usuário não-root.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r appuser && useradd -r -g appuser -s /sbin/nologin appuser

COPY --from=build /app/publish .

USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "Unifesspa.UniPlus.Geo.API.dll"]
