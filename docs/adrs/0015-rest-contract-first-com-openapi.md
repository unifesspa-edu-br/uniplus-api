---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0015: REST contract-first com OpenAPI 3.0 e versionamento de API

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

**Escolhida:** REST contract-first com OpenAPI 3.0. A spec OpenAPI é o artefato de referência. Backend implementa a partir dela; frontend consome cliente gerado dela.

Regras técnicas:

- **Contract-first.** A spec OpenAPI vive no repositório de documentação institucional e é versionada antes da implementação.
- **Erros padronizados via ProblemDetails (RFC 7807)** com `correlationId` para rastreabilidade.
- **Versionamento via Asp.Versioning** — URL path `/api/v1/` para major versions e header `api-version` para minor.
- **Geração de clientes** — clientes TypeScript gerados via NSwag ou Kiota a partir da spec.
- **Health checks** via `Microsoft.Extensions.Diagnostics.HealthChecks` em `/health` (liveness) e `/health/ready` (readiness com checks de dependências: PostgreSQL, Kafka, Redis, MinIO, Keycloak).
- **Rate limiting nativo do .NET** com políticas por rota — proteção contra abuso em endpoints públicos (inscrição, consulta de resultado).
- **Testes de contrato (Pact)** para garantir que a implementação não diverge da spec.

## Consequências

### Positivas

- Desenvolvimento paralelo frontend/backend sem bloqueios.
- Drift eliminado por geração automática + Pact em CI.
- ProblemDetails padroniza tratamento de erros em toda a plataforma.
- Versionamento via URL path mantém backward compatibility.
- Health checks habilitam readiness probes no Kubernetes.

### Negativas

- Disciplina exigida para manter spec sincronizada com a implementação.
- Geração de clients pode ter edge cases (tipos polimórficos complexos).
- REST é verboso para queries com múltiplos filtros e agregações — possíveis BFFs no futuro.

### Riscos

- **Spec desatualizada.** Mitigado por testes Pact que falham o CI em divergência.
- **Over-fetching/under-fetching.** Mitigado por endpoints específicos por caso de uso (não genéricos) e BFF como evolução possível.

## Confirmação

- Pipeline de CI executa contract tests Pact contra a spec OpenAPI.
- Build do frontend regenera o client e falha se a spec apontada não estiver acessível.

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
