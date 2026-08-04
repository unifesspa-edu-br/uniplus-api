# Geo como serviço externo — o que permanece no `uniplus-api`

O Geo deixou de ser um módulo/host deste repositório e passou a ser um serviço
institucional com repositório, contrato, imagem e release próprios
([ADR-0099](adrs/0099-geo-como-repositorio-dedicado.md)). Este guia responde a
uma pergunta operacional recorrente: **ao encontrar a palavra "Geo" no
`uniplus-api`, isso é resíduo da extração ou referência legítima?**

Fonte de verdade de código, contrato, migrations, imagem e operação do Geo:
[`unifesspa-geo-api`](https://github.com/unifesspa-edu-br/unifesspa-geo-api).

## Allowlist — referências legítimas

Estas categorias **devem** permanecer. Removê-las quebra build, infra local ou
contrato público.

### 1. Consumo do serviço externo na infra local

| Onde | O quê |
|---|---|
| `docker/docker-compose.override.example.yml` | serviço `geo-api` apontando para a imagem `ghcr.io/unifesspa-edu-br/unifesspa-geo-api` |
| `docker/.env.example` | `GEO_IMAGE_TAG` — a tag consumida (o repositório dedicado não publica `latest`) |
| `docker/docker-compose.smoke.yml` | `geo-api` na topologia de smoke/Newman |
| `docker/init-db.sql` | provisionamento do banco `uniplus_geo` / `uniplus_geo_staging` e do role `uniplus_geo_app` — o Postgres é compartilhado no ambiente local |
| rotas Traefik do mesmo override | split `/api/{cidades,estados,cep,logradouros}` → `geo-api`, espelhando o ingress de HML/PROD |

A imagem é **consumida**, não construída aqui. Procedimento de atualização de
tag em [`CONTRIBUTING.md`](../CONTRIBUTING.md#manter-a-infra-local-do-geo-em-dia).

### 2. Snapshots consumidores (ADR-0096)

Configuração e Organização Institucional guardam **referência estruturada por
valor** à cidade — `cidade_codigo_ibge` mais display cache com proveniência e
instante. É o padrão de composição-no-cliente decidido pela
[ADR-0096](adrs/0096-endereco-como-referencia-estruturada-ao-geo.md), não
acoplamento residual.

- `ReferenciaCidadeGeo` (`src/shared/Unifesspa.UniPlus.Kernel/Domain/Cidades/`) —
  inclusive a constante de proveniência `"geo-api"` gravada em `cidade_origem`
- `CidadeReferenciaDto` / `CidadeReferenciaInput` dos dois módulos
- colunas `cidade_codigo_ibge`, `cidade_nome`, `cidade_origem`,
  `cidade_capturada_em` e suas migrations

Esses nomes citam o Geo porque **descrevem a origem do dado**. Continuam
corretos com o Geo externo.

### 3. Registro histórico em ADRs

As ADRs [0090](adrs/0090-modulo-geo-localidades.md) a
[0098](adrs/0098-politica-de-service-location-do-codegen-wolverine.md) foram
decididas quando o Geo era interno. O corpo delas **não é reescrito** — cada uma
recebeu nota de contexto apontando para a ADR-0099 e qualificando quais
artefatos citados migraram. ADR registra o que foi decidido no momento em que
foi decidido.

O mesmo vale para os checkpoints em `docs/spikes/` e para
[`docs/geo-etl-dataset-dne.md`](geo-etl-dataset-dne.md): artefatos históricos com
nota no topo, cuja documentação operacional canônica passou a viver no
repositório dedicado.

## Fora da allowlist — o que seria resíduo

Se algum destes reaparecer, é regressão da extração:

- projeto, `ProjectReference` ou pacote `Unifesspa.UniPlus.Geo.*` / `Unifesspa.Geo.*`
- `using` ou namespace de internals do Geo (`GeoDbContext`, `EstadoReader`,
  `CepResolver`, `IGeoImportacaoService`, …)
- `contracts/openapi.geo.json` — a baseline canônica vive no repositório dedicado
- `Dockerfile.geo` ou qualquer build da imagem do Geo a partir daqui
- documentação **corrente** (não histórica) descrevendo o Geo como módulo em
  `src/` ou como um dos executáveis buildados neste repositório

A ausência de projeto/pacote/namespace é verificável em build e nos fitness
tests do `ArchTests`, conforme a seção Confirmação da ADR-0099.

## Topologia vigente

**2 APIs executáveis internas** — UniPlus (`src/host`, composition root dos
módulos de negócio) e Portal (`src/portal`, BFF público) — **+ Geo como serviço
externo** consumido por contrato. É o refinamento da
[ADR-0097](adrs/0097-topologia-de-deploy-em-tres-apis-monolito-modular.md), que
decidiu 3 APIs quando o Geo ainda era co-localizado nesta solution.
