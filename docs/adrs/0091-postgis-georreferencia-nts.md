---
status: "accepted"
date: "2026-06-17"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0091: PostGIS e NetTopologySuite como mecanismo de georreferência

## Contexto e enunciado do problema

O módulo `Geo` ([ADR-0090](0090-modulo-geo-localidades.md)) evoluirá para guardar coordenadas geográficas (campi, locais de prova, pontos de referência) e, eventualmente, consultas espaciais (proximidade, contenção). Coordenadas precisam de um tipo de dado que represente latitude/longitude com sistema de referência espacial (SRID) e de índices que tornem buscas espaciais viáveis.

O PostgreSQL não tem tipos geoespaciais nativos; a extensão **PostGIS** os fornece (`geometry`/`geography`) junto com índices GIST. No lado .NET, o provider Npgsql do EF Core integra o **NetTopologySuite (NTS)** — mapeando tipos NTS (`Point`, `Polygon`, …) para colunas PostGIS. O PostGIS é uma **estreia** no projeto: nenhum módulo o usava até aqui.

A decisão precisa fixar (a) o tipo de coluna canônico para coordenadas, (b) como o provider é configurado para mapear NTS, e (c) como o wiring é provado antes de o domínio depender dele.

## Drivers da decisão

- **Correção geográfica** — distâncias e proximidade sobre a superfície terrestre, não sobre um plano.
- **Padrão de mercado** — PostGIS + NTS é a combinação madura e idiomática para PostgreSQL + EF Core.
- **Índice espacial** — buscas espaciais exigem índice GIST sobre a coluna geográfica.
- **Paridade runtime↔design-time** — `dotnet ef migrations` precisa enxergar o mapeamento NTS para gerar o SQL correto.
- **Não-regressão** — habilitar NTS no `Geo` sem alterar o comportamento dos demais módulos.

## Opções consideradas

- **A**: `geometry(Point, 4326)` — geometria planar com SRID 4326.
- **B**: **`geography(Point, 4326)`** — geografia sobre o elipsoide WGS84.
- **C**: Guardar latitude/longitude como dois `double precision` sem PostGIS.

## Resultado da decisão

**Escolhida:** "B — `geography(Point, 4326)` via PostGIS + NetTopologySuite", porque cálculos sobre coordenadas geográficas (distância, proximidade) ficam corretos por construção no tipo `geography`, e o mapeamento NTS é idiomático no Npgsql/EF Core.

- Coordenadas são `geography(Point, 4326)` (WGS84). Índice **GIST** sobre a coluna geográfica.
- O provider Npgsql é configurado com `o.UseNetTopologySuite()`; tipos `NetTopologySuite.Geometries.Point` mapeiam para a coluna geográfica.
- O hook que habilita o NTS é **opcional e não-invasivo** no helper compartilhado `UseUniPlusNpgsqlConventions` (e no `BuildDesignTimeOptions`): um parâmetro `Action<NpgsqlDbContextOptionsBuilder>?` com default `null`. Os módulos existentes não passam o hook e seguem inalterados; o `Geo` passa `o => o.UseNetTopologySuite()` **tanto no runtime quanto no design-time** (paridade), para que `dotnet ef migrations` gere `geography(Point, 4326)` corretamente.
- A extensão `postgis` é pré-requisito do tipo `geography`. Ela é criada pela migration (`CREATE EXTENSION IF NOT EXISTS postgis`, idempotente). Como `postgis` **não** é uma extensão *trusted*, sua criação exige superusuário: no provisionamento via Docker, o script de init (rodado como superusuário) cria a extensão e o `IF NOT EXISTS` da migration vira um no-op seguro para o usuário de aplicação não-superusuário (o `IF NOT EXISTS` retorna antes da verificação de privilégio quando a extensão já existe); em ambientes de teste efêmeros (Testcontainers), a conexão é superusuária e a migration cria a extensão de fato.

## Consequências

### Positivas

- Coordenadas geográficas corretas e buscas espaciais viáveis (índice GIST).
- Mapeamento idiomático: o domínio usa `Point` (NTS), a persistência cuida do PostGIS.
- O hook opcional habilita o eixo geoespacial sem regressão para os demais módulos.

### Negativas

- A imagem do PostgreSQL precisa incluir o PostGIS (troca da imagem base do container).
- Mais uma extensão e mais um plugin de provider a manter em sincronia de versão (Npgsql NTS ↔ NetTopologySuite).

### Neutras

- O `Geo.Domain` passa a depender da biblioteca de geometria NetTopologySuite (core, provider-agnostic) — uma dependência de domínio legítima para um contexto geoespacial.

## Confirmação

- **Teste de integração** (Testcontainers com imagem PostGIS): um `Point` SRID 4326 é persistido e relido preservando coordenada e SRID; a extensão `postgis` é criada pela migration.
- **Teste de mapeamento**: o `GeoDbContext` mapeia a coordenada para `geography (Point, 4326)` tanto no caminho runtime (`AddGeoInfrastructure`) quanto no design-time (factory), provando a paridade do hook NTS.
- **Não-regressão**: a suíte completa dos demais módulos permanece verde com o hook default `null`.

## Prós e contras das opções

### A — `geometry(Point, 4326)`

- Bom, porque suporta toda a álgebra espacial do PostGIS.
- Ruim, porque trata coordenadas como plano cartesiano; distâncias geográficas saem incorretas sem reprojeção explícita.

### B — `geography(Point, 4326)` (escolhida)

- Bom, porque distância/proximidade sobre o elipsoide saem corretas por construção; índice GIST disponível.
- Ruim, porque algumas operações são mais caras que em `geometry` — aceitável para o volume de localidades.

### C — Dois `double precision`

- Bom, porque não exige PostGIS.
- Ruim, porque não há tipo espacial, índice espacial nem consultas de proximidade — reinventaria mal o que o PostGIS já resolve.

## Mais informações

- Habilita o eixo geoespacial do módulo `Geo` ([ADR-0090](0090-modulo-geo-localidades.md)).
- Segue a convenção de naming/migrations da [ADR-0054](0054-naming-convention-e-strategy-migrations.md).
- Documentação do Npgsql sobre o plugin NetTopologySuite (mapeamento `geometry`/`geography`).
