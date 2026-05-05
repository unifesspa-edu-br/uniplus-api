# Architecture Decision Records — `uniplus-api`

Base canônica de decisões arquiteturais do `uniplus-api`, formato [MADR 4.0](https://adr.github.io/madr/).

Cada ADR registra **uma única decisão**. Histórico de decisões institucionais que originaram parte deste acervo permanece em documentação interna não publicada — quando relevante, a seção `Mais informações` de cada ADR cita a origem como `Origem: revisão da ADR interna Uni+ ADR-NNN (não publicada)`.

## Estrutura

- Cada ADR em arquivo `NNNN-titulo-em-slug.md` (4 dígitos, slug ASCII).
- Frontmatter YAML obrigatório com `status`, `date`, `decision-makers`.
- Seções fixas: Contexto, Drivers, Opções, Resultado da decisão (única), Consequências, Confirmação opcional, Mais informações.
- Conteúdo em pt-BR; chaves do frontmatter em inglês para compatibilidade com ferramentas MADR.

## Linter

Validador local em [`tools/adr-lint/`](../../tools/adr-lint/README.md):

```bash
bash tools/adr-lint/validate.sh
```

Adicionalmente:

```bash
npx markdownlint-cli2 'docs/adrs/**/*.md'
```

## Índice

| ADR | Título | Status | Data |
|-----|--------|--------|------|
| [0001](0001-monolito-modular-como-estilo-arquitetural.md) | Monolito modular como estilo arquitetural | accepted | 2026-04-28 |
| [0002](0002-clean-architecture-com-quatro-camadas.md) | Clean Architecture com quatro camadas por módulo | accepted | 2026-04-28 |
| [0003](0003-wolverine-como-backbone-cqrs.md) | Wolverine como backbone CQRS in-process | accepted | 2026-04-28 |
| [0004](0004-outbox-transacional-via-wolverine.md) | Outbox transacional via Wolverine + EF Core sobre PostgreSQL | accepted | 2026-04-28 |
| [0005](0005-cascading-messages-para-drenagem-de-domain-events.md) | Cascading messages como drenagem canônica de domain events | accepted | 2026-04-28 |
| [0006](0006-csharp-14-e-dotnet-10-como-stack-do-backend.md) | C# 14 / .NET 10 como linguagem e runtime do backend | accepted | 2026-04-28 |
| [0007](0007-postgresql-18-como-banco-primario.md) | PostgreSQL 18 como banco de dados primário | accepted | 2026-04-28 |
| [0008](0008-redis-como-cache-distribuido.md) | Redis como cache distribuído | accepted | 2026-04-28 |
| [0009](0009-minio-como-object-storage.md) | MinIO como object storage S3-compatible | accepted | 2026-04-28 |
| [0010](0010-audience-unica-uniplus-em-tokens-oidc.md) | Audience única `uniplus` em tokens OIDC | accepted | 2026-04-28 |
| [0011](0011-mascaramento-de-cpf-em-logs.md) | Mascaramento de CPF em logs via enricher Serilog | accepted | 2026-04-28 |
| [0012](0012-archunitnet-como-fitness-tests-arquiteturais.md) | ArchUnitNET como biblioteca de fitness tests arquiteturais | accepted | 2026-04-28 |
| [0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md) | Motor de classificação como serviços de domínio puros | accepted | 2026-04-28 |
| [0014](0014-kafka-como-bus-assincrono-inter-modulos.md) | Kafka como bus assíncrono inter-módulos e para integrações externas | accepted | 2026-04-28 |
| [0015](0015-rest-contract-first-com-openapi.md) | REST contract-first com OpenAPI 3.0 e versionamento de API | accepted | 2026-04-28 |
| [0016](0016-keycloak-como-identity-provider.md) | Keycloak como identity provider OIDC do `uniplus-api` | accepted | 2026-04-28 |
| [0017](0017-kubernetes-com-helm-para-orquestracao.md) | Kubernetes com Helm para orquestração do `uniplus-api` | accepted | 2026-04-28 |
| [0018](0018-opentelemetry-para-instrumentacao-do-backend.md) | OpenTelemetry para instrumentação do `uniplus-api` | accepted | 2026-04-28 |
| [0019](0019-proibir-pii-em-path-segments-de-url.md) | Proibir PII em path segments de URL | accepted | 2026-05-01 |
| [0020](0020-identity-brokering-govbr.md) | Identity brokering gov.br via Keycloak | accepted | 2026-05-01 |
| [0021](0021-adocao-awesomeassertions-como-biblioteca-de-assertions.md) | Adoção de AwesomeAssertions como biblioteca de assertions de testes | accepted | 2026-05-02 |
| [0022](0022-contrato-rest-canonico-umbrella.md) | Contrato REST canônico V1 — frame transversal e índice das ADRs filhas | accepted | 2026-05-03 |
| [0023](0023-wire-formato-erro-rfc-9457.md) | Wire format de erro — RFC 9457 ProblemDetails como único formato | accepted | 2026-05-03 |
| [0024](0024-mapeamento-domain-error-http.md) | Mapeamento `DomainError → HTTP` via `IDomainErrorMapper` registry | accepted | 2026-05-03 |
| [0025](0025-wire-formato-sucesso-body-direto.md) | Wire format de sucesso — body é a representação direta do recurso | accepted | 2026-05-03 |
| [0026](0026-paginacao-cursor-opaco-cifrado.md) | Paginação via cursor opaco cifrado e propagação por `Link` header | accepted | 2026-05-03 |
| [0027](0027-idempotency-key-store-postgresql.md) | `Idempotency-Key` opt-in com store em PostgreSQL adjacente ao outbox | accepted | 2026-05-03 |
| [0028](0028-versionamento-per-resource-content-negotiation.md) | Versionamento per-resource via content negotiation | accepted | 2026-05-03 |
| [0029](0029-hateoas-level-1-links.md) | HATEOAS Level 1 — `_links` mínimo embutido no recurso | accepted | 2026-05-03 |
| [0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md) | Geração de OpenAPI 3.1 via `Microsoft.AspNetCore.OpenApi` com pipeline de pós-processamento | accepted | 2026-05-03 |
| [0031](0031-decoding-de-cursor-opaco-no-boundary-http.md) | Decoding de cursor opaco no boundary HTTP, não em handlers de Application | proposed | 2026-05-04 |
| [0032](0032-guid-v7-para-identidade-de-entidades.md) | Guid v7 (RFC 9562) como identidade de entidades de domínio | accepted | 2026-05-05 |
| [0033](0033-icurrentuser-abstraction-via-iusercontext.md) | `IUserContext` como abstração canônica para acesso ao principal autenticado | accepted | 2026-05-05 |
| [0034](0034-problemdetails-em-401-403-via-jwtbearer-events.md) | ProblemDetails RFC 9457 em 401/403 via `JwtBearerEvents.OnChallenge`/`OnForbidden` | accepted | 2026-05-05 |

## Como adicionar um novo ADR

1. Identifique o próximo número sequencial (`ls docs/adrs/[0-9]*.md | wc -l`).
2. Copie [`_template.md`](_template.md).
3. Renomeie para `NNNN-titulo-em-slug.md` (slug ASCII em minúsculas, hífens como separador).
4. Preencha frontmatter, contexto, drivers, opções, resultado da decisão (única), consequências.
5. Rode o linter (`bash tools/adr-lint/validate.sh`).
6. Adicione linha ao índice acima.
7. Abra PR.
