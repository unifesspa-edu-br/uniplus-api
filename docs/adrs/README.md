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
| [0035](0035-shared-schemas-cross-module-fitness-test.md) | Schemas duplicados entre baselines OpenAPI — fitness test cross-module no lugar de `$ref` multi-arquivo | accepted | 2026-05-05 |
| [0036](0036-controllers-mvc-para-negocio-minimal-api-para-shared.md) | Controllers MVC `[ApiController]` para endpoints de negócio + Minimal API restrita a shared/técnicos | accepted | 2026-05-05 |
| [0037](0037-hosting-minimal-api-vs-startup.md) | Hosting via `WebApplication.CreateBuilder` mantido vs migração para Generic Host + `Startup.cs` | accepted | 2026-05-05 |
| [0038](0038-override-configuracao-em-testes-via-env-vars.md) | Override de configuração em testes via env vars + `DisableParallelization` na collection | accepted | 2026-05-05 |
| [0039](0039-provisioning-schema-wolverine-via-deploy.md) | Provisioning do schema Wolverine como responsabilidade do deploy, não auto-create em runtime | accepted | 2026-05-05 |
| [0040](0040-helper-wolverine-outbox-cascading-canonico.md) | `WolverineOutboxConfiguration.UseWolverineOutboxCascading` como ponto canônico de configuração | accepted | 2026-05-05 |
| [0041](0041-padrao-retorno-handlers-wolverine-cascading.md) | Padrão de retorno `(Result, IEnumerable<object>)` em handlers Wolverine que mutam agregados | accepted | 2026-05-05 |
| [0042](0042-application-nao-depende-diretamente-de-dbcontext.md) | Application layer não depende de DbContext — sempre via repository + Unit of Work | accepted | 2026-05-05 |
| [0043](0043-discovery-explicito-application-via-includeassembly.md) | Discovery explícito da Application layer no Wolverine via `Discovery.IncludeAssembly` | accepted | 2026-05-05 |
| [0044](0044-roteamento-domain-events-pg-queue-kafka-opcional.md) | Roteamento de domain events: queue PG interna + tópico Kafka opcional | accepted | 2026-05-05 |
| [0045](0045-test-factory-remove-wolverine-runtime.md) | Test factory remove `WolverineRuntime` de `IHostedService` para suítes não-outbox | accepted | 2026-05-05 |
| [0046](0046-validacao-de-regras-sem-excecao-result-failure.md) | Validação de regras de negócio sem exceção — `Result.Failure(DomainError)` para fluxo esperado | accepted | 2026-05-05 |
| [0047](0047-confluent-kafka-npgsql-pisos-transitivos-wolverine.md) | `Confluent.Kafka 2.14.0` + `Npgsql 9.0.4` como pisos transitivos do Wolverine 5.32.1 | accepted | 2026-05-05 |
| [0048](0048-controllers-mvc-public-com-ca1515-suprimido.md) | Controllers MVC em `*.API` devem ser `public`, com CA1515 suprimido por justificativa | accepted | 2026-05-05 |
| [0049](0049-implementacao-hateoas-edital-resource-links-builder.md) | Implementação de HATEOAS Level 1 em `EditalDto` via `IResourceLinksBuilder<TDto>` na camada API | accepted | 2026-05-06 |
| [0050](0050-registry-ghcr-e-tagging.md) | GitHub Container Registry e estratégia de tagging das imagens da `uniplus-api` | accepted | 2026-05-08 |
| [0051](0051-apicurio-schema-registry-avro-wolverine.md) | Apicurio Schema Registry com Avro e Wolverine — schemas no Domain, registro idempotente, OAuth client_credentials | accepted | 2026-05-09 |
| [0052](0052-rastreabilidade-cross-service-traceparent-service-name-enricher.md) | Rastreabilidade cross-service via `traceparent` W3C + Serilog `ServiceName` enricher + Wolverine envelope middleware para `CorrelationId` | proposed | 2026-05-11 |
| [0053](0053-zero-test-environment-branches-in-production-code.md) | Zero ramos de ambiente de teste em código de produção — `IsEnvironment(literal)` e `EnvironmentName == literal` banidos em `src/` (ADR normativa sem enforcement automático) | accepted | 2026-05-11 |
| [0054](0054-naming-convention-e-strategy-migrations.md) | Convenção de nomenclatura `snake_case` via `EFCore.NamingConventions` + isolamento por banco e estratégia de migrations | accepted | 2026-05-13 |
| [0055](0055-organizacao-institucional-bounded-context.md) | `OrganizacaoInstitucional` como bounded context para áreas (CEPS, CRCA, PROEG, PROGEP, PLATAFORMA) com roster fechado | accepted | 2026-05-14 |
| [0056](0056-modulo-configuracao-e-read-side-via-reader.md) | Módulo `Configuracao` para catálogos cross-cutting + desmembramento read-side cross-módulo via `IXxxReader` | accepted | 2026-05-14 |
| [0057](0057-areas-rbac-snapshot-historia-invariantes.md) | RBAC por áreas com snapshot na publicação, histórico SCD Type 2 e invariantes de governança — **supersessão proposta pela ADR-0078** | accepted | 2026-05-14 |
| [0058](0058-obrigatoriedade-legal-validacao-data-driven.md) | `ObrigatoriedadeLegal` como validação data-driven com citação legal e snapshot-on-bind | accepted | 2026-05-14 |
| [0059](0059-sprint-3-decomposicao-estrategia-paralela.md) | Decomposição da Sprint 3 — foundation primeiro, depois 3 lanes paralelas | accepted | 2026-05-14 |
| [0060](0060-junction-tables-por-entidade-com-view-unificada.md) | Junction tables por entidade para `AreasDeInteresse` + view unificada por DbContext para leituras cross-catálogo | accepted | 2026-05-14 |
| [0061](0061-referencia-cross-modulo-via-snapshot-copy.md) | Referência cross-módulo via snapshot-copy (value object embedded) com `OrigemId` opcional sem FK | accepted | 2026-05-14 |
| [0062](0062-seed-de-catalogos-via-newman-e-endpoints-admin.md) | Seed de catálogos via Newman + endpoints admin (sem auto-seeder, audit captura usuário real) | accepted | 2026-05-14 |
| [0063](0063-entidades-forensics-isentas-de-soft-delete.md) | Entidades forensics append-only (`IForensicEntity`) isentas de soft-delete, mutuamente exclusivas com `EntityBase` | accepted | 2026-05-16 |
| [0064](0064-convencao-roteamento-path-based-com-prefixo-modulo.md) | Convenção de roteamento — path-based com prefixo de módulo (`/api/{modulo}/{recurso}`), separação cross-API via PathPrefix no Traefik | accepted | 2026-05-16 |
| [0065](0065-localoferta-flat-um-por-endereco-emec.md) | LocalOferta como entidade flat, uma entrada por local de oferta (endereço e-MEC) | accepted | 2026-05-19 |
| [0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) | Modelo de oferta em três níveis — Curso curricular, OfertaCurso regulatória e código e-MEC por campus | accepted | 2026-05-19 |
| [0067](0067-aninhamento-tipodeficiencia-sob-pcd.md) | Aninhamento de TipoDeficiencia sob a condição PCD na oferta de atendimento especializado | accepted | 2026-05-19 |
| [0068](0068-relogio-via-timeprovider-injetado.md) | Relógio via TimeProvider injetado, obrigatório em todo o `src/` | proposed | 2026-05-24 |
| [0069](0069-event-sourcing-seletivo-marten-contextos-criticos.md) | Event Sourcing seletivo com Marten em agregados críticos (Marten como store ancillary; EF Core permanece o main) | accepted | 2026-05-25 |
| [0070](0070-validacao-runtime-avalia-snapshot-congelado.md) | A validação de documentos em runtime avalia o snapshot congelado, não a configuração viva | accepted | 2026-05-31 |
| [0071](0071-aplicabilidade-exigencia-documental-explicita.md) | Aplicabilidade da exigência documental é configuração explícita (`GERAL`/`CONDICIONAL`), não inferida | accepted | 2026-05-31 |
| [0072](0072-correlacao-exigencia-por-id-congelado.md) | Correlação apresentação↔exigência pela identidade congelada (`exigencia_id`), não pelo tipo de documento | accepted | 2026-05-31 |
| [0073](0073-fatos-atendimento-com-identidade-congelada.md) | Os fatos de atendimento especializado carregam a identidade congelada da oferta; a validação lê o código congelado | accepted | 2026-05-31 |
| [0074](0074-base-legal-exigencia-1n-validacao-publicacao.md) | A base legal da exigência documental é 1:N e enforçada por uma validação de publicação | accepted | 2026-05-31 |
| [0075](0075-snapshot-do-ato-resolvido-no-instante.md) | O snapshot que governa um ato é resolvido deterministicamente no instante do ato e gravado nele | accepted | 2026-05-31 |
| [0076](0076-contrato-snapshot-runtime-espelha-publicacao.md) | A validação do snapshot lido em runtime reproduz, integralmente, a validação aplicada à configuração na publicação | accepted | 2026-05-31 |
| [0077](0077-identidade-institucional-canonica-de-unidade.md) | Identidade institucional canônica de `Unidade` (`Id` Guid v7 estável; `Slug`/`Sigla`/`Codigo` únicos entre vivos; `Alias` não-único; histórico de identificadores; cadastro aberto e hierárquico) — refina 0055 | accepted | 2026-06-15 |
| [0078](0078-modelo-de-autorizacao-pbac-abac.md) | Modelo de autorização PBAC + ABAC com ponto de decisão único — supersede 0057, refina 0055 | proposed | 2026-06-02 |
| [0079](0079-hierarquia-institucional-sem-heranca-de-permissao.md) | Hierarquia institucional sem herança de permissão (unidades irmãs; visibilidade por escopo de auditoria explícito) — refina 0055 | proposed | 2026-06-02 |
| [0080](0080-catalogo-declarativo-de-permissoes-e-codegen.md) | Catálogo declarativo de permissões como fonte única + geração de artefatos (codegen, fitness contra deriva) | proposed | 2026-06-02 |
| [0081](0081-lgpd-by-design-dto-por-permissao.md) | LGPD-by-design — projeção por permissão como controle primário de proteção de dado pessoal (mascaramento secundário; BOPLA) — **classificação/base legal pendente de validação DPO** | proposed | 2026-06-02 |
| [0082](0082-nome-social-publico-nome-civil-pessoal.md) | Nome social como dado público e nome civil como dado pessoal protegido (Decreto 8.727/2016) — **pendente validação DPO** | proposed | 2026-06-02 |
| [0083](0083-grupos-oidc-governados-pela-aplicacao.md) | Grupos OIDC governados pela aplicação — vínculo no banco, marca de propriedade e sincronização não-destrutiva | proposed | 2026-06-02 |
| [0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md) | Concessão excepcional e atuação institucional avaliadas no servidor (escopadas, temporais, revogáveis; dupla aprovação para sensível) | proposed | 2026-06-02 |
| [0085](0085-cache-e-revogacao-diferenciados-por-sensibilidade.md) | Cache de decisão e revogação diferenciados por sensibilidade (sensível sem cache; fail-closed) | proposed | 2026-06-02 |
| [0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md) | Trilha de auditoria de autorização com integridade verificável (código de autenticação com chave em cofre, rotacionável; append-only) | proposed | 2026-06-02 |
| [0087](0087-banco-isolado-para-o-contexto-de-autorizacao.md) | Banco isolado para o contexto de autorização (aplica ADR-0054; referências externas por identificador via leitor) | proposed | 2026-06-02 |
| [0088](0088-versionamento-cross-repo-do-contrato-de-permissoes.md) | Versionamento e publicação cross-repo do contrato de permissões (pacote versionado; versão fixa no frontend; validação na CI) | proposed | 2026-06-02 |
| [0089](0089-navegacao-bidirecional-cursor-keyset-reverso.md) | Navegação bidirecional na paginação por cursor via keyset reverso (direction query param vinculado ao cursor; flags exatas sem COUNT) | accepted | 2026-06-16 |
| [0090](0090-modulo-geo-localidades.md) | Módulo Geo como bounded context dedicado de localidades | accepted | 2026-06-17 |
| [0091](0091-postgis-georreferencia-nts.md) | PostGIS e NetTopologySuite como mecanismo de georreferência | accepted | 2026-06-17 |
| [0092](0092-etl-carga-dne-reference-data.md) | Reference data do Geo sem soft-delete, recarregado por upsert | accepted | 2026-06-17 |
| [0093](0093-rate-limiting-na-borda-para-reference-data-publico.md) | Rate-limiting de endpoints públicos de reference data na borda (gateway), não no app | accepted | 2026-06-19 |
| [0094](0094-keyset-ordenado-via-mr-sob-cursor-opaco.md) | Ordenação keyset na API via biblioteca de seek sob cursor opaco | accepted | 2026-06-19 |
| [0095](0095-chave-de-ordenacao-keyset-nao-nula.md) | Chave de ordenação keyset não-nula via coluna gerada | accepted | 2026-06-19 |
| [0096](0096-endereco-como-referencia-estruturada-ao-geo.md) | Endereço de entidades institucionais como referência estruturada ao Geo | accepted | 2026-06-22 |
| [0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) | Topologia de deploy em 3 APIs — módulos internos como libraries co-hospedadas | accepted | 2026-06-26 |
| [0098](0098-politica-de-service-location-do-codegen-wolverine.md) | Política de service location do codegen Wolverine (`NotAllowed` + allow-list por tipo) | accepted | 2026-06-26 |
| [0099](0099-geo-como-repositorio-dedicado.md) | Geo como repositório e serviço transversal dedicado | accepted | 2026-06-26 |
| [0100](0100-canonicalizacao-hash-snapshot-publicacao.md) | Contrato de canonicalização e hash do snapshot de publicação (RN08) | accepted | 2026-07-07 |
| [0101](0101-retificacao-novo-edital-novo-snapshot-motivo.md) | Retificação de processo publicado é sempre novo Edital + novo snapshot + motivo | superseded by ADR-0103 | 2026-07-07 |
| [0102](0102-invariantes-coerencia-processo-guard-rails-422.md) | Invariantes de coerência de processo como guard rails no banco, mapeadas a HTTP 422 | accepted | 2026-07-07 |
| [0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) | Retificação é uma relação entre atos publicados, não um tipo de ato | accepted | 2026-07-09 |
| [0104](0104-versao-configuracao-como-agregado-proprio.md) | A vigência da configuração ordena versões, não documentos | accepted | 2026-07-09 |
| [0105](0105-modulo-publicacoes-registro-central-dos-atos.md) | O ato publicado pertence a um módulo `Publicacoes` que não conhece os domínios | accepted | 2026-07-09 |
| [0106](0106-orquestracao-sincrona-selecao-publicacoes-ato-primeiro.md) | Publicar um Edital registra o ato em Publicações de forma síncrona, antes de concluir | superseded by ADR-0108 | 2026-07-10 |
| [0107](0107-vaga-de-linhagem-unica-por-objeto.md) | A unicidade de ato por objeto é uma vaga que a linhagem reserva, não um índice sobre o ato | accepted | 2026-07-11 |
| [0108](0108-registro-do-ato-por-mensagem-duravel.md) | O domínio registra o ato por mensagem durável, não por chamada síncrona (supersede a 0106 no mecanismo) | accepted | 2026-07-12 |
| [0109](0109-envelope-canonico-v2-do-congelamento.md) | Contrato do envelope canônico do congelamento (v2) | accepted | 2026-07-13 |
| [0110](0110-retificacao-como-sessao-editorial.md) | A retificação é uma sessão editorial sobre a configuração, não um estado do certame | accepted | 2026-07-13 |
| [0111](0111-vocabulario-fechado-de-fatos-do-candidato.md) | Vocabulário fechado de fatos do candidato (catálogo seed-governado em Configuração, identidade imutável) | accepted | 2026-07-15 |
| [0112](0112-fronteira-append-only-do-catalogo-de-regras.md) | Fronteira do append-only na correção do catálogo de regras (substituível enquanto nada congelado referenciar) | accepted | 2026-07-14 |
| [0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md) | Fase × Etapa — eixo temporal (cronograma) e eixo de pontuação são agregados distintos, ligados por bicondicional; precedência entre fases é dado de cadastro | accepted | 2026-07-15 |

> **Nota de numeração:** a sequência de `0001` a `0113` está completa, sem lacunas. Ao adicionar uma ADR nova, use `0114+`.

## Como adicionar um novo ADR

1. Identifique o próximo número livre: **o maior número da tabela acima + 1** (atualmente `0113`). **Não** use `ls | wc -l` — confira a coluna de número da tabela e use o maior valor + 1.
2. Copie [`_template.md`](_template.md).
3. Renomeie para `NNNN-titulo-em-slug.md` (slug ASCII em minúsculas, hífens como separador).
4. Preencha frontmatter, contexto, drivers, opções, resultado da decisão (única), consequências.
5. Rode o linter (`bash tools/adr-lint/validate.sh`).
6. Adicione linha ao índice acima.
7. Abra PR.
