---
status: "accepted"
date: "2026-05-16"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
  - "DevOps (DIRSI)"
---

# ADR-0064: Convenção de roteamento — path-based com prefixo de módulo

## Contexto e enunciado do problema

O `uniplus-api` é um monolito modular composto por 5 APIs ASP.NET independentes,
cada uma compilada em container Docker próprio e implantada como serviço
separado:

| API | Bounded context | Container |
|---|---|---|
| `Selecao.API` | Editais, inscrições, classificação (CEPS) | `uniplus-selecao` |
| `Ingresso.API` | Chamadas, convocações, matrículas (CRCA) | `uniplus-ingresso` |
| `OrganizacaoInstitucional.API` | `AreaOrganizacional` (roster fechado per ADR-0055) | `uniplus-organizacao` |
| `Parametrizacao.API` | Entidades cross-cutting (Modalidade, NecessidadeEspecial, TipoDocumento, Endereco) | `uniplus-parametrizacao` |
| `Portal.API` | Landing público + auth gateway | `uniplus-portal` |

Em HML/PROD as 5 APIs são expostas atrás de um **Traefik (ou Ingress
Kubernetes equivalente)** definido em `uniplus-infra`. O frontend Angular
no `uniplus-web` consome HTTP a partir de um único origin. Surge a
pergunta de como o reverse proxy roteia as requisições para a API correta.

Os controllers iniciais convergiram para dois padrões inconsistentes:

- `EditalController` (Selecao) declara `[Route("api/editais")]` — recurso
  direto sob `/api/`.
- `AreasOrganizacionaisController` (OrganizacaoInstitucional) declara
  `[Route("api")]` com paths `/api/areas-organizacionais` e
  `/api/admin/areas-organizacionais` — também sem prefixo de módulo.
- `PingController` (Portal) declara `[Route("api/portal/ping")]` — único
  caso com prefixo de módulo.

ADRs antigas (ADR-0058 §"REST surface", ADR-0062) propunham
`/api/catalogos/...` como prefixo de agrupamento — vocabulário rejeitado
pelo sponsor (o termo "catálogo" fica reservado a um futuro conceito de
domínio).

Falta uma convenção explícita que reconcilie (a) o pattern dos controllers
existentes, (b) o vocabulário sponsor e (c) o custo operacional de
configurar Traefik + DNS + TLS.

## Drivers da decisão

- **Operação do Traefik em HML/PROD**: regra de roteamento mais simples
  possível, idealmente um único subdomain com path matchers.
- **TLS gerenciado**: minimizar quantidade de certificados (Let's Encrypt
  HTTP-01 challenge é simples para 1 domain, exige DNS-01 para wildcard).
- **Frontend Angular**: um único `API_URL` configurado por ambiente, sem
  precisar diferenciar 5 origins distintos no `environment.ts`.
- **CORS**: um único origin liberado nos controllers, sem multiplicar
  whitelist por API.
- **Movimentação de recursos entre módulos**: ADR-0058 documenta critérios
  para promover `ObrigatoriedadeLegal` para um futuro módulo `Normativos` —
  decisão arquitetural rara (anos), proporcional a uma URL change.
- **Vocabulário sponsor**: nem "catálogos" nem "parametrização" devem
  aparecer como segmento de URL como **categoria genérica**, mas o nome do
  **bounded context** (`selecao`, `parametrizacao`, `ingresso`, `organizacao`,
  `portal`) é descritivo arquitetural válido — não conflita com a diretriz.

## Opções consideradas

- **A**: `/api/{recurso}` sem prefixo de módulo, separação cross-API via
  **host/subdomain** (`selecao.api.uniplus.unifesspa.edu.br`, etc).
- **B**: `/api/{modulo}/{recurso}` com prefixo de módulo, separação
  cross-API via **path prefix** sob um único host (escolhida).
- **C**: `/api/{recurso}` em todas APIs, gateway agregador dedicado
  (Ocelot, YARP) que multiplexa para os 5 backends.
- **D**: Híbrido — recursos canônicos sem prefix; admin endpoints com
  prefix de módulo.

## Resultado da decisão

**Escolhida:** "B — path-based com prefixo de módulo, sob um único host",
porque é o pattern idiomático do Traefik (`PathPrefix(/api/selecao)`),
exige apenas 1 subdomain DNS e 1 certificado TLS, simplifica CORS para o
frontend a um único origin, e mantém uniformidade entre APIs sem custo
operacional adicional.

### Forma concreta

Cada controller declara `[Route("api/{modulo}/{recurso-plural-kebab}")]` ou
`[Route("api/{modulo}")]` com paths action-level quando há split
público/admin:

```csharp
[ApiController]
[Route("api/selecao/obrigatoriedades-legais")]
public sealed class ObrigatoriedadeLegalController : ControllerBase { ... }
```

Para split público/admin no mesmo controller:

```csharp
[ApiController]
[Route("api/selecao")]
public sealed class ObrigatoriedadeLegalController : ControllerBase
{
    [HttpGet("obrigatoriedades-legais")]
    public Task<IActionResult> Listar(...) { ... }

    [HttpPost("admin/obrigatoriedades-legais")]
    public Task<IActionResult> Criar(...) { ... }
}
```

### Naming convention

- **Segmento de módulo** em minúsculas, sem hífen interno, refletindo o
  bounded context: `selecao`, `parametrizacao`, `ingresso`, `organizacao`
  (encurtado de `organizacao-institucional` para legibilidade), `portal`.
- **Segmento de recurso**: plural kebab-case (`obrigatoriedades-legais`,
  `areas-organizacionais`, `necessidades-especiais`, `tipos-documento`).
- **Admin endpoints**: `{recurso}` ganha sufixo de path `admin/` (com
  módulo já no prefix): `/api/selecao/admin/obrigatoriedades-legais`,
  `/api/parametrizacao/admin/modalidades`. RBAC restrito por
  `AreaScopedAuthorizationHandler` (ADR-0057).
- **Sub-recursos**: `/api/{modulo}/{recurso-pai}/{id}/{sub-recurso}` —
  ex.: `/api/selecao/editais/{id}/conformidade`.

### Endpoints operacionais

**Health checks** (`/health`, `/health/live`, `/health/ready`) ficam na
**raiz de cada API**, sem prefixo `/api/{modulo}/`. Esse é o pattern já
em main em todas as 5 APIs (via `app.MapHealthChecks(...)` em `Program.cs`)
e alinha com a convenção Kubernetes — probes (`livenessProbe`,
`readinessProbe`) são configuradas direto contra o pod IP, frequentemente
bypassando o ingress. Manter `/health*` na raiz preserva esse caminho
operacional sem precisar de regra extra no Traefik.

**Outros endpoints operacionais** sem semântica REST de recurso (ping,
metrics, info) seguem `/api/{modulo}/{endpoint}` — ex.: `/api/portal/ping`.
São endpoints "lógicos" da API e fazem sentido dentro do prefix de módulo.

Em todos os casos: não suportam vendor MIME, não aparecem no OpenAPI
público.

### Roteamento Traefik / Ingress

Em HML/PROD, um único host (`api.uniplus.unifesspa.edu.br`) com regras
de path-prefix usando **trailing slash explícita** para garantir limite
de segmento de caminho (sem essa precaução, `PathPrefix(/api/selecao)`
em Traefik também casaria `/api/selecao-foo`, gerando colisão potencial):

```text
Host(`api.uniplus.unifesspa.edu.br`) && PathPrefix(`/api/selecao/`)        → uniplus-selecao
Host(`api.uniplus.unifesspa.edu.br`) && PathPrefix(`/api/ingresso/`)       → uniplus-ingresso
Host(`api.uniplus.unifesspa.edu.br`) && PathPrefix(`/api/parametrizacao/`) → uniplus-parametrizacao
Host(`api.uniplus.unifesspa.edu.br`) && PathPrefix(`/api/organizacao/`)    → uniplus-organizacao
Host(`api.uniplus.unifesspa.edu.br`) && PathPrefix(`/api/portal/`)         → uniplus-portal
```

Como nenhum controller expõe endpoint em exatamente `/api/{modulo}` (sem
sub-path), a trailing slash no matcher é suficiente. Caso futuro precise
de endpoint na raiz do módulo, usar `Path(\`/api/{modulo}\`) || PathPrefix(\`/api/{modulo}/\`)`.

Para os health checks na raiz, regra separada por host (não path) ou,
preferencialmente, configurar a probe Kubernetes direto contra o pod IP
bypassando o ingress (pattern padrão).

Um único certificado TLS para `api.uniplus.unifesspa.edu.br` (HTTP-01
challenge funciona — não exige wildcard). CORS configurado para um único
origin (`uniplus.unifesspa.edu.br` em PROD).

Em DEV (`docker-compose`), cada API expõe porta distinta; o frontend
local pode rodar com `API_URL=http://localhost:7080` apontando para
qualquer reverse proxy local (ou portas diretas durante debug).

Recursos REST entre APIs nunca colidem porque o segmento de módulo
diferencia. Consumo cross-API in-process usa `IXxxReader` (ADR-0056), não
HTTP.

### Mapeamento canônico Sprint 3

| Recurso | API hospedeira | URL público | URL admin | Vendor MIME |
|---|---|---|---|---|
| Edital | Selecao | `/api/selecao/editais` | `/api/selecao/admin/editais` | `vnd.uniplus.edital.v1+json` |
| ObrigatoriedadeLegal | Selecao | `/api/selecao/obrigatoriedades-legais` | `/api/selecao/admin/obrigatoriedades-legais` | `vnd.uniplus.obrigatoriedade-legal.v1+json` |
| TipoEdital | Selecao | `/api/selecao/tipos-edital` | `/api/selecao/admin/tipos-edital` | `vnd.uniplus.tipo-edital.v1+json` |
| TipoEtapa | Selecao | `/api/selecao/tipos-etapa` | `/api/selecao/admin/tipos-etapa` | `vnd.uniplus.tipo-etapa.v1+json` |
| CriterioDesempate | Selecao | `/api/selecao/criterios-desempate` | `/api/selecao/admin/criterios-desempate` | `vnd.uniplus.criterio-desempate.v1+json` |
| LocalProva | Selecao | `/api/selecao/locais-prova` | `/api/selecao/admin/locais-prova` | `vnd.uniplus.local-prova.v1+json` |
| Modalidade | Parametrizacao | `/api/parametrizacao/modalidades` | `/api/parametrizacao/admin/modalidades` | `vnd.uniplus.modalidade.v1+json` |
| NecessidadeEspecial | Parametrizacao | `/api/parametrizacao/necessidades-especiais` | `/api/parametrizacao/admin/necessidades-especiais` | `vnd.uniplus.necessidade-especial.v1+json` |
| TipoDocumento | Parametrizacao | `/api/parametrizacao/tipos-documento` | `/api/parametrizacao/admin/tipos-documento` | `vnd.uniplus.tipo-documento.v1+json` |
| Endereco | Parametrizacao | `/api/parametrizacao/enderecos` | `/api/parametrizacao/admin/enderecos` | `vnd.uniplus.endereco.v1+json` |
| AreaOrganizacional | OrganizacaoInstitucional | `/api/organizacao/areas-organizacionais` | `/api/organizacao/admin/areas-organizacionais` | `vnd.uniplus.area-organizacional.v1+json` |

### Migração dos endpoints já em main

Dois controllers em main usam o padrão antigo sem prefix de módulo:

- `EditalController` em `Selecao.API`: `[Route("api/editais")]` →
  `[Route("api/selecao/editais")]`.
- `AreasOrganizacionaisController` em `OrganizacaoInstitucional.API`:
  `[Route("api")]` com paths `/api/areas-organizacionais` e
  `/api/admin/areas-organizacionais` → `[Route("api/organizacao")]` com
  paths `/api/organizacao/areas-organizacionais` e
  `/api/organizacao/admin/areas-organizacionais`.

A migração é breaking change para clientes, mas o projeto está em fase
**pré-HML sem consumers reais** — momento adequado. A migração será
feita em PRs separados, fora do escopo desta ADR (cada controller
afetado tem sua própria PR de cutover acompanhada por atualização do
respectivo OpenAPI spec).

### Vocabulário sponsor preservado

Os segmentos de módulo (`selecao`, `parametrizacao`, etc) são **nomes de
bounded contexts arquiteturais**, não rótulos genéricos como "catálogo"
ou "parametrização (do recurso X)". A diretriz sponsor de reservar
"catálogo" a um futuro conceito de domínio continua honrada — nenhum path
usa `/catalogos/` ou rótulo categoria-genérica.

## Consequências

### Positivas

- **Operação Traefik trivial**: regras `PathPrefix(/api/{modulo})` por
  API, 1 subdomain, 1 certificado TLS, 1 origin CORS.
- **DNS simples**: um único registro A para `api.uniplus.unifesspa.edu.br`,
  sem necessidade de wildcard ou múltiplos subdomains.
- **Frontend simples**: um `API_URL` único no `environment.ts`; todos os
  recursos navegam por path relativo (`/api/selecao/...`).
- **OpenAPI per-resource (ADR-0028) preservado**: cada API tem seu próprio
  doc, com base path `/api/{modulo}`.
- **Uniformidade**: todo recurso de negócio tem `/api/{modulo}/{recurso}`,
  sem exceções; endpoints operacionais idem.
- **Bounded context explícito na URL**: dev/admin/cliente identifica o
  módulo dono pela URL sem precisar de doc paralelo.

### Negativas

- **URLs mais verbosas**: `/api/selecao/editais` em vez de `/api/editais`.
  Custo cosmético — frontend usa interpolação ou helper, dev raramente
  digita à mão.
- **Movimentação de recurso entre módulos é breaking change**: se
  `ObrigatoriedadeLegal` for promovida para um módulo `Normativos` (per
  critérios em ADR-0058), a URL muda de `/api/selecao/obrigatoriedades-legais`
  para `/api/normativos/obrigatoriedades-legais`. Mitigado por (a) ser
  decisão arquitetural rara (anos), (b) versionamento per-resource
  (ADR-0028) permitir deprecation graceful, (c) ingress poder manter
  rewrite temporário durante migração.
- **Migração dos 2 controllers em main**: cutover precisa ser executado
  antes que mais clientes consumam os paths antigos. Trabalho one-time.

### Neutras

- O segmento de módulo na URL é descritivo, mas semanticamente redundante
  com o host (em ambientes onde o ingress já isolou as APIs por path).
  Aceitável — torna a URL self-documenting.

## Confirmação

- **Fitness test (futuro)**: análise estática que verifica em
  `*.API/Controllers/*.cs` que `[Route(...)]` segue `api/{modulo}` ou
  `api/{modulo}/{recurso-kebab}`. Implementado quando o terceiro recurso
  novo (post-#461) entrar.
- **Manifest do Traefik/Ingress** em `uniplus-infra` deve listar as 5
  rules de PathPrefix por API. PR de cutover dos controllers em main
  acompanha a configuração de ingress equivalente.
- **Code review obrigatório**: qualquer PR que introduza controller sem
  prefix de módulo na rota precisa amendment desta ADR.

## Prós e contras das opções

### A — Host-based (`Host(selecao.api.…)`)

- **Bom**: URLs curtas (`/api/editais`); promoção de recurso entre
  módulos não muda URL.
- **Ruim**: 5 subdomains DNS + 5 certificados TLS (ou wildcard com DNS-01
  challenge mais complexo); 5 origins CORS no frontend; precisa env var
  por API no frontend.

### B — Path-based (escolhida)

- **Bom**: 1 subdomain, 1 cert, 1 origin CORS, 1 env var no frontend;
  pattern idiomático do Traefik; bounded context explícito na URL.
- **Ruim**: URLs mais verbosas; promoção de recurso entre módulos é
  breaking change.

### C — Gateway agregador (Ocelot/YARP)

- **Bom**: cliente vê API monolítica; recursos podem ser remapeados sem
  breaking change.
- **Ruim**: adiciona infra extra (gateway dedicado, latência, ponto de
  falha) sem benefício proporcional para 5 APIs.

### D — Híbrido (recurso direto + admin com prefix)

- **Bom**: nenhum claro.
- **Ruim**: inconsistência por design; cada PR decide caso a caso.

## Mais informações

- [ADR-0001](0001-monolito-modular-como-estilo-arquitetural.md) — Monolito
  modular, cross-módulo via Kafka apenas (write) ou IXxxReader (read).
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) —
  Vendor MIME per resource.
- [ADR-0049](0049-hateoas-level-1-recurso-canonico.md) — HATEOAS Level 1
  (links sempre relativos).
- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — APIs
  por bounded context.
- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) —
  Carve-out cross-módulo via `IXxxReader`.
- [ADR-0058](0058-obrigatoriedade-legal-validacao-data-driven.md) — Emenda
  1 aplica as URLs com prefixo de módulo desta ADR.
- [ADR-0062](0062-seed-de-catalogos-via-newman-e-endpoints-admin.md) —
  Emenda 1 aplica as URLs admin com prefixo de módulo.
- Documentação Traefik PathPrefix —
  <https://doc.traefik.io/traefik/routing/routers/#rule>.
