---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0015: REST contract-first com OpenAPI 3.1 e versionamento de API

## Contexto e enunciado do problema

O `uniplus-api` é consumido pelas SPAs do `uniplus-web` (Seleção, Ingresso, Portal) desenvolvidas por times paralelos. Sem contrato formal, frontend fica bloqueado até que o backend implemente os endpoints, e divergências só aparecem em integração.

O contrato precisa ser fonte de verdade — implementação backend e clientes frontend derivam dele.

## Drivers da decisão

- Desenvolvimento paralelo de frontend e backend sem bloqueio.
- Geração automática de clients tipados — eliminação de drift por construção.
- Tratamento de erros padronizado para reduzir custo cognitivo.
- Compatibilidade backward para consumidores existentes.

## Opções consideradas

- REST contract-first com OpenAPI 3.0
- GraphQL
- gRPC para comunicação interna
- Code-first OpenAPI (gerar spec a partir do código)

## Resultado da decisão

**Escolhida:** REST contract-first com OpenAPI. A spec OpenAPI é o artefato de referência, conforme a emenda abaixo.

Regras técnicas:

- **Contract-first.** O contrato OpenAPI vive em `contracts/` neste repositório, conforme a [ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md).
- **Erros padronizados via ProblemDetails (RFC 7807)** com `correlationId` para rastreabilidade.
- **Versionamento** — por recurso, via negociação de conteúdo, sem versão na rota, conforme as ADRs [0028](0028-versionamento-per-resource-content-negotiation.md) e [0064](0064-convencao-roteamento-path-based-com-prefixo-modulo.md).
- **Tipos do web** — os tipos TypeScript do web são gerados pelo `openapi-typescript` a partir do contrato ([ADR-0013 do web](https://github.com/unifesspa-edu-br/uniplus-web/blob/main/docs/adrs/0013-gerador-openapi-node-only.md)).
- **Health checks** via `Microsoft.Extensions.Diagnostics.HealthChecks` em `/health/live` (liveness), `/health` (todas as verificações, com o estado de cada uma) e `/health/ready` (readiness com checks de dependências: PostgreSQL, Kafka, Redis, MinIO; o Keycloak aparece como degradado no `/health`, fora da readiness, conforme a ADR-0131).
- **Rate limiting nativo do .NET** com políticas por rota — proteção contra abuso em endpoints públicos (inscrição, consulta de resultado).
- **Testes de contrato** comparam os documentos gerados às referências aprovadas em `contracts/`.

### Emenda — 2026-11-25

O contrato publicado usa OpenAPI 3.1 e é gerado a partir do código, conforme a [ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md). A versão é por recurso e não aparece na rota, conforme as ADRs 0028 e 0064. A geração de tipos do web segue a ADR-0013 do web. Pact não foi adotado para essa comparação de contrato.

## Consequências

### Positivas

- Desenvolvimento paralelo frontend/backend sem bloqueios.
- Divergências entre documentos gerados e referências aprovadas são detectadas pelos testes de contrato.
- ProblemDetails padroniza tratamento de erros em toda a plataforma.
- Versionamento por recurso permite evolução independente.
- Health checks habilitam readiness probes no Kubernetes.

### Negativas

- Disciplina exigida para manter spec sincronizada com a implementação.
- Geração de clients pode ter edge cases (tipos polimórficos complexos).
- REST é verboso para queries com múltiplos filtros e agregações — possíveis BFFs no futuro.

### Riscos

- **Spec desatualizada.** Mitigado por testes que comparam os documentos gerados às referências aprovadas.
- **Over-fetching/under-fetching.** Mitigado por endpoints específicos por caso de uso (não genéricos) e BFF como evolução possível.

## Confirmação

- Testes de integração comparam os documentos OpenAPI gerados às referências aprovadas.
- A geração dos tipos do web segue a ADR-0013 do web.

## Prós e contras das opções

### REST contract-first

- Bom, porque é convenção de mercado mais consolidada e tem melhor ecossistema de ferramentas.
- Ruim, porque é verboso para fluxos com agregações complexas.

### GraphQL

- Bom, porque cliente solicita exatamente o que precisa.
- Ruim, porque ecossistema .NET menos maduro que REST; caching HTTP mais difícil; risco de N+1 sem DataLoader; equipe sem experiência.

### gRPC

- Bom, porque tem performance e tipagem fortes.
- Ruim, porque módulos comunicam-se via Kafka (ADR-0014); gRPC é overkill para REST público para SPA.

### Code-first OpenAPI

- Bom, porque elimina dois artefatos (spec + código).
- Ruim, porque inverte a prioridade — frontend depende do backend terminar a implementação para ter spec; elimina o benefício do desenvolvimento paralelo.

## Mais informações

- ADR-0006 define .NET 10 como stack que executa esta API.
- ADR-0008 define Redis como contador de rate limiting.
- ADR-0017 define K8s + Helm com NGINX Ingress como TLS termination e rate limiting de borda.
- **Origem:** revisão da ADR interna Uni+ ADR-011 (não publicada).
